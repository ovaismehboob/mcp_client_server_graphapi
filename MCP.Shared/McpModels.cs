using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MCP.Shared.MCP;

// MCP Message Models
public class McpMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

// MCP Request Models
public class McpRequest
{
    [JsonPropertyName("messages")]
    public List<McpMessage> Messages { get; set; } = new();

    [JsonPropertyName("context")]
    public Dictionary<string, object> Context { get; set; } = new();
}

// MCP Response Models
public class McpResponse
{
    [JsonPropertyName("message")]
    public McpMessage? Message { get; set; }

    [JsonPropertyName("usage")]
    public McpUsage? Usage { get; set; }
    
    [JsonPropertyName("context_updates")]
    public Dictionary<string, object>? ContextUpdates { get; set; }
    
    [JsonPropertyName("available_functions")]
    public List<McpFunctionDeclaration>? AvailableFunctions { get; set; }
}

public class McpUsage
{
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

// Function Calling Models
public class McpFunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public McpFunctionParameters? Parameters { get; set; }
}

public class McpFunctionParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, McpParameterProperty> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

public class McpParameterProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class McpFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = new();
}

// MCP Content Blocks
public class McpContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("function_call")]
    public McpFunctionCall? FunctionCall { get; set; }
}

// Graph API specific models
public class GraphApiResponse
{
    [JsonPropertyName("value")]
    public List<ApplicationRegistration> Value { get; set; } = new();
}

public class ApplicationRegistration
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("appId")]
    public string AppId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
