$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $projectRoot 'artifacts/release/catTransform.exe'
$process=Start-Process -FilePath $exe -PassThru
$process.Id | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts/release-pid.txt') -Encoding ascii
Start-Sleep -Seconds 5
$process.Refresh()
Write-Output "PID=$($process.Id) Exited=$($process.HasExited)"
if($process.HasExited){Write-Output "ExitCode=$($process.ExitCode)"}
$log=Join-Path $env:LOCALAPPDATA 'catTransform/logs/failures.log'
if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log -Tail 25}
Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'catTransform/logs/startup.log') -Tail 20 -ErrorAction SilentlyContinue
Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=(Get-Date).AddMinutes(-15)} -ErrorAction SilentlyContinue | Where-Object {$_.ProviderName -in '.NET Runtime','Application Error' -and $_.Message -match 'catTransform'} | Select-Object -First 3 -ExpandProperty Message
