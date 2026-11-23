# Hook Stop checks

Profile: light
Plan: `.specs/features/work-hook/plan.md`

17 checks in 2 slices · 2 one-way doors · 1 open, of which 0 block

Comandos de prova:

- PC: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~<Classe>.<Teste>"` (xUnit)
- Ponta a ponta: os testes `HookTests.Exe_*` rodam o `claudinho.exe` que o build copia para a saída dos testes, com `CLAUDINHO_HOME` numa pasta temporária e o mutex do bridge tomado pelo teste, para não subir um bridge de verdade
- Manual: no PC do trabalho, feito pelo usuário

## Checks

### S1 - a placa acompanha o Claude Code no trabalho · ~5 arquivos · ~18 KB · ~5k

**C1** - Num transcript com, nesta ordem, uma entrada `assistant` válida (`claude-opus-4-8`, uso 2 + 907 + 109294), uma `user`, uma `assistant` de subagente (`isSidechain: true`), uma `assistant` sem `usage` e uma `assistant` `<synthetic>`, com `cwd` `D:\monitor\`, o snapshot tem exatamente as chaves `v`, `t`, `tz`, `ctx`, `m`, `p`, com `ctx` 55.1, `m` `Opus 4.8`, `p` `monitor`, e não tem `h5`, `d7`, `tok` nem `usd` (AC 1, AC 4, AC 5)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Writes_snapshot_from_last_main_assistant"`

**C2** - `ctx` sai 55.1 para 110203 tokens, 100 para 200000, 25 para 250000 e 0.5 para 1000 (AC 2)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Context_percentage"`

**C3** - `m` sai `Opus 4.8` de `claude-opus-4-8`, `Sonnet 4.5` de `claude-sonnet-4-5-20250929`, `Haiku 4.5` de `claude-haiku-4-5` e `Opus 5.5` de `claude-opus-5-5` (AC 3)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Model_display_name"`

**C4** - Depois de gravar, o hook chama o início do bridge uma vez quando ele não está rodando e nenhuma quando está; se o início lança `Win32Exception`, o hook ainda devolve 0 com o snapshot gravado (AC 6, AC 10)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Starts_bridge_only_when_not_running"`

**C5** - Com stdin `lixo`, `[1]`, `{}`, um `transcript_path` que não existe, e um transcript sem nenhuma entrada que atenda ao AC 1, o hook devolve 0, o `snapshot.json` anterior fica byte a byte igual e o bridge não é iniciado (AC 7, AC 10)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Bad_input_keeps_snapshot"`

**C6** - Uma linha que não é JSON no meio do transcript e uma última linha cortada pela metade são puladas, e o snapshot sai da última entrada válida (AC 8)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Skips_bad_lines"`

**C7** - Com o transcript aberto por outro handle para escrita (`FileAccess.Write`, `FileShare.ReadWrite`), o hook lê e grava o snapshot (AC 9)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Reads_transcript_open_for_writing"`

**C8** - O `claudinho.exe hook` de verdade, com um transcript válido no stdin, sai com 0, não escreve nada no stdout e grava o `snapshot.json` em `CLAUDINHO_HOME`; com `lixo` no stdin, sai com 0 e não escreve nada no stdout (AC 1, AC 10)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Exe_hook_is_silent"`

### S2 - instalar em modo hook · ~4 arquivos · ~14 KB · ~4k

**C9** - `install --hook` num `settings.json` que já tem um `hooks.Stop` alheio e um `hooks.PreToolUse` deixa em `hooks.Stop` a entrada alheia e uma do Claudinho `{"hooks":[{"type":"command","command":"~/AppData/Local/Claudinho/claudinho.exe hook"}]}`, mantém `PreToolUse`, não cria `statusLine`, devolve 0 e o backup é o original (AC 11, door 1)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Install_hook_adds_stop_entry"`

**C10** - `install --hook` duas vezes deixa o `settings.json` igual ao da primeira vez, com uma só entrada do Claudinho em `hooks.Stop` (AC 12)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Install_hook_twice_is_idempotent"`

**C11** - `install` seguido de `install --hook` tira o `statusLine` do Claudinho; `install --hook` seguido de `install` tira a entrada do Claudinho de `hooks.Stop` e mantém a alheia; `install --hook` com um `statusLine` alheio o mantém intacto e devolve 0 (AC 13)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Modes_replace_each_other"`

**C12** - `uninstall` depois de `install --hook` tira a entrada do Claudinho; sem outra entrada, `Stop` e `hooks` somem e o `settings.json` volta a ser igual ao original; com uma entrada alheia, ela fica (AC 14)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Uninstall_removes_hook_entry"`

**C13** - Com um `remote-settings.json` e um `managed-settings.json` na pasta `.claude`, `install --hook`, `install` e `uninstall` deixam os dois byte a byte iguais, e a árvore da pasta de teste só ganha ou perde o `settings.json`, o backup e a pasta do exe (AC 15)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~InstallTests.Never_touches_managed_settings"`

**C14** - No PC do trabalho, depois de `claudinho.exe install --hook` e de uma resposta do Claude Code numa sessão nova, o `snapshot.json` em `%LOCALAPPDATA%\Claudinho` tem o horário da resposta, `ctx` e `m`, e a placa mostra o modelo e `Cota indisponível` (AC 1, AC 6, door 1: o Claude Code roda o comando `~/...` e não ignora o hook)
Proof: `manual - PC do trabalho: install --hook, abrir o Claude Code, mandar uma mensagem, conferir o snapshot.json e a tela`

**C15** - `claudinho` sem argumento sai com 2 e o uso impresso contém `hook` e `install [--hook] [--force]` (AC 16)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Exe_usage_lists_hook"`

**C16** - `.specs/STATE.md` tem a AD-005 `active` com o texto da door 2, e a AD-001 com status `superseded by AD-005` (door 2)
Proof: `pwsh -NoProfile -Command "if ((Select-String .specs/STATE.md -Pattern '^\| AD-005 .*\| active \|$') -and (Select-String .specs/STATE.md -Pattern '^\| AD-001 .*\| superseded by AD-005 \|$')) { exit 0 } else { exit 1 }"`

**C17** - Com o transcript existindo mas travado por outro handle (`FileShare.None`), o hook devolve 0, o `snapshot.json` anterior fica igual e o bridge não é iniciado (AC 7 "não pode ser lido", AC 10; achado pelo Verifier no round 1: o caso não tinha check)
Proof: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Unreadable_transcript_keeps_snapshot"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| doors do plano (2) | door 1 (entrada `hooks.Stop`) C9, C14 · door 2 (AD-005) C16 | - |
| entradas do transcript que não servem (5) | `user` C1 · `isSidechain` C1 · sem `usage` C1 · `<synthetic>` C1 · linha que não é JSON C6 | - |
| entradas recusadas pelo AC 7 (6) | stdin não JSON C5 · stdin não objeto C5 · sem `transcript_path` C5 · arquivo inexistente C5 · arquivo que não pode ser lido C17 · nenhuma entrada válida C5 | - |
| divisor do `ctx` (3) | abaixo de 200000 C2 · exatamente 200000 C2 · acima C2 | - |
| formatos de id do modelo (2) | sem data C3 · com data de 8 dígitos C3 | - |
| transições de modo novas (3) | statusLine → hook C11 · hook → statusLine C11 · hook → nada C12 | - |
| códigos de saída novos (2) | `hook` → 0 C8 · sem verbo → 2 C15 | - |

- statusLine → nada já é provado por `InstallTests.Uninstall_removes_only_claudinho`, que continua verde
- C14 depende do Claude Code e da placa no PC do trabalho; fica com o usuário
- Nenhum outro check afirma mais do que o caso que a prova exercita

## Swept

- validation: C5, C6
- failure modes: C4, C5
- idempotency: C10
- authorization: n/a - só arquivos do próprio usuário; o que é da empresa nunca é lido nem escrito (C13)
- concurrency: C7 - e duas sessões gravando juntas já passam pelo `Snapshot.Write` atômico do claudinho
- data lifecycle: C12
- dependency failure: C5 (transcript ausente), C17 (transcript travado), C4 (falha ao iniciar o bridge)
- state transitions: C11, C12
- observability: n/a - o hook não escreve nada (AC 10); o `bridge.log` não muda

## Handoff

O código existente lido e alterado, mais o novo, ÷ 4:

- S1: `Program.cs` 2,5 KB + `Statusline.cs` 3 KB + `Contract.cs` 2 KB + hook novo ~3 KB + `HookTests.cs` novo ~7 KB ≈ 18 KB ≈ 5k
- S2: `Installer.cs` 4 KB + `InstallTests.cs` 4 KB + testes novos ~4 KB + `README.md` 4 KB ≈ 16 KB ≈ 4k
- Total ≈ 9k, abaixo do budget de 150k - um builder
- Mechanism: one builder (cabe no budget)

- **Settled mid-build:** round 1 do Verifier: C15 passou a exigir `| hook |` (o `hook` sozinho casava com `install [--hook]`); C17 novo. Ler o transcript inteiro a cada `Stop` ficou: medido em 2026-11-23, um transcript de 32 MB custa ~190 ms no total, dos quais ~80 ms são só a partida do exe
