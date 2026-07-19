# Base Power Tasks

## Done

- [x] Audit imported 1.15 power synchronization
- [x] Canonical per-source server state and revisions
- [x] Owner validation and stale-sequence rejection
- [x] Fixed capacities for solar, thermal, bioreactor, and nuclear sources
- [x] Owner-only source simulation, including thermal plants
- [x] Save/late-join metadata with legacy fallback
- [x] Reject old generic power metadata updates
- [x] Persist initial source metadata on new construction
- [x] `basepower` server snapshot command
- [x] `basepower true|false` live trace toggle
- [x] Authority, capacity, handoff, replay, and client revision tests
- [x] Backward-compatible loading of legacy one-field power metadata
- [x] Suppress premature battery metadata broadcasts during entity spawning
- [x] Suppress redundant metadata from temporary Scanner Room camera battery ids
- [x] Persist and synchronize partial bioreactor and nuclear fuel consumption
- [x] Show remaining reactor fuel energy and full-output runtime on hover
- [x] Include reactor fuel progress in `basepower` diagnostics
- [x] Suppress only initial-reconciliation base-power voice callbacks while preserving relay state and later live announcements
- [x] Add bounded `[BPD1]` client evidence for source reconciliation and base-power audio transitions
- [x] Exclude redundant Map Room camera battery prefab-child records while retaining the authoritative camera energy record
- [x] Record the latest e59 run as a passive false-audio non-observation only; no deliberate base-power test or `[BPD1]` audio-decision evidence was produced

## Test next (when base-power work resumes)

- [ ] No base-power test in the next short Scanner Room camera retest; base-power work is deferred at the owner's request
- [ ] Nuclear baseline, remote fabrication, owner disconnect, rejoin, restart
- [ ] Simultaneous two-client consumption
- [ ] Solar day/night generation
- [ ] Thermal plant and transmitter chains
- [ ] Bioreactor fuel use/removal
- [ ] Nuclear rod persistence/depletion
- [ ] Battery and power-cell chargers
- [ ] Water filtration and moonpool charging
- [ ] Base split/merge, generator build/deconstruction, multiple bases
- [ ] Long session and packet/log review

## Later

- [ ] When base-power testing resumes, run the dedicated `gottem` first-join/late-join/rejoin smoke and a deliberate live cut/restore control with both client logs
- [ ] Fix failures found by multiplayer acceptance
- [ ] Final full build/tests, PR, and test release
