# Contributing Guidelines

Thank you for considering contributing to the MCP Graph API Demo project! This document provides guidelines for contributing to ensure that the project remains secure and accessible to all users.

## Security Guidelines

### Sensitive Information

When contributing to this project, please ensure that you never commit any sensitive information, including:

- API keys
- Client secrets
- Tenant IDs
- Application (client) IDs
- Personal identification information
- Connection strings

### Using the Clean Script

Before pushing changes to GitHub, always run the cleaning script to ensure sensitive information is removed:

```powershell
./clean-for-github.ps1
```

The script will:
1. Remove unnecessary files (bin, obj folders)
2. Remove Azure storage emulator files
3. Sanitize configuration files by replacing sensitive data with placeholders
4. Create backups of the original files with `.private.bak` extension

### Working with Configuration Files

- When working with configuration files, use placeholder values in the committed versions
- Store your actual credentials in local copies only
- Consider using user secrets or environment variables for local development

## Development Guidelines

### Project Structure

The project consists of three main components:

- **MCP.Client**: Blazor WebAssembly client application
- **MCP.Server**: ASP.NET Core server implementing the MCP protocol
- **MCP.Shared**: Shared models and contracts

### Testing Changes

Before submitting a pull request:

1. Run the application locally to ensure it works correctly
2. Check for any hardcoded secrets or credentials
3. Run the clean script and verify that sensitive information is removed
4. Ensure that the README and documentation are updated to reflect your changes

### Pull Request Process

1. Create a branch for your feature or fix
2. Make your changes and test them
3. Run the clean script to sanitize your changes
4. Submit a pull request to the main branch
5. Wait for code review and approval

## License

By contributing to this project, you agree that your contributions will be licensed under the same MIT License that covers the project.
