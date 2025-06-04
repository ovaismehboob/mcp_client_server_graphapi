using Microsoft.AspNetCore.Mvc;
using MCP.Server.Services;
using MCP.Shared.MCP;

namespace MCP.Server.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly McpGraphService _mcpService;
    private readonly ILogger<McpController> _logger;

    public McpController(McpGraphService mcpService, ILogger<McpController> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }
      
    /// <summary>
    /// Returns the status of the MCP service
    /// </summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        try
        {
            return Ok(new 
            { 
                status = "ok",
                timestamp = DateTime.UtcNow,
                service = "MCP Graph API"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service status");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
      
    /// <summary>
    /// Returns the available functions for use with the MCP protocol
    /// </summary>
    [HttpGet("functions")]
    public ActionResult<object> GetAvailableFunctions()
    {
        try
        {
            _logger.LogInformation("Client requesting available MCP functions");
            
            // Get functions from the service
            var functions = _mcpService.GetAvailableFunctions();
            
            return Ok(new 
            { 
                functions = functions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available functions");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
      
    /// <summary>
    /// Tests the connection to Graph API
    /// </summary>
    [HttpGet("test-graph")]
    public async Task<ActionResult<object>> TestGraphConnection()
    {
        try
        {
            _logger.LogInformation("Starting Graph API connection test");
            var result = await _mcpService.TestGraphConnectionAsync();
            
            _logger.LogInformation("Graph API test completed with result: {Success}, Message: {Message}", 
                result.Success, result.Message);
                
            return Ok(new
            {
                status = result.Success ? "ok" : "error",
                message = result.Message,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Graph API connection");
            
            // Create a more detailed error message
            var errorDetails = "Error testing Graph API connection";
            
            if (ex.InnerException != null)
            {
                errorDetails += $": {ex.Message}. Inner exception: {ex.InnerException.Message}";
            }
            else
            {
                errorDetails += $": {ex.Message}";
            }
            
            return StatusCode(500, new { 
                status = "error", 
                message = errorDetails,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }
    }

    /// <summary>
    /// Executes a specific function with the provided arguments
    /// </summary>
    [HttpPost("execute")]
    public async Task<ActionResult<object>> ExecuteFunction([FromBody] McpFunctionCall functionCall, CancellationToken cancellationToken)
    {
        try
        {
            if (functionCall == null || string.IsNullOrEmpty(functionCall.Name))
            {
                _logger.LogWarning("Received invalid function call with no function name");
                return BadRequest(new { error = "Invalid request. No function name provided." });
            }

            _logger.LogInformation("Executing function: {FunctionName}", functionCall.Name);
            
            // Use a timeout to avoid hanging requests
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 seconds timeout
            
            var result = await _mcpService.ExecuteFunctionAsync(functionCall.Name, functionCall.Arguments, timeoutCts.Token);
            
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Function execution timed out: {FunctionName}", functionCall?.Name);
            return StatusCode(504, new { error = "The function execution timed out." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function: {FunctionName}", functionCall?.Name);
            return StatusCode(500, new { error = $"Error: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<McpResponse>> ProcessMessage([FromBody] McpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null || request.Messages == null || !request.Messages.Any())
            {
                _logger.LogWarning("Received invalid MCP request with no messages");
                return BadRequest(new McpResponse
                {
                    Message = new McpMessage
                    {
                        Role = "assistant",
                        Content = "Invalid request. No messages provided."
                    }
                });
            }

            _logger.LogInformation("Received MCP request with {MessageCount} messages", request.Messages.Count);
              // Use a timeout to avoid hanging requests
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(55)); // 55 seconds timeout
            
            var response = await _mcpService.ProcessMessageAsync(request, timeoutCts.Token);
            
            _logger.LogInformation("Processed MCP request successfully");
            
            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("MCP request processing timed out");
            return StatusCode(504, new McpResponse
            {
                Message = new McpMessage
                {
                    Role = "assistant",
                    Content = "The request timed out. Please try again with a simpler query or try later when the system is less busy."
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP request");
            
            // Return a more specific error message if possible
            var errorMessage = "An error occurred while processing your request. Please try again later.";
            
            if (ex.Message.Contains("Graph"))
            {
                errorMessage = "An error occurred while connecting to Microsoft Graph API. This might be due to authentication issues or permission problems.";
            }
            else if (ex.Message.Contains("Azure") || ex.Message.Contains("OpenAI"))
            {
                errorMessage = "An error occurred with the language model service. This might be due to service availability or configuration issues.";
            }
            
            return StatusCode(500, new McpResponse
            {
                Message = new McpMessage
                {
                    Role = "assistant",
                    Content = errorMessage
                }
            });
        }
    }
}
