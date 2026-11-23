# Claudinho verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 54be0d0..6f5908a
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

PASS. All 39 checks are proven at `6f5908a`. Every automated proof was re-run at HEAD: the 24 .NET cases, the four `pc/tools` scripts, and all four firmware suites on the real CYD (18/18 Unity cases, with a person pressing BOOT+RST before each upload). C29 is still the user's manual attestation. Fix `6f5908a` closes round-1 gaps 1 through 8 and part of gap 10. Gaps 9 and the C31 part of gap 10 remain as low-weight notes, and the fix's door 6 adds one unproven claim (see Ranked gaps). None of these is a check whose claim fails.

Scope (verify.md "Re-verifying after a fix"): the diff `a6f1692..6f5908a`, the 16 round-1 rows marked NOT RUN, and the 10 ranked gaps from round 1. All proofs were re-run in full at `6f5908a`. Round 1 ran no Binding sources step (Step 1 does not run under `light`), and the fix changes no interface, so there is still nothing to run there.

## Fix diff review - verified at 6f5908a

Judged from the diff. The commit message was not used.

- `firmware/lib/core/claudinho_core.cpp:254` - `int64_t local = l.t + (int64_t)l.tz * 60;`. The footer now shows the time of the last reading (`l.t`) instead of `now`. This is a one-line change inside `make_view` and moves no other line. It closes round-1 gap 3, and `firmware/test/test_view/test_main.cpp:71` locks it in.
- `firmware/test/test_view/test_main.cpp:71` - `TEST_ASSERT_EQUAL_STRING("20:11", fresh.fresh)` at 179 s after the reading. Here the old code would give `20:13`, so this assertion tells the two behaviours apart.
- `firmware/test/test_view/test_main.cpp:114-119` - new case where usage equals pace (50% at 150 of 300 min): `TEST_ASSERT_FLOAT_WITHIN(0.01f, 50.0f, eq.w5.pace)`, `TEST_ASSERT_TRUE(eq.w5.beadOnFill)`. It targets `firmware/lib/core/claudinho_core.cpp:164` `v.beadOnFill = w.used >= w.pace;`.
- `firmware/test/test_status/test_main.cpp:32` - `("Ritmo acelerado", view_for(86, 120).title)`. With 86% used after 180 min, the projected time to 100% is about 29 min, which is before the reset at 120 min. So the window is both ≥ 80% and projecting. If the `exhaust` and `used >= 80` branches at `claudinho_core.cpp:216-226` were swapped, this would read `Quase no limite`. That closes gap 2.
- `firmware/test/test_status/test_main.cpp:52` - `TEST_ASSERT_TRUE(view_for(80, 25).w5.risky)`. At 80% exactly the projection (about 69 min) does not finish before the reset at 25 min, so only `w.used >= 80` (`claudinho_core.cpp:132`) makes it true. That closes the 80% part of gap 10.
- `pc/Claudinho.Tests/StatuslineTests.cs:84` - the key set is now a literal list of the 10 door-1 keys under an ordinal sort, not the production constant `Contract.Keys`. That closes gap 6.
- `pc/Claudinho.Tests/StatuslineTests.cs:110-121` - the real `BridgeHost.Running` is exercised against a mutex held on another thread: false before, true while held (`:118`), false after release (`:121`). Production wires the same function to `Local\Claudinho.Bridge` (`pc/Claudinho/Program.cs:24`, `pc/Claudinho/Bridge.cs:73` and `:93`). That closes gap 5. `Running` still tests that the mutex exists rather than that it is held. The bridge holds it for its whole lifetime (`Bridge.cs:73` `using var mutex`), so the two are equivalent in practice.
- `pc/tools/handshake.ps1:24` - `if ($line -eq "claudinho 1 $Board")` with `$Board = 'cyd'` as the default (`:3`). The match is now exact. That closes gap 7.
- `pc/tools/check-publish.ps1:15` - `if ($files[0].Length -lt 50MB) { ... exit 1 }`. Self-containment is now asserted through size. That closes gap 8.
- `plan.md` door 6 and the Flow item 1 now record the `~/AppData/Local/Claudinho/claudinho.exe statusline` command shape, along with the reason (the home path contains a space). That closes the "not recorded" half of gap 4. The plan's claim that Claude Code expands `~` in both shells is still unproven (new gap 2 below).
- `checks.md`: only the Handoff notes changed (a round-1 line was added and the existing lines were re-written with identical text). No check claim or proof command changed.
- The two `.ps1` files were rewritten in full, but only the lines listed above changed in content (line endings were normalised).

## Checks - verified at 6f5908a

Every proof below was re-run at `6f5908a`. Citations in files the fix touched were refreshed. Citations in files the fix did not touch (`BridgeTests.cs`, `InstallTests.cs`, `test_protocol`, `test_clock`, `ui.cpp`, `main.cpp`, `Program.cs`, `Bridge.cs`, `measure-statusline.ps1`, `check_palette.py`) are unchanged from `a6f1692` and were checked against the diff stat.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | full payload prints `5h 24% · 7d 41% · ctx 37%`, exit 0 | `dotnet test pc/Claudinho.Tests` with the batched Statusline/Bridge/Install filter, exit 0. `Aprovado ...StatuslineTests.Prints_text_for_full_payload` | `pc/Claudinho.Tests/StatuslineTests.cs:38` - `Assert.Equal("5h 24% · 7d 41% · ctx 37%", text)`; `:37` - `Assert.Equal(0, code)` | PASS |
| C2 | missing window prints `--`, exit 0 | same run, all 3 `Prints_dashes_when_window_missing` cases `Aprovado` | `pc/Claudinho.Tests/StatuslineTests.cs:53` - `Assert.Equal(expected, text)` with `[InlineData("five_hour", "5h -- · 7d 41% · ctx 37%")]` at `:43`; `:52` - `Assert.Equal(0, code)` | PASS |
| C3 | invalid JSON prints `claudinho: sem dados`, exit 0, snapshot untouched | same run, all 3 `Invalid_json_keeps_snapshot` cases `Aprovado` | `pc/Claudinho.Tests/StatuslineTests.cs:67` - `Assert.Equal("claudinho: sem dados", text)`; `:68` - `Assert.Equal("anterior", File.ReadAllText(Snap))` | PASS |
| C4 | snapshot has only `v t tz h5 d7 ctx tok usd m p`, `v`=1, `p` is the folder name | same run, all 3 `Snapshot_has_only_contract_fields` cases `Aprovado` | `pc/Claudinho.Tests/StatuslineTests.cs:84` - `Assert.Equal(new[] { "ctx", "d7", "h5", "m", "p", "t", "tok", "tz", "usd", "v" }, snap.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal))`; `:85` - `Assert.Equal(1, snap["v"]...)`; `:95-97` - `Assert.Equal(expectedP, ...)`, `DoesNotContain("\\")`, `DoesNotContain("/")` | PASS |
| C5 | published exe runs `statusline` in < 500 ms (median of 5) | `pwsh -NoProfile -File pc/tools/measure-statusline.ps1`, exit 0: `mediana: 46 ms (execuções: 51, 46, 46, 49, 45 ms)` | `pc/tools/measure-statusline.ps1:26` - `if ($med -ge 500) { exit 1 }`; `:17` - output must equal `'5h 24% · 7d 41% · ctx 37%'` | PASS |
| C6 | launcher called once when no bridge holds `Local\Claudinho.Bridge`, not when one does | same dotnet run. `Starts_bridge_only_when_absent` `Aprovado` | `pc/Claudinho.Tests/StatuslineTests.cs:105` - `Assert.Equal(1, started)` (absent); `:108` - still 1 (present); `:118` - `Assert.True(BridgeHost.Running(name))` while another thread holds the mutex; `:112`/`:121` - false before and after. Production wiring: `pc/Claudinho/Program.cs:24` `() => BridgeHost.Running(MutexName)` | PASS |
| C38 | 20 parallel writes leave a parseable snapshot equal to one input | same run. `Concurrent_writes_leave_valid_snapshot` `Aprovado` | `pc/Claudinho.Tests/StatuslineTests.cs` (the test after `:124`) - `Assert.NotNull(JsonNode.Parse(final))`, `Assert.Contains(final, contents)`. Unchanged body, shifted by +14 lines from round-1 `:117-118` to `:131-132` | PASS |
| C7 | second bridge with the mutex taken exits 0 | same run. `Second_instance_exits_zero` `Aprovado` | `pc/Claudinho.Tests/BridgeTests.cs:46` - `Assert.True(run.Wait(TimeSpan.FromSeconds(1)))`; `:47` - `Assert.Equal(0, run.Result)` | PASS |
| C8 | accepts only VID `1A86`/`303A` replying `claudinho 1 ` within 3 s; rejects other VID, Bluetooth, wrong reply | same run. `Picks_port_by_vid_and_handshake` `Aprovado` | `pc/Claudinho.Tests/BridgeTests.cs:55` - `Assert.Equal(3000, Board.HandshakeTimeoutMs)`; `:67-68` - `Assert.Same(ports["COM3"], picked)`, `Assert.Equal(["COM3"], opened)`; `:75` - `Assert.Same(ports["COM9"], picked)` after a wrong reply | PASS |
| C9 | a changed snapshot is sent as `{"v":1,...}\n` on the next tick | same run. `Sends_line_when_snapshot_changes` `Aprovado` | `pc/Claudinho.Tests/BridgeTests.cs:89` - `Assert.Equal(["{\"v\":1,\"t\":1}\n", "{\"v\":1,\"t\":2}\n"], r.Port.Written)`. Tick cadence is 250 ms (`pc/Claudinho/Program.cs:29`) | PASS |
| C10 | resend at 60 s when unchanged, not before | same run. `Resends_every_60_seconds` `Aprovado` | `pc/Claudinho.Tests/BridgeTests.cs:98` - `Assert.Single(r.Port.Written)` at 59 s; `:101` - `Assert.Equal(2, r.Port.Written.Count)` at 60 s | PASS |
| C11 | after (re)connect the snapshot is sent within 3 s | same run. `Sends_snapshot_after_connect` `Aprovado` | `pc/Claudinho.Tests/BridgeTests.cs:116` - `Assert.Equal(["{\"v\":1,\"t\":1}\n"], r.Port.Written)`; `:115` - `Assert.Equal(2, r.Connects)` | PASS |
| C12 | write failure logs `porta perdida`, retries after 5 s, keeps running | same run. `Retries_after_port_loss` `Aprovado` | `pc/Claudinho.Tests/BridgeTests.cs:129` - `Assert.Contains(r.Log, l => l.Contains("porta perdida"))`; `:134` - 1 connect at +4.9 s; `:137` - 2 connects at +5 s | PASS |
| C13 | 30 min idle closes the port and exits 0; at 29 min it keeps running | same run. `Exits_after_30_minutes_idle` `Aprovado` | `pc/Claudinho.Tests/BridgeTests.cs:149` - `Assert.False(r.Bridge.Tick(T0.AddMinutes(30)))`; `:150` - `Assert.True(r.Port.Disposed)`; exit 0 comes from `pc/Claudinho/Bridge.cs:82-83` | PASS |
| C39 | `bridge.log` > 1 MB is truncated at start | same run. `Truncates_log_over_1mb` `Aprovado` | `pc/Claudinho.Tests/BridgeTests.cs:164` - `Assert.Equal(1_048_576, BridgeHost.MaxLog)`; `:165` - `Assert.Equal(0, new FileInfo(big).Length)` | PASS |
| C14 | v1 line -> phrase, `Opus · monitor`, `24%` `renova em 2h16`, `41%` `renova qui 20:11`, `contexto 37%`, `US$ 4,12` | `python -m platformio test -d firmware -e cyd -f test_view` on the CYD, exit 0: `test_main.cpp:125: test_full_line_view [PASSED]`, 6/6 | `firmware/test/test_view/test_main.cpp:35` - `("Opus · monitor", v.meta)`; `:36-37` - `"24%"`, `"renova em 2h16"`; `:38-39` - `"41%"`, `"renova qui 20:11"`; `:40-41` - `"contexto 37%"`, `"US$ 4,12"` | PASS |
| C15 | no colour outside the AC 15 palette in `firmware/src` | `python pc/tools/check_palette.py firmware/src`, exit 0: `3 arquivos, 11 cores distintas, 0 fora da paleta` | `pc/tools/check_palette.py:32` - `return 1 if bad else 0`. A manual grep of `firmware/src` for `lv_color_*`, `lv_palette`, and 4- or 8-digit hex found only `firmware/src/main.cpp:45` `fillScreen(0x0000)`, which is black before LVGL draws. Scanner limitation: gap 1 | PASS |
| C16 | no line since boot -> `Waiting`, pose `sleep`, `Esperando o Claude Code` | same `test_view` run: `:126: test_waiting_before_first_line [PASSED]` | `firmware/test/test_view/test_main.cpp:48` - `Screen::Waiting`; `:49` - `Pose::Sleep`; `:50` - `("Esperando o Claude Code", v.title)` | PASS |
| C17 | now = t + elapsed/1000; after 61 s `2h16` -> `2h15` | `python -m platformio test -d firmware -e cyd -f test_clock` on the CYD, exit 0: `:51: test_countdown_ticks_without_new_line [PASSED]`, 3/3 | `firmware/test/test_clock/test_main.cpp:21` - `TEST_ASSERT_EQUAL(1764630660 + 61, (int32_t)now_unix(st, 5000 + 61000))`; `:22` - `("renova em 2h15", ...)` | PASS |
| C18 | `resets_at` in the past -> `0%`, `renovada ✓` | same `test_view` run: `:127: test_renewed_window [PASSED]` | `firmware/test/test_view/test_main.cpp:61` - `("0%", v.w5.pct)`; `:62` - `("renovada ✓", v.w5.reset)` | PASS |
| C19 | after 181 s: `stale`, footer `há 3 min`; at 179 s it shows the time | same `test_view` run: `:128: test_stale_after_3_minutes [PASSED]` | `firmware/test/test_view/test_main.cpp:71` - `("20:11", fresh.fresh)`, the time of the last reading (AC 14, fix at `firmware/lib/core/claudinho_core.cpp:254`); `:74-75` - `TEST_ASSERT_TRUE(old.stale)`, `("há 3 min", old.fresh)`. 45% opacity: `firmware/src/ui.cpp:26` `kFade = 115`, read only | PASS |
| C20 | malformed, truncated or 513-byte line is dropped, prior state intact | `python -m platformio test -d firmware -e cyd -f test_protocol` on the CYD, exit 0 (second upload attempt; the first failed with "No serial data received"): `:78: test_rejects_bad_lines [PASSED]`, 3/3 | `firmware/test/test_protocol/test_main.cpp:22` - `TEST_ASSERT_EQUAL_MESSAGE((int)Parse::Bad, ...)`; `:23` - `TEST_ASSERT_EQUAL(42, (int)out.t)`; `:32` - `TEST_ASSERT_FALSE(got)` after 513 bytes | PASS |
| C21 | `v`=2 -> `Protocol`, `o PC mandou o protocolo v2`, `confused` | same `test_protocol` run: `:79: test_unknown_version [PASSED]` | `firmware/test/test_protocol/test_main.cpp:54` - `Screen::Protocol`; `:55` - `Pose::Confused`; `:56` - `("o PC mandou o protocolo v2", v.sub)` | PASS |
| C22 | no `h5`/`d7` -> `--`, `indisponível neste login`, `Cota indisponível`, ctx and cost kept | same `test_view` run: `:129: test_missing_quota [PASSED]` | `firmware/test/test_view/test_main.cpp:86` - `("Cota indisponível", v.title)`; `:87-90` - `"--"` and `"indisponível neste login"` on both windows; `:91-92` - `"contexto 37%"`, `"US$ 4,12"` | PASS |
| C23 | the real CYD answers `claudinho 1 cyd` to `?\n` within 3 s right after open | after re-flashing the app (`platformio run -t upload`, SUCCESS), `pwsh -NoProfile -File pc/tools/handshake.ps1`, exit 0: `OK: COM3 respondeu 'claudinho 1 cyd' em 14 ms` | `pc/tools/handshake.ps1:24` - `if ($line -eq "claudinho 1 $Board")` with `$Board = 'cyd'` at `:3`, inside the `-lt 3000` loop at `:21` | PASS |
| C24 | `install` copies the exe, the backup equals the original, writes `statusLine` with `<exe com /> statusline`, keeps other keys | same dotnet run. `Install_writes_statusline_and_backup` `Aprovado` | `pc/Claudinho.Tests/InstallTests.cs:52` - `Assert.Equal(Original, File.ReadAllText(Settings + ".claudinho.bak"))`; `:55` - `Assert.Equal("~/AppData/Local/Claudinho/claudinho.exe statusline", ...)`, which is the shape plan door 6 now records; `:56` - `JsonNode.DeepEquals(Original, WithoutStatusLine(s))` | PASS |
| C25 | foreign `statusLine` -> exit 1, message, file untouched; `--force` replaces | same run. `Refuses_foreign_statusline` `Aprovado` | `pc/Claudinho.Tests/InstallTests.cs:68` - `Assert.Equal(1, code)`; `:69` - `Assert.Contains("statusLine já configurado; use --force para substituir", text)`; `:70` - bytes unchanged; `:74` - forced | PASS |
| C26 | installing twice leaves the same values | same run. `Install_twice_is_idempotent` `Aprovado` | `pc/Claudinho.Tests/InstallTests.cs:85` - `Assert.True(JsonNode.DeepEquals(first, Read()))` | PASS |
| C27 | `uninstall` removes the Claudinho `statusLine` and the target folder, keeps other keys | same run. `Uninstall_removes_only_claudinho` `Aprovado` | `pc/Claudinho.Tests/InstallTests.cs:98` - `Assert.False(Directory.Exists(Target))`; `:100` - `Assert.Null(s["statusLine"])`; `:101` - `DeepEquals(Original, s)` | PASS |
| C28 | `dotnet publish` yields one self-contained `claudinho.exe` that runs `--version` without installed .NET | `pwsh -NoProfile -File pc/tools/check-publish.ps1`, exit 0: `OK: claudinho 0.1.0, 94.5 MB, sem .NET no PATH` | `pc/tools/check-publish.ps1:10` - exactly one file named `claudinho.exe`; `:15` - `if ($files[0].Length -lt 50MB) { ... exit 1 }`; `:21` - `$v -notmatch '^claudinho \d'` | PASS |
| C29 | on boot: background `#141413`, orange Clawd, landscape, not mirrored | manual, carried from a6f1692. The Verifier did not observe the board. The user's relayed confirmation of 2026-11-23, after `b1e6631`: "agora tá certo, fundo escuro e clawd laranja". The fix did not touch `firmware/src` | `firmware/src/main.cpp:44` - `gfx->invertDisplay(true)`; `firmware/src/main.cpp:12` - `new Arduino_ILI9341(bus, GFX_NOT_DEFINED, 1 /*paisagem*/)` | PASS (manual, user-attested, carried) |
| C30 | `Limite` > `Ritmo` > `Quase` > `Tranquilo`, one case per row plus a tie | `python -m platformio test -d firmware -e cyd -f test_status` on the CYD, exit 0: `:104: test_status_precedence [PASSED]`, 6/6 | `firmware/test/test_status/test_main.cpp:27-30` - one case per phrase; `:32` - `("Ritmo acelerado", view_for(86, 120).title)` (≥ 80% and projecting); `:34` - tie `("Limite atingido", view_for(100, 43, 95).title)` | PASS |
| C31 | 62% at 100 of 300 min -> `Ritmo acelerado`, `nesse ritmo, acaba em 1h01` | same `test_status` run: `:105: test_projection_text [PASSED]` | `firmware/test/test_status/test_main.cpp:40` - `("Ritmo acelerado", v.title)`; `:41` - `("nesse ritmo, acaba em 1h01", v.sub)` | PASS |
| C32 | 100% with reset in 43 min -> `volta em 43 min` | same `test_status` run: `:106: test_limit_text [PASSED]` | `firmware/test/test_status/test_main.cpp:46` - `("volta em 43 min", view_for(100, 43).sub)` | PASS |
| C33 | 5h pace at 100 min = 33.3%; bead dark if used ≥ pace, light if below; no pace at the limit | same `test_view` run: `:130: test_pace_marker [PASSED]` | `firmware/test/test_view/test_main.cpp:102` - `FLOAT_WITHIN(0.05f, 33.33f, v.w5.pace)`; `:104` - `FALSE(beadOnFill)` at 20%; `:108` - TRUE at 40%; `:112` - `showPace` FALSE at 100%; `:118-119` - pace 50 and `TRUE(eq.w5.beadOnFill)` at used == pace | PASS |
| C34 | at risk with ≥ 80% or a projection before reset; not at 79% on pace | same `test_status` run: `:107: test_risk_color [PASSED]` | `firmware/test/test_status/test_main.cpp:52` - `TEST_ASSERT_TRUE(view_for(80, 25).w5.risky)`; `:53` - projection true; `:54` - `TEST_ASSERT_FALSE(view_for(79, 10).w5.risky)` | PASS |
| C35 | pose per state, including `happy` for 4.5 s | same `test_status` run: `:108: test_pose_table [PASSED]` | `firmware/test/test_status/test_main.cpp:60-63` - Idle/Alarm/Alarm/Squint; `:66` - Sleep; `:69` - Confused; `:81-82` - `Pose::Happy`, `"Cota renovada!"`; `:84` - still happy at +4.4 s; `:86` - `Pose::Idle` at +4.5 s | PASS |
| C36 | `idle` animates only the blink | same `test_status` run: `:109: test_idle_animates_only_blink [PASSED]` | `firmware/test/test_status/test_main.cpp:92` - `TEST_ASSERT_TRUE(a.blink)`; `:93-98` - bob, legs, sweat, zzz, sparkles and pulse all FALSE | PASS |
| C37 | `resets_at` 1764890460, `tz` −180 -> `renova qui 20:21` | same `test_clock` run: `:52: test_weekday_local_time [PASSED]` | `firmware/test/test_clock/test_main.cpp:29` - `TEST_ASSERT_EQUAL_STRING("qui 20:21", out)`; the `renova ` prefix comes from `firmware/lib/core/claudinho_core.cpp:172` | PASS |

## Round-1 gaps - verified at 6f5908a

| # | Round-1 gap | Status at 6f5908a |
| --- | --- | --- |
| 1 | 16 on-device proofs not run | closed - all four suites ran on the CYD, 18/18 `[PASSED]` |
| 2 | C30 did not separate Ritmo from Quase | closed - `firmware/test/test_status/test_main.cpp:32` |
| 3 | footer showed the current time | closed - `firmware/lib/core/claudinho_core.cpp:254`, `firmware/test/test_view/test_main.cpp:71` |
| 4 | the `~/...` command was not recorded as a door | closed - plan door 6. The `~` expansion is still unproven (new gap 2) |
| 5 | C6 did not test the mutex | closed - `pc/Claudinho.Tests/StatuslineTests.cs:110-121` |
| 6 | C4 compared against the production constant | closed - `pc/Claudinho.Tests/StatuslineTests.cs:84` |
| 7 | C23 accepted any board suffix | closed - `pc/tools/handshake.ps1:24` |
| 8 | C28 did not assert self-containment | closed - `pc/tools/check-publish.ps1:15` |
| 9 | C15 scanner matches only 6-digit hex | open, low - not touched by the fix (gap 1 below) |
| 10 | unsampled boundaries | 80% (`test_status:52`) and used == pace (`test_view:118-119`) closed. C31's two projecting windows is still open, low (gap 3 below) |

## Swept - carried from a6f1692

Every `Swept` row in `checks.md` resolves to check IDs. None cites an existing constraint. `authorization: n/a` is approved policy. The fix did not change the Swept section.

## Coverage, Test policy, Faults - carried from a6f1692

Not run under `light`. No coverage recompute, no `Test policy` section in `checks.md`, no fault injection.

## Ranked gaps

None of these fails a check claim.

1. The C15 scanner matches only `0xRRGGBB` and `#rrggbb` literals (`pc/tools/check_palette.py:14`). RGB565, 8-digit literals and LVGL colour helpers would slip past it. Today the only such literal is `firmware/src/main.cpp:45` `fillScreen(0x0000)`, found by manual grep. Carried from round 1, low.
2. Plan door 6 says Claude Code expands `~` in the `statusLine` command in both shells. No check or proof shows this. C24 asserts the literal string it writes (`pc/Claudinho.Tests/InstallTests.cs:55`), not that Claude Code can run it. New with the fix, low. A one-time manual run of `install` followed by an open Claude Code session would settle it.
3. C31's "the window that runs out first" case, where both windows are projecting, is not sampled. `firmware/lib/core/claudinho_core.cpp:217` uses `fmin` but nothing asserts it. The C31 claim as written in `checks.md` does not name this case. Low.

## Gate

- `dotnet test pc/Claudinho.Tests --logger "console;verbosity=detailed" --filter "FullyQualifiedName~StatuslineTests|FullyQualifiedName~BridgeTests|FullyQualifiedName~InstallTests"` - 24 passed, 0 failed. Each of the 18 named tests is listed individually as `Aprovado`.
- Scripts: `check-publish.ps1` exit 0 (94.5 MB), `measure-statusline.ps1` exit 0 (median 46 ms), `check_palette.py` exit 0, `handshake.ps1` exit 0 (`claudinho 1 cyd` in 14 ms).
- Firmware on the CYD (`python -m platformio test -d firmware -e cyd -f <suite>`): `test_protocol` 3/3, `test_clock` 3/3, `test_view` 6/6, `test_status` 6/6, so 18 passed, 0 failed. The app firmware was then re-flashed (`platformio run -t upload`, SUCCESS) before C23.
- `git status --porcelain` was empty after the proofs.
