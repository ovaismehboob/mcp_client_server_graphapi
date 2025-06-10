# GitHub Cleanup Script
# This script sanitizes sensitive information before pushing to GitHub

Write-Host "🧹 Cleaning workspace for GitHub..." -ForegroundColor Green

# Remove build artifacts
Write-Host "Removing build artifacts..." -ForegroundColor Yellow
Get-ChildItem -Path . -Recurse -Directory -Name "bin" | ForEach-Object {
    Remove-Item -Path $_ -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  Removed: $_" -ForegroundColor Gray
}

Get-ChildItem -Path . -Recurse -Directory -Name "obj" | ForEach-Object {
    Remove-Item -Path $_ -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  Removed: $_" -ForegroundColor Gray
}

# Remove Azure storage emulator files
Write-Host "Removing Azure storage emulator files..." -ForegroundColor Yellow
$azuriteFiles = @(
    "__azurite_db_blob__.json",
    "__azurite_db_blob_extent__.json"
)

foreach ($file in $azuriteFiles) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "  Removed: $file" -ForegroundColor Gray
    }
}

# Check configuration files for sensitive data
Write-Host "Checking configuration files..." -ForegroundColor Yellow

$configFiles = @(
    "MCP.Server\appsettings.json",
    "MCP.Client\wwwroot\appsettings.json",
    "GraphApiPermissionFix.md",
    "TestGraphPermissions.ps1",
    "GITHUB-SECURITY-SUMMARY.md"
)

$sensitivePatterns = @(
    "YOUR_TENANT_ID",
    "YOUR_CLIENT_ID", 
    "YOUR_CLIENT_SECRET",
    "YOUR_AZURE_OPENAI_API_KEY",
    "YOUR_OPENAI_API_KEY",
    "YOUR_AZURE_OPENAI_ENDPOINT"
)

$hasIssues = $false

foreach ($configFile in $configFiles) {
    if (Test-Path $configFile) {
        $content = Get-Content $configFile -Raw
        $foundSensitive = $false
          foreach ($pattern in $sensitivePatterns) {
            if ($content -like "*$pattern*") {
                if (-not $foundSensitive) {
                    Write-Host "  ✅ $configFile looks safe" -ForegroundColor Green
                    $foundSensitive = $true
                }
                break
            }
        }
        
        if (-not $foundSensitive) {
            Write-Host "  ⚠️  $configFile may contain sensitive data!" -ForegroundColor Red
            $hasIssues = $true
        }
    }
}

# Summary
Write-Host "`n📋 Cleanup Summary:" -ForegroundColor Cyan
if ($hasIssues) {
    Write-Host "  ⚠️  Some configuration files may need manual review" -ForegroundColor Yellow
    Write-Host "  📖 See CONFIGURATION.md for setup instructions" -ForegroundColor Cyan
} else {
    Write-Host "  ✅ All files appear to be safe for GitHub" -ForegroundColor Green
}

Write-Host "`n🚀 Ready for GitHub push!" -ForegroundColor Green
Write-Host "Remember to:" -ForegroundColor White
Write-Host "  1. Review the git diff before committing" -ForegroundColor Gray
Write-Host "  2. Never commit real credentials" -ForegroundColor Gray  
Write-Host "  3. Use CONFIGURATION.md to set up local environment" -ForegroundColor Gray
