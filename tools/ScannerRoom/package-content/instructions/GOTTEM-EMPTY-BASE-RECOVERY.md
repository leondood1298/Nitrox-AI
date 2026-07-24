# `gottem` empty-base recovery retest

This is a short load, save, and restart check for one stale childless base record. It does not replace the Scanner Room or base-power acceptance matrices, and it does not ask you to repeat the completed camera-control smoke.

## Prepare an isolated copy

Use this exact package for the server and all clients on Subnautica build `83031`. Never run the recovery test against the only copy of `gottem`.

From the locally installed package folder, copy the existing save into this package's isolated run directory:

```powershell
$info = Get-Content .\BUILD_INFO.json -Raw | ConvertFrom-Json
$source = Join-Path $env:APPDATA 'Nitrox\saves\gottem'
$saveRoot = Join-Path $env:LOCALAPPDATA "Nitrox-AI-TestRuns\$($info.PackageId)\server\saves"
$destination = Join-Path $saveRoot 'gottem'
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Source save not found: $source" }
if (Test-Path -LiteralPath $destination) { throw "Isolated test save already exists: $destination" }
New-Item -ItemType Directory -Path $saveRoot -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $destination -Recurse
```

Start the server with `.\scripts\Start-Server.ps1 -SaveName gottem`, then start one or two matching packaged clients. Preserve the server log and every participating client log.

## First load

1. The server should emit one warning that it removed childless structurally empty base `e13c7f22-7b95-46c2-a1be-a33d8ad07002`.
2. Join the world and allow nearby bases to finish loading. Neither client log should contain the prior `NullReferenceException` pair from `MoonpoolManager.RestoreMoonpools` and `BuildEntitySpawner.SpawnAsync`.
3. Confirm the real Scanner Room base still exists. A quick room entry, camera-list glance, or short scan is enough; do not repeat the completed camera matrix.
4. In the server console, run `save` and wait for completion before stopping the server normally.

If the server exits before a successful save, the same migration warning on the next start is expected. It means the isolated source record has not yet been rewritten.

## Restart

1. Restart the same isolated `gottem` copy with the same package.
2. The empty-base removal warning should no longer appear.
3. Rejoin once. The prior exception pair must remain absent, and the valid Scanner Room base must still be present.

Stop on any mismatch and follow `FAILURE-CAPTURE.md` before retrying. A successful result closes only this empty-base recovery retest; every formal Scanner Room and base-power matrix row remains `NOT_RUN`.
