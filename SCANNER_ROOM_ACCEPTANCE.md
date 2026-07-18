# Scanner Room Multiplayer Acceptance

Status values: `PASS`, `FAIL`, `BLOCKED`, `NOT RUN`. Record failures as `test ID – client – timestamp – save/log path`.

## Setup

- Build/commit: `5ef30680ae57c9e8fa1a368eeb8f233cc03e9bed` (`win-x64` Release)
- Server/save: process-level protocol probe only; Scanner Room matrix save `NOT CREATED`
- Clients: A `NOT LAUNCHED` B `NOT LAUNCHED` C/late join `NOT LAUNCHED`
- Start each scenario from synchronized inventories; record room IDs and camera IDs from logs.

## Evidence and blockers (2026-07-18)

- Artifact manifest: `test_results/artifact-5ef30680ae57/artifact-manifest.csv`; SHA-256 `DF9DDB9530E75CCCC3B6F1A326A9409AEFFB8F0E177A984ABB3F887D25786D36`.
- Automated prerequisite evidence: baseline `386 passed / 8 skipped / 0 failed`; final `402 passed / 8 skipped / 0 failed`. This does not count as a D/N matrix pass.
- Exact Release process probe: `test_results/handshake-probe-release/20260718-102100-5ef30680ae57`; legacy `nitrox` rejected, `nitrox-ai/1` accepted, one rejection log, one session, server stopped, UDP 11153 released.
- D1-D6 and N1-N2 remain `NOT RUN`: the managed computer-use runtime failed to initialize twice with `windows sandbox failed: helper_unknown_error` before any launcher/game UI action.
- N1 additionally lacks a validated latency/jitter impairment harness. Do not claim N1 from an unvalidated proxy or unit test.
- PR 53 remains unmerged and reserved for the later vehicle-authority phase.

## Matrix

| ID | Clients | Action | Expected | Status |
|---|---:|---|---|---|
| R1 | 2 | Build room first, then attached to base; A/B insert/remove 0–4 mixed/duplicate upgrades | Same counts, visuals, range/speed; no duplication | NOT RUN |
| R2 | 2–3 | Relog, restart, repeat base resync, partial/full deconstruct and rebuild | Stable room ID/state; clean removal; no orphan upgrades/markers/locks | NOT RUN |
| S1 | 2 | Compare menus; A then B start/cancel/change targets | One canonical target/generation/progress and matching UI | NOT RUN |
| S2 | 3 | Each client picks up, breaks, scans, or destroys a result | Result disappears from every affected room; no ghost ping | NOT RUN |
| S3 | 2–3 | Move/drop resources; unload/reload cells; late join during scan and after removals | Positions/snapshots converge; no obsolete-generation results | NOT RUN |
| S4 | 2 | Toggle HUD chips independently; add/remove range/speed upgrades; cut/restore power | Shared results, player-local HUD gating, correct derived state | NOT RUN |
| S5 | 2–3 | Two nearby rooms scan same then different targets with overlapping ranges | No cross-room state/result corruption | NOT RUN |
| P1 | 1/2/3 | Measure scan energy with each client as power owner | One logical drain; no player-count multiplier or later rollback | NOT RUN |
| F1 | 2–3 | Each client crafts every room recipe; remote pickup; reconnect mid-craft; concurrent use | One output, correct ownership/state, no loss/duplication | NOT RUN |
| F2 | 2 | Repeat ordinary base and lifepod crafts from both clients | Existing five-power accounting remains once per logical craft | NOT RUN |
| D1 | 2–3 | Fresh room, relog, restart, late join | Exactly two default drones unless deliberately changed; stable IDs/numbers | NOT RUN |
| D2 | 2 | Each client controls each drone; simultaneous same/different drone attempts | Exclusive same-drone control; separate drones work; clean handoff | NOT RUN |
| D3 | 2 | Drive/collide/weak-signal fade; lights; drain/recharge; damage/repair/death | Control persists through vanilla signal fade; state converges; death cleans locks/registry | NOT RUN |
| D4 | 2–3 | Dock/undock both slots rapidly; two cameras race one slot; pickup/drop/redock crafted cameras | Atomic slots, stable identity, no duplicates or stuck ownership | NOT RUN |
| D5 | 2 | Stalker grab/chew/release while idle and controlled | Ownership transfers and returns; no frozen/duplicated camera | NOT RUN |
| D6 | 2–3 | Controller dies/disconnects; deconstruct with docked/loose cameras; test two rooms | Locks release, player safe, vanilla cleanup, no wrong/stale camera selection | NOT RUN |
| N1 | 2–3 | Add latency/jitter; disconnect authority; join during resync/docking | State converges without packet echo, duplication, or permanent lock | NOT RUN |
| N2 | 2–3 | Restart immediately after upgrade, dock, undock, target change, and pickup | Last accepted server state restores consistently | NOT RUN |

## Sign-off

- Failures fixed/retested: pre-deserialization protocol mismatch rejection and delayed/retry lifecycle; focused `16/16`, full `402 passed / 8 skipped / 0 failed`
- Server log: Release protocol probe path above; Scanner Room matrix log `NOT RUN`
- Client logs: `NOT RUN`
- Save before/after: `NOT RUN`
- Result: `NOT RUN` (real-client UI control unavailable; no D/N result inferred from automation)
