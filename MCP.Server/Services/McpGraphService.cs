using System.Text.Json;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Azure.Identity;
using MCP.Shared.MCP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MCP.Server.Services;

public class GraphApiTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class McpGraphService
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<McpGraphService> _logger;
    private readonly List<McpFunctionDeclaration> _functions;

    public McpGraphService(IConfiguration configuration, ILogger<McpGraphService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Initialize Microsoft Graph client
        _graphServiceClient = InitializeGraphClient();
        
        // Register available MCP functions
        _functions = InitializeFunctions();
    }    private GraphServiceClient InitializeGraphClient()
    {
        try
        {
            _logger.LogInformation("Initializing Microsoft Graph client");
            
            // Get app registration details with better error messages
            string? clientId = null;
            string? tenantId = null;
            string? clientSecret = null;
            
            try
            {
                clientId = _configuration["AzureAd:ClientId"];
                if (string.IsNullOrEmpty(clientId))
                    throw new InvalidOperationException("AzureAd:ClientId is missing or empty in configuration");
                    
                tenantId = _configuration["AzureAd:TenantId"];
                if (string.IsNullOrEmpty(tenantId))
                    throw new InvalidOperationException("AzureAd:TenantId is missing or empty in configuration");
                    
                clientSecret = _configuration["AzureAd:ClientSecret"];
                if (string.IsNullOrEmpty(clientSecret))
                    throw new InvalidOperationException("AzureAd:ClientSecret is missing or empty in configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configuration error when initializing Microsoft Graph client");
                throw;
            }

            // Create credential using client ID and client secret
            _logger.LogInformation("Creating ClientSecretCredential for Graph authentication");
            var credentials = new ClientSecretCredential(tenantId, clientId, clientSecret);
            
            // Create a new instance of GraphServiceClient with the credentials
            _logger.LogInformation("Graph client initialized successfully");
            return new GraphServiceClient(credentials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Microsoft Graph client");
            throw new InvalidOperationException($"Failed to initialize Graph client: {ex.Message}", ex);
        }
    }

    private List<McpFunctionDeclaration> InitializeFunctions()
    {
        return new List<McpFunctionDeclaration>
        {
            new McpFunctionDeclaration
            {
                Name = "get_app_registrations",
                Description = "Get a list of all application registrations in the tenant",
                Parameters = new McpFunctionParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpParameterProperty>(),
                    Required = new List<string>()
                }
            },
            new McpFunctionDeclaration
            {
                Name = "get_app_registration_details",
                Description = "Get details for a specific application registration",
                Parameters = new McpFunctionParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpParameterProperty>
                    {
                        ["app_id"] = new McpParameterProperty
                        {
                            Type = "string",
                            Description = "The ID of the application registration"
                        }
                    },
                    Required = new List<string> { "app_id" }
                }
            }
        };    }

    public async Task<McpResponse> ProcessMessageAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing MCP request with {MessageCount} messages", request.Messages.Count);
            
            // This method is now simplified because the client will communicate with the LLM directly
            // We just need to check if there's a function call in the request context
            
            if (request.Context != null && 
                request.Context.TryGetValue("function_call", out var functionCallObj) &&
                functionCallObj is JsonElement jsonElement && 
                jsonElement.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var functionCall = JsonSerializer.Deserialize<McpFunctionCall>(jsonElement.GetRawText());
                    if (functionCall != null && !string.IsNullOrEmpty(functionCall.Name))
                    {
                        _logger.LogInformation("Executing function: {FunctionName}", functionCall.Name);
                        var result = await ExecuteFunctionAsync(functionCall.Name, functionCall.Arguments, cancellationToken);
                        
                        // Return the function result
                        return new McpResponse
                        {
                            Message = new McpMessage
                            {
                                Role = "function",
                                Content = JsonSerializer.Serialize(result)
                            },
                            AvailableFunctions = _functions
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing function from request context");
                    return new McpResponse
                    {
                        Message = new McpMessage
                        {
                            Role = "function",
                            Content = JsonSerializer.Serialize(new { error = $"Error executing function: {ex.Message}" })
                        },
                        AvailableFunctions = _functions
                    };
                }
            }
            
            // If no function call or we couldn't process it, return available functions
            return new McpResponse
            {
                Message = new McpMessage
                {
                    Role = "assistant",
                    Content = "No function was called or the function call could not be processed."
                },
                AvailableFunctions = _functions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            
            return new McpResponse
            {
                Message = new McpMessage
                {
                    Role = "assistant",
                    Content = $"I apologize, but an error occurred: {ex.Message}"
                },
                AvailableFunctions = _functions            };
        }
    }

    private async Task<string> GetAppRegistrationsInternalAsync()
    {
        try
        {
            _logger.LogInformation("Fetching application registrations from Graph API");
            
            // Use a timeout for the Graph API call
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            
            try
            {
                // Log the details of what we're trying to do
                _logger.LogInformation("Attempting to call Microsoft Graph API to get application registrations");
                _logger.LogInformation("Using client ID: {ClientId}, tenant ID: {TenantId}", 
                    _configuration["AzureAd:ClientId"], 
                    _configuration["AzureAd:TenantId"]);
                
                var applications = await _graphServiceClient.Applications.GetAsync(requestConfiguration: null, cts.Token);
                
                if (applications == null || applications.Value == null)
                {
                    _logger.LogWarning("No applications returned from Graph API");
                    return JsonSerializer.Serialize(new { error = "No applications found or unable to access application registrations" });
                }
                
                _logger.LogInformation($"Retrieved {applications.Value.Count} application registrations");
                
                var result = new 
                {
                    Value = applications.Value.Select(app => new 
                    {
                        Id = app.Id ?? "",
                        AppId = app.AppId ?? "",
                        DisplayName = app.DisplayName ?? "",
                        Description = app.Description
                    }).ToList()                };

                return JsonSerializer.Serialize(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Graph API request timed out");
                return JsonSerializer.Serialize(new { error = "The request to Microsoft Graph API timed out. Please try again later." });
            }            catch (Exception graphEx) when (graphEx.GetType().Name.Contains("ServiceException"))
            {
                // Handle Graph API errors
                string errorMessage = graphEx.Message.ToLower();
                
                // Log the full exception details for debugging
                _logger.LogError(graphEx, "Graph API ServiceException: {ErrorMessage}", graphEx.Message);
                
                if (errorMessage.Contains("unauthorized") || errorMessage.Contains("forbidden") || 
                    errorMessage.Contains("401") || errorMessage.Contains("403"))
                {
                    _logger.LogError(graphEx, "Authentication or authorization error with Graph API");
                    
                    // Build a more detailed error message
                    string permissionInfo = "Failed to access application registrations due to authentication or permission issues. " +
                        "Please ensure the application has the necessary permissions. ";
                        
                    if (errorMessage.Contains("permission"))
                    {
                        permissionInfo += "Required permission: Application.Read.All. ";
                        permissionInfo += "Please check the Azure portal to ensure this permission is granted and admin consent is provided.";
                    }
                    
                    return JsonSerializer.Serialize(new { 
                        error = permissionInfo,
                        details = errorMessage
                    });
                }
                
                _logger.LogError(graphEx, "Graph API service exception");
                return JsonSerializer.Serialize(new { error = $"Graph API error: {graphEx.Message}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting application registrations");
            
            // Try to provide more helpful error information
            string errorDetail = ex.Message;
            if (ex.InnerException != null)
            {
                errorDetail += $" Inner exception: {ex.InnerException.Message}";
            }
            
            return JsonSerializer.Serialize(new { 
                error = $"Error getting application registrations", 
                details = errorDetail
            });
        }
    }    private Task<string> GetAppRegistrationDetailsAsync(string appId)
    {
        return GetAppRegistrationDetailsInternalAsync(appId);
    }    private async Task<string> GetAppRegistrationDetailsInternalAsync(string appId)
    {
        try
        {
            _logger.LogInformation($"Fetching details for application with ID: {appId}");
            
            if (string.IsNullOrWhiteSpace(appId))
            {
                _logger.LogWarning("Empty application ID provided");
                return JsonSerializer.Serialize(new { error = "Application ID cannot be empty" });
            }
              // Declare app variable in the outer scope so it's available throughout the method
            Microsoft.Graph.Models.Application? app = null;
            
            // Use a timeout for the Graph API call
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            
            try
            {
                var applications = await _graphServiceClient.Applications
                    .GetAsync(requestConfig => requestConfig.QueryParameters.Filter = $"appId eq '{appId}'", cts.Token);

                app = applications?.Value?.FirstOrDefault();
                
                if (app == null)
                {
                    _logger.LogWarning($"Application with ID {appId} not found");
                    return JsonSerializer.Serialize(new { error = $"Application with ID {appId} not found" });
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Graph API request timed out");
                return JsonSerializer.Serialize(new { error = "The request to Microsoft Graph API timed out. Please try again later." });
            }
            catch (Exception graphEx) when (graphEx.GetType().Name.Contains("ServiceException"))
            {
                // Handle Graph API errors
                string errorMessage = graphEx.Message.ToLower();
                if (errorMessage.Contains("unauthorized") || errorMessage.Contains("forbidden") || 
                    errorMessage.Contains("401") || errorMessage.Contains("403"))
                {
                    _logger.LogError(graphEx, "Authentication or authorization error with Graph API");
                    return JsonSerializer.Serialize(new { 
                        error = "Failed to access application details due to authentication or permission issues. " +
                                "Please ensure the application has the necessary permissions."
                    });
                }
                
                _logger.LogError(graphEx, "Graph API service exception");
                return JsonSerializer.Serialize(new { error = $"Graph API error: {graphEx.Message}" });
            }
            
            // If we reach here, we have a valid app object to work with
            if (app == null)
            {
                // This should never happen, but just in case
                return JsonSerializer.Serialize(new { error = "An unexpected error occurred when retrieving application details" });
            }            _logger.LogInformation($"Retrieved details for application: {app.DisplayName}");

            // Get more detailed information
            var detailedInfo = new
            {
                Id = app.Id,
                AppId = app.AppId,
                DisplayName = app.DisplayName,
                Description = app.Description,
                SignInAudience = app.SignInAudience,
                CreatedDateTime = app.CreatedDateTime,
                IdentifierUris = app.IdentifierUris,
                PublisherDomain = app.PublisherDomain,
                WebRedirectUris = app.Web?.RedirectUris,
                RequiredResourceAccess = app.RequiredResourceAccess?.Select(r => new
                {
                    ResourceAppId = r.ResourceAppId,
                    ResourceAccess = r.ResourceAccess?.Select(ra => new
                    {
                        Id = ra.Id,
                        Type = ra.Type
                    }).ToList()
                }).ToList()
            };            return JsonSerializer.Serialize(detailedInfo);
        }        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting application details for {appId}");
            
            // Provide a more user-friendly error message
            string errorMessage = $"Error getting application details: {ex.Message}";
            
            // Check for common error types
            if (ex.Message.Contains("timeout") || ex.Message.Contains("timed out"))
            {
                errorMessage = "The request to get application details timed out. Please try again later.";
            }
            else if (ex.Message.Contains("network") || ex.Message.Contains("connection"))
            {
                errorMessage = "A network error occurred while retrieving application details. Please check your connection.";
            }
              return JsonSerializer.Serialize(new { error = errorMessage });
        }
    }
      /// <summary>
    /// Tests the connection to Graph API by attempting to fetch a small amount of data
    /// </summary>
    public async Task<GraphApiTestResult> TestGraphConnectionAsync()
    {
        try
        {
            _logger.LogInformation("Testing connection to Microsoft Graph API");
            
            // Log configuration details (without sensitive info)
            _logger.LogInformation("Graph API connection test using TenantId: {TenantId}, ClientId: {ClientId}", 
                _configuration["AzureAd:TenantId"], 
                _configuration["AzureAd:ClientId"]);
                
            // First, check if we're authenticated
            _logger.LogInformation("Testing Microsoft Graph authentication");
            
            // Use a short timeout for the test
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            
            try 
            {
                // First try to get current user to test authentication
                _logger.LogInformation("Testing authentication by getting a simple object");
                var me = await _graphServiceClient.Me
                    .GetAsync(requestConfig => {
                        requestConfig.QueryParameters.Select = new[] { "displayName" };
                    }, cts.Token);
                    
                _logger.LogInformation("Authentication test successful");
            }
            catch (Exception authEx)
            {
                _logger.LogError(authEx, "Authentication test failed");
                
                // Return specific authentication error
                string authErrorMessage = "Authentication failed. ";
                
                if (authEx.Message.Contains("Unauthorized") || authEx.Message.Contains("401"))
                {
                    authErrorMessage += "Invalid client credentials. Please check your Client ID, Tenant ID, and Client Secret.";
                }
                else 
                {
                    authErrorMessage += authEx.Message;
                }
                
                return new GraphApiTestResult { Success = false, Message = authErrorMessage };
            }
            
            // Now test application permissions
            _logger.LogInformation("Testing Graph API permissions for Application.Read.All");
            
            try
            {
                // Attempt to get a small amount of data (just one app)
                var applications = await _graphServiceClient.Applications
                    .GetAsync(requestConfig => {
                        requestConfig.QueryParameters.Top = 1;  // Just get one record to test
                        requestConfig.QueryParameters.Select = new[] { "id", "displayName" };  // Only get minimal fields
                    }, cts.Token);
                    
                if (applications != null)
                {
                    return new GraphApiTestResult 
                    { 
                        Success = true, 
                        Message = $"Successfully connected to Graph API. Retrieved {applications.Value?.Count ?? 0} applications." 
                    };
                }
                
                return new GraphApiTestResult 
                { 
                    Success = false, 
                    Message = "Connection was established but no data was returned." 
                };
            }
            catch (Exception appEx)
            {
                _logger.LogError(appEx, "Error accessing applications");
                
                string permErrorMessage = "Authentication succeeded but ";
                
                // Check for permission-related errors
                if (appEx.Message.Contains("Forbidden") || appEx.Message.Contains("403") || 
                    appEx.Message.Contains("Authorization_RequestDenied"))
                {
                    permErrorMessage += "the application lacks the required 'Application.Read.All' permission. " +
                        "Please ensure this permission is granted and admin consent is provided in the Azure portal.";
                }
                else 
                {
                    permErrorMessage += $"an error occurred: {appEx.Message}";
                }
                
                return new GraphApiTestResult { Success = false, Message = permErrorMessage };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing connection to Graph API");
            
            string errorMessage = ex.Message;
            
            // Check for common error scenarios
            if (ex.Message.Contains("Unauthorized") || ex.Message.Contains("401"))
            {
                errorMessage = "Authentication failed. Please check your client credentials.";
            }
            else if (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
            {
                errorMessage = "Missing permissions. The service principal doesn't have the required permissions.";
            }
            else if (ex.Message.Contains("timeout") || ex.Message.Contains("timed out"))
            {
                errorMessage = "Connection timed out. Check the service is accessible.";
            }
            
            return new GraphApiTestResult { Success = false, Message = errorMessage };
        }
    }
    
    /// <summary>
    /// Returns the available functions that can be called through the MCP protocol
    /// </summary>
    public List<McpFunctionDeclaration> GetAvailableFunctions()
    {
        _logger.LogInformation("Returning {Count} available functions", _functions.Count);
        return _functions;
    }
    
    /// <summary>
    /// Executes a function by name with the provided arguments
    /// </summary>
    public async Task<object> ExecuteFunctionAsync(string functionName, Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing function {FunctionName} with {ArgumentCount} arguments",
                functionName, arguments?.Count ?? 0);
                
            // Check if the function exists
            var availableFunction = _functions.FirstOrDefault(f => f.Name == functionName);
            if (availableFunction == null)
            {
                _logger.LogWarning("Function {FunctionName} not found", functionName);
                return new { error = $"Function '{functionName}' not found" };
            }
            
            // Execute the appropriate function
            string result;
            
            switch (functionName)
            {
                case "get_app_registrations":
                    result = await GetAppRegistrationsInternalAsync();
                    break;
                    
                case "get_app_registration_details":
                    var appId = "";
                    if (arguments != null && arguments.TryGetValue("app_id", out var appIdObj))
                    {
                        appId = appIdObj?.ToString() ?? "";
                    }
                    result = await GetAppRegistrationDetailsInternalAsync(appId);
                    break;
                    
                default:
                    _logger.LogWarning("Function implementation for {FunctionName} not found", functionName);
                    return new { error = $"Function implementation for '{functionName}' not found" };
            }
            
            // Try to parse the result as JSON, otherwise return as string
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<object>(result) ?? new { result = result };
            }
            catch
            {
                return new { result = result };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return new { error = $"Error executing function: {ex.Message}" };
        }
    }
}
