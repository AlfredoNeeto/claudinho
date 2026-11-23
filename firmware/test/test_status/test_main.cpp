#include <Arduino.h>
#include <unity.h>

#include "claudinho_core.h"

using namespace cl;

void setUp() {}
void tearDown() {}

static const int64_t kT = 1764630660;

// janela de 5h com uso e minutos até o reset; semana tranquila
static View view_for(double used5h, int minLeft, double used7d = 10, uint32_t nowMs = 1000) {
  Line l;
  l.t = kT;
  l.tz = -180;
  l.h5 = {true, used5h, kT + (int64_t)minLeft * 60};
  l.d7 = {true, used7d, kT + 4 * 86400};
  State st;
  on_line(st, l, nowMs);
  return make_view(st, nowMs);
}

// C30
void test_status_precedence() {
  TEST_ASSERT_EQUAL_STRING("Limite atingido", view_for(100, 43).title);
  TEST_ASSERT_EQUAL_STRING("Ritmo acelerado", view_for(62, 200).title);
  TEST_ASSERT_EQUAL_STRING("Quase no limite", view_for(86, 25).title);
  TEST_ASSERT_EQUAL_STRING("Tranquilo", view_for(18, 160).title);
  // >= 80% e projetando antes do reset: ritmo acelerado ganha de quase no limite
  TEST_ASSERT_EQUAL_STRING("Ritmo acelerado", view_for(86, 120).title);
  // empate: 5h no limite e semana acelerada -> limite ganha
  TEST_ASSERT_EQUAL_STRING("Limite atingido", view_for(100, 43, 95).title);
}

// C31
void test_projection_text() {
  View v = view_for(62, 200);
  TEST_ASSERT_EQUAL_STRING("Ritmo acelerado", v.title);
  TEST_ASSERT_EQUAL_STRING("nesse ritmo, acaba em 1h01", v.sub);
}

// C32
void test_limit_text() {
  TEST_ASSERT_EQUAL_STRING("volta em 43 min", view_for(100, 43).sub);
}

// C34
void test_risk_color() {
  TEST_ASSERT_TRUE(view_for(86, 25).w5.risky);   // >= 80
  TEST_ASSERT_TRUE(view_for(80, 25).w5.risky);   // exatamente 80, sem projeção
  TEST_ASSERT_TRUE(view_for(62, 200).w5.risky);  // projeção antes do reset
  TEST_ASSERT_FALSE(view_for(79, 10).w5.risky);  // 79% no fim da janela: no ritmo
  TEST_ASSERT_FALSE(view_for(18, 160).w5.risky);
}

// C35
void test_pose_table() {
  TEST_ASSERT_EQUAL((int)Pose::Idle, (int)view_for(18, 160).pose);
  TEST_ASSERT_EQUAL((int)Pose::Alarm, (int)view_for(62, 200).pose);
  TEST_ASSERT_EQUAL((int)Pose::Alarm, (int)view_for(86, 25).pose);
  TEST_ASSERT_EQUAL((int)Pose::Squint, (int)view_for(100, 43).pose);

  State waiting;
  TEST_ASSERT_EQUAL((int)Pose::Sleep, (int)make_view(waiting, 0).pose);
  State proto;
  on_version(proto, 2);
  TEST_ASSERT_EQUAL((int)Pose::Confused, (int)make_view(proto, 0).pose);

  // a 5h renova 60 s depois da linha: festa por 4,5 s, depois volta ao normal
  Line l;
  l.t = kT;
  l.h5 = {true, 40, kT + 60};
  l.d7 = {true, 10, kT + 4 * 86400};
  State st;
  on_line(st, l, 0);
  tick(st, 0);
  tick(st, 61000);
  View party = make_view(st, 61000);
  TEST_ASSERT_EQUAL((int)Pose::Happy, (int)party.pose);
  TEST_ASSERT_EQUAL_STRING("Cota renovada!", party.title);
  tick(st, 61000 + 4400);
  TEST_ASSERT_EQUAL((int)Pose::Happy, (int)make_view(st, 61000 + 4400).pose);
  tick(st, 61000 + 4500);
  TEST_ASSERT_EQUAL((int)Pose::Idle, (int)make_view(st, 61000 + 4500).pose);
}

// C36
void test_idle_animates_only_blink() {
  Anim a = anim_for(Pose::Idle);
  TEST_ASSERT_TRUE(a.blink);
  TEST_ASSERT_FALSE(a.bob);
  TEST_ASSERT_FALSE(a.legs);
  TEST_ASSERT_FALSE(a.sweat);
  TEST_ASSERT_FALSE(a.zzz);
  TEST_ASSERT_FALSE(a.sparkles);
  TEST_ASSERT_FALSE(a.pulse);
}

void setup() {
  delay(2000);
  UNITY_BEGIN();
  RUN_TEST(test_status_precedence);
  RUN_TEST(test_projection_text);
  RUN_TEST(test_limit_text);
  RUN_TEST(test_risk_color);
  RUN_TEST(test_pose_table);
  RUN_TEST(test_idle_animates_only_blink);
  UNITY_END();
}

void loop() {}
