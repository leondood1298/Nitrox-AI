# Scanner Room Repair Report

Current status (2026-07-24): [PR #54](https://github.com/leondood1298/Nitrox-AI/pull/54) merged the owner-authorized intermediate milestone to `master`, and [Nitrox AI Custom Build 1.16.25](https://github.com/leondood1298/Nitrox-AI/releases/tag/custom-build-1.16.25-final) was published from exact commit `a4c9ed6f5347de888d2c831ed42933b38297ddab`. Exact-master automated, package, fresh extraction/install, two-boot isolated server, port-release, and isolated launcher gates passed. The owner waived the live post-save recovery restart and formal matrix for this release only. Phase 1 remains open, every formal row remains `NOT RUN`, the held-camera offset remains accepted/deferred cosmetic debt, and development continues on `agent/nitrox-ai-development`.

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
- Loose Map Room cameras no longer serialize a duplicate generic battery child because energy is already authoritative in `MapRoomCameraRecord`; existing saves quietly ignore only that exact stale camera-battery combination.
- During scanner-camera control, the controlling client continues publishing the physical player's console position with zero velocity instead of suppressing all player movement packets. The drone camera still moves independently. Bounded `[SRD1] player_body_pin` enter, switch, identify, and exit rows make the observer-facing anchor auditable.
- Loose-camera record, light, energy, and health state now survive room-before-camera and camera-before-room restoration. A durable per-camera cache and initial-load restore barrier prevent prefab defaults from publishing before canonical component state is applied; battery initialization is tied to the current camera instance/generation so a stale coroutine cannot release suppression for a replacement object.
- An adjacent base-power presentation guard suppresses false outage/restoration voice callbacks only while newly loaded relays reconcile. It does not alter Scanner Room drain, relay state, source authority, or persistence.

## Authority decisions

- Server accepts state changes only from the room/camera simulation owner and publishes accepted revisions.
- Scan-result identity is `(room ID, generation, resource ID)`; target/range changes invalidate obsolete generations.
- Result traffic is sent to the owner and clients subscribed by an open Scanner Room UI, with an immediate canonical snapshot on subscribe.
- One camera may have one dock and one controller; docking, transfer, pickup, creature interaction, and death revoke conflicting ownership.
- Loose cameras remain entities; room deconstruction clears room state/locks/subscriptions and applies the established camera cleanup policy.

## Protocol and save impact

- Added protocol packets: `MapRoomCameraComponentState`, `MapRoomCameraPreview`, `MapRoomScanResultChanged`, `MapRoomScanResultSnapshot`, `MapRoomScanResultSubscription`, and `MapRoomScanTypesSnapshot`; existing camera control/dock/light packets gained authoritative state fields during the imported/custom evolution.
- `MapRoomScanResultSnapshot` now carries the owner query origin/range; `MapRoomScanTypesSnapshot` also carries the client's detectable TechType set; and `MapRoomScanResultChanged` now carries the range-exit flag plus query origin/range. These incompatible schemas plus the preview packet advance the connection key to protocol epoch `2` (`nitrox-ai/2`).
- All clients and server must run this same fork build; older protocol peers are not compatible with these messages.
- `MapRoomEntity` now persists dock IDs/revision, camera records, scan generation/revision/results, available scan types/revision, and fabricator metadata.
- `MapRoomMetadata` persists scan generation/revision in addition to target/progress. Camera records persist number, light, energy, health, and component revisions.
- Fields are additive with empty/default fallback. Legacy camera state is normalized deterministically and repeatedly without creating new IDs. No standalone save-version bump or destructive save upgrader was added.
- Preview pixels/revisions and scan-query validation data are deliberately absent from the save model. Existing canonical scan results/types continue to use the established `MapRoomEntity` persistence fields.
- Omitting the redundant loose-camera battery child is a serialization cleanup, not a save-schema change. Existing camera records remain the authoritative energy source.

## Verification

The following results qualify the preceding e59 scan/preview package, not the new player-body/loose-camera restore changes:

- Final command: `dotnet build Nitrox.slnx -c Release --nologo -m:1 -v:q`
- Final command: `dotnet test Nitrox.Test/Nitrox.Test.csproj -c Release --no-build --nologo -m:1`
- Result for this follow-up: build succeeded with 0 errors and 42 existing analyzer warning emissions across the `net10.0`/`net472` targets; `610 passed, 8 skipped, 0 failed` (`618 total`).
- The Release base-power, Map Room, power-source serialization, and packet-processor selection passed `252/252`; the Debug base-power/camera-policy selection passed `48/48`.
- The Windows PowerShell 5.1 evidence-summary self-test passed mixed `[SRD1]`/`[BPD1]` parsing, independent counts, deduplication/epoch boundaries, validation isolation, and bounded-output stress.
- The combined Scanner Room/Map Room/simulation-ownership/protocol selection passed `275/275`; the scan-focused authority, packet, and client-result selection passed `79/79`; preview-focused coverage passed `24/24`; the broader camera/ownership regression selection passed `122/122`.
- Automated coverage includes authority/stale replay, serialization/save models, result lifecycle, exact server supplements, override preservation, range/anchor validation, client unload/range-exit correction, initialized non-owner suppression, stable-ID deduplication, preview JPEG bounds and capture timing, all-client preview revision ordering, preview ownership/lifecycle, background-lock preservation, upgrades, topology, camera numbering/docking/control/components/migration, redundant legacy camera-battery filtering, cleanup, popup suppression, initial-load audio boundaries, and craft-power deduplication.
- Current player-body/restore focused Release and Debug selections each passed `40/40`; the broader Scanner Room/Map Room/simulation-ownership/movement/base-power selection passed `352/352`.
- The current full Release suite passed `650`, skipped the same `8` platform-dependent cases, and failed `0` (`658 total`). The Release solution build completed with `0` errors and `42` existing analyzer warning emissions across `net10.0` and `net472`.
- Packet-processor DI resolution passed `1/1`, `git diff --check` was clean, the Windows PowerShell 5.1 evidence-summary self-test passed, and independent review found no remaining P0–P2 issue.
- Exact `70ef86d3` qualification supersedes the preceding current-source totals: the full Release suite passed `661`, skipped `8`, and failed `0` (`669 total`); focused empty-base/base-serialization passed `14/14`, existing Scanner Room passed `57/57`, player-anchor/restore Release and Debug passed `40/40` each, and the broader relevant selection passed `352/352`.
- Exact `70ef86d3` package verification passed: 585 manifested files plus the manifest, bundled runtime/proxy checks, ZIP checksum, fresh extraction, installed-copy verification, isolated launcher, and initial/restart server process probes.
- Released `master` commit `a4c9ed6f` repeated the full qualification after the documentation-only merge: build `0` errors / 42 existing warnings; full Release suite `661 passed / 8 skipped / 0 failed`; PowerShell parser/evidence tests and merge diff checks passed.
- Published package `scanner-room-a4c9ed6f5347-qc14d7889-20260724T214807Z-win-x64` passed 585 manifest entries, bundled runtime/proxy checks, ZIP checksum, fresh extraction/install, new-world and existing-world server boots, exact port ownership/release, zero server errors/NREs, and an eight-second isolated launcher check. ZIP SHA-256: `4BE44322257510A665F987A9BE55091888E257A6A184B9AB5A8E4F4FDC9E750C`.

## Remaining limitation

- The 2–3 client in-game matrix in `SCANNER_ROOM_ACCEPTANCE.md` is `NOT RUN`. The owner waived it and the post-save recovery restart only for this intermediate release; neither is multiplayer acceptance signed off and Phase 1 remains open.
- The exact `70ef86d3` exploratory smoke passed loose-camera restore/selectability, observer body anchoring, preview convergence, recharge recovery, pickup-forced release, and redocking. The picker briefly saw the held camera above/in front of the avatar until switching toolbar items; this self-clearing client-local presentation issue is accepted and deferred.
- The empty-base first-load/save recovery half passed, but the run used the live save rather than an isolated copy and no post-save restart was performed. Preserve those procedural boundaries.
- The owner passively heard no false base-power audio in the latest run, but did not deliberately test base power and the available log contains no `[BPD1]` audio-decision row. This is a non-observation only, not a base-power smoke pass.
- Server supplements currently cover entities present in `EntityRegistry`. Lazy/unparsed world batches become eligible only after registration. Server scans are linear over the current registry; replace this with a spatial/TechType index only if profiling justifies it.
- Exact server matching cannot infer every vanilla `ResourceTracker.overrideTechType`. Preserving the owner-discovered union is intentional for Lead, fragments, eggs, databoxes, and similar mappings.
- The current build emits 42 existing analyzer warnings across target frameworks and retains 8 platform-dependent skipped tests; no new warning class was introduced.

## Final acceptance

The owner authorized and published this intermediate release without declaring Phase 1 complete. The post-save restart, all formal matrix rows, formal save pair, and formal real-game result remain `NOT RUN`; the waiver must not promote or erase them. Resume those cases when Phase 1 acceptance work continues. Ongoing development remains on `agent/nitrox-ai-development`, synchronized from released `master`.
