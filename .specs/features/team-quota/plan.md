# Claudinho: cota de 5h/7d no plano Team via endpoint do `/usage`

Plano escrito pelo usuário em 2026-11-23 e implementado como veio. Desvios na implementação, no fim.

## Problema

No plano **Team** (login de assinatura, `organizationType: claude_team`), o stdin do `statusLine` vem **sem**
`rate_limits`. O snapshot fica sem `h5`/`d7` e a placa mostra `Cota indisponível`, tanto no modo statusline
quanto no `--hook`.

O comando `/usage` do Claude Code mostra a cota nesse plano. Ele consulta um endpoint interno, e o teste manual
abaixo confirmou que esse endpoint responde com o token que o Claude Code já guarda no PC.

## O que foi confirmado (teste manual, 23/11/2026)

```
GET https://api.anthropic.com/api/oauth/usage
Authorization: Bearer <claudeAiOauth.accessToken de ~/.claude/.credentials.json>
anthropic-beta: oauth-2025-04-20
```

Resposta `200` (trecho relevante; o resto veio `null` ou não importa):

```json
{
  "five_hour": { "utilization": 6.0,  "resets_at": "2026-11-23T22:20:00.117534+00:00" },
  "seven_day": { "utilization": 15.0, "resets_at": "2026-11-27T11:00:00.117553+00:00" }
}
```

`~/.claude/.credentials.json` tem a forma
`{ "claudeAiOauth": { "accessToken", "refreshToken", "expiresAt" (ms unix), ... } }`.

O endpoint **não é documentado**. Ele pode mudar ou sumir, e o código precisa falhar em silêncio quando isso acontecer.

## Solução

O **bridge** completa a linha antes de mandá-la para a placa: quando o snapshot vier **sem `h5` e sem `d7`**,
ele preenche os dois com o último valor lido do endpoint.

Por que fazer no bridge, e não no statusline ou no hook:
- funciona nos dois modos de instalação (statusline e `--hook`) com um único ponto de mudança;
- a cota atualiza entre uma resposta e outra do Claude Code, inclusive com o uso que vem do claude.ai;
- o statusline continua rápido e sem rede.

**O firmware não muda.** A placa já aceita `"h5":[pct,resets_unix]` e `"d7":[pct,resets_unix]`, no formato
de `Contract.Window`.

## Regras do `Usage`

- **Só age se a linha não tiver `h5` nem `d7`.** Se o statusline já mandou a cota (Pro/Max), não faz nenhuma requisição.
- Faz no máximo uma requisição a cada `Poll` (5 min). Depois de uma falha, espera `Backoff` (15 min).
- **Token:** lê `CLAUDE_CONFIG_DIR/.credentials.json` (padrão `~/.claude/.credentials.json`) a cada consulta.
  Se faltar o arquivo, o campo, ou se `expiresAt` já tiver passado, **não faz a requisição** e espera o próximo
  `Poll`. **Nunca grava nesse arquivo, nunca usa o `refreshToken`, nunca loga o token.**
- **Conversão:** `utilization` vira `h5[0]`/`d7[0]` (arredondado com 1 casa). `resets_at`, em ISO 8601, vira
  unix em segundos. Se `utilization` for `null`, se `resets_at` faltar ou não parsear, ou se `resets_at` já
  tiver passado, a janela fica de fora.
- **Cache:** só em memória, o último resultado bom e o horário em que foi obtido. Com mais de `MaxAge`
  (15 min), não mescla.
- **Merge:** reescreve a linha acrescentando `h5`/`d7` na ordem de `Contract.Keys`, com o mesmo encoder do
  `Contract`. Se a linha não parsear, devolve igual.
- **Log** (`bridge.log`): uma linha na primeira consulta que der certo (`cota via /usage: 5h 6% 7d 15%`) e uma
  linha por tipo de falha, sem repetir a cada ciclo.
- **HTTP:** `HttpClient` estático, `Timeout = 5s`, headers `Authorization: Bearer`, `anthropic-beta:
  oauth-2025-04-20`, `User-Agent: claudinho/<versão>`. Falha de rede ou timeout não derruba o bridge.

## Fora do escopo

- Mudar o firmware.
- Mostrar a cota no texto da status line (`5h -- · 7d --`).
- Renovar o token OAuth.
- Os campos `extra_usage`, `spend`, `seven_day_opus` e afins.

## Como verificar no PC do trabalho

1. `pwsh pc/tools/check-publish.ps1` e depois `dist\claudinho.exe install --hook` (no trabalho a statusLine é
   gerenciada pela empresa, então o modo statusline nunca roda lá; ver `features/work-hook`).
2. Mandar uma mensagem no Claude Code. Em até alguns segundos, o `%LOCALAPPDATA%\Claudinho\bridge.log` mostra
   `cota via /usage: 5h X% 7d Y%`.
3. A placa mostra as barras de 5h e 7d com os mesmos números do `/usage`.
4. Renomear temporariamente o `.credentials.json`, ou esperar o token vencer: a placa volta para
   `Cota indisponível` em até 15 min, sem o bridge cair. Desfazer a mudança depois.

## Riscos

- O endpoint é interno e não documentado. Se mudar, a cota some da placa, mas nada além disso quebra.
- O token usado é da conta Team da empresa. Confirmar com o admin antes de deixar rodando.

## Desvios na implementação

- `Usage.Fetch` usa `HttpClient.Send` (síncrono) em vez de `SendAsync(...).GetAwaiter().GetResult()`; o efeito é o mesmo.
- Um status diferente de 200 sai como `HttpRequestException` com `StatusCode` (`EnsureSuccessStatusCode`), e o
  log mostra o número (`cota via /usage: 401`). Timeout aparece como `timeout`; falha de conexão como `erro de rede`.
- Um `.credentials.json` sem `expiresAt` não bloqueia a consulta: se o token tiver vencido, o 401 cai no `Backoff`.
- O passo 1 da verificação dizia `install` (modo statusline); no trabalho é `install --hook`.
