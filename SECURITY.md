# Security policy

## Supported version

Security fixes are currently provided for the latest `0.1.x-alpha` release only.

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could enable remote command execution, privilege escalation,
credential exposure or action-log bypass. Report it privately through GitHub Security Advisories for this repository.

Include the affected version, reproduction steps, impact and any suggested mitigation. Do not include real passwords,
tokens, private keys, customer hostnames or production IP addresses.

## Alpha security boundaries

- Remote inventory uses the current Windows identity and Windows-native WMI or Remote Registry permissions.
- Software-changing actions are restricted to the connected local computer.
- MSI operations require a validated ProductCode GUID.
- WinGet operations require an exact package ID returned by WinGet inventory.
- PsExec is never bundled. The optional connector uses a user-supplied Microsoft Sysinternals binary and an allow-listed probe.
- OpenFleet IT does not collect or store remote passwords.
- Power commands display the target and require explicit confirmation.
- The local audit journal is SHA-256 hash chained. It is tamper-evident, not a substitute for a protected enterprise SIEM.

The future OpenFleet agent, certificate authentication, RBAC and centralized audit service are outside this alpha.
