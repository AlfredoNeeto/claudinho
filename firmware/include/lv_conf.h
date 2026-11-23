// Config mínima do LVGL 9: o que não está aqui usa o padrão de lv_conf_internal.h
#ifndef LV_CONF_H
#define LV_CONF_H

#define LV_COLOR_DEPTH 16
#define LV_MEM_SIZE (64 * 1024U)  // a CYD não tem PSRAM
#define LV_DEF_REFR_PERIOD 33
#define LV_USE_LOG 0
#define LV_USE_CANVAS 1
#define LV_DRAW_SW_COMPLEX 1      // cantos arredondados e arcos

#endif
