# Graph API Permission Test Script
# This script uses the Microsoft Graph PowerShell module to test if your account has the right permissions
# to access application registrations in Azure AD

# Check if Microsoft Graph module is installed
if (-not (Get-Module -ListAvailable -Name Microsoft.Graph)) {
    Write-Host "Microsoft Graph PowerShell module is not installed." -ForegroundColor Yellow
    Write-Host "Installing module..." -ForegroundColor Yellow
    Install-Module Microsoft.Graph -Scope CurrentUser -Force
}

# Import the module
Import-Module Microsoft.Graph

# Define the required scopes
$scopes = @(
    "Application.Read.All"
)

# Connect to Microsoft Graph
try {
    Write-Host "Connecting to Microsoft Graph..." -ForegroundColor Blue
    Connect-MgGraph -Scopes $scopes
    
    # Get context info
    $context = Get-MgContext
    Write-Host "Connected as: $($context.Account)" -ForegroundColor Green
    Write-Host "Tenant ID: $($context.TenantId)" -ForegroundColor Green
    
    # Try to get applications
    Write-Host "Testing Application.Read.All permission..." -ForegroundColor Blue
    $applications = Get-MgApplication -Top 5
    
    if ($applications) {
        Write-Host "SUCCESS! Found $($applications.Count) applications." -ForegroundColor Green
        Write-Host "This confirms you have the necessary permissions." -ForegroundColor Green
        
        # Display first few apps
        Write-Host "`nSample of application registrations:" -ForegroundColor Cyan
        $applications | Select-Object DisplayName, Id, AppId | Format-Table
    } else {
        Write-Host "No applications found. This could indicate a permission issue." -ForegroundColor Yellow
    }
} 
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
      if ($_.Exception.Message -like "*Authorization_RequestDenied*" -or $_.Exception.Message -like "*forbidden*") {
        Write-Host "`nThe error indicates a permissions issue. Please follow these steps:" -ForegroundColor Yellow
        Write-Host "1. Go to Azure Portal > Azure Active Directory > App registrations" -ForegroundColor Yellow
        Write-Host "2. Find your app with Client ID: YOUR_CLIENT_ID" -ForegroundColor Yellow
        Write-Host "3. Go to API Permissions and ensure Application.Read.All is granted" -ForegroundColor Yellow
        Write-Host "4. Click on Grant admin consent button" -ForegroundColor Yellow
    }
}

Write-Host "`nTest completed. Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
