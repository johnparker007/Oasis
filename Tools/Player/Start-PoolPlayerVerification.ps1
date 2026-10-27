[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildRoot,
    [Parameter(Mandatory)][string]$BridgeExecutable,
    [ValidateSet('SingleBall','Algorithmic','Playable','Arcade','Impact')][string]$Mode = 'SingleBall',
    [string]$ArcadeManifest,
    [string]$FabricRuntimeDll,
    [string]$AmberImpactDll,
    [string]$UnityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.0.47f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Unity is already running; do not interrupt the user session.' }
if (Get-NetTCPConnection -State Listen -LocalPort 8080 -ErrorAction SilentlyContinue) { throw 'Port 8080 is occupied.' }
$build = (Resolve-Path -LiteralPath $BuildRoot).Path
if (!(Test-Path -LiteralPath (Join-Path $build 'machine.runtime.json'))) { throw 'Expected generated Machine build.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project = Join-Path $repo 'UnityProjects\OasisPlayer'
if (!(Test-Path -LiteralPath (Join-Path $project 'Packages\manifest.json'))) { throw 'Unity project not found.' }
$session = Join-Path $env:TEMP ('OasisPoolPlayer-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $session | Out-Null
$env:OASIS_POOL_VERIFY_BUILD = $build
$env:OASIS_POOL_VERIFY_MODE = $Mode.ToLowerInvariant()
if ($Mode -eq 'Arcade' -or ($Mode -eq 'Impact' -and ![string]::IsNullOrWhiteSpace($ArcadeManifest))) {
    if ([string]::IsNullOrWhiteSpace($ArcadeManifest)) { throw 'Arcade verification requires -ArcadeManifest.' }
    $env:OASIS_ARCADE_VERIFY_LAYOUT = (Resolve-Path -LiteralPath $ArcadeManifest).Path
} else { Remove-Item Env:OASIS_ARCADE_VERIFY_LAYOUT -ErrorAction SilentlyContinue }
if ($Mode -eq 'Impact') {
    $env:OASIS_FABRIC_RUNTIME_DLL = (Resolve-Path -LiteralPath $FabricRuntimeDll).Path
    $env:OASIS_AMBER_IMPACT_DLL = (Resolve-Path -LiteralPath $AmberImpactDll).Path
}
$env:OASIS_VERIFY_SESSION_DIR = $session
$env:UNITY_MCP_ALLOW_BATCH = '1'
$env:UNITY_MCP_LOG_DIR = Join-Path $session 'server'
$server = Start-Process -FilePath (Resolve-Path -LiteralPath $BridgeExecutable).Path -ArgumentList '--transport','http','--http-url','http://127.0.0.1:8080' -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $session 'server.stdout.log') -RedirectStandardError (Join-Path $session 'server.stderr.log')
$unity = Start-Process -FilePath $UnityExecutable -ArgumentList '-batchmode','-nographics','-projectPath',('"'+$project+'"'),'-executeMethod','OasisPlayer.Editor.Verification.OasisVerificationSession.Begin','-logFile',('"'+(Join-Path $session 'Editor.log')+'"') -WindowStyle Hidden -PassThru
[pscustomobject]@{ serverPid=$server.Id; serverPath=(Resolve-Path -LiteralPath $BridgeExecutable).Path; serverStarted=$server.StartTime.ToUniversalTime(); unityPid=$unity.Id; unityPath=(Resolve-Path -LiteralPath $UnityExecutable).Path; unityStarted=$unity.StartTime.ToUniversalTime(); directory=$session } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $session 'processes.json')
Write-Output $session
