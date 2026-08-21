# OpenFleet IT

Source-available IT asset management and compliance dashboard for Microsoft Intune, Entra ID and on-premises Active Directory.

## Current prototype

The first desktop prototype is a native Windows application built with C#, WPF and .NET 10. It includes:

- a modern glass dashboard with Windows 11 Mica support;
- fleet, compliance and BitLocker overview cards;
- a searchable device inventory with demonstration data;
- compliance health and recent activity panels;
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

The current data is intentionally fictional. Microsoft Graph and Intune connectivity will be introduced behind a read-only connector.

## License

OpenFleet IT is source-available under the [PolyForm Strict License 1.0.0](LICENSE.md). Commercial use, redistribution, and derivative works are not permitted by that license.
