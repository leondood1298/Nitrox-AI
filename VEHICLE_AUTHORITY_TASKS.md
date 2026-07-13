# Vehicle Authority Tasks

## Release 1.16.23

- [x] Merge PR #52
- [x] Build and verify Windows x64 package
- [x] Publish normal GitHub release

## Audit

- [x] Audit Cyclops, Seamoth, Prawn, movement, docking, power, damage, and persistence
- [x] Separate inherited vehicle work from the custom 1.15 import
- [x] Define the first server-authority boundary

## Current branch: `agent/vehicle-authority`

- [x] Draft PR #53 opened
- [x] Reject movement without simulation ownership
- [x] Validate piloting, docking, and paired undocking transitions
- [x] Replace packet session ids with the authenticated sender
- [x] Add `vehicle` snapshot and trace diagnostics
- [x] Fix fire id, Prawn steering, and remote damage bugs
- [x] Add authority tests: 398 passed, 8 platform skips
- [ ] Two-client vehicle acceptance test

## Test build 1.16.24

- [x] Build and package Windows x64
- [x] Publish GitHub pre-release
- [ ] Scanner Room camera movement regression
- [ ] Cyclops pilot, movement, stop, and player visibility
- [ ] Seamoth and Prawn pilot and movement
- [ ] Moonpool and Cyclops docking/undocking
- [ ] Pilot handoff and owner disconnect/rejoin
- [ ] Capture `vehicle` before/after any failure

## Next

- [ ] Revisioned server-canonical Cyclops controls
- [ ] Persist/replay Cyclops damage points, fires, and subsystem health
- [ ] Validate decoy, suppression, module, arm, grapple, and torpedo actions
- [ ] Add owner-handoff and late-join recovery snapshots
- [ ] Two-client acceptance: movement, docking, controls, power, damage, restart
