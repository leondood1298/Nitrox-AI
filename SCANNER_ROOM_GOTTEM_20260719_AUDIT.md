# `gottem` Scanner Room Audit — 2026-07-19

## Evidence identity

- Package: `scanner-room-8bb2e98f4074-qae93f7ee-20260719T043646Z-win-x64`
- Source commit: `8bb2e98f4074940d6e72c62d78e4331a49280664`
- Subnautica build: `83031`
- Authoritative evidence archive: `20260719-115743493Z-owner-main-server-scanner-room-8bb2e98f4074-qae93f7ee-20260719T043646Z-win-x64.zip`
- Authoritative archive SHA-256: `AC64F528362E606A749E21C1F68CF03B9A1F87CDAA7493DFD10F80427EBDF8DD`
- The earlier `20260719-115705390Z-...zip` archive is superseded. Its collection step treated the brackets in `game[leon]-20260719.log` as a wildcard and omitted that host-client log. Its SHA-256 is `7BDD44BAC92B9E5A6FA9492392788E5F88A9F13086AC0C95BCEB1D7443651D3E`.

The server, host-client, game, and launcher logs plus the current `gottem` save were copied only after Nitrox and Subnautica had exited. The live save was not modified.

## Result

The replacement build fixed the two defects found in the previous exploratory run:

- No healthy camera component was rejected. Camera 2 legitimately persisted at `400` health.
- Every load/save checkpoint was valid and used the same registered-room snapshot fingerprint. There was no `save_drift`, `load_invalid`, `save_live_invalid`, or invalid-value invariant.

The new observations are both confirmed:

- Camera authority, movement, dock, light, and release state converged, but the physical preview screen has no Nitrox replication path. The mismatch is a presentation-state defect rather than a camera-control failure.
- The scan-result owner actually submitted empty Shale and Limestone snapshots, and the server accepted them. This is an upstream discovery failure on the owner's client, not packet loss after discovery.

## Scanner Room timeline

| Time | Accepted event/result |
|---|---|
| 07:45:05 | `Lead`, 1 result |
| 07:45:41 | Host acquired camera 2, undocked/docked, then released |
| 07:46:04 | Laptop acquired camera 2, undocked, toggled light on/off, docked, then released |
| 07:46:29 | `UraniniteCrystal`, 1 result |
| 07:47:21 | `ShaleChunk`, 0 results |
| 07:48:01 | `LimestoneChunk`, 0 results |
| 07:48:42 | `JellyPlant` (Gel Sack), 3 results |
| 07:48:46 | `Fragment`, 0 results |
| 07:48:59 | `GenericEgg`, 0 results |

Each scan was subsequently stopped with target `None`; the empty final persisted result list is therefore expected.

## Saved-world correlation

The room has four speed modules and no range module, so its effective range is the default `300 m`. Using the midpoint of the two saved dock-camera positions as the room center (approximately `-881.34, -185.51, -725.38`), the authoritative server entity registry contains:

| Tech type | Entire saved world | Within 300 m |
|---|---:|---:|
| `LimestoneChunk` | 5,082 | 103 |
| `ShaleChunk` | 3,094 | 162 |
| `JellyPlant` | 617 | 23 |
| `UraniniteCrystal` | 1,001 | 66 |
| `SandstoneChunk` | 4,220 | 34 |
| `Quartz` | 4,872 | 129 |
| `Salt` | 3,837 | 105 |

The closest saved Limestone and Shale entities are about `76.6 m` and `93.4 m` from that center. Therefore range, upgrades, world population, server persistence, and downstream snapshot transport cannot explain the zero results. Vanilla `MapRoomFunctionality.ObtainResourceNodes` reads only the current client's `ResourceTrackerDatabase`; Nitrox currently republishes that incomplete client-local list without supplementing it from the server's complete world registry.

The cell layout supplies a second discriminator. Build 83031 level-0 cells are `16 m`; the owner had no saved Limestone or Shale entity inside the locally registered Chebyshev radius of four cells, and the first of each appears at radius five. Exactly one Uraninite entity falls inside radius four and the scan returned exactly one. The three returned Gel Sacks are the three cultivated `JellyPlant` entities in the base, while the natural Gel Sacks are outside that local registration radius. This aligns the observed counts with client cell registration, even though the Scanner Room's intended Euclidean range extends much farther.

The one Lead result also demonstrates why the repair must supplement rather than replace vanilla discovery. Its saved world entity has TechType `DrillableLead`, while its `ResourceTracker` override exposes scan TechType `Lead`. Direct server TechType matching cannot infer every vanilla override (`Lead`, generic fragments/eggs, databoxes, and similar cases), so owner-discovered vanilla results must remain in the canonical union.

## Warning and error classification

### Scanner Room

- Three `scan_types ... stale_or_invalid` rows at 07:44:19–07:44:21 were duplicate/stale defensive publications. They did not alter state or cause the result defect.
- A single host warning that camera 2 was temporarily unavailable was followed by the accepted local control application about 8 ms later. It is pending-control gate noise, not a failed control.
- All logged camera acquire, movement-role, dock, light, release, component, scan-target, and scan-result transitions were accepted. No Scanner Room divergence or invariant failure was logged.

### Actionable but outside the Scanner Room path

- Two `NullReferenceException` stacks occurred during host GlobalRoot initial sync at 07:44:16: one in `MoonpoolManager.RestoreMoonpools`, followed by one in `BuildEntitySpawner.SpawnAsync`. The join still completed, but these are genuine initial-sync defects for a separate tranche.
- The evidence summarizer counted same-day synthetic fixture rows from 00:01–00:18 as gameplay. The real 07:43–07:49 session contains 67 `[SRD1]` rows across two process epochs: 58 accepted, 6 checkpoints, and 3 logged rejects. The reported 1,033 rows/44 epochs came from mixing 966 earlier test rows into the session audit.
- Compact diagnostics shorten entity IDs to eight characters. Both saved camera IDs begin `99e1d312`, so camera-specific evidence is ambiguous when only the shortened ID is printed.

### Recovered or environmental

- LiteNetLib reported `AddressAlreadyInUse` for LAN discovery at 07:43:49. LAN discovery and connection then succeeded.
- The launcher reported `Nitrox entry point already patched`; the correct package and game build still launched.
- Server shutdown could not remove the UDP 11000 port-forward rule. The server otherwise shut down cleanly.

### Unrelated noisy warnings

- Missing Nitrox entity IDs were reported by FMOD, player cinematic, radio damage, Seamoth light, Warper, and oxygen metadata paths. They did not affect the exercised Scanner Room state.
- Two TextMeshPro dependency errors occurred during startup.
- Three `Language.main while application quitting` stacks and 17 destroyed-GameObject activation errors occurred only after the host began quitting.

## Coverage boundary

This was a useful two-client exploratory smoke using an existing save, not the formal acceptance matrix. It did not include the prescribed restart assertion, a third client, fault injection, or a post-save reload. The laptop's client log was not present on the host machine. No result in `SCANNER_ROOM_ACCEPTANCE.md` is promoted solely from this run.

## Remediation and qualification

- Hybrid discovery now supplements the vanilla owner list with exact-TechType entities from the server registry inside the validated room origin and installed range. Owner-only `ResourceTracker` overrides remain in the union, server positions are canonical for duplicate IDs, and output is bounded and deterministic.
- Live server truth now repairs erroneous ordinary unload/range-exit removals. Initialized non-owner clients suppress vanilla local-only discovery/removal mutations, and live/synthetic objects sharing a stable ID are deduplicated.
- Camera exit now captures the vanilla preview after `WaitForEndOfFrame`, downscales it to `256x256`, enforces a `64 KiB` JPEG limit, and submits it once per exclusive acquisition. The server validates ownership and camera identity, assigns a global session revision, and broadcasts the accepted image to all clients including its sender. Each client applies only increasing revisions to the shared preview texture, selected-camera label, and loaded room screens. Preview data remains ephemeral and is not saved or replayed to late joiners.
- Full Release qualification passed `587` tests, skipped `8` pre-existing platform-specific tests, and failed `0` (`595` total). The combined Scanner Room/Map Room/simulation-ownership/protocol selection passed `275/275`; scan-focused coverage passed `79/79`; preview-focused coverage passed `24/24`; and the broader camera/ownership selection passed `122/122`.
- The Release solution build completed with `0` errors and `42` existing analyzer warning emissions across target frameworks. The formal multiplayer matrix remains `NOT RUN`; the next package needs only the targeted two-client `gottem` smoke before broader acceptance work.

## Subsequent targeted retest and log follow-up

The owner subsequently reported that cameras, synchronized preview screens, and scans behaved correctly in another live `gottem` retest. This remains an exploratory result: the full prescribed matrix, restart assertions, third simultaneous client, and impairment rows were not run, so no `SCANNER_ROOM_ACCEPTANCE.md` row is promoted.

The host-client log exposed one new Scanner-adjacent orange error when undocked camera `72cf` loaded. A stale generic battery `PrefabChildEntity` could not find a matching prefab child, but the authoritative `MapRoomCameraRecord` then initialized the same camera with approximately `99.84` energy. Future saves now omit only this redundant Map Room camera battery child, and the legacy restore path quietly discards only that exact combination. Ordinary battery children retain their existing path.

The same retest intermittently played "base out of power" followed quickly by "power restored" during client loading even though base power synchronized correctly. Build-83031 inspection confirmed those two relay callbacks are presentation-only. The follow-up suppresses them only for relays created during initial reconciliation and a two-second scaled-time settle window, adds bounded `[BPD1]` evidence, and preserves real later outage/restoration audio. This is documented in `BASE_POWER_AUDIO_REPORT.md`; its targeted live smoke remains `NOT RUN` on the new build.

The paired `MoonpoolManager.RestoreMoonpools` and `BuildEntitySpawner.SpawnAsync` null-reference signatures remain unrelated and out of scope for this follow-up.

## e59 scan/preview and camera follow-up

### Evidence identity and result

- Package: `scanner-room-e59b0237f9db-q021dbc84-20260719T185635Z-win-x64`
- Source commit: `e59b0237f9dbf9b50ac42b6930890676998c0cb4`
- Package ZIP SHA-256: `31EEC927AF1B34F2D8B43A415C28F6A6E594225C87486C85E50E379E3E129E28`
- Server/save: `gottem`; Subnautica build `83031`; server and host-client logs were available, but the laptop client log was not present on the host machine.
- Hybrid scanning worked in the exploratory run: accepted results were Limestone `101`, Uraninite `66`, and Wreck `1`. Shared camera-preview publications advanced through accepted revisions `1`, `2`, and `3`.

The scan/preview defects described earlier in this audit therefore did not recur. Two narrower camera-state defects were observed instead. While client 1 controlled a camera, client 2 intermittently saw client 1's player at an incorrect position rather than fixed at the Scanner Room console. Exiting control restored the observed location; the drift was not reproduced again. Client 1 also could not switch to the other loose camera after world load.

### Correlation and root cause

Only camera `.72cf` received control requests in the e59 session. An earlier session successfully switched `.8c30` to `.72cf` and back, which demonstrates that exclusive-control handoff and camera cycling work when both cameras are controllable.

The unavailable camera `.8c30` had saved energy `99.64972`, health `100`, and component revision `38`. It spawned late without its room record applied, initialized from prefab defaults at energy `0` and health `400`, and published that state after queued ownership was applied. The server accepted the default component state as revision `39`. Vanilla correctly omitted the zero-energy camera from selectable controls, so no switch request for `.8c30` was generated. This is a restore-ordering overwrite, not a control-handoff rejection.

The intermittent observer drift has a separate mechanism. Vanilla scanner-camera control locks the physical player at the console and moves only the camera root, but the multiplayer movement broadcaster previously suppressed player packets while the main camera was disabled/piloting. An observer could therefore continue extrapolating the last velocity until camera exit resumed publications and corrected the body.

### Repair

- During scanner-camera control, the controlling client now publishes the physical player's console transform with zero velocity. Drone movement remains on the camera path. Bounded `[SRD1] player_body_pin` enter, switch, identify, and exit rows expose each anchor lifecycle without logging camera imagery.
- Canonical camera record, light, component, and camera-number state is retained in a durable per-camera cache even when the loose object does not yet exist. State is re-armed for later respawns.
- Map Room camera entities loaded during initial synchronization receive a restore barrier even when their room record has not arrived yet. Prefab light/component defaults cannot publish through that barrier; canonical component application releases it.
- Battery initialization is tied to the current camera object and generation so a stale coroutine cannot clear suppression for a replacement instance. Applied canonical values seed local broadcast history and avoid an immediate echo.

Final automated qualification passes: focused Release and Debug selections each passed `40/40`, the broader relevant selection passed `352/352`, and the full Release suite passed `650` with `8` pre-existing platform skips and `0` failures (`658 total`). The Release solution build completed with `0` errors and `42` existing analyzer warning emissions; packet-processor DI resolution, the Windows PowerShell 5.1 evidence-summary self-test, and `git diff --check` also passed. Independent review found no remaining P0–P2 issue. The replacement immutable package remains `PENDING` until the clean commit is pushed and packaged.

### Base-power and acceptance boundary

The owner reported that the false outage/restoration audio seemed resolved in the e59 run. No deliberate generation cut/restore or other base-power test was performed, and the available client log contains no `[BPD1]` audio-decision row. Record this only as a passive non-observation; the targeted audio smoke and every base-power matrix row remain unchanged.

This remains an exploratory two-client smoke. The formal Scanner Room matrix, third-client work, impairment rows, prescribed restart assertions, and formal before/after save pair remain `NOT RUN`. The next package requires only a short reload/control retest with both loose cameras: retain charge/health/selectability, switch A-B-A, keep the observer-visible player body at the console, exit normally, and preserve the server plus both client logs. No full matrix, PowerShell evidence script, or deliberate base-power test is required for that retest.
