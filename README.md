# Claudinho

Monitor de mesa da cota do Claude Code: uma ESP32 com tela mostra o uso da sessão de 5h e da
semana, quanto falta para renovar e se você está gastando rápido demais. O Clawd na tela muda de
pose conforme o risco.

Os dados vêm da barra de status do Claude Code (ou de um hook, veja abaixo) e chegam à placa pelo cabo
USB ou por Bluetooth. Pelo Bluetooth basta a placa estar numa fonte USB na tomada, a poucos metros
do PC: o bridge procura primeiro no USB e, se não achar, procura por Bluetooth, sem parear. Pelo
Bluetooth o nome da pasta do projeto não é enviado. Plano completo em `.specs/features/claudinho/plan.md`; prévia da tela em `design/painel.html`.

## Em cada PC (casa, trabalho)

1. Plugue a placa. O driver CH340 costuma instalar sozinho pelo Windows Update.
2. Pegue o `claudinho.exe`: baixe do Release do repositório, ou gere com
   `pwsh pc/tools/check-publish.ps1` (sai em `dist/claudinho.exe`).
3. Rode `claudinho.exe install` e abra o Claude Code de novo.

   Num PC em que a empresa define a status line (configurações gerenciadas do plano Team ou
   Enterprise), a do Claudinho nunca roda. Nesse caso, use `claudinho.exe install --hook`: um hook
   `Stop` lê o transcript da sessão depois de cada resposta. Cada PC usa um modo só, e rodar
   `install` com ou sem `--hook` troca de um para o outro.

Para remover: `claudinho uninstall`. O `settings.json` original fica salvo em
`~/.claude/settings.json.claudinho.bak`.

## Firmware (uma vez, em qualquer PC)

No **PowerShell** (o Git Bash quebra o instalador da Espressif):

```powershell
python -m pip install platformio
.\dist\claudinho.exe uninstall             # tira o statusLine e fecha o bridge; senão ele volta a cada resposta
# coloque a CYD em modo de gravação: segure BOOT, toque RST, solte BOOT (a tela fica preta)
python -m platformio run -d firmware -e cyd -t upload
.\dist\claudinho.exe install               # de volta ao statusLine (o exe do passo 2 acima)
```

O reset automático pelo cabo desta CYD não entra no modo de gravação, por isso o passo dos botões
vem antes de cada gravação (o `platformio.ini` já grava sem resetar antes).

O bridge segura a porta USB da placa, e com o Claudinho instalado o Claude Code o religa depois de
cada resposta. Fechar só o bridge não basta: remova o `statusLine` antes de gravar ou rodar os
testes na placa (`pio test`) e reinstale no fim. Fechar todas as sessões do Claude Code também
serve.

## Limites que valem saber

- A cota de 5h e da semana só aparece com login de assinatura. No Pro e no Max ela vem na barra
  de status. No Team, e no modo `--hook`, ela não vem: o bridge então consulta o mesmo endpoint
  que o `/usage` do Claude Code usa (`api.anthropic.com/api/oauth/usage`), no máximo a cada 5 min,
  com o token que o Claude Code já guarda em `~/.claude/.credentials.json`. O bridge só lê esse
  arquivo; nunca grava, renova ou registra o token. O endpoint não é documentado: se ele falhar ou
  mudar, a tela volta para `Cota indisponível` em até 15 min. No PC da empresa, confirme com o
  admin antes de deixar isso rodando, porque o token é da conta da empresa. Com chave de API, a
  tela mostra `Cota indisponível`; contexto e custo continuam aparecendo.
- No modo `--hook` o custo não aparece: o transcript não traz. O contexto é estimado sobre uma
  janela de 200 mil tokens, que passa a 1 milhão quando o uso passa disso.
- A placa só vê o uso quando o Claude Code responde. Conversas no claude.ai ou no app gastam da
  mesma cota, mas só entram na conta na próxima resposta do Claude Code. Se você usa os dois
  juntos, trate o % da placa como um mínimo. O selo laranja `há N min` avisa que o número está
  parado. Quando a cota vem do endpoint do `/usage`, ela se atualiza sozinha a cada 5 min enquanto
  o bridge está rodando, mesmo sem resposta do Claude Code.
- O Clawd e a paleta são da Anthropic: projeto de uso pessoal, não publique nem venda com eles.
