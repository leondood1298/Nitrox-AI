# Scanner Room Repair Report

Current follow-up: the 2026-07-19 `gottem` evidence from source commit `8bb2e98f4074940d6e72c62d78e4331a49280664` confirmed the earlier health/save fixes and exposed two presentation/discovery gaps. This branch now adds hybrid server-backed scan discovery, ephemeral multiplayer camera-preview synchronization, and collision-resistant compact camera diagnostics. The formal real-game matrix remains open.

Final automated baseline: `0c94cbfb90a8fe249d40f7088c6d94ae8b3cf1f7` after merged PRs #1–#44. Scanner Room work after the imported baseline changes 104 files (+3,536/−75).

## Result

- Server-authoritative room target/progress, result generations/revisions, result snapshots/deltas, available scan types, topology identity, fabricator state, camera registry/docks/components/control, and cleanup.
- Persistent upgrades, derived range/speed, scan results, fabricator metadata, dock slots, camera numbering/light/energy/health, and legacy camera normalization.
- Owner-gated continuous scan power and dock charging; preserved per-craft distributed power accounting.
- Late-join/resync reconstruction, scoped UI subscriptions, resource invalidation, marker refresh, Stalker handoff, pickup/dock races, death/disconnect/deconstruction cleanup, and overlapping-room isolation.
- Protected unrelated handheld-scanner popup and general fabricator-power behavior with focused regressions.
- Exact, in-range server `WorldEntity` results supplement the owner's vanilla `ResourceTrackerDatabase` snapshot; owner-only override mappings remain in the union, server positions win duplicate IDs, and deterministic ordering/caps preserve override-only records.
- The server validates finite scan queries, a bounded base-root anchor, and the range implied by persisted Scanner Room range modules. Identical snapshots are clean idempotent accepts, stopped scans cannot acquire `None` results, and erroneous client unload/range-exit removals are corrected from live server truth.
- Once canonical result state is initialized, non-owner clients suppress vanilla local-only discovery/removal mutations. Stable-ID live and synthetic results are deduplicated, while accepted equal-revision corrective snapshots can restore an accidentally removed live result.
- Vanilla's process-global camera-preview texture/selection now converges through one bounded JPEG publication per real exclusive control acquisition. The accepted image is revision-ordered and broadcast to every client, including its sender, so simultaneous exits cannot leave different last-image orderings. Preview state is presentation-only: it is not persisted and is not replayed during initial sync.
- Background simulation acquisition preserves an active exclusive camera-control lock instead of silently downgrading it; release and destructive cleanup revoke preview eligibility.
- Compact camera diagnostics include a distinguishing GUID segment, and preview publish/receive/apply/server decisions record only bounded metadata rather than image payloads.

## Authority decisions

- Server accepts state changes only from the room/camera simulation owner and publishes accepted revisions.
- Scan-result identity is `(room ID, generation, resource ID)`; target/range changes invalidate obsolete generations.
- Result traffic is sent to the owner and clients subscribed by an open Scanner Room UI, with an immediate canonical snapshot on subscribe.
- One camera may have one dock and one controller; docking, transfer, pickup, creature interaction, and death revoke conflicting ownership.
- Loose cameras remain entities; room deconstruction clears room state/locks/subscriptions and applies the established camera cleanup policy.

## Protocol and save impact

- Added protocol packets: `MapRoomCameraComponentState`, `MapRoomCameraPreview`, `MapRoomScanResultChanged`, `MapRoomScanResultSnapshot`, `MapRoomScanResultSubscription`, and `MapRoomScanTypesSnapshot`; existing camera control/dock/light packets gained authoritative state fields during the imported/custom evolution.
- `MapRoomScanResultSnapshot` now carries the owner query origin/range; `MapRoomScanTypesSnapshot` also carries the client's detectable TechType set. These incompatible schemas plus the preview packet advance the connection key to protocol epoch `2` (`nitrox-ai/2`).
- All clients and server must run this same fork build; older protocol peers are not compatible with these messages.
- `MapRoomEntity` now persists dock IDs/revision, camera records, scan generation/revision/results, available scan types/revision, and fabricator metadata.
- `MapRoomMetadata` persists scan generation/revision in addition to target/progress. Camera records persist number, light, energy, health, and component revisions.
- Fields are additive with empty/default fallback. Legacy camera state is normalized deterministically and repeatedly without creating new IDs. No standalone save-version bump or destructive save upgrader was added.
- Preview pixels/revisions and scan-query validation data are deliberately absent from the save model. Existing canonical scan results/types continue to use the established `MapRoomEntity` persistence fields.

## Verification

- Final command: `dotnet build Nitrox.slnx -c Release --nologo -m:1 -v:q`
- Final command: `dotnet test Nitrox.Test/Nitrox.Test.csproj -c Release --no-build --nologo -m:1`
- Result for this follow-up: build succeeded with 0 errors and 42 existing analyzer warning emissions across the `net10.0`/`net472` targets; `587 passed, 8 skipped, 0 failed` (`595 total`).
- The combined Scanner Room/Map Room/simulation-ownership/protocol selection passed `275/275`; the scan-focused authority, packet, and client-result selection passed `79/79`; preview-focused coverage passed `24/24`; the broader camera/ownership regression selection passed `122/122`.
- Automated coverage includes authority/stale replay, serialization/save models, result lifecycle, exact server supplements, override preservation, range/anchor validation, client unload/range-exit correction, initialized non-owner suppression, stable-ID deduplication, preview JPEG bounds and capture timing, all-client preview revision ordering, preview ownership/lifecycle, background-lock preservation, upgrades, topology, camera numbering/docking/control/components/migration, cleanup, popup suppression, and craft-power deduplication.

## Remaining limitation

- The 2–3 client in-game matrix in `SCANNER_ROOM_ACCEPTANCE.md` is `NOT RUN`; this environment cannot launch multiple licensed Subnautica clients. Until those rows pass, the repair is automated-test complete but not multiplayer acceptance signed off.
- The targeted two-client retest must still prove Unity GPU readback/JPEG decode/blit, physical screen image/label convergence, and nonzero Limestone/Shale results in `gottem`.
- Server supplements currently cover entities present in `EntityRegistry`. Lazy/unparsed world batches become eligible only after registration. Server scans are linear over the current registry; replace this with a spatial/TechType index only if profiling justifies it.
- Exact server matching cannot infer every vanilla `ResourceTracker.overrideTechType`. Preserving the owner-discovered union is intentional for Lead, fragments, eggs, databoxes, and similar mappings.
- The current build emits 42 existing analyzer warnings across target frameworks and retains 8 platform-dependent skipped tests; no new warning class was introduced.

## Final acceptance

Run `SCANNER_ROOM_ACCEPTANCE.md`, attach server/client logs and before/after saves, replace each `NOT RUN`, and set its sign-off result. Any failure should be fixed in a focused follow-up PR and the affected row rerun.
