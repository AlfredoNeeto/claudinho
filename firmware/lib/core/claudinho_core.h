// Núcleo do Claudinho: protocolo, relógio, status e modelo de tela.
// C++ puro (só ArduinoJson), igual para todas as placas; a UI só desenha o View.
#pragma once
#include <stddef.h>
#include <stdint.h>

namespace cl {

constexpr size_t kMaxLine = 512;
constexpr int64_t kLen5h = 300 * 60;    // segundos
constexpr int64_t kLen7d = 10080 * 60;
constexpr uint32_t kStaleMs = 180000;   // 3 min sem linha
constexpr uint32_t kPartyMs = 4500;

struct Window { bool present = false; double used = 0; int64_t resets = 0; };

struct Line {
  int64_t t = 0;
  int tz = 0;  // minutos em relação ao UTC
  Window h5, d7;
  bool hasCtx = false; double ctx = 0;
  bool hasUsd = false; double usd = 0;
  char model[24] = "";
  char proj[48] = "";
};

enum class Parse { Ok, Bad, Version, Hello };
Parse parse_line(const char* s, Line& out, int& version);

// Junta bytes até '\n'. Linha com mais de 512 bytes é descartada inteira.
class LineReader {
 public:
  bool feed(char c);  // true quando line() tem uma linha completa
  const char* line() const { return buf_; }
 private:
  char buf_[kMaxLine + 1] = {};
  size_t n_ = 0;
  bool overflow_ = false;
};

enum class Screen { Waiting, Data, Protocol };
enum class Pose { Idle, Alarm, Squint, Happy, Sleep, Confused };
enum class Status { Calm, Fast, High, Limit, NoQuota, Party };

struct Anim { bool blink, bob, legs, sweat, zzz, sparkles, pulse; };
Anim anim_for(Pose p);

struct WinView {
  bool quota = false, renewed = false, risky = false, limit = false;
  bool showPace = false, beadOnFill = false;
  float used = 0;  // 0..100
  float pace = 0;  // 0..100, quanto da janela já passou
  char pct[8] = "";
  char reset[32] = "";
};

struct View {
  Screen screen = Screen::Waiting;
  Pose pose = Pose::Sleep;
  Status status = Status::Calm;
  bool stale = false;
  char title[32] = "";
  char sub[48] = "";
  char meta[64] = "";
  WinView w5, w7;
  char ctx[24] = "";
  char usd[24] = "";
  char fresh[16] = "";
};

struct State {
  bool hasLine = false;
  Line line;
  uint32_t rxMs = 0;
  int badVersion = 0;  // != 0: tela de protocolo
  uint32_t partyUntilMs = 0;
  bool party = false;
  bool h5WasActive = false;
};

void on_line(State& st, const Line& l, uint32_t nowMs);
void on_version(State& st, int v);
void tick(State& st, uint32_t nowMs);  // detecta a renovação da 5h para a comemoração
int64_t now_unix(const State& st, uint32_t nowMs);
View make_view(const State& st, uint32_t nowMs);

void fmt_dur(char* out, size_t n, double minutes);           // "43 min", "2h16", "3d 4h"
void fmt_weekday(char* out, size_t n, int64_t unix, int tz);  // "qui 20:21"

}  // namespace cl
