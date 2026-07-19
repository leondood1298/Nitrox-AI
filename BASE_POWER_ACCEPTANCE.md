# Base Power Multiplayer Acceptance

Use matching client/server builds. Join client 1 first unless a row says otherwise. Stop Scanner Room scans and remove other variable loads when isolating a test.

## Diagnostics

- Server console: `basepower` — canonical sources, power/capacity, reactor fuel progress, revision, client sequence, owner, parent base, counters.
- Server console: `basepower true` — log every accepted update.
- Server console: `basepower false` — stop live trace.
- Before a restart test, run `save` in the server console and wait for `World state saved`; closing the server does not force an immediate save.
- Capture `basepower` before and after each failure. Save the server log and both client logs.

## Targeted load-audio reconciliation smoke

Status: **NOT RUN on the follow-up build.** This short check does not replace or promote any matrix row below.

1. Start the existing `gottem` world with its healthy powered base and run `basepower`.
2. Join the first client, late-join the second client, then disconnect and rejoin the second client once. Loading must not play a false "base out of power" immediately followed by "power restored" announcement.
3. Deliberately remove or disable all generation long enough for the base to lose power, then restore generation. The real live outage and restoration must each remain audible.
4. Preserve both client logs. `[BPD1]` rows should show loading callbacks as `out=suppress`, the deliberate live transitions as `out=pass`, source reconciliation as `ev=source_apply`, and any bounded-log cap as `out=truncated`.

## Matrix

| Test | Expected | Status |
|---|---|---|
| Legacy save first join | Existing power does not reset; sources migrate to typed revisioned state | ACCEPTED — one-time legacy migration may begin at zero; typed state must persist after first explicit save |
| Two-client idle nuclear base | Both clients show the same stable/recharging total | PARTIAL — both clients resumed generation from the saved source state |
| Client 2 fabricates one item | One 5-power cost appears on both clients and server source state | NOT RUN |
| Both clients fabricate together | Both costs are accounted once | NOT RUN |
| Owner disconnect during recharge | Remaining client becomes owner and recharge continues | NOT RUN |
| Previous owner rejoins | Both clients converge without rollback | NOT RUN |
| Restart below full power | Stored source level is restored, then generation resumes | PASS 1.16.20 — bioreactor 119.65 and nuclear 216.48 restored before join; generation resumed |
| Solar day/night | Generation and stored power agree on both clients | NOT RUN |
| Thermal plant direct connection | Generation and stored power agree on both clients | NOT RUN |
| Thermal transmitter chain | Connection, generation, unload/reload, and restart remain correct | NOT RUN |
| Bioreactor fuel cycle | Fuel inventory/consumption and power agree on both clients after restart | PARTIAL — insertion and activation synchronized; consumption/restart not tested |
| Nuclear rod cycle | Rod inventory/depletion and power agree on both clients after restart | NOT RUN |
| Battery/power-cell charger | Item charge and base drain occur once and agree | NOT RUN — temporary camera battery metadata errors are confirmed fixed |
| Water filtration | Progress, output, and base drain occur once and agree | NOT RUN |
| Moonpool vehicle charging | Vehicle charge and base drain occur once and agree | NOT RUN |
| Scanner Room continuous drain | Scanner operation drains shared base power once | INCONCLUSIVE — 0.5/s load was below combined 5/s generation |
| Build/deconstruct generator | Capacity and canonical source list update on both clients | NOT RUN |
| Split/merge powered base | Sources reconnect to the correct relay without duplication | NOT RUN |
| Two nearby independent bases | Power never crosses between unrelated bases | NOT RUN |
| Late join after long session | New client receives current source and aggregate state | NOT RUN |

## Reactor fuel UI and persistence

1. With both clients present, look at each reactor. Both clients should see the same fuel item count, remaining energy, and estimated runtime.
2. Keep a reactor generating for at least two minutes. Remaining energy and runtime should decrease on both clients within about one second of each other.
3. Disconnect the current source owner while fuel is partly consumed. The remaining client should continue from approximately the same value without restoring fuel life.
4. Run `save`, capture `basepower`, restart, and run `basepower` before joining. `fuel-progress` should match the saved value.
5. Rejoin both clients. The hover estimate should resume from the saved value; it must not reset to a completely full current fuel item.
6. Let one bioreactor item or nuclear rod deplete. The inventory transition, remaining-energy display, and both clients should agree.

## Failure report

Provide the failed row, join order, exact actions, both displayed power values, `basepower` output before/after, and relevant logs.
