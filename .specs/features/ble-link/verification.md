# Link BLE verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 69eadea..3454562
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

FAIL. The code holds up as far as the proofs that could run reach it. All 8 named .NET proofs pass at `3454562`, and the full suite passes (31/31). The firmware compiles. But 10 of the 15 checks have at least one proof that did not run at HEAD. Seven need the CYD in boot mode for `pio test` or on the charger for `ble-check.ps1`, and the rest need a person. Under the rule that a proof that did not run is not a PASS, only C8, C9, C10, C13 and C15 are PASS. Nothing ran here contradicts any check. The verdict is FAIL because the hardware and manual proofs have not run at HEAD. No check failed its claim.

The user reported that `ble-check.ps1` gave C1/C3/C14 OK (`conectado: ble 20:50:0D:11:D3:62` in 2.3 s) after `96e9630` and that C6 then took about 20 s. That run used an earlier version of the script, before `3454562` raised the C6 bound from 10 s to 30 s. The verifier did not observe it. It is recorded below as user-reported data and does not turn any row into PASS.

## Binding sources

Step 1 does not run under `light`. The sources were still opened for context: `.specs/STATE.md` AD-002 (superseded) and AD-004 (active), plus `.specs/features/claudinho/plan.md` door 3 (`:74`) and the "Link BLE" row of Out of scope (`:181`). AD-004 records the NUS UUIDs, the NimBLE stack and that `p` is dropped over the air. These match the plan's doors 1-3 and `Ble.cs:19-21`. Claudinho door 3 described a single write characteristic. This plan's door 1 replaces that explicitly and gives the reason. No contradiction was found. No table is given because the step did not run.

## Checks - verified at 3454562

PC proofs: one invocation, `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~BleTests|FullyQualifiedName~BridgeTests.Logs_link_name_on_connect|FullyQualifiedName~BridgeTests.Retries_after_port_loss" --logger "console;verbosity=detailed"`, exit 0, 8/8. Each of these appears as `Aprovado`: `BleTests.Connect_tries_usb_before_ble`, `Ble_candidate_needs_handshake`, `Air_line_drops_only_project`, `Chunks_cover_line_in_order`, `Unavailable_bluetooth_logs_once`, `Ble_failure_reason_logged_when_it_changes`, `BridgeTests.Logs_link_name_on_connect`, `BridgeTests.Retries_after_port_loss`.

Firmware proofs: `pio test -d firmware -e cyd -f test_protocol` was not run, because the CYD is not on USB and boot mode needs a person. The named Unity cases exist and are registered: `RUN_TEST(test_rejects_bad_lines)` `firmware/test/test_protocol/test_main.cpp:130`, `RUN_TEST(test_chunked_line)` `:133`, `RUN_TEST(test_interleaved_origins)` `:134`. Compile check `python -m platformio run -d firmware -e cyd`: SUCCESS, Flash 1,015,096 / 3,145,728 B, RAM 71,716 B static.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | CYD advertises NUS; bridge logs `conectado: ble ` within 20 s; nRF shows `Claudinho` | `ble-check.ps1` not run (board off USB, needs a person to power it). Manual nRF not done. User-reported: OK in 2.3 s with an earlier version of the script | `pc/tools/ble-check.ps1:39` - `$t = Wait-Connected 1 20`; `:27` - pattern `'conectado: ble ([0-9A-F]{2}:){5}[0-9A-F]{2}$'`. Firmware: `firmware/src/main.cpp:38` `createService("6E400001-...")`, `:43` `addServiceUUID`, `:45` `setName("Claudinho")` (read only) | NOT RUN - hardware |
| C2 | line in 1/20/182-byte chunks leaves the reader intact and parses `Ok`; screen changes within 1 s over the air | `pio test ... -f test_protocol` not run (hardware). Manual not done | `firmware/test/test_protocol/test_main.cpp:81` - `sizes[] = {1, 20, 182}`; `:90` - `TEST_ASSERT_EQUAL(1, lines)`; `:91` - `TEST_ASSERT_EQUAL_STRING(kFull, r.line())`; `:94` - `TEST_ASSERT_EQUAL((int)Parse::Ok, ...)` | NOT RUN - hardware |
| C3 | real CYD notifies `claudinho 1 cyd` on TX for `?\n` on RX, nothing on serial | `ble-check.ps1` not run. Manual not done. User-reported: OK (handshake implied by the `conectado: ble` line) | `pc/tools/ble-check.ps1:27` (only matches after the bridge's handshake at `pc/Claudinho/Bridge.cs:142` `line.StartsWith("claudinho 1 ", ...)`). Firmware: `firmware/src/main.cpp:60-61` - `if (fromBle) bleTx->notify(...) else Serial.print(kHello)` (read only). The "nothing on serial" half has only the manual proof | NOT RUN - hardware |
| C4 | 513 bytes in chunks without `\n` dropped whole; next good line passes | `pio test ... -f test_protocol` not run (hardware) | `firmware/test/test_protocol/test_main.cpp:104` - `TEST_ASSERT_FALSE(got)` after 513 `x` in 20-byte chunks plus `\n`; `:106-107` - `TEST_ASSERT_TRUE(got)`, `TEST_ASSERT_EQUAL_STRING("{\"v\":1,\"t\":7}", r.line())` | NOT RUN - hardware |
| C5 | two readers fed interleaved byte by byte return both lines intact | `pio test ... -f test_protocol` not run (hardware) | `firmware/test/test_protocol/test_main.cpp:123` - `TEST_ASSERT_EQUAL_STRING("{\"v\":1,\"t\":111,\"m\":\"usb\"}", ra.line())`; `:124` - same for `rb` (`ble-link`). Production wiring: `firmware/src/main.cpp:20` `cl::LineReader reader, bleReader;`, `:92` / `:96` | NOT RUN - hardware |
| C6 | new bridge after a killed BLE bridge logs `conectado: ble ` within 30 s; nRF sees the advert again within 2 s | `ble-check.ps1` not run at HEAD. The 30 s bound (`3454562`) was never run. User-reported about 20 s with the earlier script. Manual not done | `pc/tools/ble-check.ps1:45` - `$t = Wait-Connected 2 30`; `:46` fails otherwise. Firmware: `firmware/src/main.cpp:37` `server->advertiseOnDisconnect(true)` (read only). The 2 s half has only the manual proof | NOT RUN - hardware |
| C7 | over USB nothing changes: `?\n` -> `claudinho 1 cyd` on serial within 3 s; serial rejection proofs stay green | `handshake.ps1` not run (board off USB). `pio test ... -f test_protocol` not run | `pc/tools/handshake.ps1:24` - `if ($line -eq "claudinho 1 $Board")` inside `-lt 3000` at `:21`; `firmware/test/test_protocol/test_main.cpp:22` - `TEST_ASSERT_EQUAL_MESSAGE((int)Parse::Bad, ...)`, `:23` - `TEST_ASSERT_EQUAL(42, (int)out.t)`. USB path: `firmware/src/main.cpp:92` `handle(reader.line(), false)`, `:61` `Serial.print(kHello)` | NOT RUN - hardware |
| C8 | BLE searched only when USB returns nothing; scan window 5 s | dotnet run above, `Connect_tries_usb_before_ble` `Aprovado` | `pc/Claudinho.Tests/BleTests.cs:11` - `Assert.Equal(TimeSpan.FromSeconds(5), Ble.ScanWindow)`; `:18` - `Assert.Equal(["usb"], calls)` when USB finds the board; `:22` - `Assert.Equal(["usb", "ble"], calls)` when USB returns null. Production: `pc/Claudinho/Program.cs:29` `Board.Connect(Board.Usb, ble.Find)`; `pc/Claudinho/Ble.cs:62` uses `ScanWindow` | PASS |
| C9 | a BLE candidate with a wrong reply is closed; the next one replying `claudinho 1 cyd` is used | same run, `Ble_candidate_needs_handshake` `Aprovado` | `pc/Claudinho.Tests/BleTests.cs:33` - `Assert.Same(board, finder.Find())`; `:34` - `Assert.True(other.Disposed)`; `:36` - `Assert.Contains("?\n", board.Written)`. 3 s default: `pc/Claudinho/Ble.cs:92` `timeoutMs = Board.HandshakeTimeoutMs` (3000, `Bridge.cs:110`) | PASS |
| C10 | air line = snapshot without `p`, the other 9 keys and values equal (accents in `m`), `?\n` untouched | same run, `Air_line_drops_only_project` `Aprovado` | `pc/Claudinho.Tests/BleTests.cs:51` - `Assert.False(sent.ContainsKey("p"))`; `:52` - `Assert.Equal(["v", "t", "tz", "h5", "d7", "ctx", "tok", "usd", "m"], ...)`; `:53` - `JsonNode.DeepEquals(full[key], value)` per key; `:54` - `Assert.Contains("Opus ção", air)`; `:56` - `Assert.Equal("?\n", Ble.ForAir("?\n"))` | PASS |
| C11 | chunks joined = line bytes in order, none larger than requested; real screen changes within 2 s | same run, `Chunks_cover_line_in_order` `Aprovado`. Manual not done | `pc/Claudinho.Tests/BleTests.cs:66` - `Assert.InRange(c.Length, 1, size)`; `:67` - `Assert.Equal(data, chunks.SelectMany(c => c).ToArray())` for sizes 20/182/244/1000. The 2 s on-screen half has only the manual proof | NOT RUN - manual |
| C12 | board off -> `porta perdida`, bridge keeps running; board back -> `conectado: ble ` again | same run, `Retries_after_port_loss` `Aprovado` (pre-existing test, transport-agnostic). Manual not done | `pc/Claudinho.Tests/BridgeTests.cs:129-130` - `Assert.True(r.Bridge.Tick(...))`, `Assert.Contains(r.Log, l => l.Contains("porta perdida"))`; `:135` - 1 connect at +5.9 s; `:138` - 2 at +6 s. BLE drop -> `IOException`: `pc/Claudinho/Ble.cs:185`, `:190` (read only, no automated proof) | NOT RUN - manual |
| C13 | no Bluetooth: three searches return null, open nothing, log `bluetooth indisponível` once | same run, `Unavailable_bluetooth_logs_once` `Aprovado` | `pc/Claudinho.Tests/BleTests.cs:78` - `Assert.Null(finder.Find())` x3; `:80` - `Assert.Equal(0, scans)`; `:81` - `Assert.Equal(["bluetooth indisponível"], log)` | PASS |
| C14 | on connect, logs `conectado: usb COMn` / `conectado: ble XX:XX:XX:XX:XX:XX` | same run, `Logs_link_name_on_connect` `Aprovado`. `ble-check.ps1` not run (user-reported OK) | `pc/Claudinho.Tests/BridgeTests.cs:150` - `Assert.Equal(["conectado: usb COM3"], r.Log)` (name injected by the fake); `pc/tools/ble-check.ps1:27` - the `ble` format regex. Production names `pc/Claudinho/Bridge.cs:190` and `pc/Claudinho/Ble.cs:134` are not exercised by any test that ran | NOT RUN - hardware |
| C15 | a failed BLE open logs `ble falhou: 0x<HResult> <msg>`, the same reason in a row only once, logged again after a connect | same run, `Ble_failure_reason_logged_when_it_changes` `Aprovado` | `pc/Claudinho.Tests/BleTests.cs:99` - `Assert.Equal(["ble falhou: 0x80070016 A", "ble falhou: 0x80070079 B", "ble falhou: 0x80070079 B"], log)` over reasons A, A, B, connect, B (`:88`). Format producer `pc/Claudinho/Ble.cs:163` is not exercised (gap 5) | PASS |

## Level and sampling

- C14: the unit proof injects `Name = "usb COM3"` into a fake. It proves the bridge logs `conectado: {Name}` once (`Bridge.cs:45`). It does not prove the strings that production builds. The BLE address formatting (`Ble.cs:134`, byte reversal of `BluetoothAddress`) only reaches a proof through `ble-check.ps1`.
- C12: the only automated proof is a claudinho test the feature did not touch. It proves the shared `Bridge` retry. The BLE-specific part, a dropped GATT link becoming an `IOException` (`Ble.cs:185`, `:190`), has no automated proof. The code's own `shortcut:` comment (`Ble.cs:182`) says a drop is only noticed on the next write, which can take up to 60 s.
- C3 and C6: each claim has two parts, and `ble-check.ps1` proves only one of them. "Nothing on serial" (C3) and "advert back in 2 s" (C6) are covered only by the manual proofs.
- C9: "subscribe to TX" (`Ble.cs:153`) is not reached by the unit test. The fake answers without a subscription. Only the hardware script reaches it.
- C5: the test feeds two independent `LineReader` objects, so it cannot fail unless `LineReader` holds shared state. The AC 5 guarantee actually lives in the wiring at `main.cpp:20`, `:92` and `:96`, which was confirmed by reading.
- C2 and C4 drive `cl::LineReader` directly. The `onWrite` -> `xStreamBuffer` -> `loop()` path (`main.cpp:29`, `:95`) is reached only by the manual proof.

## Swept existing

Every `Swept` row resolves to check IDs or `n/a`. The concurrency row also cites a code property: "the bytes pass from the NimBLE task to `loop()` through a buffer, without the callback touching LVGL". It holds. `firmware/src/main.cpp:27-30` `onWrite` only calls `xStreamBufferSend`, and `handle()` is called only from `loop()` (`:92`, `:96`).

## Coverage, Test policy, Faults

Not run under `light`. No coverage recompute, `checks.md` has no `Test policy` section, and no faults were injected.

## Ranked gaps

1. Ten checks (C1-C7, C11, C12, C14) have proofs that did not run at `3454562`. These are `pio test -f test_protocol` (C2, C4, C5, C7), `ble-check.ps1` (C1, C3, C6, C14), `handshake.ps1` (C7), and the nRF/screen manual proofs (C1, C2, C3, C6, C11, C12). C6's renegotiated 30 s bound has never been run. The user-reported `ble-check` run predates `3454562`. To close this, flash, run the three scripts and `pio test`, and do the manual nRF and screen checks.
2. Over BLE, a snapshot that is not valid JSON crashes the bridge, while over USB it does not. `Ble.ForAir` calls `JsonNode.Parse` (`pc/Claudinho/Ble.cs:30`) inside `BleLink.Write`. The `JsonException` is caught neither there (`:200`) nor by `Bridge.Tick` (`pc/Claudinho/Bridge.cs:57`), and `Snapshot.Read` returns raw text (`pc/Claudinho/Statusline.cs:66`). The statusline writes atomically, so this needs a hand-edited or foreign file. C11's manual step tells the user to edit `snapshot.json` by hand. No check covers it. It bears on AC 12 ("continue running").
3. C14's BLE name format (`Ble.cs:134`) and USB name (`Bridge.cs:190`) have no proof that ran at HEAD. The unit test injects the name.
4. A BLE link drop has no automated proof (`Ble.cs:185`, `:190`). It is detected only on the next write, so after the board restarts the screen can wait up to about 65 s (shortcut at `Ble.cs:182`). AC 12 sets no time bound, so this is a precision gap in C12 and not a failure.
5. C15's format `0x<HResult:X8> <message>` (`Ble.cs:163`) is not exercised. The test passes pre-formatted reasons.
6. The `Uncached` -> `Cached` GATT fallback (`Ble.cs:78-82`, commit `96e9630`) has no check. It is supported only by the user's report, and checks.md Handoff records the stale-cache risk as untested.
7. Low: the overflow path of `xStreamBufferSend(..., 0)` (`main.cpp:29`) can drop part of a write. The comment assumes the result is always invalid and gets discarded. Nothing exercises this.

## Gate

- `dotnet test pc/Claudinho.Tests` - 31 passed, 0 failed (exit 0).
- Batched named proofs (filter above) - 8 passed, 0 failed, each listed as `Aprovado`.
- `python -m platformio run -d firmware -e cyd` - SUCCESS (compile only, no flash).
- Not run: `pio test -f test_protocol`, `pc/tools/ble-check.ps1`, `pc/tools/handshake.ps1`, all manual proofs.
- `git status --porcelain` after the runs shows `?? pc/tools/heap-watch.ps1`. The verifier did not create this file, and it is outside the diff range. It was left untouched.
