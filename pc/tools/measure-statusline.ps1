# C5: mediana de 5 execuções do statusline publicado abaixo de 500 ms
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$exe = Join-Path $PSScriptRoot '..\..\dist\claudinho.exe'
if (-not (Test-Path $exe)) { & (Join-Path $PSScriptRoot 'check-publish.ps1') | Out-Null }
$fixture = Get-Content (Join-Path $PSScriptRoot 'fixtures\full.json') -Raw
$env:CLAUDINHO_HOME = Join-Path ([IO.Path]::GetTempPath()) 'claudinho-measure'

# segura o mutex do bridge: o statusline acha que já existe um e não sobe outro durante a medição
$owned = $false
$m = [Threading.Mutex]::new($true, 'Local\Claudinho.Bridge', [ref]$owned)
try {
  $times = foreach ($i in 1..5) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = $fixture | & $exe statusline
    $sw.Stop()
    if ($out -ne '5h 24% · 7d 41% · ctx 37%') { Write-Error "saída inesperada: $out"; exit 1 }
    $sw.Elapsed.TotalMilliseconds
  }
} finally {
  if ($owned) { $m.ReleaseMutex() }
  $m.Dispose()
}
$med = ($times | Sort-Object)[2]
"mediana: $([math]::Round($med)) ms (execuções: $(($times | ForEach-Object { [math]::Round($_) }) -join ', ') ms)"
if ($med -ge 500) { exit 1 }
