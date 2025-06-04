# MCP Graph API Demo

This project demonstrates the use of the Model Context Protocol (MCP) with Azure Graph API to query application registrations in your Azure tenant.

The application uses a Blazor WebAssembly client that communicates with an ASP.NET Core server implementing the MCP protocol. The server connects to Microsoft Graph API to retrieve application registration data, which is then processed and presented through an AI-assisted chat interface.

## Project Structure

- **MCP.Client**: Blazor WebAssembly client that interacts with the MCP server
- **MCP.Server**: ASP.NET Core server that implements the MCP protocol for Azure Graph API
- **MCP.Shared**: Shared models and interfaces between client and server

## Prerequisites

- .NET 9.0
- Azure subscription with Application Registration permissions
- Azure OpenAI resource or OpenAI API key

## Configuration

Before running the application, you need to configure it with your own Azure credentials:

1. **Update the server configuration**:
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

2. **Update the client configuration**:
   Edit `MCP.Client/wwwroot/appsettings.json` and update the following settings:

   ```json
   "AzureAd": {
     "Authority": "https://login.microsoftonline.com/<YOUR_TENANT_ID>",
     "ClientId": "<YOUR_CLIENT_ID>",
     "ValidateAuthority": true
   },
   "AzureOpenAI": {
     "Endpoint": "https://your-resource.openai.azure.com/",
     "ApiKey": "<YOUR_AZURE_OPENAI_API_KEY>",
     "DeploymentName": "your-deployment-name"
   }
   ```

1. Update the API keys in `MCP.Client/wwwroot/appsettings.json`:
   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://your-endpoint.openai.azure.com/",
       "ApiKey": "<YOUR_API_KEY>",
       "DeploymentName": "gpt-35-turbo"
     }
   }
   ```

2. Update the configuration in `MCP.Server/appsettings.json`:
   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://your-endpoint.openai.azure.com/",
       "DeploymentName": "gpt-35-turbo",
       "ApiKey": "<YOUR_API_KEY>"
     },
     "AzureAd": {
       "Instance": "https://login.microsoftonline.com/",
       "TenantId": "<YOUR_TENANT_ID>",
       "ClientId": "<YOUR_CLIENT_ID>",
       "ClientSecret": "<YOUR_CLIENT_SECRET>",
       "Scopes": ["User.Read", "Application.Read.All"]
     }
   }
   ```

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

3. **Configure Azure OpenAI**:
   - Create an Azure OpenAI resource or use OpenAI directly
   - Create a deployment for GPT model (e.g., gpt-35-turbo or gpt-4)
   - Get the API key and endpoint

4. **Update Configuration Files**:
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
