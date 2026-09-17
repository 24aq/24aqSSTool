# 24aq SS Tools bootstrapper
# Replace the two values below after you create a GitHub Release.

$DownloadUrl = "https://github.com/YOUR-USERNAME/24aq-ss-Tool/releases/download/v1.0.0/24aqSSTool.exe"
$ExpectedSha256 = ""

$InstallDir = Join-Path $env:LOCALAPPDATA "24aqSSTool"
$ExePath = Join-Path $InstallDir "24aqSSTool.exe"

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

Write-Host "[24aq] Downloading SS Tools..." -ForegroundColor Green
Invoke-WebRequest -Uri $DownloadUrl -OutFile $ExePath

if ($ExpectedSha256 -ne "") {
    $ActualSha256 = (Get-FileHash -Path $ExePath -Algorithm SHA256).Hash

    if ($ActualSha256 -ne $ExpectedSha256) {
        Remove-Item $ExePath -Force
        throw "SHA-256 mismatch. Download removed."
    }
}

Write-Host "[24aq] Starting..." -ForegroundColor Green
Start-Process $ExePath