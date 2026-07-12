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
- [x] Restrict continuous Scanner Room power drain to its simulation owner (#31)
- [x] Persist and owner-gate Scanner Room fabricator state (#32)
- [x] Cover 0-4 mixed upgrade modules and deterministic derived effects (#33)
- [x] Clear dock state on camera pickup and reject inventory-camera dock races (#34)
- [x] Hand camera simulation ownership to Stalkers during grab/chew/drop (#35)
- [x] Synchronize camera repair/damage and clean death state/locks (#36)
- [x] Filter remotely controlled cameras from Scanner Room screen selection (#37)
- [x] Normalize legacy/duplicate camera saves and test deterministic IDs (#38)
- [x] Validate Scanner Room identity during base topology updates (#39)
- [x] Invalidate scan results on server entity destruction and pickup (#40)
- [x] Scope scan-result snapshots/deltas to subscribed Scanner Room clients (#41)
- [x] Clean Scanner Room results, markers, subscriptions, and locks on deconstruction (#42)
- [x] Cover handheld scanner popups and fabricator power deduplication (#43)
- [x] Add executable 2–3 client acceptance matrix (#44)
- [x] Final automated build/test and protocol/save/limitations report (#45)
- [x] Persist shared lifepod and radio repairs across account joins (#46)
- [x] Repair Scanner Room UI startup patch and restored camera registration (#47)
- [x] Bootstrap legacy docked cameras and prefer physical restored instances (#48)
- [x] Initialize missing legacy camera batteries during restore (#48)
- [x] Bound dock charging and preserve pending camera control mode (#48)
- [x] Prevent pending camera control from latching the No Signal overlay (#48)

## Remaining

- [ ] Execute in-game acceptance matrix (`SCANNER_ROOM_ACCEPTANCE.md`; requires 2–3 game clients)

## Next

- [ ] Execute in-game acceptance matrix (`SCANNER_ROOM_ACCEPTANCE.md`; requires 2–3 game clients)

## Rules

- Push only to `leondood1298/Nitrox-AI`; upstream push stays disabled.
- One focused draft PR at a time; merge before starting the next branch.
- Compile local test builds to Desktop folders using `1.16.1`, `1.16.2`, etc.; do not update GitHub Releases.
- Keep updating the current Scanner Room repair PR until the user merges it.
- Preserve unrelated custom fixes.
- Build and run the full test suite before every PR.
