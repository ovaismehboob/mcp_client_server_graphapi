using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP.Shared.MCP;

namespace MCP.Client.Services;

public class ChatService
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _llmHttpClient;
    private readonly List<McpMessage> _messages = new();
    private List<McpFunctionDeclaration>? _availableFunctions;
    private readonly IConfiguration _configuration;

    public IReadOnlyList<McpMessage> Messages => _messages.AsReadOnly();

    public event Action? OnMessagesChanged;    public ChatService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        // Create a separate HttpClient for the LLM service
        _llmHttpClient = new HttpClient
        {
            BaseAddress = new Uri(configuration["AzureOpenAI:Endpoint"] ?? 
                                 "https://api.openai.com/v1/")
        };

        // Set default headers for AzureOpenAI
        if (!string.IsNullOrEmpty(configuration["AzureOpenAI:ApiKey"]))
        {
            _llmHttpClient.DefaultRequestHeaders.Add("api-key", configuration["AzureOpenAI:ApiKey"]);
        }
        else if (!string.IsNullOrEmpty(configuration["OpenAI:ApiKey"]))
        {
            _llmHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration["OpenAI:ApiKey"]}");
        }
    }
    
    /// <summary>
    /// Check if the chat service is available by pinging the status endpoint
    /// </summary>
    public async Task<(bool IsAvailable, string Message)> CheckServiceAvailabilityAsync()
    {
        try
        {
            // Use a short timeout for the status check
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            var response = await _httpClient.GetAsync("mcp/status", cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                return (true, "Service is available");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
            return (false, $"Service returned error: {response.StatusCode}, {errorContent}");
        }
        catch (Exception ex)
        {
            return (false, $"Service is unavailable: {ex.Message}");
        }
    }
      /// <summary>
    /// Retrieves available functions from the MCP server
    /// </summary>
    public async Task<List<McpFunctionDeclaration>> GetAvailableFunctionsAsync()
    {
        if (_availableFunctions != null)
        {
            return _availableFunctions;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync("mcp/functions", cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cts.Token);
                var jsonDocument = JsonDocument.Parse(content);
                
                if (jsonDocument.RootElement.TryGetProperty("functions", out var functionsElement))
                {
                    _availableFunctions = JsonSerializer.Deserialize<List<McpFunctionDeclaration>>(
                        functionsElement.GetRawText());
                    return _availableFunctions ?? new List<McpFunctionDeclaration>();
                }
            }
            
            return new List<McpFunctionDeclaration>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching available functions: {ex.Message}");
            return new List<McpFunctionDeclaration>();
        }
    }
    
    /// <summary>
    /// Executes a function on the MCP server
    /// </summary>
    public async Task<string> ExecuteFunctionAsync(string functionName, Dictionary<string, object> arguments)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            var functionCall = new McpFunctionCall
            {
                Name = functionName,
                Arguments = arguments
            };
            
            var response = await _httpClient.PostAsJsonAsync("mcp/execute", functionCall, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(cts.Token);
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
            return $"{{\"error\": \"Error executing function: {response.StatusCode}, {errorContent}\"}}";
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"Exception executing function: {ex.Message}\"}}";
        }
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        try
        {
            // Add user message to the list
            var userMessage = new McpMessage
            {
                Role = "user",
                Content = message
            };
            
            _messages.Add(userMessage);
            NotifyStateChanged();

            // Set timeout for the request
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            try
            {
                // Get available functions from MCP server
                var availableFunctions = await GetAvailableFunctionsAsync();
                
                // Convert functions to OpenAI tool format
                var tools = availableFunctions.Select(f => new
                {
                    type = "function",
                    function = new
                    {
                        name = f.Name,
                        description = f.Description,
                        parameters = new
                        {
                            type = f.Parameters?.Type ?? "object",
                            properties = f.Parameters?.Properties ?? new Dictionary<string, McpParameterProperty>(),
                            required = f.Parameters?.Required ?? new List<string>()
                        }
                    }
                }).ToList();
                
                // Prepare messages for LLM
                var llmMessages = _messages.Select(m => new 
                {
                    role = m.Role,
                    content = m.Content
                }).ToList();
                
                // Add system message if not present
                if (!_messages.Any(m => m.Role == "system"))
                {
                    llmMessages.Insert(0, new
                    {
                        role = "system",
                        content = "You are an assistant that helps users understand their Microsoft Azure tenant. " +
                                  "You can access information about application registrations through functions. " +
                                  "Always be helpful, concise, and accurate."
                    });
                }
                  // Create request to LLM
                var llmRequestBody = new 
                {
                    model = _llmHttpClient.BaseAddress?.AbsoluteUri.Contains("openai.azure.com") == true ? 
                           _configuration["AzureOpenAI:DeploymentName"] ?? "gpt-35-turbo" : 
                           _configuration["OpenAI:ModelName"] ?? "gpt-3.5-turbo", // Use configured model
                    messages = llmMessages,
                    tools = tools,
                    tool_choice = "auto"
                };
                  // Determine the correct endpoint
                string endpoint;
                if (_llmHttpClient.BaseAddress?.AbsoluteUri.Contains("openai.azure.com") == true)
                {
                    // Azure OpenAI
                    var deploymentName = _configuration["AzureOpenAI:DeploymentName"] ?? "gpt-35-turbo";
                    endpoint = $"openai/deployments/{deploymentName}/chat/completions?api-version=2023-07-01-preview";
                }
                else
                {
                    // Standard OpenAI
                    endpoint = "chat/completions";
                }
                
                // Send request to LLM
                var llmRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(llmRequestBody), 
                        Encoding.UTF8, 
                        "application/json")
                };
                
                var llmResponse = await _llmHttpClient.SendAsync(llmRequest, cts.Token);
                
                if (!llmResponse.IsSuccessStatusCode)
                {
                    var errorContent = await llmResponse.Content.ReadAsStringAsync(cts.Token);
                    _messages.Add(new McpMessage 
                    { 
                        Role = "assistant", 
                        Content = $"Error communicating with the language model: {llmResponse.StatusCode}, {errorContent}" 
                    });
                    NotifyStateChanged();
                    return false;
                }
                
                // Process LLM response
                var responseContent = await llmResponse.Content.ReadAsStringAsync(cts.Token);
                var jsonResponse = JsonNode.Parse(responseContent);
                
                if (jsonResponse == null)
                {
                    _messages.Add(new McpMessage { Role = "assistant", Content = "Invalid response from language model" });
                    NotifyStateChanged();
                    return false;
                }
                
                // Check if the model wants to call a tool
                var toolCalls = jsonResponse?["choices"]?[0]?["message"]?["tool_calls"];
                if (toolCalls != null)
                {                    // There are tool calls to process
                    var assistantMessage = new McpMessage
                    {
                        Role = "assistant",
                        Content = jsonResponse?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? 
                                  "I need to look that up for you..."
                    };
                    _messages.Add(assistantMessage);
                    NotifyStateChanged();
                    
                    // Process each tool call
                    foreach (var toolCall in toolCalls.AsArray())
                    {
                        var functionName = toolCall?["function"]?["name"]?.GetValue<string>();
                        var functionArgs = toolCall?["function"]?["arguments"]?.GetValue<string>();
                        
                        if (string.IsNullOrEmpty(functionName) || string.IsNullOrEmpty(functionArgs))
                        {
                            continue;
                        }
                        
                        // Convert arguments from string to dictionary
                        var argsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(functionArgs) ?? 
                                       new Dictionary<string, object>();
                                       
                        // Execute the function on MCP server
                        var functionResult = await ExecuteFunctionAsync(functionName, argsDict);
                        
                        // Add function result to messages
                        _messages.Add(new McpMessage
                        {
                            Role = "function",
                            Content = functionResult
                        });
                        NotifyStateChanged();
                        
                        // Create a followup request to LLM with function result
                        var followupMessages = _messages.Select(m => new 
                        {
                            role = m.Role,
                            content = m.Content
                        }).ToList();
                          var followupRequestBody = new
                        {
                            model = _llmHttpClient.BaseAddress?.AbsoluteUri.Contains("openai.azure.com") == true ? 
                                   _configuration["AzureOpenAI:DeploymentName"] ?? "gpt-35-turbo" : 
                                   _configuration["OpenAI:ModelName"] ?? "gpt-3.5-turbo",
                            messages = followupMessages,
                            tools = tools,
                            tool_choice = "auto"
                        };
                        
                        var followupRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(followupRequestBody), 
                                Encoding.UTF8, 
                                "application/json")
                        };
                        
                        var followupResponse = await _llmHttpClient.SendAsync(followupRequest, cts.Token);
                        
                        if (followupResponse.IsSuccessStatusCode)
                        {
                            var followupContent = await followupResponse.Content.ReadAsStringAsync(cts.Token);                            var followupJson = JsonNode.Parse(followupContent);
                            
                            if (followupJson != null)
                            {
                                // Add the final assistant message
                                var finalMessage = new McpMessage
                                {
                                    Role = "assistant",
                                    Content = followupJson?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? 
                                              "I'm sorry, I couldn't process that information."
                                };
                                _messages.Add(finalMessage);
                                NotifyStateChanged();
                            }
                        }
                    }
                    
                    return true;
                }
                else
                {                    // No tool calls, just a regular response
                    var content = jsonResponse?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? 
                                 "I'm sorry, I couldn't generate a response.";
                                 
                    _messages.Add(new McpMessage { Role = "assistant", Content = content });
                    NotifyStateChanged();
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                _messages.Add(new McpMessage 
                { 
                    Role = "assistant", 
                    Content = "The request timed out. The server took too long to respond." 
                });
                NotifyStateChanged();
                return false;
            }
        }
        catch (Exception ex)
        {
            _messages.Add(new McpMessage 
            { 
                Role = "assistant", 
                Content = $"An error occurred: {ex.Message}" 
            });
            NotifyStateChanged();
            return false;
        }
    }    public void AddMessage(McpMessage message)
    {
        _messages.Add(message);
        NotifyStateChanged();
    }

    public void ClearMessages()
    {
        _messages.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnMessagesChanged?.Invoke();
}
