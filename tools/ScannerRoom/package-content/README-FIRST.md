# Nitrox Scanner Room two-machine test build

This is one immutable Windows x64 bundle for the server, clients, and deterministic N1 network proxy. Do not run it directly from Google Drive. Use the ZIP plus its `.sha256` sidecar, wait for Drive sync to finish, verify the archive, extract it, and install a verified local copy.

Before extraction, from the synced folder:

```powershell
$archives = @(Get-Item .\Nitrox-AI-scanner-room-*.zip)
if ($archives.Count -ne 1) { throw "Expected exactly one test archive; found $($archives.Count)." }
$zip = $archives[0]
$expected = ((Get-Content "$($zip.FullName).sha256") -split '\s+')[0]
$actual = (Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
if ($actual -ne $expected) { throw 'Archive SHA-256 mismatch or incomplete Google Drive sync.' }
```

After extraction, open Windows PowerShell in the extracted package folder:

```powershell
.\scripts\Verify-Package.ps1
$syncedPackage = (Get-Location).Path
$syncedInbox = Join-Path $syncedPackage 'evidence-inbox'
$install = .\scripts\Install-Local.ps1
Set-Location $install
$env:NITROX_SCANNER_EVIDENCE_INBOX = $syncedInbox
```

Then:

1. Read `TEST_STATUS.md`. Final packaging requires the exact field `AUTOMATED_QUALIFICATION: PASS`; `MANUAL_MATRIX: NOT_RUN` is expected because the real-game rows remain for you.
2. Follow `instructions\TWO-MACHINE-SETUP.md` on both machines.
3. Run the short `instructions\GOTTEM-EMPTY-BASE-RECOVERY.md` first. This is the only requested check for this follow-up package.
4. Keep `instructions\SCANNER-ROOM-MATRIX.md` for later phase acceptance; do not infer a real-game pass from automated or targeted exploratory tests.
5. Capture evidence after every failure and once after a completely successful run.

All runtime data stays under `%LOCALAPPDATA%`, outside Google Drive. The bundled .NET runtime means the second machine does not need .NET installed.

## Launcher isolation

The launcher supports the packaged `--data-path` option. `Start-Client.ps1` quotes and passes the printed package-specific directory so launcher and game data remain isolated. Treat a missing isolated directory, an option-parser error, or an immediate launcher exit as a failure.

Do not accept launcher updates or replace files during this test. If Windows marks the verified extracted files as downloaded and blocks scripts, run `Get-ChildItem -Recurse -File | Unblock-File` only after both archive and package verification succeed.
