# MCP Graph API Demo

This project demonstrates the use of the Model Context Protocol (MCP) with Azure Graph API to query application registrations in your Azure tenant.

The application uses a Blazor WebAssembly client that communicates with an ASP.NET Core server implementing the MCP protocol. The server connects to Microsoft Graph API to retrieve application registration data, which is then processed and presented through an AI-assisted chat interface.

## Architecture Overview

```
+---------------------+                 +---------------------+
|     MCP Client      |                 |     MCP Server      |
|                     |                 |                     |
|  +---------------+  |    MCP API      |  +--------------+   |
|  |     LLM       |  |  Requests &     |  |   Graph API  |   |
|  | Integration   |<-+-->Functions     |  |   Functions  |<--+--> Microsoft Graph API
|  | (SK + OpenAI) |  |                 |  |              |   |
|  +---------------+  |                 |  +--------------+   |
|                     |                 |                     |
|  +---------------+  |                 |  +--------------+   |
|  |    UI/UX      |  |                 |  | Azure AD     |   |
|  |  (Blazor WASM)|  |                 |  | Integration  |<--+--> Azure Active Directory
|  +---------------+  |                 |  +--------------+   |
+---------------------+                 +---------------------+

Client handles:                        Server handles:
- User Interface                       - Function Definitions
- Prompt Engineering                   - Graph API Integration
- LLM Integration                      - Azure AD Authentication
- Function Orchestration               - Data Processing
```

This architecture strictly follows MCP principles where:
1. The MCP Client handles all LLM interactions and user experience
2. The MCP Server provides function definitions and handles actual Graph API calls
3. Communication happens via standardized MCP protocol

## Project Structure

- **MCP.Client**: Blazor WebAssembly client that interacts with the MCP server
  - Handles user interface and chat experience
  - Integrates with Azure OpenAI or OpenAI APIs
  - Uses Semantic Kernel to orchestrate LLM interactions and function calling

- **MCP.Server**: ASP.NET Core server that implements the MCP protocol for Azure Graph API
  - Provides function definitions for Graph API operations
  - Handles authentication with Azure AD
  - Executes Graph API calls when requested by the client
  - Returns structured data to the client

- **MCP.Shared**: Shared models and interfaces between client and server
  - Defines the MCP protocol structures
  - Contains shared data models and interfaces

## What is the Model Context Protocol (MCP)?

The Model Context Protocol (MCP) is an architectural pattern for LLM-powered applications that separates:

1. **LLM Integration** (Client-side): User interface, prompt engineering, LLM calls
2. **Tool/Function Integration** (Server-side): Capabilities that the LLM can use

MCP provides a clean separation of concerns:
- The client handles all LLM interactions and user experience
- The server focuses on providing function definitions and executing them when called
- This separation allows for better security, modularity, and scalability

This project implements MCP by:
1. Exposing Graph API capabilities as functions on the server
2. Allowing the client's LLM to discover and call these functions
3. Never exposing the LLM keys to the server or the function implementation details to the client

## Prerequisites

- .NET 9.0
- Azure subscription with Application Registration permissions
- Azure OpenAI resource or OpenAI API key

## Configuration

Before running the application, you need to configure it with your own Azure credentials:

### MCP Server Configuration

Edit `MCP.Server/appsettings.json` and update the following settings:

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "<YOUR_TENANT_ID>",
  "ClientId": "<YOUR_CLIENT_ID>",
  "ClientSecret": "<YOUR_CLIENT_SECRET>",
  "Scopes": ["User.Read", "Application.Read.All"]
}
```

### MCP Client Configuration

Edit `MCP.Client/wwwroot/appsettings.json` and update the following settings:

```json
"AzureAd": {
  "Authority": "https://login.microsoftonline.com/<YOUR_TENANT_ID>",
  "ClientId": "<YOUR_CLIENT_ID>",
  "ValidateAuthority": true
},
"ApiBaseUrl": "http://localhost:5197",
"AzureOpenAI": {
  "Endpoint": "https://your-resource.openai.azure.com/",
  "ApiKey": "<YOUR_AZURE_OPENAI_API_KEY>",
  "DeploymentName": "gpt-35-turbo"
}
```

> **Note**: The MCP Server no longer requires OpenAI configuration as it doesn't interact with LLMs directly. All LLM integration is handled by the client.

## Setup Instructions

1. **Prerequisites**:
   - Install .NET 9.0 SDK
   - Have an Azure subscription with permissions to create app registrations
   - Have access to Azure OpenAI or OpenAI API

2. **Register an Application in Azure AD**:
   - Go to Azure Portal > Azure Active Directory > App registrations
   - Create a new registration
   - Set up a client secret
   - Grant the following API permissions:
     - Microsoft Graph API > Application.Read.All
     - Make sure to grant admin consent
   - See [GraphApiPermissionFix.md](GraphApiPermissionFix.md) for detailed troubleshooting of permissions

3. **Configure Azure OpenAI**:
   - Create an Azure OpenAI resource or use OpenAI directly
   - Create a deployment for GPT model (e.g., gpt-35-turbo or gpt-4)
   - Get the API key and endpoint

4. **Update Configuration Files**:
   - Update the server and client configuration files as described in the Configuration section above

## Running the Application

1. Start the MCP Server:
   ```bash
   cd MCP.Server
   dotnet run
   ```

2. Start the MCP Client (in a new terminal):
   ```bash
   cd MCP.Client
   dotnet run
   ```

3. Open a browser and navigate to:
   - http://localhost:5268 (client)
   - http://localhost:5197/swagger (server API documentation)

4. Use the chat interface to ask questions about your Azure App Registrations:
   - "List all app registrations in my tenant"
   - "Get details for app registration with ID [app-id]"
   - "What permissions does [app-name] have?"

## Developing with MCP

To extend this application with new Graph API capabilities:

1. Add new function declarations in `MCP.Server/Services/McpGraphService.cs`
2. Implement the function logic in the same file
3. Update the `ExecuteFunctionAsync` method to handle your new function
4. The client's LLM will automatically discover and use the new function when appropriate

Example function declaration:
```csharp
new McpFunctionDeclaration
{
    Name = "get_user_details",
    Description = "Get details for a specific user in the tenant",
    Parameters = new McpFunctionParameters
    {
        Type = "object",
        Properties = new Dictionary<string, McpParameterProperty>
        {
            ["user_id"] = new McpParameterProperty
            {
                Type = "string",
                Description = "The ID of the user"
            }
        },
        Required = new List<string> { "user_id" }
    }
}
```

## GitHub Preparation

Before pushing this project to a public GitHub repository:

1. Run the included cleaning script to sanitize configuration files:
   ```powershell
   .\clean-for-github.ps1
   ```

2. Verify that no sensitive data is present:
   - All `appsettings.json` files should use placeholders (`<YOUR_*>`)
   - No actual API keys or credentials should be included
   - No `.bak` or `.private` files should be committed

3. Review the [GITHUB-CHECKLIST.md](GITHUB-CHECKLIST.md) file for a complete pre-publishing checklist

## Security Considerations

- Never commit real API keys or secrets to GitHub
- The MCP architecture enhances security by keeping LLM keys on the client side only
- The server only needs access to Graph API, not to any LLM services
- Use Azure Key Vault for production deployments to securely store API keys
- See [CONTRIBUTING.md](CONTRIBUTING.md) for more security best practices

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
   - Follow the Configuration section to update the settings files

## Running the Application

1. **Start the server**:
   ```powershell
   cd MCP.Server
   dotnet run --launch-profile https
   ```

2. **Start the client**:
   ```powershell
   cd MCP.Client
   dotnet run
   ```

3. **Access the application**:
   Open your browser and navigate to `https://localhost:7030/chat`

## Troubleshooting

If you encounter permission issues, check the following:

1. **Verify API Permissions**: Make sure your app registration has the required Microsoft Graph permissions and admin consent has been granted.

2. **Check Authentication**: Ensure your Azure AD credentials are correct in the configuration files.

3. **Run the Test Script**: Use the provided PowerShell script to test your Graph API permissions:
   ```powershell
   ./TestGraphPermissions.ps1
   ```

## Features

- Chat interface to query information about your Azure tenant
- Access to Graph API functions through MCP
- Semantic Kernel integration for LLM functions

## Preparing for GitHub

Before pushing this project to a public repository, run the cleaning script to remove sensitive information:

```powershell
./clean-for-github.ps1
```

This script will:
1. Remove unnecessary files (bin, obj folders)
2. Remove Azure storage emulator files
3. Sanitize configuration files by replacing sensitive data with placeholders
4. Create backups of the original files with `.private.bak` extension

## Contributing

If you'd like to contribute to this project, please read our [Contributing Guidelines](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgements

- [Model Context Protocol (MCP)](https://github.com/microsoft/MicrosoftModelContextProtocol) - For providing the framework for AI model interactions
- [Microsoft Graph API](https://learn.microsoft.com/en-us/graph/overview) - For accessing Azure AD data
