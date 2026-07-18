# Evidence inbox

This folder is intentionally mutable. Its README is package-hashed; only collector-created `.zip`, `.zip.sha256`, `.zip.summary.txt`, and `.zip.summary.json` files may be added without invalidating package verification.

Run `Collect-Evidence.ps1` with this synced path as `-OutputInbox`, or set `NITROX_SCANNER_EVIDENCE_INBOX` before collection. A package installed under `%LOCALAPPDATA%` is not synced automatically.

Read the bounded `.zip.summary.txt` and `.zip.summary.json` sidecars first. The ZIP plus its SHA-256 sidecar is the authoritative evidence; do not unpack or edit it before analysis. Wait for Google Drive to finish syncing every sidecar before reporting completion.
