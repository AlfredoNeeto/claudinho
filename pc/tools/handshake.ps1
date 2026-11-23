# C23: a placa real responde "claudinho 1 <board>" ao "?" em até 3 s, logo depois de abrir a porta.
# Feche o bridge antes (ele segura a porta): Get-Process claudinho | Stop-Process
$ErrorActionPreference = 'Stop'
$vids = '1A86', '303A'
$present = [IO.Ports.SerialPort]::GetPortNames()
$ports = foreach ($vid in $vids) {
  Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Enum\USB" -ErrorAction SilentlyContinue |
    Where-Object PSChildName -like "VID_$vid*" | Get-ChildItem | ForEach-Object {
      (Get-ItemProperty "$($_.PSPath)\Device Parameters" -ErrorAction SilentlyContinue).PortName
    } | Where-Object { $_ -in $present }
}
if (-not $ports) { Write-Error "nenhuma porta com VID $($vids -join '/') conectada"; exit 1 }

foreach ($name in $ports) {
  $sp = [IO.Ports.SerialPort]::new($name, 115200)
  $sp.DtrEnable = $false; $sp.RtsEnable = $false; $sp.NewLine = "`n"; $sp.ReadTimeout = 200
  $sp.Open()
  try {
    $sw = [Diagnostics.Stopwatch]::StartNew(); $next = 0
    while ($sw.ElapsedMilliseconds -lt 3000) {
      if ($sw.ElapsedMilliseconds -ge $next) { $sp.Write("?`n"); $next += 500 }
      try { $line = $sp.ReadLine().TrimEnd("`r") } catch [TimeoutException] { continue }
      if ($line -like 'claudinho 1 *') { "OK: $name respondeu '$line' em $($sw.ElapsedMilliseconds) ms"; exit 0 }
    }
  } finally { $sp.Dispose() }
  "sem resposta em $name"
}
exit 1
