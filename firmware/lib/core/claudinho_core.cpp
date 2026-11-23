#include "claudinho_core.h"

#include <ArduinoJson.h>
#include <math.h>
#include <stdio.h>
#include <string.h>

namespace cl {

// ---------- protocolo (door 1) ----------

static void read_window(JsonVariantConst v, Window& w) {
  if (!v.is<JsonArrayConst>()) return;
  JsonArrayConst a = v.as<JsonArrayConst>();
  if (a.size() != 2 || !a[0].is<double>() || !a[1].is<int64_t>()) return;
  w.present = true;
  w.used = a[0].as<double>();
  w.resets = a[1].as<int64_t>();
}

Parse parse_line(const char* s, Line& out, int& version) {
  if (strcmp(s, "?") == 0) return Parse::Hello;
  if (strlen(s) > kMaxLine) return Parse::Bad;

  JsonDocument doc;
  if (deserializeJson(doc, s) != DeserializationError::Ok) return Parse::Bad;
  if (!doc.is<JsonObject>() || !doc["v"].is<int>()) return Parse::Bad;
  version = doc["v"].as<int>();
  if (version != 1) return Parse::Version;
  if (!doc["t"].is<int64_t>()) return Parse::Bad;

  Line l;
  l.t = doc["t"].as<int64_t>();
  l.tz = doc["tz"] | 0;
  read_window(doc["h5"], l.h5);
  read_window(doc["d7"], l.d7);
  if (doc["ctx"].is<double>()) { l.hasCtx = true; l.ctx = doc["ctx"].as<double>(); }
  if (doc["usd"].is<double>()) { l.hasUsd = true; l.usd = doc["usd"].as<double>(); }
  snprintf(l.model, sizeof l.model, "%s", doc["m"] | "");
  snprintf(l.proj, sizeof l.proj, "%s", doc["p"] | "");
  out = l;
  return Parse::Ok;
}

bool LineReader::feed(char c) {
  if (c == '\r') return false;
  if (c == '\n') {
    bool ok = !overflow_ && n_ > 0;
    buf_[n_] = '\0';
    n_ = 0;
    overflow_ = false;
    return ok;
  }
  if (n_ >= kMaxLine) { overflow_ = true; return false; }
  buf_[n_++] = c;
  return false;
}

// ---------- estado ----------

void on_line(State& st, const Line& l, uint32_t nowMs) {
  st.line = l;
  st.hasLine = true;
  st.rxMs = nowMs;
  st.badVersion = 0;
  st.h5WasActive = l.h5.present && l.h5.resets > l.t;
}

void on_version(State& st, int v) { st.badVersion = v; }

int64_t now_unix(const State& st, uint32_t nowMs) {
  return st.line.t + (int64_t)((uint32_t)(nowMs - st.rxMs) / 1000);
}

void tick(State& st, uint32_t nowMs) {
  if (st.party && (int32_t)(nowMs - st.partyUntilMs) >= 0) st.party = false;
  if (!st.hasLine || !st.line.h5.present) return;
  bool active = st.line.h5.resets > now_unix(st, nowMs);
  if (st.h5WasActive && !active) {
    st.party = true;
    st.partyUntilMs = nowMs + kPartyMs;
  }
  st.h5WasActive = active;
}

// ---------- formatação ----------

void fmt_dur(char* out, size_t n, double minutes) {
  long m = lround(minutes < 0 ? 0 : minutes);
  if (m < 60) snprintf(out, n, "%ld min", m);
  else if (m < 1440) snprintf(out, n, "%ldh%02ld", m / 60, m % 60);
  else snprintf(out, n, "%ldd %ldh", m / 1440, (m % 1440) / 60);
}

void fmt_weekday(char* out, size_t n, int64_t unix, int tz) {
  static const char* kDays[] = {"dom", "seg", "ter", "qua", "qui", "sex", "sáb"};
  int64_t local = unix + (int64_t)tz * 60;
  int64_t days = local >= 0 ? local / 86400 : (local - 86399) / 86400;
  int64_t sec = local - days * 86400;
  int wd = (int)(((days % 7) + 7 + 4) % 7);  // 01/01/1970 foi quinta
  snprintf(out, n, "%s %02d:%02d", kDays[wd], (int)(sec / 3600), (int)((sec % 3600) / 60));
}

static void fmt_pct(char* out, size_t n, double v) { snprintf(out, n, "%ld%%", (long)floor(v + 0.5)); }

// ---------- status ----------

struct Win {
  bool present = false, renewed = false, limit = false, exhaust = false;
  double used = 0, pace = 0, remMin = 0, exhaustMin = 0;
};

static Win analyze(const Window& w, int64_t now, int64_t len) {
  Win r;
  r.present = w.present;
  if (!w.present) return r;
  int64_t rem = w.resets - now;
  if (rem <= 0) { r.renewed = true; return r; }
  if (rem > len) rem = len;
  double elMin = (len - rem) / 60.0;
  r.remMin = rem / 60.0;
  r.used = w.used;
  r.pace = (len - rem) * 100.0 / len;
  r.limit = w.used >= 100;
  if (w.used > 0 && w.used < 100 && elMin > 0) {
    double toFull = (100 - w.used) / (w.used / elMin);  // projeção linear
    if (toFull < r.remMin) { r.exhaust = true; r.exhaustMin = toFull; }
  }
  return r;
}

static bool risky(const Win& w) { return w.limit || w.exhaust || w.used >= 80; }

Anim anim_for(Pose p) {
  switch (p) {
    case Pose::Idle: return {true, false, false, false, false, false, false};
    case Pose::Alarm: return {true, true, true, true, false, false, false};
    case Pose::Squint: return {false, false, false, false, false, false, true};
    case Pose::Happy: return {false, true, false, false, false, true, false};
    case Pose::Sleep: return {false, false, false, false, true, false, false};
    case Pose::Confused: return {true, false, false, false, false, false, false};
  }
  return {};
}

static void fill_win(WinView& v, const Win& w, bool is5h, const Line& l, int64_t now) {
  v.quota = w.present;
  if (!w.present) {
    snprintf(v.pct, sizeof v.pct, "--");
    snprintf(v.reset, sizeof v.reset, "indisponível neste login");
    return;
  }
  v.renewed = w.renewed;
  if (w.renewed) {
    snprintf(v.pct, sizeof v.pct, "0%%");
    snprintf(v.reset, sizeof v.reset, "renovada ✓");
    return;
  }
  v.used = (float)w.used;
  v.pace = (float)w.pace;
  v.limit = w.limit;
  v.risky = risky(w);
  v.showPace = !w.limit && w.pace > 0;
  v.beadOnFill = w.used >= w.pace;
  fmt_pct(v.pct, sizeof v.pct, w.used);
  char t[24];
  if (is5h) {
    fmt_dur(t, sizeof t, w.remMin);
    snprintf(v.reset, sizeof v.reset, "renova em %s", t);
  } else {
    fmt_weekday(t, sizeof t, (is5h ? l.h5 : l.d7).resets, l.tz);
    snprintf(v.reset, sizeof v.reset, "renova %s", t);
  }
  (void)now;
}

View make_view(const State& st, uint32_t nowMs) {
  View v;
  if (st.badVersion != 0) {
    v.screen = Screen::Protocol;
    v.pose = Pose::Confused;
    snprintf(v.title, sizeof v.title, "Firmware desatualizado");
    snprintf(v.sub, sizeof v.sub, "o PC mandou o protocolo v%d", st.badVersion);
    return v;
  }
  if (!st.hasLine) {
    v.screen = Screen::Waiting;
    v.pose = Pose::Sleep;
    snprintf(v.title, sizeof v.title, "Esperando o Claude Code");
    snprintf(v.sub, sizeof v.sub, "abra uma sessão no terminal");
    return v;
  }

  const Line& l = st.line;
  int64_t now = now_unix(st, nowMs);
  Win a = analyze(l.h5, now, kLen5h), b = analyze(l.d7, now, kLen7d);
  v.screen = Screen::Data;
  fill_win(v.w5, a, true, l, now);
  fill_win(v.w7, b, false, l, now);

  char t[24];
  if (st.party) {
    v.status = Status::Party;
    snprintf(v.title, sizeof v.title, "Cota renovada!");
    snprintf(v.sub, sizeof v.sub, "a sessão de 5h zerou");
  } else if (!a.present && !b.present) {
    v.status = Status::NoQuota;
    snprintf(v.title, sizeof v.title, "Cota indisponível");
    snprintf(v.sub, sizeof v.sub, "este login não informa 5h e 7d");
  } else if (a.limit || b.limit) {
    const Win& w = a.limit ? a : b;
    v.status = Status::Limit;
    fmt_dur(t, sizeof t, w.remMin);
    snprintf(v.title, sizeof v.title, "Limite atingido");
    snprintf(v.sub, sizeof v.sub, "volta em %s", t);
  } else if (a.exhaust || b.exhaust) {
    double m = a.exhaust && b.exhaust ? fmin(a.exhaustMin, b.exhaustMin) : (a.exhaust ? a.exhaustMin : b.exhaustMin);
    v.status = Status::Fast;
    fmt_dur(t, sizeof t, m);
    snprintf(v.title, sizeof v.title, "Ritmo acelerado");
    snprintf(v.sub, sizeof v.sub, "nesse ritmo, acaba em %s", t);
  } else if (a.used >= 80 || b.used >= 80) {
    double used = a.used >= 80 ? a.used : b.used;
    v.status = Status::High;
    snprintf(v.title, sizeof v.title, "Quase no limite");
    snprintf(v.sub, sizeof v.sub, "restam %ld%% até renovar", (long)floor(100 - used + 0.5));
  } else {
    v.status = Status::Calm;
    snprintf(v.title, sizeof v.title, "Tranquilo");
    snprintf(v.sub, sizeof v.sub, "nesse ritmo, sobra cota");
  }

  switch (v.status) {
    case Status::Fast: case Status::High: v.pose = Pose::Alarm; break;
    case Status::Limit: v.pose = Pose::Squint; break;
    case Status::Party: v.pose = Pose::Happy; break;
    default: v.pose = Pose::Idle; break;
  }

  if (l.model[0] && l.proj[0]) snprintf(v.meta, sizeof v.meta, "%s · %s", l.model, l.proj);
  else snprintf(v.meta, sizeof v.meta, "%s%s", l.model, l.proj);

  if (l.hasCtx) snprintf(v.ctx, sizeof v.ctx, "contexto %ld%%", (long)floor(l.ctx + 0.5));
  else snprintf(v.ctx, sizeof v.ctx, "contexto --");
  if (l.hasUsd) {
    snprintf(v.usd, sizeof v.usd, "US$ %.2f", l.usd);
    if (char* dot = strchr(v.usd, '.')) *dot = ',';
  }

  uint32_t age = nowMs - st.rxMs;
  v.stale = age > kStaleMs;
  if (v.stale) snprintf(v.fresh, sizeof v.fresh, "há %lu min", (unsigned long)(age / 60000));
  else {
    int64_t local = l.t + (int64_t)l.tz * 60;  // horário da última leitura (AC 14), não o relógio atual
    int64_t sec = ((local % 86400) + 86400) % 86400;
    snprintf(v.fresh, sizeof v.fresh, "%02d:%02d", (int)(sec / 3600), (int)((sec % 3600) / 60));
  }
  return v;
}

}  // namespace cl
