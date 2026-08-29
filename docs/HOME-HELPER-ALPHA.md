# OpenFleet Helper 0.2 alpha foundation

OpenFleet Helper is an optional, read-only Windows companion for family and small-workgroup PCs. It does not accept PowerShell, uploaded scripts, software changes, shutdown, restart or logoff commands.

## Current security boundary

- HTTPS is mandatory on port `47831`.
- The pairing code is generated locally, expires after five minutes, works once and is locked after five failed attempts.
- Pairing issues a unique one-year client certificate. `/v1/inventory` rejects requests without a paired certificate.
- The console operator must compare the SHA-256 server fingerprint shown locally by the Helper before accepting the certificate. The in-app confirmation/import workflow is still a TODO.
- No administrator password is requested or stored.
- The Helper creates no Windows Firewall rule. For testing, add a narrowly scoped inbound rule for TCP 47831 on the Private profile and local subnet only, then remove it after testing.
- The device identifier and paired public thumbprints live under `%LOCALAPPDATA%\OpenFleetIT\Helper`. CA and server private keys are non-exportable and persist in the current user's Windows certificate store. Nothing is written to the repository.

This alpha is an engineering foundation, not yet a background Windows service. Run it interactively so the owner of the remote PC can see and approve the pairing code.

## Endpoints

| Method | Path | Authentication | Purpose |
|---|---|---|---|
| GET | `/health` | HTTPS server fingerprint | Protocol, version and capabilities |
| POST | `/v1/pair` | Short-lived local code | Issue one controller certificate |
| GET | `/v1/inventory` | Paired client certificate | Read-only Windows, software and driver snapshot |

All responses set `Cache-Control: no-store`. Request bodies above 32 KiB are rejected.

## Run locally

```powershell
dotnet run --project .\OpenFleetIT.Helper\OpenFleetIT.Helper.csproj --configuration Release
```

The terminal prints the pairing code and server fingerprint. Do not post either value in an issue or log them in a shared system.

## Before a Home release

The desktop console still needs fingerprint confirmation, secure import into the Windows certificate store, local-network discovery, unpair/revocation, rate limiting across restarts, Private-profile firewall automation, DHCP reconnect tests and a signed Windows service installer.
