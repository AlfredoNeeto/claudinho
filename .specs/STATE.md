# State

## Decisions

| ID | Decision | Status |
| --- | --- | --- |
| AD-001 | Os dados vêm só do stdin do `statusLine` do Claude Code. Nenhuma credencial e nenhuma rede. Ver `features/claudinho/plan.md` door 2 | active |
| AD-002 | PC e placa falam uma única linha JSON versionada (`"v":1`), igual em qualquer transporte. Um processo `claudinho bridge` por PC é dono do link (USB serial hoje, BLE depois); o statusline nunca toca em hardware. Ver doors 1 e 3 | active |
| AD-003 | Firmware num projeto PlatformIO só, com `lib/core` em C++ puro compartilhado e uma env por placa (`cyd` agora, `amoled216` depois). UI em LVGL 9 com a paleta do Claude; Arduino_GFX só como driver de pixel. Ver door 5 | active |

## Handoff

Plano do Claudinho revisado para suportar a AMOLED futura; aguardando aprovação. Nenhum check e nenhum código ainda.
