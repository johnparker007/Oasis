param([string]$OutputDirectory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'fabric_layout_probe.c'
$output = Join-Path $OutputDirectory 'fabric_layout_probe.exe'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
& cl.exe /nologo /W4 /WX /O2 /Fe:$output $source
if ($LASTEXITCODE -ne 0) { throw "cl.exe failed with exit code $LASTEXITCODE" }
Write-Output $output
