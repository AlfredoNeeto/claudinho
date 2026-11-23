# Link BLE checks

Profile: light
Plan: `.specs/features/ble-link/plan.md`

17 checks in 2 slices · 3 one-way doors · 0 open

Comandos de prova (os mesmos do claudinho):

- PC: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~<Classe>.<Teste>"` (xUnit)
- Firmware, núcleo: `pio test -d firmware -e cyd -f <pasta>` na CYD plugada, no PowerShell, sem o bridge segurando a porta (`claudinho uninstall` antes)
- Hardware: `pc/tools/ble-check.ps1` roda o `claudinho bridge` de verdade contra a CYD **ligada só na fonte** (sem cabo de dados no PC) e lê o `bridge.log`
- Manual: o que só se vê na tela ou num app BLE (nRF Connect no celular), feito pelo usuário

## Checks

### S1 - a placa recebe a linha por BLE · ~4 arquivos · ~25 KB · ~7k

**C1** - A CYD anuncia o serviço `6E400001-B5A3-F393-E0A9-E50E24DCCA9E`: um bridge num PC sem placa no USB registra `conectado: ble ` em até 20 s; no nRF Connect ela aparece com o nome `Claudinho` (AC 1)
Proof: `pwsh -NoProfile -File pc/tools/ble-check.ps1`
Proof: `manual - nRF Connect, aba Scanner: aparece "Claudinho" com o serviço 6E400001-…`

**C2** - Uma linha v1 partida em pedaços de 1, 20 e 182 bytes sai do leitor como a mesma linha, e o parse dá `Ok`; pelo ar, a tela muda em até 1 s (AC 2)
Proof: `pio test -d firmware -e cyd -f test_protocol` (caso `test_chunked_line`)
Proof: `manual - nRF Connect: escrever uma linha v1 na RX (6E400002) e ver a tela mudar em até 1 s`

**C3** - A CYD real responde `claudinho 1 cyd` pela TX ao `?\n` recebido na RX (o bridge só registra `conectado: ble` depois do handshake), e nada sai pela serial (AC 3)
Proof: `pwsh -NoProfile -File pc/tools/ble-check.ps1`
Proof: `manual - com o monitor serial aberto, escrever ?\n na RX pelo nRF Connect: a TX notifica "claudinho 1 cyd" e o monitor serial não mostra nada`

**C4** - Pedaços que somam 513 bytes sem `\n` são descartados inteiros e a próxima linha boa passa (AC 4)
Proof: `pio test -d firmware -e cyd -f test_protocol` (caso `test_chunked_line`)

**C5** - Dois leitores alimentados intercalados, byte a byte, com uma linha cada, devolvem as duas linhas intactas (AC 5)
Proof: `pio test -d firmware -e cyd -f test_protocol` (caso `test_interleaved_origins`)

**C6** - Encerrado o bridge conectado por BLE, um bridge novo registra `conectado: ble ` em até 30 s (renegociado com o usuário: morto à força, o bridge não fecha o link e o Windows o segura ~20 s; era 10 s); no nRF Connect, a placa volta a aparecer em até 2 s depois de desconectar (AC 6)
Proof: `pwsh -NoProfile -File pc/tools/ble-check.ps1`
Proof: `manual - nRF Connect: desconectar e ver "Claudinho" de volta no Scanner em até 2 s`

**C7** - Pelo USB nada muda: a CYD responde `claudinho 1 cyd` ao `?\n` na serial em até 3 s, e as provas da serial do claudinho continuam verdes (AC 7)
Proof: `pwsh -NoProfile -File pc/tools/handshake.ps1`
Proof: `pio test -d firmware -e cyd -f test_protocol` (caso `test_rejects_bad_lines`)

### S2 - o bridge acha a placa por BLE · ~6 arquivos · ~30 KB · ~8k

**C8** - O bridge chama a busca BLE só quando a busca USB devolve nada, e a janela da busca BLE é 5 s (AC 8)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BleTests.Connect_tries_usb_before_ble"`

**C9** - Um candidato BLE que responde outra coisa ao `?\n` é fechado e o próximo, que responde `claudinho 1 cyd`, é o usado (AC 9)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BleTests.Ble_candidate_needs_handshake"`

**C10** - A linha que vai pelo ar é a do snapshot sem `p`, com as outras 9 chaves e valores iguais (incluindo acentos em `m`); `?\n` passa intacto (AC 10)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BleTests.Air_line_drops_only_project"`

**C11** - Os pedaços escritos na RX, juntados, são os bytes da linha em ordem, nenhum maior que o tamanho pedido; pela placa real a tela muda em até 2 s depois de editar o `snapshot.json` (AC 11)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BleTests.Chunks_cover_line_in_order"`
Proof: `manual - placa na fonte, bridge rodando: editar o snapshot.json e ver a tela mudar em até 2 s, sem o nome do projeto`

**C12** - Com a placa desligada da fonte, o bridge registra `porta perdida` e continua rodando; religada, registra `conectado: ble ` de novo (AC 12)
Proof: `manual - bridge conectado por BLE: tirar a fonte, esperar até 60 s pelo "porta perdida" no bridge.log, religar e ver "conectado: ble" de novo`
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Retries_after_port_loss"`

**C13** - Sem Bluetooth disponível, três buscas seguidas devolvem nada, não abrem candidato nenhum e registram `bluetooth indisponível` uma vez só (AC 13)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BleTests.Unavailable_bluetooth_logs_once"`

**C14** - Ao conectar, o bridge registra `conectado: <nome do link>`, que é `usb COMn` para a serial e `ble XX:XX:XX:XX:XX:XX` para o BLE (AC 14)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Logs_link_name_on_connect"`
Proof: `pwsh -NoProfile -File pc/tools/ble-check.ps1`

**C15** - Quando abrir um candidato BLE falha, o bridge registra `ble falhou: 0x<HResult> <mensagem>`, e o mesmo motivo seguido só uma vez; depois de conectar, o mesmo motivo volta a ser registrado (AC 12, achado no build: o `0x80070016` passava calado)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BleTests.Ble_failure_reason_logged_when_it_changes"`

**C16** - Com a CYD no USB só para energia e leitura (o script segura a COM, então o bridge conecta por BLE), por 60 min o menor `heap livre ... min M` impresso a cada 60 s fica em M ≥ 20 KB e a placa não imprime `BOOT` de novo (Impact: o LVGL e o BLE dividem o heap; achado no build, aprovado pelo usuário)
Proof: `pwsh -NoProfile -File pc/tools/heap-watch.ps1`

**C17** - Uma linha de snapshot que não é um objeto JSON (truncada, array, lixo) vira nada pelo ar: o `ForAir` não lança, não manda o nome do projeto, e zero bytes vão para a RX (AC 10, AC 12; achado pelo Verifier no round 1: o `JsonException` derrubava o bridge)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BleTests.Invalid_snapshot_sends_nothing_over_air"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| doors do plano (3) | door 1 (NUS) C1, C3 · door 2 (NimBLE) C1, C6 · door 3 (sem `p`) C10 | - |
| origens de bytes no firmware (2) | USB C7 · BLE C2, C3 | - |
| resultados da busca BLE (4) | handshake ok C9 · resposta errada C9 · sem Bluetooth C13 · USB achou antes C8 | - |
| linhas novas e reaproveitadas do `bridge.log` (5) | `conectado: usb` C14 · `conectado: ble` C14 · `bluetooth indisponível` C13 · `porta perdida` C12 · `ble falhou` C15 | - |
| linhas recusadas pela RX (2) | mais de 512 bytes em pedaços C4 · malformada (mesmo parser do USB) C7 | - |

- C1, C3, C6, C11 e C12 dependem de rádio e de tela; a parte automática roda em `ble-check.ps1`, a visual é manual e fica com o usuário
- Nenhum outro check afirma mais do que o caso que a prova exercita

## Swept

- validation: C4, C10, C17
- failure modes: C12, C17, C16
- idempotency: n/a - cada linha é o estado inteiro; reenviar a mesma só redesenha igual (C10 do claudinho)
- authorization: n/a - aberto por escolha do usuário (plano, Assumptions); o que não deve sair do PC não vai pelo ar (C10)
- concurrency: C5 - e os bytes passam da task do NimBLE para o `loop()` por um buffer, sem o callback tocar no LVGL
- data lifecycle: n/a - nada novo é persistido
- dependency failure: C13, C12
- state transitions: C6, C12
- observability: C14, C13, C15

## Handoff

O código existente lido e alterado, mais o novo, ÷ 4:

- S1: `main.cpp` 2 KB + `test_protocol` ~5 KB + `platformio.ini` 1 KB + cabeçalhos da NimBLE lidos ~15 KB ≈ 25 KB ≈ 7k
- S2: `Bridge.cs` 6 KB + `Program.cs` 2,5 KB + `BridgeTests.cs` ~8 KB + BLE novo ~7 KB + testes novos ~4 KB + script ~2 KB ≈ 30 KB ≈ 8k
- Total ≈ 15k, abaixo do budget de 150k - um builder
- Mechanism: one builder (cabe no budget)

- **Settled mid-build:** no Windows 11 26300 a busca GATT com `BluetoothCacheMode.Uncached` dá `COMException 0x80070016` com a CYD; o `BleLink` tenta `Uncached` e cai para `Cached` (achado e testado pelo usuário: `ble-check.ps1` C1/C3/C14 OK em 2,3 s). Não parear o Claudinho no Windows: pareado, a mesma busca devolvia `Unreachable`
- **Settled mid-build:** C6 renegociado de 10 s para 30 s (o usuário escolheu ajustar a prova, não anunciar com um PC conectado, o que mudaria a door 1); C15 novo: o motivo da falha ao abrir um candidato BLE vai para o `bridge.log`, sem repetir o mesmo motivo seguido
- **Não testado:** se o modo `Cached` fica com a tabela GATT velha depois de regravar o firmware; o `Uncached` vem primeiro para cobrir isso
