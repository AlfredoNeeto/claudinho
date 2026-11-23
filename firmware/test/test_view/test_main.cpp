#include <Arduino.h>
#include <unity.h>

#include "claudinho_core.h"

using namespace cl;

void setUp() {}
void tearDown() {}

// t de modo que o reset semanal caia numa quinta às 20:11 locais (tz -180)
static const int64_t kD7Reset = 1764889860;
static const int64_t kT = kD7Reset - 3 * 86400;

static Line full_line() {
  Line l;
  l.t = kT;
  l.tz = -180;
  l.h5 = {true, 23.5, kT + 136 * 60};
  l.d7 = {true, 41, kD7Reset};
  l.hasCtx = true; l.ctx = 37;
  l.hasUsd = true; l.usd = 4.12;
  snprintf(l.model, sizeof l.model, "Opus");
  snprintf(l.proj, sizeof l.proj, "monitor");
  return l;
}

// C14
void test_full_line_view() {
  State st;
  on_line(st, full_line(), 1000);
  View v = make_view(st, 1000);
  TEST_ASSERT_EQUAL((int)Screen::Data, (int)v.screen);
  TEST_ASSERT_EQUAL_STRING("Tranquilo", v.title);
  TEST_ASSERT_EQUAL_STRING("Opus · monitor", v.meta);
  TEST_ASSERT_EQUAL_STRING("24%", v.w5.pct);
  TEST_ASSERT_EQUAL_STRING("renova em 2h16", v.w5.reset);
  TEST_ASSERT_EQUAL_STRING("41%", v.w7.pct);
  TEST_ASSERT_EQUAL_STRING("renova qui 20:11", v.w7.reset);
  TEST_ASSERT_EQUAL_STRING("contexto 37%", v.ctx);
  TEST_ASSERT_EQUAL_STRING("US$ 4,12", v.usd);
}

// C16
void test_waiting_before_first_line() {
  State st;
  View v = make_view(st, 1000);
  TEST_ASSERT_EQUAL((int)Screen::Waiting, (int)v.screen);
  TEST_ASSERT_EQUAL((int)Pose::Sleep, (int)v.pose);
  TEST_ASSERT_EQUAL_STRING("Esperando o Claude Code", v.title);
}

// C18
void test_renewed_window() {
  Line l = full_line();
  l.h5.resets = kT - 10;
  State st;
  on_line(st, l, 1000);
  View v = make_view(st, 1000);
  TEST_ASSERT_TRUE(v.w5.renewed);
  TEST_ASSERT_EQUAL_STRING("0%", v.w5.pct);
  TEST_ASSERT_EQUAL_STRING("renovada ✓", v.w5.reset);
}

// C19
void test_stale_after_3_minutes() {
  State st;
  on_line(st, full_line(), 0);
  View fresh = make_view(st, 179000);
  TEST_ASSERT_FALSE(fresh.stale);
  TEST_ASSERT_EQUAL_STRING("20:13", fresh.fresh);

  View old = make_view(st, 181000);
  TEST_ASSERT_TRUE(old.stale);
  TEST_ASSERT_EQUAL_STRING("há 3 min", old.fresh);
}

// C22
void test_missing_quota() {
  Line l = full_line();
  l.h5 = {};
  l.d7 = {};
  State st;
  on_line(st, l, 1000);
  View v = make_view(st, 1000);
  TEST_ASSERT_EQUAL_STRING("Cota indisponível", v.title);
  TEST_ASSERT_EQUAL_STRING("--", v.w5.pct);
  TEST_ASSERT_EQUAL_STRING("--", v.w7.pct);
  TEST_ASSERT_EQUAL_STRING("indisponível neste login", v.w5.reset);
  TEST_ASSERT_EQUAL_STRING("indisponível neste login", v.w7.reset);
  TEST_ASSERT_EQUAL_STRING("contexto 37%", v.ctx);
  TEST_ASSERT_EQUAL_STRING("US$ 4,12", v.usd);
}

// C33: 100 min de uma janela de 300 = 33,3%; bolinha escura com uso >= ritmo
void test_pace_marker() {
  Line l = full_line();
  l.h5 = {true, 20, kT + 200 * 60};
  State st;
  on_line(st, l, 1000);
  View v = make_view(st, 1000);
  TEST_ASSERT_FLOAT_WITHIN(0.05f, 33.33f, v.w5.pace);
  TEST_ASSERT_TRUE(v.w5.showPace);
  TEST_ASSERT_FALSE(v.w5.beadOnFill);

  l.h5.used = 40;
  on_line(st, l, 1000);
  TEST_ASSERT_TRUE(make_view(st, 1000).w5.beadOnFill);

  l.h5.used = 100;
  on_line(st, l, 1000);
  TEST_ASSERT_FALSE(make_view(st, 1000).w5.showPace);
}

void setup() {
  delay(2000);
  UNITY_BEGIN();
  RUN_TEST(test_full_line_view);
  RUN_TEST(test_waiting_before_first_line);
  RUN_TEST(test_renewed_window);
  RUN_TEST(test_stale_after_3_minutes);
  RUN_TEST(test_missing_quota);
  RUN_TEST(test_pace_marker);
  UNITY_END();
}

void loop() {}
