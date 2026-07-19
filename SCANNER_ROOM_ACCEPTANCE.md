# Scanner Room Multiplayer Acceptance

Status values: `PASS`, `FAIL`, `BLOCKED`, `NOT RUN`. Record failures as `test ID - client - timestamp - save/log path`.

## Setup

- Build/commit: see the final package `BUILD_INFO.json`; one immutable Windows x64 Release build is used by the server and every client.
- Game version: Subnautica build 83031.
- Server/save: automated boot, save-load, and protocol probes passed. Exploratory runs used the pre-existing `gottem` save; the dedicated Scanner Room matrix save is `NOT CREATED`.
- Clients: host `leon` and LAN client `leonlaptop` joined the exploratory run. A/B/C remain `NOT LAUNCHED` under the formal matrix procedure.

## Automated qualification (prior scan/preview follow-up)

- Full Release suite: `587 passed / 8 skipped / 0 failed` (`595 total`). The eight skips are four pre-existing platform-specific filesystem/root-permission tests in each of the Unix and macOS fixtures, skipped on Windows.
- Focused Scanner Room, Map Room, simulation-ownership, and protocol selection: `275 passed / 0 failed`.
- Hybrid scan discovery/authority, packet, and client-result coverage: `79 passed / 0 failed`.
- Camera preview parser, authority/lifecycle, revision, and capture-order coverage: `24 passed / 0 failed`; the broader camera/ownership regression selection passed `122/122`.
- Persistence qualification passed real JSON and ProtoBuf round-trips plus late-join/orphan recovery. The broader persistence/scenario selection passed `20/20`.
- Authority-transition atomicity passed `22/22`; its three deterministic transfer-race tests passed five consecutive runs (`15/15`).
- The deterministic network-impairment proxy passed all six self-tests: command validation, repeatable scheduling, jitter bounds, reorder/expiry, bounded queue, and localhost bidirectional echo.
- Release launcher/client/patcher targets, including `net472`, and the Windows x64 launcher build succeeded. The launcher remained alive with an isolated local `--data-path` and created its log without an option-parser error.
- Prior process probe: legacy protocol `nitrox` was rejected and `nitrox-ai/1` connected; the server port was released after exact-process termination. This follow-up intentionally advances the incompatible connection key to `nitrox-ai/2`; the fresh package must use matching clients/server.
- Save-load probe: a clean JSON world was saved, loaded on restart, reached `World finished loading`, listened on UDP, and released the port after termination.
- Automated redirected-input `save`/`stop` is not counted as a pass. The console did not consume redirected commands, consistent with the unrelated upstream console limitation tracked as issue #2616. Interactive server-console commands remain part of the manual run.
- Compact `[SRD1]` diagnostics use process epochs, per-source sequence numbers, transition-only events, bounded histories, sampled repetitive warnings, unsampled invariant failures, and short state fingerprints. Evidence collection produces bounded summaries plus authoritative zipped logs/saves.
- The `gottem` evidence exposed and now covers two narrow defects: vanilla Scanner Room cameras use `400` maximum health rather than `100`, and existing room state could change while the shallow `GlobalRootData` graph was being serialized. Camera bounds/defaults/diagnostic bands now use the correct scale, and registered Scanner Room fields are locked through `GlobalRootData` serialization with deadlock-safe contention handling.

These checks exhaust the deterministic, serialization, process, packaging, and impairment-harness work available without launching two real game clients. They do not count as a real-game matrix pass.

### Current player-body/loose-camera follow-up

- Release and Debug focused policy/cache selections: `40 passed / 0 failed` each.
- Broader Scanner Room, Map Room, simulation-ownership, movement, base-power, and power-source selection: `352 passed / 0 failed`.
- Full Release suite: `650 passed / 8 skipped / 0 failed` (`658 total`). The eight skips are the same pre-existing platform/filesystem cases.
- Release solution build: `0` errors and `42` existing analyzer warning emissions across `net10.0` and `net472`.
- Packet-processor DI resolution: `1 passed / 0 failed`. Windows PowerShell 5.1 evidence-summary self-test: `PASS`. `git diff --check`: clean. Independent review found no remaining P0–P2 issue.
- Automated qualification is `PASS`. Immutable-package verification is pending the clean pushed commit; neither result promotes a real-game matrix row.

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

## Exploratory replacement-package smoke (2026-07-19)

- Server/save: `gottem`, package `scanner-room-8bb2e98f4074-qae93f7ee-20260719T043646Z-win-x64`, source commit `8bb2e98f4074940d6e72c62d78e4331a49280664`, Subnautica build `83031`.
- Authoritative server/save/host archive: `20260719-115743493Z-owner-main-server-scanner-room-8bb2e98f4074-qae93f7ee-20260719T043646Z-win-x64.zip`, SHA-256 `AC64F528362E606A749E21C1F68CF03B9A1F87CDAA7493DFD10F80427EBDF8DD`. The earlier archive with SHA-256 `7BDD44BAC92B9E5A6FA9492392788E5F88A9F13086AC0C95BCEB1D7443651D3E` is superseded because its bracketed host-log path was copied as a wildcard.
- The prior 400-health and autosave-snapshot defects did not recur. All exercised camera acquire/movement/dock/light/release transitions were accepted, all save checkpoints were valid, and no Scanner Room invariant failed.
- The physical camera preview image/selection remained client-local. This is now addressed with a bounded `256x256`, at-most-64-KiB JPEG packet published once after vanilla captures the frame, accepted only from the exclusive controller, ordered by a global session revision, broadcast to every client including the sender, applied to the process-global preview texture and every loaded Scanner Room screen, and deliberately excluded from save/JIP state.
- Actual accepted scan counts were Lead `1`, Uraninite `1`, Shale `0`, Limestone `0`, and JellyPlant/Gel Sack `3`. The saved server world contains 162 Shale and 103 Limestone entities within the room's 300 m range; the nearest are about 93.4 m and 76.6 m away. The owner client's locally registered cells explain the zero snapshots.
- Scan discovery now unions owner-reported vanilla results with exact-TechType, in-range server registry entities. Owner-only override mappings such as `DrillableLead -> Lead`, fragments, eggs, and databoxes remain intact. The server validates the base-root anchor and installed range modules, orders/caps results deterministically, treats identical snapshots idempotently, and corrects ordinary client unload/range-exit removals when the live server entity is still in range.
- Initialized non-owner clients suppress vanilla local-only result mutations, stable-ID live/synthetic objects are deduplicated, and equal-revision corrective snapshots may restore canonical state. Camera preview eligibility is tied to one real exclusive acquisition, and background simulation assignment cannot silently downgrade that lock.
- Known discovery boundary: only entities already present in the server `EntityRegistry` can supplement a scan. Persisted `gottem` entities are registered; an unparsed lazy world batch remains unavailable until it is registered.
- The formal action sequence, restart assertion, impairment profile, third-client step, and formal save pair were not run. No R/S/P/F/D/N row is promoted from `NOT RUN`.

## Exploratory e59 targeted smoke (2026-07-19)

- Server/save: `gottem`, package `scanner-room-e59b0237f9db-q021dbc84-20260719T185635Z-win-x64`, source commit `e59b0237f9dbf9b50ac42b6930890676998c0cb4`, Subnautica build `83031`. Package ZIP SHA-256: `31EEC927AF1B34F2D8B43A415C28F6A6E594225C87486C85E50E379E3E129E28`.
- Hybrid discovery and shared preview behaved correctly: accepted scans returned Limestone `101`, Uraninite `66`, and Wreck `1`; accepted preview revisions advanced from `1` through `3`.
- One intermittent observer defect occurred: while client 1 controlled a camera, client 2 saw client 1's player in an incorrect location instead of remaining at the Scanner Room console. Exiting camera control restored the observed player position, and the issue was not reproduced again in that run.
- Client 1 could not switch to the other camera after both cameras had been left loose across world load. Only camera `.72cf` was requested/controlled in this session. An earlier same-day session successfully switched `.8c30` to `.72cf` and back, so the camera-control handoff path itself is proven; the new failure was restored-camera availability.
- Save/log correlation identifies the restore race: `.8c30` had saved energy `99.64972`, health `100`, and component revision `38`, then its late-spawned prefab initialized at energy `0` and health `400`; that default component publication was accepted as revision `39`, leaving vanilla `CanBeControlled` to skip the empty camera.
- The available evidence includes the server and host-client logs but not the laptop client log. No formal action sequence, third-client step, impairment row, or formal save pair was recorded, so every R/S/P/F/D/N row remains `NOT RUN`.
- Base-power audio was not heard during this run, but no deliberate base-power test was performed and no `[BPD1]` audio-decision row was present. This is passive non-observation only and does not promote the base-power smoke or matrix.
- The current repair continues physical-player publications from the console at zero velocity while scanner-camera control is active, with bounded `[SRD1] player_body_pin` enter/switch/identify/exit evidence. Drone movement remains independent.
- Canonical loose-camera state now survives room/camera spawn ordering through a durable cache and initial-load restore barrier. Component application, battery initialization, and broadcast suppression are tied to the current camera instance/generation so a late prefab default cannot supersede saved state.

## Real-game matrix

| ID | Clients | Action | Expected | Status |
|---|---:|---|---|---|
| R1 | 2 | Build room first, then attach to base; A/B insert/remove 0-4 mixed/duplicate upgrades | Same counts, visuals, range/speed; no duplication | NOT RUN |
| R2 | 2-3 | Relog, restart, repeat base resync, partial/full deconstruct and rebuild | Stable room ID/state; clean removal; no orphan upgrades/markers/locks | NOT RUN |
| S1 | 2 | Compare menus; include nearby Limestone/Shale outside local streamed cells; A then B start/cancel/change targets | One canonical type set/target/generation/progress, complete in-range exact results, and matching UI | NOT RUN |
| S2 | 3 | Each client picks up, breaks, scans, or destroys a result | Result disappears from every affected room; no ghost ping | NOT RUN |
| S3 | 2-3 | Move/drop resources; unload/reload cells; late join during scan and after removals | Positions/snapshots converge; no obsolete-generation results | NOT RUN |
| S4 | 2 | Toggle HUD chips independently; add/remove range/speed upgrades; cut/restore power | Shared results, player-local HUD gating, correct derived state | NOT RUN |
| S5 | 2-3 | Two nearby rooms scan same then different targets with overlapping ranges | No cross-room state/result corruption | NOT RUN |
| P1 | 1/2/3 | Measure scan energy with each client as power owner | One logical drain; no player-count multiplier or later rollback | NOT RUN |
| F1 | 2-3 | Each client crafts every room recipe; remote pickup; reconnect mid-craft; concurrent use | One output, correct ownership/state, no loss/duplication | NOT RUN |
| F2 | 2 | Repeat ordinary base and lifepod crafts from both clients | Existing five-power accounting remains once per logical craft | NOT RUN |
| D1 | 2-3 | Fresh room, relog, restart, late join | Exactly two default drones unless deliberately changed; stable IDs/numbers | NOT RUN |
| D2 | 2 | Each client controls/exits each drone while the other watches the physical preview and controller's player body; race same/different drones | Exclusive same-drone control; separate drones work; body stays at console; clean handoff/exit; last image and camera label converge | NOT RUN |
| D3 | 2 | Drive/collide/weak-signal fade; lights; drain/recharge; damage/repair/death | Control persists through vanilla signal fade; state converges; death cleans locks/registry | NOT RUN |
| D4 | 2-3 | Dock/undock both slots rapidly; two cameras race one slot; pickup/drop/redock crafted cameras | Atomic slots, stable identity, no duplicates or stuck ownership | NOT RUN |
| D5 | 2 | Stalker grab/chew/release while idle and controlled | Ownership transfers and returns; no frozen/duplicated camera | NOT RUN |
| D6 | 2-3 | Controller dies/disconnects; deconstruct with docked/loose cameras; test two rooms | Locks release, player safe, vanilla cleanup, no wrong/stale camera selection | NOT RUN |
| N1 | 2-3 | Add packaged latency/jitter; disconnect authority; join during resync/docking | State converges without packet echo, duplication, or permanent lock | NOT RUN |
| N2 | 2-3 | Restart immediately after upgrade, dock, undock, target change, and pickup | Last accepted server state restores consistently | NOT RUN |

## Sign-off

- Automated qualification: earlier scan/preview repair `PASS`; current player-body/loose-camera restore follow-up `PASS`
- Replacement immutable package: `PENDING`
- 2026-07-18 exploratory smoke: prior diagnostic invariant failures are fixed and did not recur in the replacement run
- 2026-07-19 replacement smoke: `FAIL` on scan discovery and camera preview presentation in the superseded package; both fixes now pass automated qualification and await the targeted two-client retest
- 2026-07-19 e59 targeted smoke: scan discovery and preview behaved correctly; one intermittent observer-body drift and one loose-camera restore/selectability failure await the replacement-package retest
- Formal server/client matrix evidence: `NOT RUN`
- Save before/after formal real-game matrix: `NOT RUN`
- Real-game result: `NOT RUN`

Use the package instructions, stop at the first mismatch, and collect the compact evidence bundle before another restart or retry.
