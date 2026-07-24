# Scanner Room Network Impairment Proxy

This is a deterministic, packet-opaque UDP proxy for the Scanner Room N1 acceptance gate. It forwards one learned client to one configured Nitrox server and applies the same impairment profile independently in both directions. It never decodes or modifies a packet.

## Build and verify

From the repository root:

```powershell
$proxyBuildRoot = Join-Path $env:TEMP 'Nitrox-ScannerRoom-NetworkProxy'
dotnet build .\tools\ScannerRoom\NetworkImpairmentProxy\NetworkImpairmentProxy.csproj -c Release `
  -p:BaseOutputPath="$proxyBuildRoot\bin\" -p:BaseIntermediateOutputPath="$proxyBuildRoot\obj\"
dotnet "$proxyBuildRoot\bin\Release\net10.0\ScannerRoom.NetworkImpairmentProxy.dll" --self-test
dotnet publish .\tools\ScannerRoom\NetworkImpairmentProxy\NetworkImpairmentProxy.csproj -c Release -r win-x64 --self-contained false `
  -o "$proxyBuildRoot\publish" -p:BaseOutputPath="$proxyBuildRoot\publish-bin\" `
  -p:BaseIntermediateOutputPath="$proxyBuildRoot\publish-obj\"
```

The local output paths avoid transient apphost locks in Google Drive folders. The self-test exercises the scheduling core with a manual clock and performs one real localhost client -> proxy -> echo server -> proxy -> client exchange. The framework-dependent publish has no third-party dependencies and is intended to run with the .NET 10 runtime bundled in the Scanner Room test package.

## N1 example

Run the proxy on a machine that can reach the real server, then point exactly one impaired Nitrox client at the proxy's listen endpoint. For a server at `192.168.1.20:11000` and a proxy machine at `192.168.1.30`:

```powershell
.\ScannerRoom.NetworkImpairmentProxy.exe `
  --listen 0.0.0.0:11001 `
  --server 192.168.1.20:11000 `
  --delay-ms 120 `
  --jitter-ms 30 `
  --loss-percent 2.00 `
  --reorder-every 20 `
  --reorder-hold-ms 250 `
  --seed 1425
```

Configure the impaired client to connect to `192.168.1.30:11001`. Do not connect another client to that endpoint: the first non-server sender is learned and all other source endpoints are counted as `foreign` and ignored. The host/server can continue to use its normal connection.

Press Ctrl+C to stop. Startup, client learning, periodic totals, and final totals use compact `[NIP1]` lines. `up` is client-to-server and `dn` is server-to-client. Each direction reports `rx/bytes`, `tx/bytes`, seeded `loss`, queue `overflow`, reordered pairs, expired unpaired holds, current queue depth, and send errors. There is deliberately no per-packet logging.

## Determinism and limits

- Upstream and downstream use separate streams derived from `--seed`, so traffic in one direction does not perturb decisions in the other.
- Seeded jitter is an integer in `[-jitter,+jitter]`; jitter cannot exceed base delay.
- Loss is capped at 50.00% and queue depth at 65,536 packets.
- `--reorder-every N` holds every Nth accepted packet and swaps it behind the next accepted packet. If no next packet arrives within `--reorder-hold-ms`, the held packet is released without reordering.
- Queue overflow drops are counted separately from configured loss.
- The listener and server must use the same IP family and the server must be a numeric unicast address.
