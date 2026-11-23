# C16 (ble-link): com o link BLE ativo, o heap mínimo da CYD fica >= 20 KB por 60 min e a placa não reinicia.
# A CYD vai no USB deste PC só para energia e para esta leitura: o script segura a COM, então o bridge cai no BLE.
param(
  [int]$Minutes = 60,
  [int]$MinKB = 20,
  [string]$Exe = (Join-Path $env:LOCALAPPDATA 'Claudinho\claudinho.exe')
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { Write-Error "não achei $Exe; rode claudinho install ou passe -Exe"; exit 1 }

$present = [IO.Ports.SerialPort]::GetPortNames()
$port = Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Enum\USB' -ErrorAction SilentlyContinue |
  Where-Object { $_.PSChildName -like 'VID_1A86*' } | Get-ChildItem |
  ForEach-Object { (Get-ItemProperty "$($_.PSPath)\Device Parameters" -ErrorAction SilentlyContinue).PortName } |
  Where-Object { $_ -in $present } | Select-Object -First 1
if (-not $port) { Write-Error 'a CYD não está no USB deste PC'; exit 1 }

# o bridge rodando pode estar segurando a COM; fecha, pega a porta e sobe um bridge novo, que só acha o BLE
Get-Process claudinho -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); $_.WaitForExit() }
$sp = [IO.Ports.SerialPort]::new($port, 115200)
$sp.DtrEnable = $false; $sp.RtsEnable = $false; $sp.NewLine = "`n"; $sp.ReadTimeout = 1000
$sp.Open()
$log = Join-Path (Split-Path $Exe) 'bridge.log'
$before = if (Test-Path $log) { @(Get-Content $log).Count } else { 0 }
Start-Process $Exe 'bridge' -WindowStyle Hidden | Out-Null

try {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $ble = $false; $samples = 0; $low = [int]::MaxValue
  while ($sw.Elapsed.TotalMinutes -lt $Minutes) {
    if (-not $ble) {
      $ble = [bool](Test-Path $log) -and [bool](@(Get-Content $log | Select-Object -Skip $before) -match 'conectado: ble ')
      if (-not $ble -and $sw.Elapsed.TotalSeconds -gt 30) { Write-Error 'o bridge não conectou por BLE em 30 s; veja o bridge.log'; exit 1 }
    }
    try { $line = $sp.ReadLine().TrimEnd("`r") } catch [TimeoutException] { continue }
    if ($line -eq 'BOOT') { Write-Error "a placa reiniciou sozinha aos $([math]::Round($sw.Elapsed.TotalMinutes, 1)) min"; exit 1 }
    if ($line -match '^heap livre (\d+) min (\d+)$') {
      $samples++; $low = [math]::Min($low, [int]$Matches[2])
      "$([math]::Round($sw.Elapsed.TotalMinutes, 1)) min: livre $([int]$Matches[1] / 1KB -as [int]) KB, mínimo $($low / 1KB -as [int]) KB"
    }
    if ($samples -eq 0 -and $sw.Elapsed.TotalSeconds -gt 75) { Write-Error 'nenhuma linha "heap livre ... min ..." em 75 s: firmware antigo?'; exit 1 }
  }
  if (-not $ble) { Write-Error 'o bridge nunca conectou por BLE'; exit 1 }
  if ($low -lt $MinKB * 1KB) { Write-Error "heap mínimo $($low / 1KB -as [int]) KB, abaixo de $MinKB KB"; exit 1 }
  "OK C16: $samples leituras em $Minutes min com BLE ativo, heap mínimo $($low / 1KB -as [int]) KB, sem reinício"
  exit 0
}
finally {
  $sp.Dispose()
}
