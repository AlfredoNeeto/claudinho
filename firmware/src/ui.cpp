// UI da CYD: layout da demo design/painel.html (modo barras), cores só da paleta (AC 15).
#include "ui.h"

#include <lvgl.h>
#include <math.h>
#include <string.h>

#include <initializer_list>

LV_FONT_DECLARE(font_lora_22);
LV_FONT_DECLARE(font_num_26);
LV_FONT_DECLARE(font_label_12);
LV_FONT_DECLARE(font_text_12);
LV_FONT_DECLARE(font_text_11);

namespace {

// paleta (AC 15)
const uint32_t kBg = 0x141413, kTrack = 0x2b2a27, kGhost = 0x45423c, kIvory = 0xfaf9f5, kMuted = 0xb0aea5,
               kDim = 0x7a786f, kCream = 0xe8e6dc, kOrange = 0xd97757, kGreen = 0x8fa874, kBlue = 0x6a9bcc, kBlack = 0x000000;

constexpr int W = 320, H = 240, X0 = 14, BW = 292;
constexpr int U = 6;                      // tamanho do "pixel" do Clawd
constexpr int CW = 104, CH = 80;          // canvas do Clawd (sobra espaço para suor, z e brilhos)
constexpr int OX = 12, OY = 16;           // origem do Clawd dentro do canvas
constexpr uint8_t kFade = 115;            // 45% quando o dado está velho (AC 19)

lv_obj_t *canvas, *title, *sub, *meta, *divider, *ctxL, *usdL, *freshDot, *freshL;
struct Row { lv_obj_t *label, *pct, *track, *ghost, *fill, *bead, *reset; } rows[2];
uint16_t cbuf[CW * CH];
cl::Screen lastScreen = (cl::Screen)-1;

lv_color_t C(uint32_t hex) { return lv_color_hex(hex); }
uint16_t C16(uint32_t hex) { return lv_color_to_u16(lv_color_hex(hex)); }

lv_obj_t* label(lv_obj_t* parent, const lv_font_t* f, uint32_t color) {
  lv_obj_t* l = lv_label_create(parent);
  lv_obj_set_style_text_font(l, f, 0);
  lv_obj_set_style_text_color(l, C(color), 0);
  lv_label_set_text(l, "");
  return l;
}

lv_obj_t* rect(lv_obj_t* parent, int w, int h, uint32_t color) {
  lv_obj_t* o = lv_obj_create(parent);
  lv_obj_remove_style_all(o);
  lv_obj_set_size(o, w, h);
  lv_obj_set_style_bg_opa(o, LV_OPA_COVER, 0);
  lv_obj_set_style_bg_color(o, C(color), 0);
  lv_obj_set_style_radius(o, LV_RADIUS_CIRCLE, 0);
  lv_obj_remove_flag(o, LV_OBJ_FLAG_SCROLLABLE);
  return o;
}

void set_text(lv_obj_t* l, const char* s) {
  if (strcmp(lv_label_get_text(l), s) != 0) lv_label_set_text(l, s);
}

// ---------- Clawd em pixels (o mesmo desenho da demo) ----------

const char* kBody[] = {"..##########..", "..##########..", "..##########..", "##############", "##############", "..##########.."};
const char* kBodyUp[] = {"#.##########.#", "#.##########.#", "##############", "..##########..", "..##########..", "..##########.."};
const int kLegCols[] = {3, 5, 8, 10};

void px(int x, int y, int w, int h, uint16_t c) {
  for (int j = y; j < y + h; j++)
    if (j >= 0 && j < CH)
      for (int i = x; i < x + w; i++)
        if (i >= 0 && i < CW) cbuf[j * CW + i] = c;
}

void glyph(const char* const* rows_, int n, int x, int y, int s, uint16_t c) {
  for (int j = 0; j < n; j++)
    for (int i = 0; rows_[j][i]; i++)
      if (rows_[j][i] == '#') px(x + i * s, y + j * s, s, s, c);
}

void draw_clawd(cl::Pose pose, float t) {
  const cl::Anim a = cl::anim_for(pose);
  for (int i = 0; i < CW * CH; i++) cbuf[i] = C16(kBg);

  int x = OX, y = OY;
  if (pose == cl::Pose::Happy) y -= (int)(fabsf(sinf(t * 7)) * U * 1.6f);
  else if (a.bob && ((int)(t * 3) % 2)) y -= U / 2;

  const uint16_t orange = C16(kOrange), black = C16(kBlack);
  const bool up = pose == cl::Pose::Alarm || pose == cl::Pose::Happy;
  for (int j = 0; j < 6; j++)
    for (int i = 0; i < 14; i++)
      if ((up ? kBodyUp : kBody)[j][i] == '#') px(x + i * U, y + j * U, U, U, orange);
  const int step = a.legs ? (int)(t * 6) % 2 : -1;
  for (int k = 0; k < 4; k++) px(x + kLegCols[k] * U, y + 6 * U, U, 3 * U - ((step >= 0 && k % 2 == step) ? U : 0), orange);

  const int h = U / 2;
  const bool blink = a.blink && fmodf(t, 5.5f) < 0.16f;
  for (int k = 0; k < 2; k++) {
    const int ex = x + (k ? 9 : 4) * U, ey = y + U;
    switch (pose) {
      case cl::Pose::Sleep: px(ex - h / 2, ey + 4 * h / 3 * 2, U + h, h, black); break;
      case cl::Pose::Squint: {  // > <
        const int s = k ? -1 : 1, base = k ? ex + h : ex;
        px(base, ey + h / 2, h, h, black);
        px(base + s * h, ey + h / 2 + h, h, h, black);
        px(base, ey + h / 2 + 2 * h, h, h, black);
        break;
      }
      case cl::Pose::Happy: px(ex, ey, U, U, black); break;
      default:
        if (blink) px(ex, ey + U + h / 2, U, h, black);
        else px(ex, ey, U, 2 * U, black);
    }
  }

  if (a.sweat) {  // gota azul caindo ao lado da cabeça
    float k = fmodf(t * 0.9f, 1.0f);
    if (k < 0.75f) {
      int dx = x + 14 * U + 4, dy = y + (int)(k * U * 2.5f);
      const uint16_t blue = C16(kBlue);
      px(dx + 1, dy, 2, 2, blue);
      px(dx, dy + 2, 4, 3, blue);
    }
  }
  if (a.zzz) {
    static const char* kZ[] = {"###", "..#", ".#.", "#..", "###"};
    const uint16_t muted = C16(kMuted);
    for (int i = 0; i < 3; i++) {
      float k = fmodf(t * 0.35f + i / 3.0f, 1.0f);
      if (k > 0.85f) continue;
      glyph(kZ, 5, x + 13 * U + (int)(k * 14), y - (int)(k * 14) + 2, k < 0.4f ? 1 : 2, muted);
    }
  }
  if (a.sparkles) {
    const int pts[][2] = {{-2, 1}, {16, 0}, {-1, 6}, {15, 6}, {7, -2}};
    for (int i = 0; i < 5; i++) {
      float k = fmodf(t * 1.4f + i * 0.23f, 1.0f);
      if (k > 0.7f) continue;
      const int cx = x + pts[i][0] * U, cy = y + pts[i][1] * U, s = 2 + (int)(k * 3);
      const uint16_t c = C16(i % 2 ? kCream : kOrange);
      px(cx - s / 2, cy - s * 3 / 2, s, s * 3, c);
      px(cx - s * 3 / 2, cy - s / 2, s * 3, s, c);
    }
  }
  if (pose == cl::Pose::Confused) {
    static const char* kQ[] = {".##.", "#..#", "...#", "..#.", ".#..", "....", ".#.."};
    glyph(kQ, 7, x + 14 * U + 2, y, 3, orange);
  }
  lv_obj_invalidate(canvas);
}

// ---------- barras e linhas ----------

Row make_row(lv_obj_t* scr, const char* name, int y) {
  Row r;
  r.label = label(scr, &font_label_12, kMuted);
  lv_label_set_text(r.label, name);
  lv_obj_set_pos(r.label, X0, y);
  r.pct = label(scr, &font_num_26, kIvory);
  lv_obj_align(r.pct, LV_ALIGN_TOP_RIGHT, -X0, y - 6);
  r.track = rect(scr, BW, 10, kTrack);
  lv_obj_set_pos(r.track, X0, y + 27);
  r.ghost = rect(r.track, 0, 10, kGhost);
  r.fill = rect(r.track, 0, 10, kCream);
  r.bead = rect(r.track, 6, 6, kIvory);
  r.reset = label(scr, &font_text_11, kMuted);
  lv_obj_set_pos(r.reset, X0, y + 39);
  return r;
}

void apply_row(Row& r, const cl::WinView& w, bool fade, float t) {
  set_text(r.pct, w.pct);
  lv_obj_align(r.pct, LV_ALIGN_TOP_RIGHT, -X0, lv_obj_get_y(r.label) - 6);
  lv_obj_set_style_text_color(r.pct, C(w.quota && w.risky ? kOrange : kIvory), 0);
  set_text(r.reset, w.reset);
  lv_obj_set_style_text_color(r.reset, C(w.renewed ? kGreen : kMuted), 0);
  const lv_opa_t o = fade ? kFade : LV_OPA_COVER;
  lv_obj_set_style_opa(r.label, o, 0);
  lv_obj_set_style_opa(r.pct, o, 0);
  lv_obj_set_style_opa(r.reset, o, 0);

  const bool bars = w.quota && !w.renewed;
  const int fillW = bars && w.used > 0 ? LV_MAX(10, (int)(BW * fminf(w.used, 100) / 100)) : 0;
  const int paceW = bars && w.showPace ? LV_MAX(10, (int)(BW * w.pace / 100)) : 0;
  lv_obj_set_width(r.ghost, paceW);
  lv_obj_set_width(r.fill, fillW);
  lv_obj_set_style_bg_color(r.fill, C(w.risky ? kOrange : kCream), 0);
  lv_obj_set_style_bg_opa(r.fill, w.limit ? (lv_opa_t)(255 * (0.65f + 0.35f * sinf(t * 2.4f))) : LV_OPA_COVER, 0);
  if (paceW > 0) {
    lv_obj_remove_flag(r.bead, LV_OBJ_FLAG_HIDDEN);
    const int cx = LV_CLAMP(5, (int)(BW * w.pace / 100), BW - 5);
    lv_obj_set_pos(r.bead, cx - 3, 2);
    lv_obj_set_style_bg_color(r.bead, C(w.beadOnFill ? kBg : kIvory), 0);
  } else {
    lv_obj_add_flag(r.bead, LV_OBJ_FLAG_HIDDEN);
  }
}

void show_data(bool on) {
  lv_obj_t* objs[] = {meta, divider, ctxL, usdL, freshDot, freshL, rows[0].label, rows[0].pct, rows[0].track, rows[0].reset,
                      rows[1].label, rows[1].pct, rows[1].track, rows[1].reset};
  for (lv_obj_t* o : objs) on ? lv_obj_remove_flag(o, LV_OBJ_FLAG_HIDDEN) : lv_obj_add_flag(o, LV_OBJ_FLAG_HIDDEN);
}

void layout(cl::Screen s) {
  if (s == cl::Screen::Data) {
    show_data(true);
    lv_obj_set_pos(canvas, 16 - OX, 20 - OY);
    lv_obj_set_style_text_align(title, LV_TEXT_ALIGN_LEFT, 0);
    lv_obj_set_style_text_align(sub, LV_TEXT_ALIGN_LEFT, 0);
    lv_obj_set_width(title, LV_SIZE_CONTENT);
    lv_obj_set_width(sub, LV_SIZE_CONTENT);
    lv_obj_set_pos(title, 112, 20);
    lv_obj_set_pos(sub, 112, 52);
  } else {  // aguardando e protocolo: Clawd grande no centro
    show_data(false);
    lv_obj_set_pos(canvas, W / 2 - 7 * U - OX, 40 - OY);
    for (lv_obj_t* l : {title, sub}) {
      lv_obj_set_width(l, W);
      lv_obj_set_style_text_align(l, LV_TEXT_ALIGN_CENTER, 0);
    }
    lv_obj_set_pos(title, 0, 112);
    lv_obj_set_pos(sub, 0, 144);
  }
}

}  // namespace

void ui_init() {
  lv_obj_t* scr = lv_screen_active();
  lv_obj_set_style_bg_color(scr, C(kBg), 0);
  lv_obj_set_style_bg_opa(scr, LV_OPA_COVER, 0);
  lv_obj_remove_flag(scr, LV_OBJ_FLAG_SCROLLABLE);

  canvas = lv_canvas_create(scr);
  lv_canvas_set_buffer(canvas, cbuf, CW, CH, LV_COLOR_FORMAT_RGB565);

  title = label(scr, &font_lora_22, kIvory);
  sub = label(scr, &font_text_12, kMuted);
  meta = label(scr, &font_text_11, kDim);
  lv_obj_set_pos(meta, 112, 70);

  rows[0] = make_row(scr, "Sessão", 96);
  rows[1] = make_row(scr, "Semana", 158);

  divider = rect(scr, BW, 1, kTrack);
  lv_obj_set_style_radius(divider, 0, 0);
  lv_obj_set_pos(divider, X0, 215);
  ctxL = label(scr, &font_text_11, kMuted);
  lv_obj_set_pos(ctxL, X0, 221);
  usdL = label(scr, &font_text_11, kMuted);
  freshL = label(scr, &font_label_12, kMuted);
  freshDot = rect(scr, 6, 6, kGreen);
}

void ui_update(const cl::State& st, uint32_t nowMs) {
  static uint32_t last = 0;
  if (nowMs - last < 100 && lastScreen != (cl::Screen)-1) return;  // ~10 quadros/s bastam
  last = nowMs;
  const float t = nowMs / 1000.0f;
  const cl::View v = cl::make_view(st, nowMs);

  if (v.screen != lastScreen) { layout(v.screen); lastScreen = v.screen; }
  draw_clawd(v.pose, t);

  const bool hot = v.status == cl::Status::Fast || v.status == cl::Status::High || v.status == cl::Status::Limit;
  set_text(title, v.title);
  lv_obj_set_style_text_color(title, C(hot ? kOrange : v.status == cl::Status::Party ? kGreen : kIvory), 0);
  set_text(sub, v.sub);
  if (v.screen != cl::Screen::Data) {
    lv_obj_set_style_text_color(title, C(kIvory), 0);
    return;
  }

  set_text(meta, v.meta);
  apply_row(rows[0], v.w5, v.stale, t);
  apply_row(rows[1], v.w7, v.stale, t);

  set_text(ctxL, v.ctx);
  set_text(usdL, v.usd);
  lv_obj_align_to(usdL, ctxL, LV_ALIGN_OUT_RIGHT_MID, 12, 0);
  lv_obj_set_style_opa(ctxL, v.stale ? kFade : LV_OPA_COVER, 0);
  lv_obj_set_style_opa(usdL, v.stale ? kFade : LV_OPA_COVER, 0);
  set_text(freshL, v.fresh);
  lv_obj_set_style_text_color(freshL, C(v.stale ? kOrange : kMuted), 0);
  lv_obj_align(freshL, LV_ALIGN_TOP_RIGHT, -X0, 220);
  lv_obj_set_style_bg_color(freshDot, C(v.stale ? kOrange : kGreen), 0);
  lv_obj_align_to(freshDot, freshL, LV_ALIGN_OUT_LEFT_MID, -5, 0);
}
