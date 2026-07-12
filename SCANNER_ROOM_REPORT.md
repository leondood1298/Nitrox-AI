# Scanner Room Repair Report

Final automated baseline: `0c94cbfb90a8fe249d40f7088c6d94ae8b3cf1f7` after merged PRs #1–#44. Scanner Room work after the imported baseline changes 104 files (+3,536/−75).

## Result

- Server-authoritative room target/progress, result generations/revisions, result snapshots/deltas, available scan types, topology identity, fabricator state, camera registry/docks/components/control, and cleanup.
- Persistent upgrades, derived range/speed, scan results, fabricator metadata, dock slots, camera numbering/light/energy/health, and legacy camera normalization.
- Owner-gated continuous scan power and dock charging; preserved per-craft distributed power accounting.
- Late-join/resync reconstruction, scoped UI subscriptions, resource invalidation, marker refresh, Stalker handoff, pickup/dock races, death/disconnect/deconstruction cleanup, and overlapping-room isolation.
- Protected unrelated handheld-scanner popup and general fabricator-power behavior with focused regressions.

## Authority decisions

- Server accepts state changes only from the room/camera simulation owner and publishes accepted revisions.
- Scan-result identity is `(room ID, generation, resource ID)`; target/range changes invalidate obsolete generations.
- Result traffic is sent to the owner and clients subscribed by an open Scanner Room UI, with an immediate canonical snapshot on subscribe.
- One camera may have one dock and one controller; docking, transfer, pickup, creature interaction, and death revoke conflicting ownership.
- Loose cameras remain entities; room deconstruction clears room state/locks/subscriptions and applies the established camera cleanup policy.

## Protocol and save impact

- Added protocol packets: `MapRoomCameraComponentState`, `MapRoomScanResultChanged`, `MapRoomScanResultSnapshot`, `MapRoomScanResultSubscription`, and `MapRoomScanTypesSnapshot`; existing camera control/dock/light packets gained authoritative state fields during the imported/custom evolution.
- All clients and server must run this same fork build; older protocol peers are not compatible with these messages.
- `MapRoomEntity` now persists dock IDs/revision, camera records, scan generation/revision/results, available scan types/revision, and fabricator metadata.
- `MapRoomMetadata` persists scan generation/revision in addition to target/progress. Camera records persist number, light, energy, health, and component revisions.
- Fields are additive with empty/default fallback. Legacy camera state is normalized deterministically and repeatedly without creating new IDs. No standalone save-version bump or destructive save upgrader was added.

## Verification

- Final command: `dotnet build Nitrox.slnx -c Release --nologo -m:1 -v:q`
- Final command: `dotnet test Nitrox.Test/Nitrox.Test.csproj -c Release --no-build --nologo -m:1`
- Result before this report: build succeeded with 39 existing warnings; 306 passed, 8 skipped, 0 failed.
- Automated coverage includes authority/stale replay, serialization/save models, result lifecycle, upgrades, topology, camera numbering/docking/control/components/migration, cleanup, popup suppression, and craft-power deduplication.

## Remaining limitation

- The 2–3 client in-game matrix in `SCANNER_ROOM_ACCEPTANCE.md` is `NOT RUN`; this environment cannot launch multiple licensed Subnautica clients. Until those rows pass, the repair is automated-test complete but not multiplayer acceptance signed off.
- The 39 build warnings and 8 platform-dependent skipped tests predate this closeout and remain visible; no new warning class was introduced.

## Final acceptance

Run `SCANNER_ROOM_ACCEPTANCE.md`, attach server/client logs and before/after saves, replace each `NOT RUN`, and set its sign-off result. Any failure should be fixed in a focused follow-up PR and the affected row rerun.
