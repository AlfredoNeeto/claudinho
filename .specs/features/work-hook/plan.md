# Hook Stop para o PC do trabalho

## Problem

No PC do trabalho a placa fica parada em `aguardando` o dia inteiro. A empresa define uma
`statusLine` própria nas configurações gerenciadas (`~/.claude/remote-settings.json`, assinado e
sincronizado do console do claude.ai), e ela tem prioridade sobre o `settings.json` do usuário.
Por isso o `claudinho statusline` nunca roda lá: o `snapshot.json` não é gravado e o bridge nem
chega a ser iniciado, porque quem o inicia é o statusline.

Mesmo que a status line do usuário rodasse, a cota de 5h e da semana não viria: segundo relatos de
bug (não a documentação oficial), no plano Team o Claude Code não manda `rate_limits` para a status
line. No trabalho não existe fonte local para a cota.

O usuário não quer contornar o controle da empresa. Editar ou bloquear o `remote-settings.json`,
pôr um `curl` falso no PATH, reaproveitar o token dele ou interceptar o tráfego para o servidor
interno estão fora de questão. O pedido não traz números.

Quando isso estiver pronto: no trabalho, depois de cada resposta do Claude Code, a placa mostra o
modelo, o contexto aproximado e o nome do projeto, com `Cota indisponível` no lugar dos anéis. Em
casa nada muda.

## Flow

Reaproveita o `Contract.Build`, o `Snapshot.Write`, o mutex do bridge e o `StartBridge` do
statusline: o hook só monta, a partir do transcript, um payload no mesmo formato que o statusLine
recebe, e o resto da cadeia não sabe de onde ele veio.

1. o Claude Code termina uma resposta e roda o hook `Stop` do usuário -> `claudinho hook` (new, door 1) recebe no stdin o JSON do evento (`transcript_path`, `cwd`)
2. `claudinho hook` (door 1) lê o transcript `.jsonl` local, pega a última entrada `assistant` fora de subagente que tenha `message.usage`, e monta `{model.display_name, workspace.project_dir, context_window.used_percentage}`
3. `Contract.Build` (exists) gera a linha v1, `Snapshot.Write` (exists) grava o `snapshot.json`
4. `BridgeHost.Running` (exists) e `StartBridge` (exists) sobem o bridge se ele não estiver rodando; `Bridge` (exists) manda para a placa por USB ou BLE, sem mudança
5. `claudinho install --hook` (door 1) grava a entrada do hook no `settings.json` do usuário; `claudinho uninstall` (exists) passa a tirá-la também

## Impact

| Front | What changes |
| --- | --- |
| domain | AD-001 dizia "os dados vêm só do stdin do `statusLine`". Passa a ser: stdin do `statusLine` **ou** stdin do hook `Stop` mais o transcript local que ele aponta. Continua sem credencial e sem rede (door 2). Quem se apoia na frase hoje: só os planos `claudinho` e `ble-link` |
| domain | termo novo: `modo hook` - o PC onde o Claudinho foi instalado com `install --hook`. Num PC é um modo ou o outro, nunca os dois (AC 13) |
| PC | `Program.cs` ganha o verbo `hook`; `Installer` ganha `--hook` e passa a cuidar de `hooks.Stop`. `Statusline`, `Contract`, `Bridge` e `Ble` não mudam |
| firmware | nada. Um snapshot sem `h5`/`d7` já mostra `Cota indisponível`, o mesmo estado de quem usa chave de API |
| stored data | o `settings.json` do PC do trabalho ganha `hooks.Stop`; o backup `settings.json.claudinho.bak` continua sendo feito só da primeira vez. Nada a migrar no `snapshot.json` |
| docs | `README.md` ganha a instrução do trabalho e a linha sobre o que aparece lá |

## Relations

None - no stored-data shape change

## Surface

None - nenhuma rota HTTP. O único contrato lido de fora é o comando que o Claude Code roda, `claudinho hook` (stdin: JSON do evento `Stop` com `transcript_path` e `cwd`; saída: `snapshot.json`, nada no stdout, código 0), fixado pela door 1 em Landing; os códigos de saída dos dois comandos estão em Observable

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. Entrada no `settings.json` do usuário (o Claude Code a lê; renomear o verbo quebra quem já instalou) | `"hooks": {"Stop": [{"hooks": [{"type": "command", "command": "~/AppData/Local/Claudinho/claudinho.exe hook"}]}]}`, com o comando pela mesma regra de `Installer.Command` (`~/...` sem espaço, senão caminho entre aspas) | O bridge lendo `~/.claude/projects/**/*.jsonl` sozinho e iniciando no login do Windows: precisa de vigiar pastas e de autostart, que o projeto não tem. Fica de reserva para o caso de hooks do usuário bloqueados (Open question 1) |
| 2. AD-005, que substitui a AD-001 | "Os dados vêm do stdin do `statusLine` ou do stdin do hook `Stop` mais o transcript local que ele aponta. Nenhuma credencial e nenhuma rede." | Consultar o `/v1/limites` do servidor da empresa: traz a cota, mas põe rede e um token pessoal no PC. Vira feature à parte, depois que o admin responder |

- Nothing else in this change is hard to reverse: o cálculo do contexto e o nome do modelo mudam num commit

## Criteria

### S1: a placa acompanha o Claude Code no PC do trabalho (P1)

Depois de cada resposta, a tela mostra modelo, contexto e projeto, sem cota.

**Acceptance Criteria**

1. WHEN `claudinho hook` recebe no stdin um JSON com `transcript_path` e `cwd` THEN ele SHALL gravar no `snapshot.json` uma linha v1 com `v`, `t`, `tz`, `ctx`, `m` e `p`, tirados da última entrada do transcript com `"type":"assistant"`, `isSidechain` diferente de `true`, `message.usage` e `message.model` diferente de `<synthetic>` (mensagens que o próprio Claude Code gera em erros e recusas, com uso zerado)
2. The hook SHALL calcular `ctx` como `(input_tokens + cache_creation_input_tokens + cache_read_input_tokens) / 200000 × 100`, com uma casa decimal; IF a soma passar de 200000 THEN o divisor SHALL ser 1000000
3. The hook SHALL gravar `m` a partir de `message.model` tirando `claude-`, um sufixo de data de 8 dígitos e juntando a versão com ponto: `claude-opus-4-8` → `Opus 4.8`, `claude-sonnet-4-5-20250929` → `Sonnet 4.5`
4. The hook SHALL gravar `p` só com o nome da pasta do `cwd`, pela mesma regra do statusline
5. The hook SHALL NOT gravar `h5`, `d7`, `tok` nem `usd`
6. WHEN o hook grava o snapshot e o mutex `Local\Claudinho.Bridge` não está tomado THEN ele SHALL iniciar `claudinho bridge` sem herdar o stdout
7. IF o stdin não é um objeto JSON, o `transcript_path` falta, o arquivo não existe ou não pode ser lido, ou nenhuma entrada atende ao AC 1 THEN o hook SHALL deixar o `snapshot.json` como estava
8. IF uma linha do transcript não é JSON válido (por exemplo, a última, ainda sendo escrita) THEN o hook SHALL pular essa linha e seguir com as outras
9. WHILE o Claude Code mantém o transcript aberto para escrita o hook SHALL conseguir lê-lo
10. The hook SHALL sair sempre com código 0 e não escrever nada no stdout, inclusive nos casos do AC 7 (no `Stop`, o código 2 faria o Claude Code continuar a resposta)

**Independent test:** com um transcript de exemplo em `pc/tools/fixtures`, rodar `claudinho hook` com `{"transcript_path": "...", "cwd": "D:\\monitor"}` no stdin e ver o `snapshot.json` com `ctx`, `m` e `"p":"monitor"`, sem `h5`; com a placa ligada, a tela mostra `Cota indisponível` e o modelo.

### S2: instalar em modo hook (P1)

`claudinho install --hook` deixa o PC do trabalho pronto sem tocar nas configurações da empresa.

**Acceptance Criteria**

11. WHEN `claudinho install --hook` roda THEN o `settings.json` do usuário SHALL ter em `hooks.Stop` uma entrada com o comando da door 1, mantendo as outras entradas de `hooks.Stop` e os outros eventos de `hooks`
12. WHEN `claudinho install --hook` roda de novo THEN `hooks.Stop` SHALL continuar com uma só entrada do Claudinho
13. WHEN `claudinho install --hook` roda THEN ele SHALL tirar o `statusLine` do Claudinho, se houver; e WHEN `claudinho install` roda sem `--hook` THEN ele SHALL tirar a entrada do Claudinho de `hooks.Stop`
14. WHEN `claudinho uninstall` roda THEN ele SHALL tirar a entrada do Claudinho de `hooks.Stop`, e também `Stop` e `hooks` se ficarem vazios, mantendo as entradas que não são do Claudinho
15. The installer SHALL NOT ler nem escrever nenhum arquivo além do `settings.json` do usuário, do backup e da pasta do exe; em particular, nunca o `remote-settings.json` nem um `managed-settings.json`
16. WHEN `claudinho` roda sem argumento ou com um verbo desconhecido THEN o uso impresso SHALL listar `hook` e `install [--hook] [--force]`

**Independent test:** num `CLAUDE_CONFIG_DIR` temporário com um `settings.json` que já tem um hook `Stop` alheio, rodar `install --hook` duas vezes, depois `install`, depois `uninstall`, e comparar o JSON a cada passo.

## Out of scope

| Excluded | Why |
| --- | --- |
| Cota de 5h e da semana no trabalho | Sem fonte local. Depende do admin: estratégia 1 (encadear `claudinho statusline` na status line gerenciada) ou 2 (endpoint de leitura em `/v1/limites`), feature à parte depois da resposta |
| Tokens (`tok`) e custo (`usd`) no trabalho | O custo não está no transcript, e somar tokens pede ler o arquivo inteiro a cada resposta para um número que o statusline mede de outro jeito |
| Bridge lendo os transcripts sem hook (estratégia 4) | Só faz falta se os hooks do usuário estiverem bloqueados (Open question 1) |
| Detectar sozinho se o PC tem status line gerenciada | Exigiria ler o `remote-settings.json`, que é da empresa; o usuário escolhe o modo com `--hook` |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Caminho escolhido | Estratégia 3 (hook `Stop`) agora; 2 como feature à parte | Recomendação da pesquisa: menor mudança, reaproveita snapshot → bridge. O usuário respondeu à checagem da pergunta 1 para seguir com ela | y |
| Hooks do usuário rodam no trabalho | Sim: em 2026-11-23 o usuário rodou `Select-String -Path ~/.claude/remote-settings.json -Pattern 'allowManagedHooksOnly','disableAllHooks'` no PC do trabalho e não houve resultado (era a pergunta 1, `blocks`) | Um `managed-settings.json` em `C:\Program Files\ClaudeCode\` ou uma política no registro também poderiam bloquear e não foram olhados; a prova manual C14 é o que confirma | y |
| Janela de contexto | 200 mil tokens, 1 milhão quando o uso passa disso (AC 2) | O transcript traz o id do modelo sem dizer se a sessão é de 1M; o número fica aproximado até a sessão passar de 200 mil | n |
| Comando do hook no Windows | O mesmo `~/...` do statusLine, que o Claude Code expande no Git Bash e no PowerShell | `Installer.Command` já prova isso para o statusLine; para hooks só a prova manual no trabalho confirma | n |
| README | Em "Em cada PC", o trabalho usa `claudinho.exe install --hook`; em "Limites", uma linha dizendo que no modo hook a tela mostra modelo, contexto e projeto, sem cota | É o que muda para quem usa | n |
| Profile de verificação | `light`, como nas outras features | Sem `AGENTS.md`; padrão da skill | n |

**Open questions:**

| # | Kind | Question | Until answered |
| --- | --- | --- | --- |
| 2 | open | O admin aceita a estratégia 1 ou 2? | A cota no trabalho fica fora desta feature |

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| command `claudinho hook` | output format and verbosity | AC 10 (nada no stdout) |
| command `claudinho hook` | exit codes | AC 10 |
| command `claudinho hook` | what it prints when it fails halfway | AC 7, AC 10 (nada; o snapshot anterior fica) |
| command `claudinho hook` | flags and defaults | n/a - sem flags; tudo vem do stdin |
| command `claudinho install --hook` | output, exit codes, failure halfway | existing - mesmas mensagens e códigos do `install` (exe em uso → 1); `--force` continua só para um `statusLine` alheio |
| command `claudinho install --hook` | flags and defaults | AC 11, AC 13, AC 16 |
| screen `painel` | estado sem cota | existing - `Cota indisponível`, igual a quem usa chave de API |
| screen `painel` | estado parado | existing - selo `há N min` quando o snapshot envelhece |
| document `README.md` | structure and next step | Assumptions - README |

## Sources

- `.specs/STATE.md` AD-001 - a regra que a door 2 substitui
- Documentação do Claude Code: hooks (`Stop`, entrada com `transcript_path` e `cwd`; código 2 bloqueia a parada) e managed settings (`allowManagedHooksOnly`)
