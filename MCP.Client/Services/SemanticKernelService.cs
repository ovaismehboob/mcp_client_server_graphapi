using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel.Memory;
using MCP.Shared.MCP;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;

namespace MCP.Client.Services;

public class SemanticKernelService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private readonly List<McpMessage> _messages = new();
    private List<McpFunctionDeclaration>? _availableFunctions;

    public IReadOnlyList<McpMessage> Messages => _messages.AsReadOnly();

    public event Action? OnMessagesChanged;

    public SemanticKernelService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        // Initialize Semantic Kernel based on configuration
        _kernel = CreateKernel();
    }    private Kernel CreateKernel()
    {
        // Create kernel builder
        var builder = Kernel.CreateBuilder();
        
        // Configure OpenAI or Azure OpenAI as the LLM service
        if (!string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]) && 
            !string.IsNullOrEmpty(_configuration["AzureOpenAI:Endpoint"]))
        {
            // Use Azure OpenAI
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"] ?? "gpt-35-turbo";
            var endpoint = _configuration["AzureOpenAI:Endpoint"] ?? string.Empty;
            var apiKey = _configuration["AzureOpenAI:ApiKey"] ?? string.Empty;
            
            try
            {
                // For Blazor WASM, we need to use a custom approach to add the AI service
                // Create an HTTP client without using the default handlers that cause issues in WebAssembly
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "MCP-Client-Browser");
                
                // Create and register the chat completion service
                builder.Services.AddSingleton<IChatCompletionService>(_ => 
                {
                    // Manually create the service avoiding the internal HttpClientHandler initialization
                    return new AzureOpenAIChatCompletionService(
                        deploymentName: deploymentName,
                        endpoint: endpoint,
                        apiKey: apiKey,
                        httpClient: httpClient);
                });
                
                Console.WriteLine($"Using Azure OpenAI with deployment {deploymentName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error configuring Azure OpenAI: {ex.Message}");
                
                // Try fallback to OpenAI if Azure OpenAI fails
                if (!string.IsNullOrEmpty(_configuration["OpenAI:ApiKey"]))
                {
                    Console.WriteLine("Attempting to fall back to OpenAI configuration...");
                    // Fall back to OpenAI - continue with the next else-if block
                }
                else
                {
                    throw;
                }
            }
        }
        
        if (!builder.Services.Any(sd => sd.ServiceType == typeof(IChatCompletionService)) 
            && !string.IsNullOrEmpty(_configuration["OpenAI:ApiKey"]))
        {
            // Use OpenAI
            var modelName = _configuration["OpenAI:ModelName"] ?? "gpt-3.5-turbo";
            var apiKey = _configuration["OpenAI:ApiKey"] ?? string.Empty;
            
            try
            {
                // Create an HTTP client without using the default handlers that cause issues in WebAssembly
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "MCP-Client-Browser");
                
                // Create and register the chat completion service
                builder.Services.AddSingleton<IChatCompletionService>(_ => 
                {
                    // Manually create the service avoiding the internal HttpClientHandler initialization
                    return new OpenAIChatCompletionService(
                        modelId: modelName,
                        apiKey: apiKey,
                        httpClient: httpClient);
                });
                
                Console.WriteLine($"Using OpenAI with model {modelName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error configuring OpenAI: {ex.Message}");
                throw;
            }
        }
        
        if (!builder.Services.Any(sd => sd.ServiceType == typeof(IChatCompletionService)))
        {
            throw new InvalidOperationException("No LLM configuration found. Please configure either AzureOpenAI or OpenAI in appsettings.json");
        }

        return builder.Build();
    }

    /// <summary>
    /// Check if the service is available by pinging the status endpoint
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
            Console.WriteLine($"Calling MCP function: {functionName} with arguments: {JsonSerializer.Serialize(arguments)}");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            var functionCall = new McpFunctionCall
            {
                Name = functionName,
                Arguments = arguments ?? new Dictionary<string, object>()
            };
            
            // Make the HTTP request to the MCP server
            var response = await _httpClient.PostAsJsonAsync("mcp/execute", functionCall, cts.Token);
            
            // Check for success
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cts.Token);
                Console.WriteLine($"SUCCESS - Function {functionName} returned result: {result.Substring(0, Math.Min(result.Length, 100))}...");
                return result;
            }
            
            // Handle error
            var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
            Console.WriteLine($"ERROR - Function {functionName} failed with status {response.StatusCode}: {errorContent}");
            return $"{{\"error\": \"Error executing function: {response.StatusCode}, {errorContent}\"}}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EXCEPTION - Function {functionName} failed with exception: {ex.Message}");
            return $"{{\"error\": \"Exception executing function: {ex.Message}\"}}";
        }
    }
      /// <summary>
    /// Register MCP functions with the Semantic Kernel
    /// </summary>
    private async Task RegisterMcpFunctionsAsync()
    {
        // Get available functions from the MCP server
        var functions = await GetAvailableFunctionsAsync();
          // Always recreate the plugin to ensure function definitions are current
        // (this also helps ensure the LLM properly considers these functions during each interaction)
        Console.WriteLine("Registering MCP functions with Semantic Kernel");
        
        // Remove existing plugin if it exists
        try
        {
            // First check if the plugin exists
            if (_kernel.Plugins.Any(p => p.Name == "McpFunctions"))
            {
                Console.WriteLine("Removing existing McpFunctions plugin to ensure fresh registration");
                var pluginToRemove = _kernel.Plugins.First(p => p.Name == "McpFunctions");
                _kernel.Plugins.Remove(pluginToRemove);
            }
        }
        catch (Exception ex) 
        {
            Console.WriteLine($"Error removing existing plugin: {ex.Message}");
        }
        
        // Ensure we have functions to register
        if (functions == null || functions.Count == 0)
        {
            Console.WriteLine("WARNING: No functions available from MCP server. Function calling will not work!");
            return;
        }
        
        Console.WriteLine($"Found {functions.Count} functions to register");
        
        // Collect all functions before registering them as a single plugin
        var kernelFunctions = new List<KernelFunction>();
        foreach (var function in functions)
        {
            // For each function from the MCP server, create a kernel function that will call the server
            var kernelFunction = KernelFunctionFactory.CreateFromMethod(
                async (KernelArguments args) =>
                {
                    // Convert KernelArguments to a dictionary for the MCP server
                    var argsDict = args.ToDictionary(a => a.Key, a => a.Value as object);
                    
                    Console.WriteLine($"Executing function {function.Name} with arguments: {JsonSerializer.Serialize(argsDict)}");
                    
                    // Call the function on the MCP server
                    var result = await ExecuteFunctionAsync(function.Name, argsDict);
                    
                    // Log the result for debugging
                    Console.WriteLine($"Function {function.Name} result: {result}");                    // Add a general system message indicating a function was called (not specific to any function)
                    Console.WriteLine($"Function {function.Name} was called and returned data");
                    
                    // Add a simple system message to inform the LLM about the function call
                    _messages.Add(new McpMessage
                    {
                        Role = "system",
                        Content = $"Function {function.Name} has been called. The result contains real data in JSON format. Parse this data and respond based on this data only."
                    });
                      // Remove any existing function messages for this function to avoid duplication
                    var existingFunctionMessages = _messages
                        .Where(m => m.Role == "function" && m.Name == function.Name)
                        .ToList();
                    
                    foreach (var msg in existingFunctionMessages)
                    {
                        Console.WriteLine($"Replacing old function message for {function.Name} with new result");
                        _messages.Remove(msg);
                    }
                      // Add the new function result - Let the LLM parse the JSON directly
                    _messages.Add(new McpMessage
                    {
                        Role = "function",
                        Name = function.Name,
                        Content = result
                    });
                    
                    // Let the LLM handle parsing directly
                    Console.WriteLine("Passing raw function result to LLM for parsing");
                      // Check for error responses only
                    if (result.Contains("\"error\":"))
                    {
                        Console.WriteLine("Found error in function result");
                        // Let the LLM handle parsing the error
                    }
                    
                    NotifyStateChanged();
                      return result;
                },
                function.Name,
                function.Description ?? $"Function {function.Name}",
                function.Parameters != null ? ConvertMcpParametersToKernelParameters(function.Parameters) : null
            );
            
            kernelFunctions.Add(kernelFunction);
        }
        
        // Register all functions as a single plugin
        if (kernelFunctions.Count > 0)
        {
            _kernel.Plugins.AddFromFunctions("McpFunctions", kernelFunctions);
            Console.WriteLine($"Successfully registered {kernelFunctions.Count} MCP functions with the kernel");
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
                // Register MCP functions with the kernel
                await RegisterMcpFunctionsAsync();
                  // Build a generic prompt from the chat history
                var prompt = new StringBuilder();
                prompt.AppendLine("You are an assistant that helps users with their questions by using available functions.");
                prompt.AppendLine("GENERAL GUIDELINES:");
                prompt.AppendLine("1. Use available functions to retrieve real data when needed.");
                prompt.AppendLine("2. After each function call, analyze the returned JSON data and answer based on it.");
                prompt.AppendLine("3. NEVER make up information. ONLY use data from function calls when available.");
                prompt.AppendLine("4. Format responses in a clear, organized way showing insights from the data.");
                prompt.AppendLine("5. When functions return JSON, parse it properly to extract the relevant information.");
                prompt.AppendLine();
                
                // Add the messages to the prompt
                foreach (var msg in _messages)
                {
                    switch (msg.Role.ToLower())
                    {
                        case "user":
                            prompt.AppendLine($"USER: {msg.Content}");
                            break;
                        case "assistant":
                            prompt.AppendLine($"ASSISTANT: {msg.Content}");
                            break;
                        case "system":
                            prompt.AppendLine($"SYSTEM: {msg.Content}");
                            break;
                        case "function":
                            if (!string.IsNullOrEmpty(msg.Name))
                            {
                                prompt.AppendLine($"FUNCTION ({msg.Name}): {msg.Content}");
                            }
                            break;
                    }
                    prompt.AppendLine();
                }
                  // Set up execution settings for tool calling with stronger directives
                var executionSettings = new OpenAIPromptExecutionSettings 
                { 
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0.0, // Zero temperature for absolute deterministic responses
                    TopP = 1.0, // Use maximum top_p for more consistent function calling
                    MaxTokens = 4000 // Increased max tokens for larger responses with data
                };
                
                var kernelArguments = new KernelArguments(executionSettings);
                  // Add a general system message to guide the LLM
                kernelArguments["SystemMessage"] = @"You are an assistant that helps users by utilizing available MCP functions.

IMPORTANT GUIDELINES:
- Use the provided functions to retrieve data whenever possible
- Functions provide REAL DATA - use this data to answer user questions
- Parse JSON responses from functions to extract relevant information
- Format your responses in a clear, organized way
- If functions return errors, explain them in a helpful manner
- Be accurate and precise with the data you receive from functions
- Do not guess or make up information - rely on the function calls";
                  // Add general context for the AI
                // Let the kernel automatically determine available functions
                kernelArguments["UseTools"] = "true";
                kernelArguments["ImportantNote"] = "Use the appropriate function to retrieve data when necessary, and parse JSON responses to extract meaningful information.";
                
                // Get a response using the kernel
                var response = await _kernel.InvokePromptAsync(
                    prompt.ToString(),
                    kernelArguments,
                    cancellationToken: cts.Token);
                
                // Add assistant response to the message list
                _messages.Add(new McpMessage
                {
                    Role = "assistant",
                    Content = response.ToString()
                });
                
                NotifyStateChanged();
                return true;
            }
            catch (OperationCanceledException)
            {
                _messages.Add(new McpMessage 
                { 
                    Role = "assistant", 
                    Content = "The request timed out. The server took too long to respond." 
                });
                NotifyStateChanged();
                return false;            }
        }
        catch (Exception ex)
        {
            // Log the full exception details
            Console.WriteLine($"Error in SendMessageAsync: {ex}");
            
            // Create a more user-friendly error message
            string errorMessage = ex.Message;
            
            // Special handling for the NonDisposableHttpClientHandler error
            if (errorMessage.Contains("NonDisposableHttpClientHandler"))
            {
                errorMessage = "There was an issue with the HTTP client configuration. This is a known issue with Semantic Kernel in Blazor WebAssembly. Please try refreshing the page.";
            }
            
            _messages.Add(new McpMessage 
            { 
                Role = "assistant", 
                Content = $"An error occurred: {errorMessage}" 
            });
            NotifyStateChanged();
            return false;
        }
    }

    public void AddMessage(McpMessage message)
    {
        _messages.Add(message);
        NotifyStateChanged();
    }

    public void ClearMessages()
    {
        _messages.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnMessagesChanged?.Invoke();    private IEnumerable<KernelParameterMetadata> ConvertMcpParametersToKernelParameters(McpFunctionParameters mcpParams)
    {
        if (mcpParams == null)
            return new List<KernelParameterMetadata>();
            
        var result = new List<KernelParameterMetadata>();
        
        // Convert each MCP parameter to a kernel parameter
        foreach (var param in mcpParams.Properties ?? new Dictionary<string, McpParameterProperty>())
        {
            // Create parameter metadata with proper initialization
            var paramMeta = new KernelParameterMetadata(param.Key)
            {
                Description = param.Value.Description,
                ParameterType = typeof(string), // Default to string since we're passing everything as strings through JSON
                IsRequired = false // Default to false
            };
            
            result.Add(paramMeta);
        }
        
        return result;
    }
}
