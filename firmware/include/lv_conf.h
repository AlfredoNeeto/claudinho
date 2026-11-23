// Config mínima do LVGL 9: o que não está aqui usa o padrão de lv_conf_internal.h
#ifndef LV_CONF_H
#define LV_CONF_H

#define LV_COLOR_DEPTH 16
// malloc do sistema em vez de um bloco fixo de 64 KB: com o BLE ligado o bloco não cabe na DRAM estática (ble-link)
#define LV_USE_STDLIB_MALLOC LV_STDLIB_CLIB
#define LV_DEF_REFR_PERIOD 33
#define LV_USE_LOG 0
#define LV_USE_CANVAS 1
#define LV_DRAW_SW_COMPLEX 1      // cantos arredondados e arcos

#endif
