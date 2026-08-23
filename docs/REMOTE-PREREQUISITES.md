# Remote administration prerequisites

OpenFleet IT uses the Windows identity of the person running the application. It does not store remote credentials.

## Read-only WMI inventory

- The operator must have permission to query the remote WMI namespaces used by OpenFleet.
- Windows Firewall must allow the applicable Windows Management Instrumentation rules.
- RPC endpoint mapping and the negotiated WMI ports must be reachable.
- Defender, BitLocker and TPM providers may require local administrator rights on the target.

## Remote Registry inventory

- The Remote Registry service must be available on the target when remote software or Secure Boot registry data is requested.
- TCP 445/RPC access and the relevant firewall rules must be permitted by organizational policy.
- OpenFleet reads HKLM remotely. It does not load or impersonate another user's HKCU hive.

## Power commands

- Remote shutdown and reboot use the Windows `shutdown.exe /m` mechanism with the current identity.
- The identity must hold the remote shutdown privilege on the target.
- A 30-second delay is used so that an operator can run the cancel action.
- Remote logoff is intentionally unavailable in this alpha.

## Optional PsExec probe

PsExec is not distributed with OpenFleet IT. Select a Microsoft Sysinternals `PsExec.exe` that you obtained and licensed
directly from Microsoft. The alpha connector executes only `cmd /c echo OPENFLEET_PSEXEC_READY` after confirmation.
PsExec may create its temporary remote service during this probe.

## Recommended test environment

Use disposable Windows client or Windows Server virtual machines joined to a lab domain. Do not validate destructive
commands for the first time on a production workstation.
