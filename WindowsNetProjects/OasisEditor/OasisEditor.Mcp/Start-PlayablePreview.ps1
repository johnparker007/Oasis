[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlayerExecutable,
    [Parameter(Mandatory)][string]$BuildRoot,
    [ValidateSet('buttons','pool')][string]$Interaction = 'buttons'
)
$ErrorActionPreference = 'Stop'
$player = (Resolve-Path -LiteralPath $PlayerExecutable).Path
$build = (Resolve-Path -LiteralPath $BuildRoot).Path
if (!(Test-Path -LiteralPath (Join-Path $build 'machine.runtime.json'))) { throw 'Expected an Editor-generated Machine build.' }
# Explicit user launcher: opens the playable application; never used by background verification.
Start-Process -FilePath $player -ArgumentList '--mode','machine-preview','--build',('"'+$build+'"'),'--interaction',$Interaction,'--windowed'
