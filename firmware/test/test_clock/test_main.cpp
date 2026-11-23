#include <Arduino.h>
#include <unity.h>

#include "claudinho_core.h"

using namespace cl;

void setUp() {}
void tearDown() {}

// C17: o relógio anda com t + millis; 61 s depois, 2h16 vira 2h15 sem linha nova
void test_countdown_ticks_without_new_line() {
  Line l;
  l.t = 1764630660;
  l.tz = -180;
  l.h5 = {true, 23.5, l.t + 136 * 60};
  State st;
  on_line(st, l, 5000);

  TEST_ASSERT_EQUAL_STRING("renova em 2h16", make_view(st, 5000).w5.reset);
  TEST_ASSERT_EQUAL(1764630660 + 61, (int32_t)now_unix(st, 5000 + 61000));
  TEST_ASSERT_EQUAL_STRING("renova em 2h15", make_view(st, 5000 + 61000).w5.reset);
}

// C37: dia da semana e hora local a partir de resets_at e tz
void test_weekday_local_time() {
  char out[24];
  fmt_weekday(out, sizeof out, 1764890460, -180);
  TEST_ASSERT_EQUAL_STRING("qui 20:21", out);
  fmt_weekday(out, sizeof out, 1764890460, 0);
  TEST_ASSERT_EQUAL_STRING("qui 23:21", out);
  fmt_weekday(out, sizeof out, 1764890460 + 2 * 86400, -180);
  TEST_ASSERT_EQUAL_STRING("sáb 20:21", out);
}

void test_duration_format() {
  char out[24];
  fmt_dur(out, sizeof out, 43);
  TEST_ASSERT_EQUAL_STRING("43 min", out);
  fmt_dur(out, sizeof out, 136);
  TEST_ASSERT_EQUAL_STRING("2h16", out);
  fmt_dur(out, sizeof out, 61.29);
  TEST_ASSERT_EQUAL_STRING("1h01", out);
  fmt_dur(out, sizeof out, 3 * 1440 + 4 * 60);
  TEST_ASSERT_EQUAL_STRING("3d 4h", out);
}

void setup() {
  delay(2000);
  UNITY_BEGIN();
  RUN_TEST(test_countdown_ticks_without_new_line);
  RUN_TEST(test_weekday_local_time);
  RUN_TEST(test_duration_format);
  UNITY_END();
}

void loop() {}
