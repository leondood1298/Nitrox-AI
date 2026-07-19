# Scanner Room Multiplayer Acceptance

Status values: `PASS`, `FAIL`, `BLOCKED`, `NOT RUN`. Record failures as `test ID - client - timestamp - save/log path`.

## Setup

- Build/commit: see the final package `BUILD_INFO.json`; one immutable Windows x64 Release build is used by the server and every client.
- Game version: Subnautica build 83031.
- Server/save: automated boot, save-load, and protocol probes passed. An exploratory run used the pre-existing `gottem` save; the dedicated Scanner Room matrix save is `NOT CREATED`.
- Clients: host `leon` and LAN client `leonlaptop` joined the exploratory run. A/B/C remain `NOT LAUNCHED` under the formal matrix procedure.

## Automated qualification (2026-07-19 follow-up)

- Full Release suite: `521 passed / 8 skipped / 0 failed` (`529 total`). The eight skips are four pre-existing platform-specific filesystem/root-permission tests in each of the Unix and macOS fixtures, skipped on Windows.
- Focused Scanner Room and camera selection: `131 passed / 0 failed`; the deterministic save/dock lock-inversion regression also passed ten consecutive runs.
- Persistence qualification passed real JSON and ProtoBuf round-trips plus late-join/orphan recovery. The broader persistence/scenario selection passed `20/20`.
- Authority-transition atomicity passed `22/22`; its three deterministic transfer-race tests passed five consecutive runs (`15/15`).
- The deterministic network-impairment proxy passed all six self-tests: command validation, repeatable scheduling, jitter bounds, reorder/expiry, bounded queue, and localhost bidirectional echo.
- Release launcher/client/patcher targets, including `net472`, and the Windows x64 launcher build succeeded. The launcher remained alive with an isolated local `--data-path` and created its log without an option-parser error.
- Process probe: legacy protocol `nitrox` was rejected and `nitrox-ai/1` connected; the server port was released after exact-process termination.
- Save-load probe: a clean JSON world was saved, loaded on restart, reached `World finished loading`, listened on UDP, and released the port after termination.
- Automated redirected-input `save`/`stop` is not counted as a pass. The console did not consume redirected commands, consistent with the unrelated upstream console limitation tracked as issue #2616. Interactive server-console commands remain part of the manual run.
- Compact `[SRD1]` diagnostics use process epochs, per-source sequence numbers, transition-only events, bounded histories, sampled repetitive warnings, unsampled invariant failures, and short state fingerprints. Evidence collection produces bounded summaries plus authoritative zipped logs/saves.
- The `gottem` evidence exposed and now covers two narrow defects: vanilla Scanner Room cameras use `400` maximum health rather than `100`, and existing room state could change while the shallow `GlobalRootData` graph was being serialized. Camera bounds/defaults/diagnostic bands now use the correct scale, and registered Scanner Room fields are locked through `GlobalRootData` serialization with deadlock-safe contention handling.

These checks exhaust the deterministic, serialization, process, packaging, and impairment-harness work available without launching two real game clients. They do not count as a real-game matrix pass.

## Scope and source state

- The narrow PR 53 vehicle-movement authority slice was incorporated: finite input validation, owner/pilot enforcement, bounded batches, and mutation plus enqueue under the ownership lock. PR 53 itself remains unmerged; unrelated portions were not imported.
- The four pre-existing Unity/Google Drive stat-only metadata paths are intentionally outside this tranche and remain untouched.

## Exploratory two-client smoke (2026-07-18)

- Server/save: `gottem`, using its pre-existing Scanner Room. The launcher identified source commit `9f484b9068d55e56f9e3e181042e0b84ec2a535a`; this was not an isolated-package matrix run.
- Two clients exercised Scanner Room cameras, control, lights, dock/undock, scan target/results, HUD behavior, and upgrades without a visible gameplay mismatch.
- No formal test markers, prescribed action sequence, before/after save pair, restart assertion, impairment profile, or third-client step was recorded. Therefore no R/S/P/F/D/N row is promoted from `NOT RUN`.
- Server diagnostics nevertheless recorded `invalid_camera_component`/`component invalid_value` for a healthy camera reporting health `400`, plus two state changes during autosave. The remaining `stale_or_invalid` scan-type and `non_owner` light rejects were expected defensive behavior.
- Evidence bundles:
  - server/save: `20260719-033214363Z-owner-main-server-scanner-room-9f484b9068d5-qf70d8cfd-20260718T223259Z-win-x64.zip`, SHA-256 `2B619A4B5B463F509CFFFCC918AB8DD6F1A61101C40C18E3DDEB93213871F55F`;
  - host client: `20260719-033253061Z-owner-main-client-scanner-room-9f484b9068d5-qf70d8cfd-20260718T223259Z-win-x64.zip`, SHA-256 `DEF2C1030039D469BCBD40A6FAF0AF1D941BB46881432E3DCFBF315CE345B4DF`.

## Real-game matrix

| ID | Clients | Action | Expected | Status |
|---|---:|---|---|---|
| R1 | 2 | Build room first, then attach to base; A/B insert/remove 0-4 mixed/duplicate upgrades | Same counts, visuals, range/speed; no duplication | NOT RUN |
| R2 | 2-3 | Relog, restart, repeat base resync, partial/full deconstruct and rebuild | Stable room ID/state; clean removal; no orphan upgrades/markers/locks | NOT RUN |
| S1 | 2 | Compare menus; A then B start/cancel/change targets | One canonical target/generation/progress and matching UI | NOT RUN |
| S2 | 3 | Each client picks up, breaks, scans, or destroys a result | Result disappears from every affected room; no ghost ping | NOT RUN |
| S3 | 2-3 | Move/drop resources; unload/reload cells; late join during scan and after removals | Positions/snapshots converge; no obsolete-generation results | NOT RUN |
| S4 | 2 | Toggle HUD chips independently; add/remove range/speed upgrades; cut/restore power | Shared results, player-local HUD gating, correct derived state | NOT RUN |
| S5 | 2-3 | Two nearby rooms scan same then different targets with overlapping ranges | No cross-room state/result corruption | NOT RUN |
| P1 | 1/2/3 | Measure scan energy with each client as power owner | One logical drain; no player-count multiplier or later rollback | NOT RUN |
| F1 | 2-3 | Each client crafts every room recipe; remote pickup; reconnect mid-craft; concurrent use | One output, correct ownership/state, no loss/duplication | NOT RUN |
| F2 | 2 | Repeat ordinary base and lifepod crafts from both clients | Existing five-power accounting remains once per logical craft | NOT RUN |
| D1 | 2-3 | Fresh room, relog, restart, late join | Exactly two default drones unless deliberately changed; stable IDs/numbers | NOT RUN |
| D2 | 2 | Each client controls each drone; simultaneous same/different drone attempts | Exclusive same-drone control; separate drones work; clean handoff | NOT RUN |
| D3 | 2 | Drive/collide/weak-signal fade; lights; drain/recharge; damage/repair/death | Control persists through vanilla signal fade; state converges; death cleans locks/registry | NOT RUN |
| D4 | 2-3 | Dock/undock both slots rapidly; two cameras race one slot; pickup/drop/redock crafted cameras | Atomic slots, stable identity, no duplicates or stuck ownership | NOT RUN |
| D5 | 2 | Stalker grab/chew/release while idle and controlled | Ownership transfers and returns; no frozen/duplicated camera | NOT RUN |
| D6 | 2-3 | Controller dies/disconnects; deconstruct with docked/loose cameras; test two rooms | Locks release, player safe, vanilla cleanup, no wrong/stale camera selection | NOT RUN |
| N1 | 2-3 | Add packaged latency/jitter; disconnect authority; join during resync/docking | State converges without packet echo, duplication, or permanent lock | NOT RUN |
| N2 | 2-3 | Restart immediately after upgrade, dock, undock, target change, and pickup | Last accepted server state restores consistently | NOT RUN |

## Sign-off

- Automated qualification: `PASS`
- Exploratory two-client smoke: `FAIL` on diagnostic invariants; no visible gameplay mismatch; focused fix retest pending
- Formal server/client matrix evidence: `NOT RUN`
- Save before/after formal real-game matrix: `NOT RUN`
- Real-game result: `NOT RUN`

Use the package instructions, stop at the first mismatch, and collect the compact evidence bundle before another restart or retry.
