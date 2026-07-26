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
- [x] Validate restored camera control, loose control, drain, and dock recharge (#48)
- [x] Confirm vanilla weak-signal and screenshot-preview behavior (#48)
- [x] Preserve two-client camera movement through rapid dock/undock (#49)
- [x] Validate two-client camera movement after rapid redocking (#49)
- [x] Republish early scan progress and refresh non-owner hologram blips (#50)
- [x] Suppress loose-world duplicates for cameras currently recorded as docked (#50)
- [x] Validate restored cameras across save reload and two clients (#50)
- [x] Resynchronize non-owner red blips after scan-target changes (#51)
- [x] Validate shared Scanner Room results and HUD chips on two clients (#51)
- [x] Remove collected resources from the collector's local result list (#51)
- [x] Evict collected resources from the collector's rendered HUD cache after pickup (#51)
- [x] Apply an authoritative stopped scan state and refresh UI on client join (#51)
- [x] Publish scan cancellation immediately and discard queued progress metadata (#51)
- [x] Remove prefab dock cameras from authoritatively empty restored slots (#51)
- [x] Validate scan cancellation and camera reload without duplicates (#51)
- [x] Remove every duplicate stable-ID result on pickup and server removal (#51)
- [x] Fall back to resource type/position when the collector's local scan-result ID differs (#51)
- [x] Broadcast idempotent camera releases after movement-ownership races (#51)
- [x] Persist bootstrapped prefab cameras as world entities on first undock (#51)
- [x] Recover registered cameras that were orphaned by older undock saves (#51)
- [x] Validate current-save orphan recovery and first-undock persistence registration (#51)
- [x] Validate two-client camera handoff/release restores control availability (#51)
- [x] Apply authoritative number, battery, health, and light state to loose cameras on reload (#51)
- [x] Clear the collector's local Scanner Room result when a breakable target is destroyed (#51)
- [x] Preserve and diagnose the `gottem` exploratory two-client evidence
- [x] Correct the Scanner Room camera health contract to the vanilla 400-point scale
- [x] Capture registered Scanner Room state consistently during `GlobalRootData` autosave serialization
- [x] Qualify the follow-up with focused race/persistence coverage and the full Release suite
- [x] Confirm the camera-health and autosave fixes in the replacement `gottem` two-client smoke
- [x] Audit the replacement logs/save and correlate empty Limestone/Shale scans with the server world registry
- [x] Supplement vanilla scan discovery with exact, in-range server entities while preserving client override mappings
- [x] Synchronize the vanilla session-global camera preview image and selection with a bounded ephemeral packet
- [x] Distinguish sibling Scanner Room cameras in compact diagnostics and add preview/scan evidence events
- [x] Correct live server results after erroneous client unload/range-exit removals
- [x] Suppress initialized non-owner vanilla result mutations and deduplicate live/synthetic stable IDs
- [x] Broadcast accepted preview revisions to all clients so simultaneous camera exits converge
- [x] Preserve active exclusive camera control during background simulation acquisition
- [x] Qualify the scan/preview follow-up with 275 combined regressions, 79 scan-focused regressions, and the full 587-pass Release suite
- [x] Confirm hybrid discovery and shared preview in the e59 `gottem` smoke (Limestone 101, Uraninite 66, Wreck 1; preview revisions 1-3)
- [x] Audit the intermittent observer-body drift and the restored loose camera that could not be selected
- [x] Keep the physical player anchored at the Scanner Room console with zero-velocity broadcasts during camera control and bounded `[SRD1] player_body_pin` diagnostics
- [x] Preserve loose-camera record/light/component state across restore ordering with a durable cache, initial-load barrier, and camera-instance battery initialization
- [x] Qualify the body-anchor/restore follow-up: focused Release/Debug `40/40`, broader relevant `352/352`, full Release `650 passed / 8 skipped / 0 failed`, build `0` errors
- [x] Build and verify exact `70ef86d3` immutable package `scanner-room-70ef86d3284e-q772a1be7-20260724T204353Z-win-x64`
- [x] Complete and audit the targeted two-client smoke: restore/control/switch/preview/body anchoring/recharge, pickup-forced release, and redocking passed
- [x] Record the self-clearing held-camera offset as a deferred low-severity client-local cosmetic issue
- [x] Record the owner's intermediate-release waiver and authorization without promoting any formal matrix row or completing Phase 1
- [x] Merge protected PR #54 to `master` and qualify exact merge commit `a4c9ed6f`: build 0 errors, full Release `661 passed / 8 skipped / 0 failed`
- [x] Build and verify exact-master package `scanner-room-a4c9ed6f5347-qc14d7889-20260724T214807Z-win-x64`, SHA-256 `4BE44322257510A665F987A9BE55091888E257A6A184B9AB5A8E4F4FDC9E750C`
- [x] Publish GitHub release `custom-build-1.16.25-final` as an intermediate milestone with Phase 1 and all formal rows still open
- [x] Fast-forward and recreate `agent/nitrox-ai-development` at released `master` after GitHub's automatic branch deletion

## Remaining

- [ ] Retest D0 from an unpolluted checkpoint with both clients present for construction; test camera movement observation in both directions, have the non-builder perform pickup/drop/redock, preserve both client logs, and compare canonical IDs/counts throughout
- [ ] Separate camera creation provenance from generic pickup/drop packets before adding a server-side unknown-camera admission gate; preserve crafted, console-created, and legacy loose cameras
- [ ] Restart once after a Scanner Room save and execute the formal matrix before Phase 1 completion; these gates were waived only for this intermediate release
- [ ] Undock camera 1, restart, and verify it persists loose instead of vanishing or duplicating (#51)
- [ ] Verify Shale HUD markers disappear for collector and observer after local break cleanup (#51)

## Next

- [ ] Select the next bounded correctness goal while preserving the deferred restart/matrix work

## Rules

- Push only to `leondood1298/Nitrox-AI`; upstream push stays disabled.
- One focused PR at a time; merge before starting the next branch.
- Publish interim two-client test artifacts as immutable Drive packages. GitHub releases are reserved for phase completion or an explicitly owner-designated intermediate milestone whose open gates remain visible.
- Use `agent/nitrox-ai-development` as the durable project branch; merge through protected PRs, then synchronize it to released `master` before continuing.
- Preserve unrelated custom fixes.
- Build and run the full test suite before every PR.
