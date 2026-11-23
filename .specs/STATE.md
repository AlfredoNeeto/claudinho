# State

## Decisions

| ID | Decision | Status |
| --- | --- | --- |
| AD-001 | Os dados vêm só do stdin do `statusLine` do Claude Code. Nenhuma credencial e nenhuma rede. Ver `features/claudinho/plan.md` door 2 | superseded by AD-005 |
| AD-002 | PC e placa falam uma única linha JSON versionada (`"v":1`), igual em qualquer transporte. Um processo `claudinho bridge` por PC é dono do link (USB serial hoje, BLE depois); o statusline nunca toca em hardware. Ver doors 1 e 3 | superseded by AD-004 |
| AD-003 | Firmware num projeto PlatformIO só, com `lib/core` em C++ puro compartilhado e uma env por placa (`cyd` agora, `amoled216` depois). UI em LVGL 9 com a paleta do Claude; Arduino_GFX só como driver de pixel. Ver door 5 | active |
| AD-004 | PC e placa falam a linha JSON versionada (`"v":1`); um processo `claudinho bridge` por PC é dono do link e procura USB primeiro, depois BLE; o statusline nunca toca em hardware. O BLE é o Nordic UART Service (`6E400001-B5A3-F393-E0A9-E50E24DCCA9E`, RX `…0002` write, TX `…0003` notify) com o mesmo fluxo de bytes da serial, via NimBLE no firmware; pelo ar o bridge tira a chave `p`. Ver `features/ble-link/plan.md` doors 1-3 | active |
| AD-005 | Os dados vêm do stdin do `statusLine` ou do stdin do hook `Stop` mais o transcript local que ele aponta. Nenhuma credencial e nenhuma rede. Ver `features/work-hook/plan.md` door 2 | active |

## Handoff

Claudinho entregue e verificado. `ble-link` aprovado; código no PC e no firmware escrito, testes do PC verdes. Falta gravar o firmware na CYD e rodar as provas de hardware e as manuais.
