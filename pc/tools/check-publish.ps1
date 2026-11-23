# C28: um único claudinho.exe self-contained que roda sem nenhum .NET instalado
$ErrorActionPreference = 'Stop'
$pc  = Split-Path $PSScriptRoot -Parent
$out = Join-Path (Split-Path $pc -Parent) 'dist'
dotnet publish (Join-Path $pc 'Claudinho') -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $out | Out-Null
if ($LASTEXITCODE) { Write-Error 'dotnet publish falhou'; exit 1 }

$files = @(Get-ChildItem $out -File)
if ($files.Count -ne 1 -or $files[0].Name -ne 'claudinho.exe') {
  Write-Error "esperado só claudinho.exe em dist, veio: $($files.Name -join ', ')"; exit 1
}

# esconde qualquer .NET da máquina: o exe tem de levar o runtime dentro
$env:DOTNET_ROOT = 'C:\nao-existe'
$env:PATH = "$env:SystemRoot\System32"
$v = & (Join-Path $out 'claudinho.exe') --version
if ($LASTEXITCODE -ne 0 -or $v -notmatch '^claudinho \d') { Write-Error "--version falhou: $v"; exit 1 }
"OK: $v, $([math]::Round($files[0].Length / 1MB, 1)) MB, sem .NET no PATH"
