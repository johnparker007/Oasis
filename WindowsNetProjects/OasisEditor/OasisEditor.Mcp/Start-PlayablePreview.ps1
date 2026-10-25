[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlayerExecutable,
    [Parameter(Mandatory)][string]$BuildRoot,
    [ValidateSet('','buttons','pool')][string]$Interaction = '',
    [string]$FabricRuntimeDll,
    [string]$AmberImpactDll
)
$ErrorActionPreference = 'Stop'
$player = (Resolve-Path -LiteralPath $PlayerExecutable).Path
$build = (Resolve-Path -LiteralPath $BuildRoot).Path
if (!(Test-Path -LiteralPath (Join-Path $build 'machine.runtime.json'))) { throw 'Expected an Editor-generated Machine build.' }
# Explicit user launcher: opens the playable application; never used by background verification.
$arguments = @('--mode','machine-preview','--build',('"'+$build+'"'),'--windowed')
if ($Interaction) { $arguments += @('--interaction',$Interaction) }
if ($FabricRuntimeDll) { $arguments += @('--fabric-runtime',('"'+(Resolve-Path -LiteralPath $FabricRuntimeDll).Path+'"')) }
if ($AmberImpactDll) { $arguments += @('--amber-impact',('"'+(Resolve-Path -LiteralPath $AmberImpactDll).Path+'"')) }
Start-Process -FilePath $player -ArgumentList $arguments
