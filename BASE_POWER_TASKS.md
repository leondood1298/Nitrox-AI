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

## Test next

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

- [ ] Fix failures found by multiplayer acceptance
- [ ] Final full build/tests, PR, and test release
