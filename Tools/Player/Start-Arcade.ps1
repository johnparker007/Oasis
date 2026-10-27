[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlayerExecutable,
    [Parameter(Mandatory)][string]$ArcadeManifest,
    [string]$FabricRuntimeDll,
    [string]$AmberImpactDll
)
$ErrorActionPreference = 'Stop'
$player = (Resolve-Path -LiteralPath $PlayerExecutable).Path
$layout = (Resolve-Path -LiteralPath $ArcadeManifest).Path
# User launcher only. Background verification uses the separate hidden batch tools.
$arguments = @('--mode','arcade','--arcade',('"'+$layout+'"'),'--windowed')
if ($FabricRuntimeDll) { $arguments += @('--fabric-runtime',('"'+(Resolve-Path -LiteralPath $FabricRuntimeDll).Path+'"')) }
if ($AmberImpactDll) { $arguments += @('--amber-impact',('"'+(Resolve-Path -LiteralPath $AmberImpactDll).Path+'"')) }
Start-Process -FilePath $player -ArgumentList $arguments
