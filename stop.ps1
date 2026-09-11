# Stop the notifier container and the host Companion proxy.
Set-Location $PSScriptRoot
docker compose down
Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object { $_.CommandLine -match 'CompanionProxy' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Write-Host "Stopped notifier and Companion proxy."
