[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlayerExecutable,
    [Parameter(Mandatory)][string]$ArcadeManifest
)
$ErrorActionPreference = 'Stop'
$player = (Resolve-Path -LiteralPath $PlayerExecutable).Path
$layout = (Resolve-Path -LiteralPath $ArcadeManifest).Path
# User launcher only. Background verification uses the separate hidden batch tools.
Start-Process -FilePath $player -ArgumentList '--mode','arcade','--arcade',('"'+$layout+'"'),'--windowed'
