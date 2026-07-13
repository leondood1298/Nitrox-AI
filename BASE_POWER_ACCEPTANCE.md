# Base Power Multiplayer Acceptance

Use matching client/server builds. Join client 1 first unless a row says otherwise. Stop Scanner Room scans and remove other variable loads when isolating a test.

## Diagnostics

- Server console: `basepower` — canonical sources, power/capacity, revision, client sequence, owner, parent base, counters.
- Server console: `basepower true` — log every accepted update.
- Server console: `basepower false` — stop live trace.
- Capture `basepower` before and after each failure. Save the server log and both client logs.

## Matrix

| Test | Expected | Status |
|---|---|---|
| Legacy save first join | Existing power does not reset; sources migrate to typed revisioned state | PARTIAL — joined at zero; prior stored level unknown |
| Two-client idle nuclear base | Both clients show the same stable/recharging total | PARTIAL — nuclear generation appeared synchronized; exact values not recorded |
| Client 2 fabricates one item | One 5-power cost appears on both clients and server source state | NOT RUN |
| Both clients fabricate together | Both costs are accounted once | NOT RUN |
| Owner disconnect during recharge | Remaining client becomes owner and recharge continues | NOT RUN |
| Previous owner rejoins | Both clients converge without rollback | NOT RUN |
| Restart below full power | Stored source level is restored, then generation resumes | NOT RUN |
| Solar day/night | Generation and stored power agree on both clients | NOT RUN |
| Thermal plant direct connection | Generation and stored power agree on both clients | NOT RUN |
| Thermal transmitter chain | Connection, generation, unload/reload, and restart remain correct | NOT RUN |
| Bioreactor fuel cycle | Fuel inventory/consumption and power agree on both clients after restart | PARTIAL — insertion and activation synchronized; consumption/restart not tested |
| Nuclear rod cycle | Rod inventory/depletion and power agree on both clients after restart | NOT RUN |
| Battery/power-cell charger | Item charge and base drain occur once and agree | NOT RUN |
| Water filtration | Progress, output, and base drain occur once and agree | NOT RUN |
| Moonpool vehicle charging | Vehicle charge and base drain occur once and agree | NOT RUN |
| Scanner Room continuous drain | Scanner operation drains shared base power once | INCONCLUSIVE — 0.5/s load was below combined 5/s generation |
| Build/deconstruct generator | Capacity and canonical source list update on both clients | NOT RUN |
| Split/merge powered base | Sources reconnect to the correct relay without duplication | NOT RUN |
| Two nearby independent bases | Power never crosses between unrelated bases | NOT RUN |
| Late join after long session | New client receives current source and aggregate state | NOT RUN |

## Failure report

Provide the failed row, join order, exact actions, both displayed power values, `basepower` output before/after, and relevant logs.
