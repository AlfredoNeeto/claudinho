# Hook Stop verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: c1dda59..26d177c (round 1 covered 3da360a..c1dda59)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

FAIL, and only because the manual proof C14 still has not run. It is recorded as not run, pending the user. It is not a PASS. The other 16 checks (C1-C13, C15, C16, C17) pass at `26d177c`. Every automated proof was re-run in full at that commit, and each named test is listed individually as `Aprovado`. Round 1's findings 2 (C15 precision), 3 (AC 7 "não pode ser lido" uncovered) and 5 (STATE.md CRLF) are closed. Finding 4 (whole transcript read on every Stop) is closed as a recorded decision, not a code change. Two new findings are low. Neither one contradicts a check (see Ranked gaps).

## Scope of this round

- Fix commit `26d177c` touches four files. `pc/Claudinho.Tests/HookTests.cs` gains the new test `Unreadable_transcript_keeps_snapshot` (C17), and C15's assertion goes from `"hook"` to `"| hook |"`. `.specs/STATE.md` has its line endings restored to LF, with no content change. `.specs/features/work-hook/checks.md` gets C17, the AC 7 coverage row now at 6 members, C17 under `dependency failure`, the header count 17, and a `Settled mid-build` line under `## Handoff`. The commit also adds the round 1 `verification.md`.
- No production code changed. `git diff c1dda59..26d177c -- pc/Claudinho` is empty. That means no faults need re-injecting (under `light` none run anyway), and no citation into production code moved.
- Outside its stated purpose: nothing. All four files match the brief. The only extra is committing the round 1 report, which is the expected artifact.
- Re-verified here: C15, C17, C16, plus all proofs re-run at HEAD and the refreshed citations for `HookTests.cs` (C6, C7, C8 moved by +13 lines). C14 is carried as not run.
- Carried from `c1dda59`: the evidence for C1-C5 and C9-C13. Their files and line numbers did not change (`InstallTests.cs` is not in the diff, and `HookTests.cs` lines before 129 are untouched). Also carried: Level and sampling, and Swept existing. Their proofs were still re-run at `26d177c`.

## Binding sources

Step 1 does not run under `light`, and the plan marks no source as binding. Carried from c1dda59.

## Checks - proofs verified at 26d177c

All PC proofs ran in one invocation: `dotnet test pc/Claudinho.Tests --filter "FullyQualifiedName~HookTests.Writes_snapshot_from_last_main_assistant|FullyQualifiedName~HookTests.Context_percentage|FullyQualifiedName~HookTests.Model_display_name|FullyQualifiedName~HookTests.Starts_bridge_only_when_not_running|FullyQualifiedName~HookTests.Bad_input_keeps_snapshot|FullyQualifiedName~HookTests.Skips_bad_lines|FullyQualifiedName~HookTests.Reads_transcript_open_for_writing|FullyQualifiedName~HookTests.Exe_hook_is_silent|FullyQualifiedName~InstallTests.Install_hook_adds_stop_entry|FullyQualifiedName~InstallTests.Install_hook_twice_is_idempotent|FullyQualifiedName~InstallTests.Modes_replace_each_other|FullyQualifiedName~InstallTests.Uninstall_removes_hook_entry|FullyQualifiedName~InstallTests.Never_touches_managed_settings|FullyQualifiedName~HookTests.Exe_usage_lists_hook|FullyQualifiedName~HookTests.Unreadable_transcript_keeps_snapshot" --logger "console;verbosity=detailed"`. It exited 0 with 25/25 passing: 15 named tests, of which three are theories with 4, 4 and 5 cases. Each test and each theory case is listed individually as `Aprovado`, including `HookTests.Unreadable_transcript_keeps_snapshot [11 ms]` and `HookTests.Exe_usage_lists_hook [37 ms]`. C16 ran separately with the exact `pwsh` command from checks.md and exited 0.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | last main `assistant` entry; keys exactly `v,t,tz,ctx,m,p`; ctx 55.1, m `Opus 4.8`, p `monitor` | batch, `Aprovado` at 26d177c | carried from c1dda59 (lines unchanged): `pc/Claudinho.Tests/HookTests.cs:54` - `Assert.Equal(["v", "t", "tz", "ctx", "m", "p"], ...)`; `:58-61` | PASS |
| C2 | ctx for 110203 / 200000 / 250000 / 1000 | batch, 4 cases `Aprovado` | carried: `pc/Claudinho.Tests/HookTests.cs:73` - `Assert.Equal(expected, ReadSnap()["ctx"]!.GetValue<double>())` over `:65-68` | PASS |
| C3 | `m` for four model ids | batch, 4 cases `Aprovado` | carried: `pc/Claudinho.Tests/HookTests.cs:85` - `Assert.Equal(expected, ReadSnap()["m"]!.GetValue<string>())` over `:77-80` | PASS |
| C4 | bridge started once when not running; `Win32Exception` still returns 0 | batch, `Aprovado` | carried: `pc/Claudinho.Tests/HookTests.cs:95`, `:98`, `:101-102` | PASS |
| C5 | five bad inputs -> 0, snapshot unchanged, bridge not started | batch, 5 cases `Aprovado` | carried: `pc/Claudinho.Tests/HookTests.cs:124` - `Assert.Equal(0, Run(stdin, ...))`; `:125` - `Assert.Equal("anterior", File.ReadAllText(Snap))`; `:126` - `Assert.Equal(0, started)` | PASS |
| C6 | bad middle line and truncated last line skipped | batch, `Aprovado` | refreshed: `pc/Claudinho.Tests/HookTests.cs:146` (fixture); `:150` - `Assert.Equal("Sonnet 4.5", s["m"]!.GetValue<string>())`; `:151` - `Assert.Equal(10, s["ctx"]!.GetValue<double>())` | PASS |
| C7 | reads while another handle writes with `FileShare.ReadWrite` | batch, `Aprovado` | refreshed: `pc/Claudinho.Tests/HookTests.cs:158` - writer held open; `:161` - `Assert.Equal(55.1, ReadSnap()["ctx"]!.GetValue<double>())` | PASS |
| C8 | real exe `hook`: exit 0, empty stdout, snapshot in `CLAUDINHO_HOME`; same with `lixo` | batch, `Aprovado` | refreshed: `pc/Claudinho.Tests/HookTests.cs:187` - `Assert.Equal(0, code)`; `:188` - `Assert.Equal("", stdout)`; `:189` - `Assert.Equal("Opus 4.8", ReadSnap()["m"]!.GetValue<string>())`; `:192-193` for `lixo`; mutex `:167`, `CLAUDINHO_HOME` `:172` | PASS |
| C9 | `install --hook` adds the door-1 entry and keeps foreign entries | batch, `Aprovado` | carried: `pc/Claudinho.Tests/InstallTests.cs:63-70` | PASS |
| C10 | `install --hook` twice is idempotent | batch, `Aprovado` | carried: `pc/Claudinho.Tests/InstallTests.cs:84-85` | PASS |
| C11 | modes replace each other | batch, `Aprovado` | carried: `pc/Claudinho.Tests/InstallTests.cs:96`, `:100-101`, `:109-110` | PASS |
| C12 | `uninstall` removes our hook entry | batch, `Aprovado` | carried: `pc/Claudinho.Tests/InstallTests.cs:119`, `:124-127` | PASS |
| C13 | managed and remote settings byte-identical | batch, `Aprovado` | carried: `pc/Claudinho.Tests/InstallTests.cs:152-153`, `:148`, `:154-155` | PASS |
| C14 | on the work PC, after `install --hook` and one reply, the snapshot and the board update | not run by the verifier: manual, on the user's work PC. Pending the user | No automated proof exists. Production command shape: `pc/Claudinho/Installer.cs:38`, `:44` (read only, unchanged since c1dda59) | NOT RUN - manual, pending user |
| C15 | no argument -> exit 2, usage contains `hook` and `install [--hook] [--force]` | batch, `Exe_usage_lists_hook` `Aprovado` at 26d177c | `pc/Claudinho.Tests/HookTests.cs:200` - `Assert.Equal(2, code)`; `:201` - `Assert.Contains("| hook |", stdout)`; `:202` - `Assert.Contains("install [--hook] [--force]", stdout)`. `:201` no longer matches the text `install [--hook]`. It needs the delimited verb, which is printed at `pc/Claudinho/Program.cs:49`. Round 1 gap 2 is closed in the test (see new gap 2 about the check text) | PASS |
| C16 | `STATE.md` has AD-005 `active` and AD-001 `superseded by AD-005` | `pwsh -NoProfile -Command "if ((Select-String .specs/STATE.md -Pattern '^\| AD-005 .*\| active \|$') -and (Select-String .specs/STATE.md -Pattern '^\| AD-001 .*\| superseded by AD-005 \|$')) { exit 0 } else { exit 1 }"` exit 0 at 26d177c | `.specs/STATE.md:11` - `\| AD-005 \| Os dados vêm do stdin do statusLine ou do stdin do hook Stop ... \| active \|`; `.specs/STATE.md:7` - `\| AD-001 \| ... \| superseded by AD-005 \|`. The file is LF again (`git ls-files --eol`: `i/lf w/lf`, 0 CR bytes). `git diff 3da360a..26d177c -- .specs/STATE.md` is exactly 2 insertions and 1 deletion | PASS |
| C17 | transcript exists but is locked by another handle (`FileShare.None`) -> hook returns 0, `snapshot.json` unchanged, bridge not started | batch, `Unreadable_transcript_keeps_snapshot` `Aprovado` at 26d177c | `pc/Claudinho.Tests/HookTests.cs:133` - a valid fixture (`Assistant()`), so only the lock can make the read fail; `:135` - `new FileStream(Transcript, FileMode.Open, FileAccess.ReadWrite, FileShare.None)` held open; `:137` - `Assert.Equal(0, Run(Event(), () => false, () => started++))`; `:138` - `Assert.Equal("anterior", File.ReadAllText(Snap))`; `:139` - `Assert.Equal(0, started)`. Path in production code: `File.Exists` passes, the `FileStream` open at `pc/Claudinho/Hook.cs:35` throws `IOException` (sharing violation), and the catch at `:20` returns 0 before `Snapshot.Write`. Without that catch the exception would escape and `:137` would fail. Each assertion targets the value the check defines | PASS |

## Level and sampling

Carried from c1dda59: AC 6 non-inheritance by reading, AC 15 "não ler" by reading, C10 `DeepEquals`, C13 AppData filter. New this round:
- C17 covers AC 7's "não pode ser lido" through a sharing violation (`IOException`). The other unreadable form, an ACL denial (`UnauthorizedAccessException`), is caught by the same clause at `pc/Claudinho/Hook.cs:20`, confirmed by reading, and has no proof of its own. One member of the same catch is enough under `light`.
- C17 checks AC 10's exit code in process. It does not check stdout for this case. `Hook.Run` takes no writer and writes nothing (`pc/Claudinho/Hook.cs:12-24`), and C8 covers silence at the exe level. The C17 claim does not name stdout.
- AC 16 also says "ou com um verbo desconhecido". C15 exercises only the no-argument case. By reading, an unknown verb lands in the same `default:` branch (`pc/Claudinho/Program.cs:48-50`), so this is not a gap in behaviour.

## Swept existing

Carried from c1dda59: concurrency and observability rows held, and `pc/Claudinho` is unchanged since then. checks.md now adds `C17 (transcript travado)` under `dependency failure`. That row resolves to a check ID, and the check is proven above.

## Coverage, Test policy, Faults

Not run under `light`. checks.md has no `Test policy` section, and no faults were injected. One observation from reading: the AC 7 row now lists 6 members, and the new member "arquivo que não pode ser lido" has a proof, C17.

## Round 1 findings - disposition

1. C14 not run: still open. Pending the user (gap 1 below).
2. C15 precision: closed in the test (`HookTests.cs:201`). The check text was not updated (new gap 2).
3. AC 7 unreadable file: closed by C17.
4. Whole transcript read on every `Stop`: kept as a recorded decision in checks.md `## Handoff` (`.specs/features/work-hook/checks.md:108`). The decision cites ~190 ms total for a 32 MB transcript, of which ~80 ms is exe start-up. The verifier did not reproduce that timing. Running `claudinho.exe hook` outside the test harness would start a real bridge. Nothing contradicts the number, and no AC sets a time bound.
5. STATE.md CRLF: closed. The file is LF again, and the net diff since the feature base is 2+/1-.

## Ranked gaps

1. C14 has not run. It is manual, on the user's work PC, and pending the user. It is the only proof that Claude Code runs a user `Stop` hook under the company's managed settings and expands `~/...` in the hook command. To close it, run `claudinho.exe install --hook` on the work PC, send one message, then check `%LOCALAPPDATA%\Claudinho\snapshot.json` and the board (model shown, `Cota indisponível`).
2. Low, precision in the check text. The C15 claim (`.specs/features/work-hook/checks.md:62`) still reads "contém `hook`". The `Settled mid-build` line (`:108`) says C15 "passou a exigir `| hook |`", but the check itself was not reworded. The test is now stronger than its check, so a future editor reading only the claim could weaken it again. This does not fail C15.
3. Low, placement. The `Settled mid-build` bullet sits under `## Handoff` after the budget list (`.specs/features/work-hook/checks.md:108`). That matches the brief ("records in checks.md `## Handoff`"), so this is noted only.

## Gate

- `dotnet test pc/Claudinho.Tests`: 57 passed, 0 failed, exit 0 (56 in round 1, plus C17).
- Batched named proofs (filter above): 25 passed, 0 failed. Every test and theory case is listed individually as `Aprovado`.
- C16 `pwsh` command: exit 0.
- Not run: C14 (manual, user's work PC).
- `git status --porcelain` was empty before the runs. After the runs it shows only this report.
