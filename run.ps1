$ErrorActionPreference = "Stop"

$repo = "24aq/24aqSSTool"

Write-Host "24aq SS Tools - downloading latest version..."

$release = Invoke-RestMethod `
    -Uri "https://api.github.com/repos/$repo/releases/latest" `
    -Headers @{ "User-Agent" = "24aqSSTool" }

$asset = $release.assets |
    Where-Object { $_.name -eq "24aqSSTool-win-x64.zip" } |
    Select-Object -First 1

if (-not $asset) {
    throw "24aqSSTool-win-x64.zip was not found."
}

$installDir = "$env:LOCALAPPDATA\24aqSSTool"
$zipPath = "$env:TEMP\24aqSSTool.zip"

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null

Write-Host "Downloading..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath

Write-Host "Extracting..."
Expand-Archive -Path $zipPath -DestinationPath $installDir -Force

$exe = Get-ChildItem `
    -Path $installDir `
    -Filter "24aqSSTool.exe" `
    -Recurse |
    Select-Object -First 1

if (-not $exe) {
    throw "24aqSSTool.exe was not found."
}

Write-Host "Starting 24aq SS Tools..."
Start-Process $exe.FullName
