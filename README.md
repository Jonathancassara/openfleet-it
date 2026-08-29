# OpenFleet IT

Source-available IT asset management and compliance dashboard for Microsoft Intune, Entra ID and on-premises Active Directory.

## v0.1.0-alpha.1

OpenFleet IT alpha is a native Windows application built with C#, WPF and .NET 10. It includes:

- a modern glass dashboard with Windows 11 Mica support;
- workstation details including Windows edition, version and build;
- firewall, last boot and pending-reboot status cards;
- a searchable, real installed-application inventory from the 64-bit, 32-bit and current-user Windows uninstall registry;
- guarded MSI repair/uninstall and exact-ID WinGet update actions for the connected local computer;
- real Defender, BitLocker, TPM, Secure Boot, firewall and restart-state inventory;
- persistent DNS suffix management for environment discovery;
- a bounded IPv4 range scanner with live-host results;
- an application update settings panel;
- English as the default interface language with complete French localization;
- persistent language selection in Settings;
- workstation targeting by hostname or IP address;
- live Windows information collection through WMI using the current Windows identity;
- real operating-system version/build, last-boot, firewall-profile and pending-restart detection;
- read-only repair and uninstall capability detection from registered installer metadata;
- Microsoft Store/MSIX inventory for the connected local user;
- Plug-and-Play device and signed-driver inventory;
- confirmed logoff, shutdown, reboot and scheduled-action cancellation workflows;
- a local SHA-256 chained action journal;
- an optional user-supplied PsExec connection probe;
- a custom resizable window with native minimize, maximize and close actions.

## Run locally

Requirements: Windows 11 and the .NET 10 SDK.

```powershell
dotnet run --project OpenFleetIT.App
```

Build a release:

```powershell
dotnet build OpenFleetIT.slnx --configuration Release
```

Software inventory deliberately avoids `Win32_Product`, which can trigger MSI consistency checks. Remote software-changing actions, Microsoft Graph/Intune integration, RBAC and the optional signed OpenFleet agent are outside this alpha.

## Publish the standalone alpha

```powershell
dotnet publish OpenFleetIT.App/OpenFleetIT.App.csproj -p:PublishProfile=win-x64
```

The self-contained output is written to `artifacts/OpenFleetIT-v0.1.0-alpha.1-win-x64` and does not require a separate .NET installation.

## Current development

The `v0.1.1-alpha.1` stabilization work adds cancellable package actions, per-package concurrency protection and CSV/JSON software inventory export. The `v0.2.0-alpha.1` Home foundation adds shared protocol contracts and an optional read-only Windows Helper secured with HTTPS, one-time pairing and client certificates.

See [Home Helper alpha documentation](docs/HOME-HELPER-ALPHA.md) for its current security boundary and remaining work. The desktop application now includes a **Home devices** panel for fingerprint verification, pairing, certificate storage and read-only reconnection.

## Security

Read [SECURITY.md](SECURITY.md) before enabling remote administration. OpenFleet IT never bundles PsExec and does not request or store remote passwords.

## License

OpenFleet IT is source-available under the [PolyForm Strict License 1.0.0](LICENSE.md). Commercial use, redistribution, and derivative works are not permitted by that license.

## Feedback and contributions

OpenFleet IT currently accepts bug reports and feature requests through [GitHub Issues](https://github.com/Jonathancassara/openfleet-it/issues). External pull requests and modified distributions are not accepted. See [CONTRIBUTING.md](CONTRIBUTING.md) and [LICENSE.md](LICENSE.md).
