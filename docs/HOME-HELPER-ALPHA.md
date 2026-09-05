# OpenFleet Helper 0.2 alpha foundation

OpenFleet Helper is an optional, read-only Windows companion for family and small-workgroup PCs. It does not accept PowerShell, uploaded scripts, software changes, shutdown, restart or logoff commands.

## Current security boundary

- HTTPS is mandatory on port `47831`.
- The pairing code is generated locally, expires after five minutes, works once and is locked after five failed attempts.
- Pairing issues a unique one-year client certificate. `/v1/inventory` rejects requests without a paired certificate.
- The console operator must enter and compare the SHA-256 server fingerprint shown locally by the Helper before accepting the certificate. The **Home devices** panel pins that fingerprint and imports the issued client certificate into the current user's Windows certificate store.
- No administrator password is requested or stored.
- The supplied installer creates one inbound rule scoped to the Helper executable, TCP 47831, the Windows Private profile and `LocalSubnet`. The uninstaller removes that rule.
- The device identifier and paired public thumbprints live under `%LOCALAPPDATA%\OpenFleetIT\Helper`. CA and server private keys are non-exportable and persist in the current user's Windows certificate store. Nothing is written to the repository.

The Helper can run interactively for development or as the `OpenFleetIT.Helper` Windows service under the restricted `LocalService` identity. The same executable supports `--pairing-info`, which reads the current code and fingerprint through a loopback-only endpoint.

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

## Install on a remote Windows PC

The Helper release artifact contains the executable and both installation scripts. In an elevated PowerShell window opened inside that folder:

```powershell
.\Install-OpenFleetHelper.ps1
```

The installer copies the Helper to `%ProgramFiles%\OpenFleet IT\Helper`, registers automatic recovery and starts it as `LocalService`. It then displays the pairing information. To display a fresh running service's information later:

```powershell
& "$env:ProgramFiles\OpenFleet IT\Helper\OpenFleetIT.Helper.exe" --pairing-info
```

To remove the service, its executable and firewall rule while preserving the service identity for a safe reinstall:

```powershell
.\Uninstall-OpenFleetHelper.ps1
```

## Before a Home release

The desktop console still needs local-network discovery, unpair/revocation, rate limiting across restarts, DHCP reconnect tests, a packaged signed installer and disposable-VM validation of installation and upgrade paths.
