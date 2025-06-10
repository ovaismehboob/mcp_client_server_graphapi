# Configure Azure AD and OpenAI Settings

This document explains how to configure the application with your Azure AD and OpenAI credentials.

## Prerequisites

1. Azure AD application registration
2. Azure OpenAI or OpenAI API key

## Configuration Steps

### 1. Server Configuration (MCP.Server/appsettings.json)

Replace the placeholder values in `MCP.Server/appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR_AZURE_OPENAI_ENDPOINT.openai.azure.com/",
    "DeploymentName": "gpt-35-turbo",
    "ApiKey": "YOUR_AZURE_OPENAI_API_KEY"
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "ModelName": "gpt-3.5-turbo"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "Scopes": ["User.Read", "Application.Read.All"]
  }
}
```

### 2. Client Configuration (MCP.Client/wwwroot/appsettings.json)

Replace the placeholder values in `MCP.Client/wwwroot/appsettings.json`:

```json
{
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "ValidateAuthority": true
  },
  "ApiBaseUrl": "http://localhost:5197",
  "AzureOpenAI": {
    "Endpoint": "https://YOUR_AZURE_OPENAI_ENDPOINT.openai.azure.com/",
    "ApiKey": "YOUR_AZURE_OPENAI_API_KEY",
    "DeploymentName": "gpt-35-turbo"
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY"
  }
}
```

### 3. Azure AD Application Setup

1. Go to [Azure Portal](https://portal.azure.com/)
2. Navigate to Azure Active Directory > App registrations
3. Create a new registration or use existing one
4. Configure the following:
   - **Redirect URIs**: Add your application URLs
   - **API Permissions**: Grant the following permissions:
     - Microsoft Graph: `User.Read`
     - Microsoft Graph: `Application.Read.All` (admin consent required)
5. Generate a client secret in "Certificates & secrets"
6. Note down:
   - **Tenant ID** (Directory ID)
   - **Client ID** (Application ID)
   - **Client Secret** (generated secret value)

### 4. Azure OpenAI Setup

1. Create an Azure OpenAI resource in Azure Portal
2. Deploy a model (e.g., gpt-35-turbo)
3. Get your:
   - **Endpoint URL**
   - **API Key**
   - **Deployment Name**

### 5. Alternative: OpenAI API

If using OpenAI instead of Azure OpenAI:
1. Get API key from [OpenAI Platform](https://platform.openai.com/)
2. Update the `OpenAI.ApiKey` field
3. Leave Azure OpenAI fields empty

## Security Notes

- Never commit real credentials to version control
- Use environment variables or Azure Key Vault for production
- The `.gitignore` file is configured to exclude sensitive files
- Consider using User Secrets for local development:

```bash
dotnet user-secrets init --project MCP.Server
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret" --project MCP.Server
```

## Local Development

For local development, you can create `appsettings.Development.json` files that won't be committed to git:

1. Copy the main appsettings.json
2. Rename to appsettings.Development.json  
3. Add your real credentials
4. The .gitignore will exclude these files

## Testing Configuration

Run the `TestGraphPermissions.ps1` script to verify your Azure AD configuration is working correctly.
