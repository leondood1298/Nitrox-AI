# Base Power Load-Audio Follow-up

## Observation

During the latest live `gottem` retests, the base remained powered and synchronized, but some client joins played "base out of power" followed almost immediately by "power restored." No durable power rollback or source rejection accompanied the announcements.

Build-83031 inspection shows that `BasePowerRelay.PowerDownEvent` and `PowerUpEvent` are presentation callbacks: they schedule the two voice notifications but do not mutate relay or source state. A newly restored relay can therefore observe its temporary pre-reconciliation state before inbound source metadata settles and produce a false audio pair even though canonical power converges correctly.

## Latest passive observation

In the subsequent e59 Scanner Room smoke, the owner did not hear the false outage/restoration pair and reported that base-power audio seemed resolved. This was not a deliberate base-power test: generation was not cut/restored, no base-power action sequence was run, and the available client log contains no `[BPD1]` audio-decision row. The observation is reassuring but cannot prove suppression during load or preservation of genuine later announcements. The dedicated audio smoke remains `NOT RUN` and is deferred from the next short camera retest at the owner's request.

## Narrow repair

- Relays whose vanilla `Start` runs while multiplayer initial synchronization is incomplete receive a per-instance, two-second scaled-time load window.
- Only the two vanilla base-power voice callbacks are skipped while that relay is still in initial reconciliation or inside its short settle window.
- Relay power, source metadata, ownership, revisions, events, packets, persistence, and server authority are untouched.
- The window is removed after it expires. A genuine later power loss/restoration therefore follows vanilla behavior and remains audible.
- `[BPD1]` client rows record bounded source applies and audio decisions with a process epoch, sequence, compact distinguishing IDs, initial/wait state, and explicit trace-limit markers. Source and audio entries have independent budgets so reconciliation noise cannot consume the audio evidence allowance.

## Adjacent Scanner Room warning cleanup

The same log audit found a separate orange camera warning. A loose Map Room camera had a stale generic `PrefabChildEntity` battery record, while its authoritative `MapRoomCameraRecord` restored the correct energy shortly afterward. Future camera serialization now omits only that redundant battery child, and existing saves quietly discard only the exact legacy camera-plus-`BatteryMetadata` combination. Ordinary tool and vehicle battery children are unchanged; no save migration or schema change is required.

The paired `MoonpoolManager.RestoreMoonpools` / `BuildEntitySpawner.SpawnAsync` null-reference exceptions are unrelated and remain a separately documented initial-sync defect.

## Automated qualification

The historical results below qualify the preceding base-power audio repair. The current Scanner Room player-body/loose-camera follow-up was also qualified independently: the focused Release and Debug selections each passed `40/40`, the broader relevant selection including base-power coverage passed `352/352`, and the full Release suite passed `650` with `8` pre-existing skips and `0` failures (`658 total`). Its Release build completed with `0` errors and `42` existing analyzer warning emissions.

- Debug targeted base-power and camera-policy selection: `48 passed / 0 failed`.
- Release base-power, Map Room, power-source serialization, and packet-processor selection: `252 passed / 0 failed`.
- Full Release suite: `610 passed / 8 skipped / 0 failed` (`618 total`). The eight skips are the existing platform/filesystem cases.
- Release solution build: `0` errors and `42` existing analyzer warning emissions across `net10.0` and `net472`.
- The Windows PowerShell 5.1 evidence-summary self-test passed mixed `[SRD1]`/`[BPD1]` deduplication, epoch separation, malformed/conflicting-row isolation, and minimum byte-cap preservation.

The short real-game audio smoke in `BASE_POWER_ACCEPTANCE.md` remains `NOT RUN`. It is not part of the next Scanner Room camera retest. No existing base-power or Scanner Room matrix row is promoted by the automated results.
