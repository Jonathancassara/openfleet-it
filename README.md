# OpenFleet IT

Source-available IT asset management and compliance dashboard for Microsoft Intune, Entra ID and on-premises Active Directory.

## Current prototype

The first desktop prototype is a native Windows application built with C#, WPF and .NET 10. It includes:

- a modern glass dashboard with Windows 11 Mica support;
- workstation details including Windows edition, version and build;
- firewall, last boot and pending-reboot status cards;
- a searchable, real installed-application inventory from the 64-bit, 32-bit and current-user Windows uninstall registry;
- safe prototype actions for repair, update and uninstall workflows;
- endpoint health and recent activity panels;
- persistent DNS suffix management for environment discovery;
- a bounded IPv4 range scanner with live-host results;
- an application update settings panel;
- English as the default interface language with complete French localization;
- persistent language selection in Settings;
- workstation targeting by hostname or IP address;
- live Windows information collection through WMI using the current Windows identity;
- real operating-system version/build, last-boot, firewall-profile and pending-restart detection;
- read-only repair and uninstall capability detection from registered installer metadata;
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

Dashboard health and activity panels still contain demonstration data. Microsoft Graph and Intune connectivity will be introduced behind a read-only connector. Software inventory is read-only and deliberately avoids `Win32_Product`, which can trigger MSI consistency checks.

## License

OpenFleet IT is source-available under the [PolyForm Strict License 1.0.0](LICENSE.md). Commercial use, redistribution, and derivative works are not permitted by that license.
