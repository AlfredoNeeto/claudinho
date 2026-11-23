# Claudinho checks

Profile: light
Plan: `.specs/features/claudinho/plan.md`

39 checks in 6 slices · 5 one-way doors · 1 open (blocks go-live only: conta do Claude Code no trabalho)

Comandos de prova (o repo é novo; estes são os executores que o próprio projeto cria):

- PC: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~<Classe>.<Teste>"` (xUnit)
- Firmware, núcleo: `pio test -d firmware -e cyd -f <pasta>` roda os testes Unity **na CYD plugada** (não há compilador C++ no PC para a env `native`); o nome do caso aparece na saída do Unity. `pio` = `python -m platformio`, rodado no **PowerShell**: dentro do Git Bash o instalador da Espressif recusa o ambiente MSYS
- Hardware: scripts em `pc/tools/` que falam com a placa real

## Checks

### S1 - statusline · ~6 arquivos · ~25 KB · ~7k

**C1** - Com `five_hour` 23.5, `seven_day` 41 e `context_window` 37, `claudinho statusline` imprime exatamente `5h 24% · 7d 41% · ctx 37%` e sai com 0 (AC 1)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~StatuslineTests.Prints_text_for_full_payload"`

**C2** - Sem `rate_limits`, ou sem uma das janelas, o % dessa janela sai como `--` e o código de saída é 0 (AC 2)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~StatuslineTests.Prints_dashes_when_window_missing"`

**C3** - Stdin que não é JSON imprime `claudinho: sem dados`, sai com 0 e deixa `snapshot.json` byte a byte igual (AC 3)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~StatuslineTests.Invalid_json_keeps_snapshot"`

**C4** - O `snapshot.json` tem só as chaves `v t tz h5 d7 ctx tok usd m p`, com `v` = 1 e `p` = nome da pasta de `workspace.project_dir` (nunca o caminho com `\` ou `/`) (AC 4)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~StatuslineTests.Snapshot_has_only_contract_fields"`

**C5** - O exe publicado roda `statusline` com o payload completo em menos de 500 ms, na mediana de 5 execuções (AC 5)
Proof: `pwsh -NoProfile -File pc/tools/measure-statusline.ps1`

**C6** - Quando nenhum bridge segura o mutex `Local\Claudinho.Bridge`, o `statusline` chama o lançador do bridge uma vez; quando um bridge segura, não chama (AC 6)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~StatuslineTests.Starts_bridge_only_when_absent"`

**C38** - 20 gravações paralelas do snapshot deixam um `snapshot.json` que sempre faz parse e é igual a uma das 20 entradas (troca atômica, Flow hop 2)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~StatuslineTests.Concurrent_writes_leave_valid_snapshot"`

### S2 - bridge · ~5 arquivos · ~30 KB · ~8k

**C7** - Um segundo bridge iniciado com o mutex já tomado sai com 0 (AC 7)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Second_instance_exits_zero"`

**C8** - A busca de placa aceita só portas com VID `1A86` ou `303A` que respondam `claudinho 1 ` ao `?\n` em até 3 s; rejeita outro VID, porta Bluetooth e resposta errada (AC 8)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Picks_port_by_vid_and_handshake"`

**C9** - Snapshot alterado é enviado à porta como uma linha `{"v":1,...}\n` no tick seguinte, em até 1 s (AC 9)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Sends_line_when_snapshot_changes"`

**C10** - Sem mudança, o bridge reenvia o snapshot quando completa 60 s do último envio, e não antes (AC 10)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Resends_every_60_seconds"`

**C11** - Depois de conectar ou reconectar, o snapshot atual é enviado em até 3 s (AC 11)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Sends_snapshot_after_connect"`

**C12** - Quando a escrita na porta falha, o bridge grava uma linha com `porta perdida` em `bridge.log`, tenta de novo 5 s depois e continua rodando (AC 12)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Retries_after_port_loss"`

**C13** - Com 30 min sem mudança no snapshot, o bridge fecha a porta e encerra com 0; com 29 min continua (AC 13)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Exits_after_30_minutes_idle"`

**C39** - Ao iniciar, um `bridge.log` maior que 1 MB é truncado para vazio (o log não cresce sem limite)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BridgeTests.Truncates_log_over_1mb"`

### S3 - firmware: protocolo e tela · ~14 arquivos · ~110 KB · ~28k

**C14** - Para uma linha v1 válida, o modelo de tela tem: frase, `Opus · monitor`, linha `Sessão` com `24%` e `renova em 2h16`, linha `Semana` com `41%` e `renova qui 20:11`, rodapé com `contexto 37%` e `US$ 4,12` (AC 14)
Proof: `pio test -d firmware -e cyd -f test_view` (caso `test_full_line_view`)

**C15** - Nenhuma cor fora da paleta do AC 15 aparece no código da UI (`firmware/src`)
Proof: `python pc/tools/check_palette.py firmware/src`

**C16** - Sem nenhuma linha desde o boot, o modelo de tela é `Waiting`, com pose `sleep` e texto `Esperando o Claude Code` (AC 16)
Proof: `pio test -d firmware -e cyd -f test_view` (caso `test_waiting_before_first_line`)

**C17** - O "agora" da placa é `t` da última linha + (millis − millis da recepção) ÷ 1000; com 61 s passados, `renova em 2h16` vira `renova em 2h15` (AC 17)
Proof: `pio test -d firmware -e cyd -f test_clock` (caso `test_countdown_ticks_without_new_line`)

**C18** - Com `resets_at` no passado, a janela mostra `0%` e `renovada ✓` (AC 18)
Proof: `pio test -d firmware -e cyd -f test_view` (caso `test_renewed_window`)

**C19** - Com a última linha há 181 s, o modelo marca `stale` (opacidade 45%) e o rodapé mostra `há 3 min`; com 179 s, mostra o horário (AC 19)
Proof: `pio test -d firmware -e cyd -f test_view` (caso `test_stale_after_3_minutes`)

**C20** - Linha malformada, truncada (sem `}`) ou com 513 bytes é descartada e o estado anterior fica intacto (AC 20)
Proof: `pio test -d firmware -e cyd -f test_protocol` (caso `test_rejects_bad_lines`)

**C21** - Linha com `v` = 2 leva ao modelo `Protocol` com `o PC mandou o protocolo v2` e pose `confused` (AC 21)
Proof: `pio test -d firmware -e cyd -f test_protocol` (caso `test_unknown_version`)

**C22** - Linha sem `h5` e `d7` mostra `--`, `indisponível neste login` nas duas linhas e a frase `Cota indisponível`, mantendo contexto e custo (AC 22)
Proof: `pio test -d firmware -e cyd -f test_view` (caso `test_missing_quota`)

**C23** - A CYD real responde `claudinho 1 cyd` ao `?\n` em até 3 s, inclusive logo depois de abrir a porta (AC 23)
Proof: `pwsh -NoProfile -File pc/tools/handshake.ps1`

### S4 - instalação · ~3 arquivos · ~15 KB · ~4k

**C24** - `install` copia o exe para a pasta de destino, salva `settings.json.claudinho.bak` igual ao original e grava `statusLine` `{"type":"command","command":"<exe com /> statusline"}`, mantendo as outras chaves iguais (AC 23 do plano)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Install_writes_statusline_and_backup"`

**C25** - Com um `statusLine` de outro programa, `install` sai com 1, imprime `statusLine já configurado; use --force para substituir` e não altera o arquivo; com `--force`, substitui (AC 24 do plano)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Refuses_foreign_statusline"`

**C26** - Rodar `install` duas vezes deixa `settings.json` com os mesmos valores da primeira (AC 25 do plano)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Install_twice_is_idempotent"`

**C27** - `uninstall` remove a chave `statusLine` do Claudinho e a pasta de destino, mantendo as outras chaves (AC 26 do plano)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Uninstall_removes_only_claudinho"`

**C28** - `dotnet publish` gera um único `claudinho.exe` self-contained que roda `--version` sem depender do .NET instalado (AC 27 do plano)
Proof: `pwsh -NoProfile -File pc/tools/check-publish.ps1`

### S5 - placa real · 0 arquivos novos

**C29** - No boot, a CYD mostra fundo `#141413` e o Clawd laranja (não azulado), em paisagem, sem espelhamento (AC 29 do plano)
Proof: `manual - foto da tela depois do upload: Clawd laranja no canto superior esquerdo, texto legível da esquerda para a direita`

### S6 - leitura de relance · no núcleo do S3

**C30** - A frase segue a ordem `Limite atingido` > `Ritmo acelerado` > `Quase no limite` > `Tranquilo`, numa tabela com um caso por linha e um caso de empate (100% e projeção ao mesmo tempo dá `Limite atingido`) (AC 30)
Proof: `pio test -d firmware -e cyd -f test_status` (caso `test_status_precedence`)

**C31** - Com 62% em 100 min de uma janela de 300 min, a frase é `Ritmo acelerado` e o texto `nesse ritmo, acaba em 1h01` (AC 31)
Proof: `pio test -d firmware -e cyd -f test_status` (caso `test_projection_text`)

**C32** - Com 100% e reset em 43 min, o texto é `volta em 43 min` (AC 32)
Proof: `pio test -d firmware -e cyd -f test_status` (caso `test_limit_text`)

**C33** - O ritmo de uma janela de 5h com 100 min decorridos é 33,3%; a bolinha é escura com uso ≥ ritmo e clara com uso < ritmo; no limite não há ritmo (AC 33)
Proof: `pio test -d firmware -e cyd -f test_view` (caso `test_pace_marker`)

**C34** - Uma janela está em risco (laranja) com ≥ 80% ou projeção antes do reset, e não está com 79% no ritmo (AC 34)
Proof: `pio test -d firmware -e cyd -f test_status` (caso `test_risk_color`)

**C35** - Cada estado tem sua pose: `Tranquilo`→`idle`, `Ritmo acelerado`→`alarm`, `Quase no limite`→`alarm`, `Limite atingido`→`squint`, renovação→`happy` por 4,5 s, sem linha→`sleep`, protocolo→`confused` (AC 35)
Proof: `pio test -d firmware -e cyd -f test_status` (caso `test_pose_table`)

**C36** - A pose `idle` só anima a piscada; as outras animações ficam desligadas (AC 36)
Proof: `pio test -d firmware -e cyd -f test_status` (caso `test_idle_animates_only_blink`)

**C37** - `resets_at` 1764890460 com `tz` −180 vira `renova qui 20:21` (dia e hora local) (AC 37)
Proof: `pio test -d firmware -e cyd -f test_clock` (caso `test_weekday_local_time`)

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| campos da door 1 (10) | `v` C4 · `t` C4 · `tz` C4 · `h5` C4 · `d7` C4 · `ctx` C4 · `tok` C4 · `usd` C4 · `m` C4 · `p` C4 | - |
| linhas rejeitadas pela placa (4) | malformada C20 · truncada C20 · 513 bytes C20 · `v` ≠ 1 C21 | - |
| portas candidatas (5) | VID `1A86` C8 · VID `303A` C8 · outro VID C8 · Bluetooth C8 · handshake errado C8 | - |
| frases de status (6) | `Limite atingido` C30 · `Ritmo acelerado` C30 · `Quase no limite` C30 · `Tranquilo` C30 · `Cota indisponível` C22 · `Cota renovada!` C35 | - |
| poses do Clawd (6) | `idle` C35 · `alarm` C35 · `squint` C35 · `happy` C35 · `sleep` C16 · `confused` C21 | - |
| telas do firmware (4) | `Waiting` C16 · `Data` C14 · `Protocol` C21 · `Stale` C19 | - |
| resultados do install (4) | limpo C24 · `statusLine` alheio C25 · repetido C26 · uninstall C27 | - |
| saídas do statusline (3) | completo C1 · janela ausente C2 · JSON inválido C3 | - |
| eventos do bridge (5) | mudança C9 · 60 s C10 · (re)conexão C11 · porta perdida C12 · 30 min ocioso C13 | - |
| doors do plano (5) | door 1 C4, C20 · door 2 C1 · door 3 C8, C23 · door 4 C28 · door 5 C15, C29 | - |

- Claims que nomeiam o contrato da door 1: C4, C9, C20, C21, C23 - cada um com prova que cruza a fronteira (arquivo, porta ou placa)
- C29 é a única prova manual; é calibração de hardware e não tem comando que a substitua

## Swept

- validation: C20, C3
- failure modes: C12, C3
- idempotency: C26, C7
- authorization: n/a - dispositivo pessoal num cabo USB local; não há usuário, rede nem chamada remota
- concurrency: C38, C7
- data lifecycle: C39
- dependency failure: C2, C12
- state transitions: C16, C19, C18, C30
- observability: C12

## Handoff

Tudo é código novo; a estimativa é o tamanho do que será escrito e lido, ÷ 4:

- S1 + S2 + S4 (PC, C#) ≈ 70 KB ≈ 18k; S3 + S6 (núcleo e UI do firmware, sem contar fontes geradas, que não são lidas) ≈ 110 KB ≈ 28k; S5 = 0. Total ≈ 46k, abaixo do budget de 150k - um builder
- Mechanism: one builder (cabe no budget)
