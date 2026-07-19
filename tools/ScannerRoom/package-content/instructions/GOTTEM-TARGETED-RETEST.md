# `gottem` targeted follow-up retest

This is a short two-client smoke for the fixes prompted by the latest live session. It does not replace the full Scanner Room or base-power acceptance matrices.

## Setup

- Use this exact package for the server and both clients, Subnautica build `83031`, and the existing server named `gottem`.
- Preserve the current world. No fresh Scanner Room, proxy, three-client setup, or full test battery is required.
- Keep the server console and both client logs. Run `scannerroom` and `basepower` once before the checks and once afterward.

## Checks

1. **Scanner regression:** Have each client control and release one camera. After exit, the same last camera image and selected-camera label should appear on both physical preview screens. Run one Limestone or Shale scan and confirm the known nearby outcrops appear; a brief Gel Sack scan is enough as a comparison.
2. **Loose-camera restore:** Leave camera `72cf` undocked, rejoin one client, and confirm the camera retains its charge and works. The client log must not contain the legacy missing-prefab-child error for that camera battery.
3. **Load audio:** Join client A, late-join client B, then rejoin B once. None of those load boundaries should play a false "base out of power" immediately followed by "power restored."
4. **Live audio control:** Deliberately remove or disable all generation long enough for the base to lose power, then restore it. The genuine outage and restoration announcements must still play. Restore the base to its original healthy state afterward.

## Evidence and pass boundary

- Preserve the server and both client logs. `[SRD1]` covers Scanner Room transitions; `[BPD1]` covers base-power source applies and audio decisions.
- Expected `[BPD1]`: load callbacks use `out=suppress`; deliberate live callbacks use `out=pass`; `out=missing` or an unexpected trace gap needs review. `out=truncated` reports a bounded evidence cap and is not by itself a gameplay failure.
- Stop on the first mismatch and use `FAILURE-CAPTURE.md`. If all four checks pass, collect one final evidence bundle.
- Report this as the targeted follow-up smoke only. Leave every formal acceptance-ledger row unchanged.
