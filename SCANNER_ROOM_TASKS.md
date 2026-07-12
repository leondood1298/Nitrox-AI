# Scanner Room Repair Tasks

Update this file after each completed work slice. Keep entries short; link the PR and move the next item to the top of **Remaining**.

## Done

- [x] Import external 1.15 baseline into `leondood1298/Nitrox-AI` (#1)
- [x] Authoritative scan target/progress revisions and upgrade persistence (#2)
- [x] Exclusive server-authoritative camera control (#3)
- [x] Persistent dock slots and docking revisions (#4)
- [x] Explicit undocking (#5)
- [x] Disconnect lock cleanup (#6)
- [x] Authoritative camera lights (#7)
- [x] Camera static-list normalization (#8)
- [x] Persistent camera numbering/registry (#9)
- [x] Persist camera light state (#10)
- [x] Deconstruction camera cleanup (#11)
- [x] Validate camera/room control association (#12)
- [x] Reject duplicate cross-room docking (#13)
- [x] Atomic camera registry transfer between rooms (#14)
- [x] Persist camera energy and health (#15)
- [x] Single-owner dock charging (#16)
- [x] Persist free-camera damage (#17)
- [x] Persistent canonical scan-result model (#18)
- [x] Invalidate results atomically on target change (#19)
- [x] Server-authoritative result deltas (#20)
- [x] Client result reconstruction for join/resync (#21)
- [x] Owner-only result discovery/removal publication (#22)
- [x] Atomic bounded result snapshots (#23)
- [x] Resnapshot results after range changes (#24)
- [x] Synchronize moving resource positions/range crossings (#25)
- [x] Authoritative scan progress publication/validation (#26)
- [x] Harden result IDs and coordinates (#27)
- [x] Persistent owner-validated scan-type set (#28)
- [x] Publish/apply canonical scan types in Scanner Room UI (#29)
- [x] Refresh hologram/HUD markers after canonical result changes (#30)

## Remaining

- [ ] Scanner Room continuous power ownership/accounting audit and fix
- [ ] Scanner Room fabricator identity, concurrency, reconnect, output/pickup audit
- [ ] Upgrade live-sync/resync tests for 0-4 mixed modules and derived effects
- [ ] Camera pickup/drop inventory lifecycle and dock race handling
- [ ] Stalker grab/chew/drop ownership handoff
- [ ] Camera repair/death cleanup and lock release
- [ ] Camera viewer vs controller behavior and screen selection audit
- [ ] Default/crafted camera migration and duplicate-save recovery tests
- [ ] Base split/merge, partial construction, rebuild, and room identity audit
- [ ] Result invalidation from all server entity-destruction/pickup paths
- [ ] Result snapshot/delta visibility scoping (avoid unrelated-client broadcast)
- [ ] Deconstruction cleanup for scan results, markers, subscriptions, and locks
- [ ] Regression tests: handheld scanner popup and fabricator power deduplication
- [ ] Manual 2-3 client acceptance matrix: join/restart/resync/races/power
- [ ] Final full build/test, protocol/save report, limitations, and cleanup

## Next

- [ ] Scanner Room continuous power ownership/accounting audit and fix

## Rules

- Push only to `leondood1298/Nitrox-AI`; upstream push stays disabled.
- One focused draft PR at a time; merge before starting the next branch.
- Preserve unrelated custom fixes.
- Build and run the full test suite before every PR.
