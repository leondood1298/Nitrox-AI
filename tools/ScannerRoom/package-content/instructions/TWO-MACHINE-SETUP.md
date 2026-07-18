# Two-machine setup

Use the same verified archive on both Windows x64 machines. Machine 1 hosts the server and client A; Machine 2 runs client B. Client C is a fresh late-join profile on Machine 2 after B exits.

## Preconditions

- Both machines must run the exact Subnautica build recorded as `GameBuildQualifiedLocally` in `BUILD_INFO.json`. `Start-Server.ps1` and `Start-Client.ps1` enforce this against the supplied game path.
- Disable unrelated Subnautica mods and do not accept a Nitrox launcher update during testing.
- Synchronize both Windows clocks before the run. `w32tm /resync` from an elevated console is suitable; Windows Settings > Time & language > Sync now is also sufficient.
- Use distinct stable Nitrox player names: `ScannerA`, `ScannerB`, and `ScannerC`. Record any different names in the first TEST marker.
- Preserve the synced package `evidence-inbox` path. Evidence collected under the local installation does not sync unless `NITROX_SCANNER_EVIDENCE_INBOX` or `-OutputInbox` points back to Google Drive.

## Install on each machine

From the extracted, verified package folder:

```powershell
$syncedPackage = (Get-Location).Path
$syncedInbox = Join-Path $syncedPackage 'evidence-inbox'
.\scripts\Verify-Package.ps1
$install = .\scripts\Install-Local.ps1
Set-Location $install
$env:NITROX_SCANNER_EVIDENCE_INBOX = $syncedInbox
```

Do not use `-Refresh` unless intentionally replacing the verified local copy. In every new PowerShell window either set the same environment variable again or pass `-OutputInbox $syncedInbox` when collecting evidence.

If Subnautica is not in the default Steam path, pass its actual root through `-GamePath` to both start scripts and select that same installation when the fresh launcher profile asks.

## Machine 1: server

Find the active LAN IPv4 address with `Get-NetIPAddress -AddressFamily IPv4`. In one PowerShell window:

```powershell
.\scripts\Start-Server.ps1 -ResetData
```

Allow the Windows firewall prompt for private networks. Wait for `Server is listening on port 11000 UDP`. Keep this console visible; it accepts `save`, `stop`, `scannerroom`, and `scannermark D1-before-restart`.

`-ResetData` deletes only this package's isolated server data. Omit it when restarting the same acceptance save. `stop` returns to PowerShell without closing the window.

## Machine 1: client A

In a second PowerShell window, restore `$env:NITROX_SCANNER_EVIDENCE_INBOX` if needed, then:

```powershell
.\scripts\Start-Client.ps1 -ServerAddress 127.0.0.1 -MachineLabel A -ResetData
```

Use player name `ScannerA`, select the validated Subnautica installation if prompted, and use the game server entry named `Scanner Acceptance`.

## Machine 2: client B and late-join C

Replace the sample address with Machine 1's LAN IPv4 address:

```powershell
.\scripts\Start-Client.ps1 -ServerAddress 192.168.1.25 -MachineLabel B -ResetData
```

Use player name `ScannerB`. For a clean late join, exit both the B game and launcher, then start `-MachineLabel C -ResetData` and use player name `ScannerC`. A two-machine run cannot prove a three-simultaneous-client row; record that limitation instead of calling it a three-client pass.

## N1 deterministic impairment on Machine 2

The package verifier self-tests the proxy. In a separate Machine 2 PowerShell window, point it at Machine 1's real LAN address:

```powershell
.\scripts\Start-NetworkImpairmentProxy.ps1 -ServerAddress 192.168.1.25 -MachineLabel B
```

The fixed acceptance profile is 120 ms delay, +/-30 ms seeded jitter, 2.00% loss, every twentieth accepted packet reordered, 250 ms reorder hold, and seed 1425. The proxy listens on UDP 11001 and accepts exactly one learned client endpoint. Start the impaired client with Machine 2's LAN IPv4 address and port 11001:

```powershell
.\scripts\Start-Client.ps1 -ServerAddress 192.168.1.30 -Port 11001 -MachineLabel B -ResetData
```

Do not connect A or another client through the proxy. Press Ctrl+C once after the row and wait for its final `[NIP1] ev=stop` line. Its data path and log path are printed for evidence collection.

## Efficient canonical setup

From the server console, include the target player name:

```text
gamemode creative ScannerA
```

Build the canonical base and two Scanner Rooms without gathering materials, then run `gamemode survival ScannerA` before power, energy drain, damage, or death checks. Keep one untouched room and one mutation room. Run `scannerroom` and record room/camera fingerprints after setup.
