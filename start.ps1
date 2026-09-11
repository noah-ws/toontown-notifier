# Starts the host Companion proxy (so Docker can reach TTR on localhost) and the notifier container.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

New-Item -ItemType Directory -Force -Path data | Out-Null

$proxyPort = 11547
$listening = Get-NetTCPConnection -LocalPort $proxyPort -State Listen -ErrorAction SilentlyContinue
if (-not $listening) {
    Write-Host "Building Companion proxy..."
    dotnet build src/CompanionProxy/CompanionProxy.csproj -c Release --nologo
    $dll = Join-Path $PSScriptRoot "src\CompanionProxy\bin\Release\net10.0\CompanionProxy.dll"
    Write-Host "Starting Companion proxy on 0.0.0.0:$proxyPort (forwards to 127.0.0.1:1547-1552)..."
    Start-Process -FilePath "dotnet" -ArgumentList $dll -WorkingDirectory $PSScriptRoot -WindowStyle Minimized
    Start-Sleep -Seconds 1
} else {
    Write-Host "Companion proxy already listening on $proxyPort"
}

if (-not (Test-Path .env)) {
    Copy-Item .env.example .env
    Write-Host "Created .env from .env.example. Set DISCORD_WEBHOOK_URL before you expect Discord pings."
}

docker compose up --build -d
Write-Host "Notifier is up. Follow logs with: docker compose logs -f"
Write-Host "In TTR: Options, Companion App Support, log into a Toon, then approve the in-game prompt."
