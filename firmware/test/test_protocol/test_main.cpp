#include <Arduino.h>
#include <string.h>
#include <unity.h>

#include "claudinho_core.h"

using namespace cl;

void setUp() {}
void tearDown() {}

// C20: malformada, truncada e 513 bytes são descartadas; o estado anterior fica igual
void test_rejects_bad_lines() {
  Line keep;
  keep.t = 42;
  snprintf(keep.model, sizeof keep.model, "Opus");
  int ver = 0;

  const char* bad[] = {"isso não é json", "{\"v\":1,\"t\":5", "{\"v\":1,", "{\"t\":5}", "[1,2]"};
  for (const char* s : bad) {
    Line out = keep;
    TEST_ASSERT_EQUAL_MESSAGE((int)Parse::Bad, (int)parse_line(s, out, ver), s);
    TEST_ASSERT_EQUAL(42, (int)out.t);
    TEST_ASSERT_EQUAL_STRING("Opus", out.model);
  }

  // 513 bytes na serial: a linha inteira some, a próxima linha boa passa
  LineReader r;
  bool got = false;
  for (int i = 0; i < 513; i++) got |= r.feed('x');
  got |= r.feed('\n');
  TEST_ASSERT_FALSE(got);
  const char* good = "{\"v\":1,\"t\":7}\n";
  for (const char* p = good; *p; p++) got = r.feed(*p);
  TEST_ASSERT_TRUE(got);
  TEST_ASSERT_EQUAL_STRING("{\"v\":1,\"t\":7}", r.line());

  // 512 bytes ainda é aceito pelo leitor
  for (int i = 0; i < 512; i++) TEST_ASSERT_FALSE(r.feed('y'));
  TEST_ASSERT_TRUE(r.feed('\n'));
  TEST_ASSERT_EQUAL(512, (int)strlen(r.line()));
}

// C21: v = 2 leva à tela de protocolo
void test_unknown_version() {
  Line out;
  int ver = 0;
  TEST_ASSERT_EQUAL((int)Parse::Version, (int)parse_line("{\"v\":2,\"t\":5}", out, ver));
  TEST_ASSERT_EQUAL(2, ver);

  State st;
  on_version(st, ver);
  View v = make_view(st, 1000);
  TEST_ASSERT_EQUAL((int)Screen::Protocol, (int)v.screen);
  TEST_ASSERT_EQUAL((int)Pose::Confused, (int)v.pose);
  TEST_ASSERT_EQUAL_STRING("o PC mandou o protocolo v2", v.sub);
}

// handshake e a linha completa da door 1
void test_hello_and_full_line() {
  Line out;
  int ver = 0;
  TEST_ASSERT_EQUAL((int)Parse::Hello, (int)parse_line("?", out, ver));

  const char* s = "{\"v\":1,\"t\":1763870000,\"tz\":-180,\"h5\":[23.5,1763880000],\"d7\":[41,1764300000],"
                  "\"ctx\":37,\"tok\":152340,\"usd\":4.12,\"m\":\"Opus\",\"p\":\"relatório\"}";
  TEST_ASSERT_EQUAL((int)Parse::Ok, (int)parse_line(s, out, ver));
  TEST_ASSERT_TRUE(out.h5.present);
  TEST_ASSERT_EQUAL_FLOAT(23.5f, (float)out.h5.used);
  TEST_ASSERT_EQUAL(1763880000, (int32_t)out.h5.resets);
  TEST_ASSERT_EQUAL(-180, out.tz);
  TEST_ASSERT_EQUAL_STRING("relatório", out.proj);
}

static const char* kFull =
    "{\"v\":1,\"t\":1763870000,\"tz\":-180,\"h5\":[23.5,1763880000],\"d7\":[41,1764300000],"
    "\"ctx\":37,\"tok\":152340,\"usd\":4.12,\"m\":\"Opus\"}";

// C2, C4: pelo BLE a linha chega em pedaços (escritas na RX); o leitor junta igual
void test_chunked_line() {
  const size_t sizes[] = {1, 20, 182};
  for (size_t size : sizes) {
    LineReader r;
    char buf[600];
    int n = snprintf(buf, sizeof buf, "%s\n", kFull);
    int lines = 0;
    for (int i = 0; i < n; i += size) {
      for (int j = i; j < n && j < i + (int)size; j++) lines += r.feed(buf[j]);
    }
    TEST_ASSERT_EQUAL(1, lines);
    TEST_ASSERT_EQUAL_STRING(kFull, r.line());
    Line out;
    int ver = 0;
    TEST_ASSERT_EQUAL((int)Parse::Ok, (int)parse_line(r.line(), out, ver));
    TEST_ASSERT_EQUAL_STRING("", out.proj);  // door 3: sem p pelo ar
  }

  // 513 bytes em pedaços de 20 sem '\n': some inteira, a próxima linha boa passa
  LineReader r;
  bool got = false;
  for (int i = 0; i < 513; i += 20)
    for (int j = i; j < 513 && j < i + 20; j++) got |= r.feed('x');
  got |= r.feed('\n');
  TEST_ASSERT_FALSE(got);
  for (const char* p = "{\"v\":1,\"t\":7}\n"; *p; p++) got = r.feed(*p);
  TEST_ASSERT_TRUE(got);
  TEST_ASSERT_EQUAL_STRING("{\"v\":1,\"t\":7}", r.line());
}

// C5: USB e BLE ao mesmo tempo, um leitor por origem
void test_interleaved_origins() {
  const char* a = "{\"v\":1,\"t\":111,\"m\":\"usb\"}\n";
  const char* b = "{\"v\":1,\"t\":222,\"m\":\"ble-link\"}\n";
  LineReader ra, rb;
  bool gotA = false, gotB = false;
  const size_t la = strlen(a), lb = strlen(b);
  for (size_t i = 0; i < la || i < lb; i++) {
    if (i < la) gotA |= ra.feed(a[i]);
    if (i < lb) gotB |= rb.feed(b[i]);
  }
  TEST_ASSERT_TRUE(gotA);
  TEST_ASSERT_TRUE(gotB);
  TEST_ASSERT_EQUAL_STRING("{\"v\":1,\"t\":111,\"m\":\"usb\"}", ra.line());
  TEST_ASSERT_EQUAL_STRING("{\"v\":1,\"t\":222,\"m\":\"ble-link\"}", rb.line());
}

void setup() {
  delay(2000);
  UNITY_BEGIN();
  RUN_TEST(test_rejects_bad_lines);
  RUN_TEST(test_unknown_version);
  RUN_TEST(test_hello_and_full_line);
  RUN_TEST(test_chunked_line);
  RUN_TEST(test_interleaved_origins);
  UNITY_END();
}

void loop() {}
