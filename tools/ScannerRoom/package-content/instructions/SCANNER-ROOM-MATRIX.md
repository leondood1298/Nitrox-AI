# Scanner Room final real-game matrix

`TEST_STATUS.md` lists the checks already exhausted without Unity. This matrix covers only behavior that still needs the actual game, rendering, physics, live network timing, or a real process restart.

Before and after every table row, run `scannermark <ID>-before` / `scannermark <ID>-after` in the server console and run `Mark-Step.ps1` on every involved client. Use `-Phase pass` only after both clients agree. Expected state must match on both clients before moving on; on the first mismatch, stop and follow `FAILURE-CAPTURE.md`.

## Session 1: build, upgrades, target/results, and default cameras

| ID | Action | Required observation |
|---|---|---|
| R1 | Build one Scanner Room free-standing, then attached to a base. A/B alternately insert and remove 0-4 mixed range/speed modules, including duplicate types. | Counts, models, range, and speed match; no module duplicates or loss. |
| S1 | A starts, cancels, and changes a target; B repeats. | One target/generation/progress and matching menus. |
| S2 | While scanning, A then B pick up/break/destroy a displayed resource. | The ping disappears on both clients and does not return as a ghost. |
| S4 | Toggle HUD chips independently, change upgrades, and cut/restore base power. | Results are shared, HUD visibility is player-local, derived range/speed and powered state agree. |
| D1 | With a fresh room, relog B as C. Run `scannermark D1-persist` immediately before `save`/`stop`, restart without `-ResetData`, reconnect, then run the same `scannermark D1-persist` label again. | Exactly two default drones unless deliberately changed; IDs, numbers, slots, and fingerprint remain stable. The summary reports no manual-checkpoint fingerprint conflict. |

## Session 2: overlapping rooms, fabrication, control, and docking

| ID | Action | Required observation |
|---|---|---|
| S3 | Move/drop resources, leave/re-enter the cells, and late-join C during a scan and after removals. | Positions and snapshots converge; no obsolete-generation result returns. |
| S5 | Run two nearby rooms on the same target, then different targets, with overlapping range. | No cross-room target/result corruption. |
| F1 | A and B each craft every Scanner Room recipe; remote player picks up output; reconnect once mid-craft; attempt concurrent use once. | Exactly one output per craft, correct ownership/state, no loss or duplication. |
| F2 | Repeat one ordinary base craft and one lifepod craft from both clients. | Existing five-power accounting remains once per logical craft. |
| D2 | Each player controls each drone. Race the same drone, then control different drones simultaneously; release and reacquire. | One controller per drone, different drones work independently, clean handoff and selection. |
| D4 | Rapidly dock/undock both slots; race two cameras for one slot; pickup/drop/redock one crafted camera. | Atomic slots, stable identity/number, no duplicate object, no stuck owner. |

## Session 3: power, physical behavior, cleanup, and restart edges

| ID | Action | Required observation |
|---|---|---|
| P1 | Measure one scan interval with A as base owner and again after B owns/simulates the base. | One logical drain; no player-count multiplier and no later rollback. |
| D3 | Drive through signal fade, collide, toggle lights, drain/recharge, damage/repair, and destroy one camera. | Vanilla signal behavior persists; state matches; death clears locks, registry, selection, and duplicate objects. |
| D5 | Let a Stalker grab/chew/release an idle camera, then repeat while a player controls it. | Authority transfers/returns; no frozen, duplicated, or permanently selected camera. |
| D6 | Disconnect the controller, reconnect, repeat with player death, then deconstruct rooms containing docked and loose cameras. Check the untouched second room. | Locks and selection clear, player remains safe, cleanup is local to the correct room/cameras. |
| R2 | Relog, restart, repeat base resync, then partially/fully deconstruct and rebuild the mutation room. | Stable surviving state and clean removal; no orphan upgrades, markers, cameras, or locks. |

## N2: accepted-state restart checkpoints

For each item below: perform the mutation, run `scannermark N2-<name>-persist`, run `save`, wait for completion, run `stop`, restart the same server without `-ResetData`, reconnect A/B, run `scannerroom`, and run the exact same `scannermark N2-<name>-persist` label again. The bounded summary automatically compares fingerprints with the same room and label across process epochs; any `manual-checkpoint-fingerprint-conflict` is a failure.

1. Upgrade insertion/removal.
2. Camera dock.
3. Camera undock.
4. Scan target change.
5. Camera pickup.

The last accepted server state must return every time. Preserve the first failure before another restart.

## N1: timing and reconnect

First perform abrupt client disconnect during control, docking, and result resync on the normal connection; late-join C at each boundary. Then repeat control handoff, one dock race, and one result resync through the packaged proxy using the fixed profile from `TWO-MACHINE-SETUP.md`. Record the proxy `[NIP1] ev=start`, learned client, last stats, and final stop lines. Queue overflow or send errors are a failed impairment run, not a product result; preserve evidence and rerun the proxy gate once after diagnosing the environment.

## Final successful-run evidence

Evidence is required even when every row passes. Run `scannerroom`, `scannermark FINAL-pass`, and final `Mark-Step.ps1 -TestId FINAL -Phase pass` markers. Then collect server, A, B/C, and proxy evidence into the synced inbox using the data paths printed by their start scripts. Confirm each ZIP has `.sha256`, `.summary.txt`, and `.summary.json` sidecars and wait for Google Drive sync to finish. Read the bounded summary sidecars first; the ZIP remains the authoritative full evidence.
