param([Parameter(Mandatory)][string]$SessionDirectory)
$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $SessionDirectory).Path
$state = Get-Content -LiteralPath (Join-Path $directory 'processes.json') -Raw | ConvertFrom-Json
if ($state.directory -ne $directory) { throw 'Session directory does not match recorded process state.' }
foreach ($kind in @('unity','server')) {
    $process = Get-Process -Id $state.($kind+'Pid') -ErrorAction SilentlyContinue
    if (!$process) { continue }
    if ($process.Path -ne $state.($kind+'Path') -or [math]::Abs(($process.StartTime.ToUniversalTime()-([datetime]$state.($kind+'Started')).ToUniversalTime()).TotalSeconds) -gt 2) { throw 'Process identity changed; refusing to stop it.' }
    if ($kind -eq 'unity') {
        [IO.File]::WriteAllText((Join-Path $directory 'stop.txt'),'Verification complete')
        if (!$process.WaitForExit(30000)) { throw 'Unity did not stop gracefully; left running.' }
    } else {
        & taskkill.exe /PID $process.Id /T /F | Out-Null
        if ($LASTEXITCODE -ne 0 -and (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) { throw 'Bridge did not stop.' }
    }
}
Write-Output 'Owned verification session stopped; logs retained.'
