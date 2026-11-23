#pragma once
#include <stdint.h>

#include "claudinho_core.h"

// Monta os widgets da CYD (320x240, barras) uma vez.
void ui_init();
// Aplica o View do núcleo e anima o Clawd; chame a cada volta do loop.
void ui_update(const cl::State& st, uint32_t nowMs);
