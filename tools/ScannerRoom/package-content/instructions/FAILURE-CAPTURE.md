# Failure and final-pass evidence capture

Do not retry, reconnect, save, reset, or restart first. Evidence is most useful while the bad state still exists and no save write is in progress.

1. Record the row, machine, UTC time, last action, expected state, and visible state:

   ```powershell
   .\scripts\Mark-Step.ps1 -TestId D4 -Phase fail -Machine B -Note 'camera A visible twice after slot race'
   ```

2. In the server console run `scannermark D4-failure` and `scannerroom`.
3. Take screenshots and copy them into the printed run data path's `screenshots` folder. Avoid including unrelated desktop content.
4. On every involved machine collect from the data path printed by its start script. `$syncedInbox` must be the Google Drive package inbox saved during installation:

   ```powershell
   .\scripts\Collect-Evidence.ps1 -Role server -MachineLabel M1 -DataPath "$env:LOCALAPPDATA\Nitrox-AI-TestRuns\<package-id>\server" -OutputInbox $syncedInbox -FailureNote 'D4 slot race'
   .\scripts\Collect-Evidence.ps1 -Role client -MachineLabel B -DataPath "$env:LOCALAPPDATA\Nitrox-AI-TestRuns\<package-id>\client-b" -OutputInbox $syncedInbox -FailureNote 'D4 duplicate camera'
   .\scripts\Collect-Evidence.ps1 -Role proxy -MachineLabel B -DataPath "$env:LOCALAPPDATA\Nitrox-AI-TestRuns\<package-id>\proxy-b" -OutputInbox $syncedInbox -FailureNote 'N1 impairment profile'
   ```

   Omit the proxy command when the proxy was not involved. The collector refuses an implicit local inbox so evidence cannot silently remain outside Google Drive.

5. Read each bounded `.zip.summary.txt` sidecar first. Confirm `ValidationIssues=0` unless the reported issue is the failure being investigated.
   `ServerSampledSequenceGaps` is advisory because repetitive server rejections are deliberately sampled; `ClientSequenceGaps`, invalid epochs, or sequence conflicts are evidence-integrity problems.
6. Confirm each ZIP has a matching `.zip.sha256` file. The summary sidecars are advisory; the hashed ZIP is authoritative.
7. Let Google Drive finish syncing. Only then retry, reconnect, save, reset, or restart.

For a completely successful run, use an empty `-FailureNote` and collect the same server/client/proxy roles after the `FINAL-pass` markers.

Text logs, configuration metadata, and user-profile paths are pattern-redacted in the evidence snapshot. Binary crash files and game save data may still contain player/world identifiers; keep the synced evidence folder private. Backups are excluded, and the collector warns when the uncompressed snapshot is unusually large.
