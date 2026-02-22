# Increment Build Number Script
# Run this before each debug build to auto-increment the build number

$versionFile = Join-Path $PSScriptRoot "version.json"

if (Test-Path $versionFile) {
    $json = Get-Content $versionFile -Raw | ConvertFrom-Json
    
    # Increment build number
    $json.buildNumber = [int]$json.buildNumber + 1
    
    # Update build date
    $json.buildDate = Get-Date -Format "yyyy-MM-dd"
    
    # Save back to file
    $json | ConvertTo-Json | Set-Content $versionFile
    
    Write-Host "Build number incremented to: $($json.version).$($json.buildNumber)" -ForegroundColor Green
    Write-Host "Build date updated to: $($json.buildDate)" -ForegroundColor Green
} else {
    Write-Host "version.json not found!" -ForegroundColor Red
}
