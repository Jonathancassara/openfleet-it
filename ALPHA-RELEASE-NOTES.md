# OpenFleet IT v0.1.0-alpha.1

## Included

- English/French Windows 11-style WPF interface.
- Local and remote Windows, firewall, restart, software and driver inventory.
- Local Microsoft Store/MSIX inventory.
- Defender, BitLocker, TPM and Secure Boot status.
- IPv4 discovery with reverse DNS.
- Read-only WinGet update detection.
- Confirmed local WinGet updates and allow-listed MSI repair/uninstall actions.
- Confirmed logoff, shutdown, reboot and cancellation controls.
- Optional user-supplied PsExec connectivity probe.
- SHA-256 chained local audit journal.
- Self-contained Windows x64 executable.

## Known alpha limitations

- The executable is not digitally signed. Windows SmartScreen may warn until a trusted code-signing certificate is used.
- WinGet must be installed through Microsoft App Installer and its execution alias must be enabled.
- Software-changing actions are local-only.
- Remote WMI, Registry and power actions depend on Windows permissions and firewall configuration.
- Remote logoff is unavailable.
- The audit chain is tamper-evident but is not protected by a hardware-backed key or centralized SIEM.
- Microsoft Graph, Intune, Entra ID, RBAC and the optional signed OpenFleet agent are not part of this alpha.

## Verification performed

- Release solution build with zero warnings and zero errors.
- Alpha parser/action tests pass.
- Self-contained Windows x64 publication succeeds.
- Framework-dependent runtime smoke test remained running for five seconds and was stopped cleanly.
- Published executable SHA-256: `83FDC5F56C67CCE0C99E82694DD95E9C220A0A3C2AC09F9BF21251E1D52DCD74`.

The checksum must be regenerated if the executable is rebuilt.

The unsigned self-contained executable could not be launched in the build environment because its Windows application-control policy blocks unsigned newly generated executables. This policy was not bypassed. Sign the release with a trusted Authenticode certificate before production distribution.
