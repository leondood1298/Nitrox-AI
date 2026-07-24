# `gottem` camera follow-up retest

This is a short two-client smoke for the player-body anchor and loose-camera restore fixes. It does not replace the Scanner Room or base-power acceptance matrices.

## Setup

- Use this exact package for the `gottem` server and both clients on Subnautica build `83031`.
- Before reload, leave both Scanner Room cameras loose and note each camera's charge/health.
- Preserve the server log and both client logs. No fresh room, third client, full matrix, or PowerShell evidence script is required.

## Required checks

1. Reload/rejoin and confirm both loose cameras retain their prior charge/health and both remain selectable.
2. Client 1 controls camera A while client 2 watches client 1's physical player body. The body must remain at the Scanner Room console while the drone moves.
3. Client 1 switches A-B-A. Both cameras must take control normally; no camera should disappear from selection because it loaded at zero energy.
4. Client 1 exits camera control. Both clients must see ordinary player position/movement resume normally. Swap client roles only if convenient.

The earlier e59 run already confirmed hybrid scans and shared preview (Limestone `101`, Uraninite `66`, Wreck `1`; preview revisions `1-3`). One Limestone scan or preview glance is an optional sanity check, not another full test battery.

## Evidence and boundary

- `[SRD1] player_body_pin` should show bounded enter, camera identify/switch, and exit transitions. Delayed restore may also emit bounded `restore_apply` rows with `pending`, `ok`, or `delayed_object` outcomes.
- Treat a charged camera loading at zero energy, a battery-restore timeout/error, an unselectable loose camera, observer-body drift, or failure to resume normal movement as a mismatch. Stop and preserve the three logs.
- Do not deliberately cut base power in this retest. If the false outage/restoration audio happens incidentally, note the client and time; otherwise record only a passive non-observation.
- Leave every formal acceptance-ledger row unchanged.
