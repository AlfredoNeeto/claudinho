# C1, C3, C6, C14 (ble-link): com a CYD só na fonte, o bridge de verdade a acha por BLE e reconecta depois de cair.
# Antes: `claudinho uninstall` (o bridge instalado segura o mutex) e `pwsh pc/tools/check-publish.ps1` (gera dist/claudinho.exe).
param([string]$Exe = (Join-Path $PSScriptRoot '..\..\dist\claudinho.exe'))
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Exe)) { Write-Error "não achei $Exe; rode pc/tools/check-publish.ps1"; exit 1 }
if (Get-Process claudinho -ErrorAction SilentlyContinue) { Write-Error 'um claudinho já está rodando; rode claudinho uninstall antes'; exit 1 }

# com a placa no USB o bridge nem procura o BLE (AC 8): este teste exige a placa só na fonte
$present = [IO.Ports.SerialPort]::GetPortNames()
$usb = Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Enum\USB' -ErrorAction SilentlyContinue |
  Where-Object { $_.PSChildName -like 'VID_1A86*' -or $_.PSChildName -like 'VID_303A*' } | Get-ChildItem |
  ForEach-Object { (Get-ItemProperty "$($_.PSPath)\Device Parameters" -ErrorAction SilentlyContinue).PortName } |
  Where-Object { $_ -in $present }
if ($usb) { Write-Error "a placa está no USB deste PC ($($usb -join ', ')); ligue-a numa fonte"; exit 1 }

$dir = Join-Path ([IO.Path]::GetTempPath()) "claudinho-ble-$PID"
New-Item -ItemType Directory -Force $dir | Out-Null
$log = Join-Path $dir 'bridge.log'
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
Set-Content (Join-Path $dir 'snapshot.json') -NoNewline -Value "{`"v`":1,`"t`":$now,`"tz`":-180,`"h5`":[42,$($now + 7200)],`"d7`":[17,$($now + 300000)],`"ctx`":12,`"usd`":1.5,`"m`":`"Opus`",`"p`":`"ble-check`"}"
$env:CLAUDINHO_HOME = $dir

function Wait-Connected([int]$count, [int]$seconds) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.Elapsed.TotalSeconds -lt $seconds) {
    $hits = @(if (Test-Path $log) { Select-String -Path $log -Pattern 'conectado: ble ([0-9A-F]{2}:){5}[0-9A-F]{2}$' })
    if ($hits.Count -ge $count) { return $sw.Elapsed.TotalSeconds }
    Start-Sleep -Milliseconds 250
  }
  return $null
}

$bridge = $null
try {
  $bridge = Start-Process $Exe 'bridge' -PassThru -WindowStyle Hidden
  $t = Wait-Connected 1 20
  if ($null -eq $t) { Write-Error "sem 'conectado: ble' em 20 s. bridge.log:`n$(Get-Content $log -Raw -ErrorAction SilentlyContinue)"; exit 1 }
  "OK C1/C3/C14: $((Select-String -Path $log -Pattern 'conectado: ble').Line) em $([math]::Round($t, 1)) s"

  $bridge.Kill(); $bridge.WaitForExit()
  $bridge = Start-Process $Exe 'bridge' -PassThru -WindowStyle Hidden
  # 30 s: morto à força, o bridge não fecha o link e o Windows o segura ~20 s até o tempo limite dele
  $t = Wait-Connected 2 30
  if ($null -eq $t) { Write-Error "o bridge novo não reconectou em 30 s. bridge.log:`n$(Get-Content $log -Raw)"; exit 1 }
  "OK C6: reconectou em $([math]::Round($t, 1)) s depois de o primeiro bridge cair"
  exit 0
}
finally {
  if ($bridge -and -not $bridge.HasExited) { $bridge.Kill(); $bridge.WaitForExit() }
  Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
}
