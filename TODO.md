# OpenFleet IT — TODO

## Recommended delivery order

1. **v0.1.1 — Stabilization:** tests, cancellation, reliable error states, exports and installer quality.
2. **v0.2.0 — Home read-only:** securely pair and inventory family PCs on the same private network.
3. **v0.3.0 — Home maintenance:** carefully allow-list remote maintenance with confirmation and audit.
4. **v0.4.0 — Enterprise preview:** WinRM/Kerberos, Intune/Entra and Active Directory read-only integrations.
5. **v0.5.0 — Linux preview:** manage paired or enterprise Windows devices from an Avalonia console.

## Phase 1 — Read-only workstation inventory

- [x] Build the WPF/.NET 10 desktop shell.
- [x] Add English and French localization.
- [x] Add hostname/IP targeting.
- [x] Collect Windows version, build and last boot through WMI.
- [x] Collect firewall and pending-restart status when permissions allow it.
- [x] Add bounded IPv4 discovery and reverse-DNS hostnames.
- [x] Replace demonstration applications with the real Windows uninstall registry inventory.
- [x] Read 64-bit and 32-bit machine-wide application entries.
- [x] Read current-user application entries for local inventory.
- [x] Mark repair and uninstall capabilities from authoritative registry values.
- [x] Add Microsoft Store/MSIX package inventory for the connected local user.
- [x] Add read-only update availability detection through WinGet with graceful unavailable/error states.
- [x] Add installed Plug-and-Play device and driver-version inventory through WMI.
- [x] Add automated parser tests against representative WinGet outputs.

## Phase 2 — Safe local software operations

- [x] Introduce a dedicated, allow-listed action execution service.
- [x] Show an exact action preview and require explicit confirmation.
- [x] Add controlled administrator elevation.
- [x] Implement WinGet updates with exact package-ID validation.
- [x] Implement allow-listed MSI uninstall workflows by ProductCode.
- [x] Implement MSI repair only when Windows Installer metadata advertises it.
- [x] Capture exit code, standard output, timeout and final status when supported by elevation mode.
- [x] Persist a SHA-256 chained tamper-evident local action journal without secrets.
- [x] Add cancellation and prevent concurrent actions on the same package.

## Phase 3 — Remote administration

- [x] Keep remote inventory read-only by default.
- [x] Document WMI, Remote Registry and firewall prerequisites.
- [ ] Design a signed OpenFleet agent or constrained WinRM transport.
- [x] Add an optional, user-supplied PsExec connection probe without credential storage or tool redistribution.
- [ ] Add mutual authentication and certificate rotation.
- [ ] Add role-based access control and per-action authorization.
- [x] Add remote operation audit trails.
- [x] Avoid remote process creation through unrestricted WMI commands.

## Phase 4 — Engineering quality

- [ ] Refactor presentation logic to MVVM.
- [ ] Add unit tests for malformed, duplicate and incomplete Registry and WMI data.
- [x] Add parser tests for malformed, duplicate, localized and ANSI-formatted WinGet output.
- [ ] Add integration tests for inventory providers and command timeouts.
- [ ] Add automated UI smoke tests for launch, navigation, localization and empty/error states.
- [x] Add GitHub Actions build and test workflows.
- [x] Add Dependabot and CodeQL scanning workflows; rely on GitHub secret scanning for the public repository.
- [x] Add structured, privacy-safe local audit records.
- [ ] Add accessibility and keyboard-navigation checks.
- [ ] Verify high-DPI, 125–200% scaling and minimum-window-size layouts.
- [ ] Add visual regression checks for disabled controls, selection, scrolling and long text.
- [ ] Digitally sign Windows artifacts when a trusted code-signing certificate is available.
- [ ] Create a signed MSI or MSIX installer with clean uninstall and upgrade paths.
- [ ] Verify downloaded updates and release artifacts by signature and SHA-256 before execution.
- [x] Add self-contained Windows x64 publishing.
- [x] Generate final release checksums and an SPDX software bill of materials.
- [x] Prepare the `v0.1.0-alpha.1` release notes and artifact profile.
- [x] Merge the validated feature branch into `main`.

## Phase 5 — Product usability and reporting

- [ ] Persist known devices, friendly names, groups and last successful connection time.
- [ ] Add clear online, offline, unauthorized, unreachable and unsupported states.
- [ ] Show the source and collection timestamp for every inventory section.
- [ ] Add a diagnostics view with privacy-safe connection errors and remediation guidance.
- [x] Export selected-device software inventory to CSV and JSON.
- [ ] Export complete selected-device and fleet inventory to CSV and JSON.
- [ ] Export a printable compliance summary without credentials, private tokens or recovery keys.
- [ ] Add filters for operating system, restart pending, security status, application and driver age.
- [ ] Add configurable health rules instead of a hard-coded synthetic health score.
- [ ] Add local notifications for restart pending, disabled protection, low disk space and failed inventory.
- [ ] Add a release channel setting for stable, preview and disabled application updates.
- [ ] Add a first-run wizard for Local, Home, Workgroup and Enterprise modes.

## Phase 6 — Home and small-workgroup mode

- [x] Create the `OpenFleet Helper` foundation with a documented, versioned and allow-listed API.
- [x] Keep the first Helper alpha strictly read-only.
- [ ] Discover Helpers only on the local private network using mDNS or an equivalent bounded mechanism.
- [x] Add one-time pairing with a six-digit code, five-minute expiry and bounded attempts.
- [x] Issue a unique client certificate and require it on the inventory endpoint.
- [x] Add an in-app fingerprint verification and client-certificate import workflow.
- [ ] Store private keys in the Windows certificate store and never store administrator passwords.
- [ ] Restrict the Helper firewall rule to the Private profile and local subnet by default.
- [ ] Add certificate revocation, unpairing, device reset and controller replacement workflows.
- [x] Add Helper health, agent-version and capability reporting.
- [x] Collect Windows/build, uptime, firewall, restart, software and driver state through the Helper.
- [ ] Add disk and Defender/security detail to the Helper snapshot.
- [ ] Add low-disk-space, stale Defender signatures and missing-update alerts.
- [ ] Add a local consent option before any future maintenance command.
- [ ] Add rate limiting, replay protection, request expiry and complete audit records.
- [ ] Test pairing and reconnect behavior after DHCP address changes and router restarts.
- [ ] Test isolation across Public, Private and Domain Windows Firewall profiles.

## Phase 7 — Safe remote maintenance

- [ ] Define a versioned remote-command allow-list with explicit capability negotiation.
- [ ] Require confirmation showing target, exact action, timeout and rollback/cancellation options.
- [ ] Prevent concurrent or conflicting actions on the same device or package.
- [ ] Add remote WinGet update detection before enabling remote updates.
- [ ] Add controlled application repair, update and uninstall through the Helper.
- [ ] Add Defender quick-scan and Windows Update scan actions.
- [ ] Add scheduled reboot/shutdown with user notification and cancellation.
- [ ] Refuse arbitrary PowerShell, command-shell and uploaded-script execution.
- [ ] Record requester, target, command identifier, result and integrity information without secrets.
- [ ] Add emergency command revocation and a read-only safe mode.

## Phase 8 — Enterprise integrations

- [ ] Implement constrained, read-only WinRM over HTTPS for environments without the Helper.
- [ ] Support Kerberos for domain environments without adding hosts to insecure `TrustedHosts` lists.
- [ ] Use Windows Credential Manager or certificate authentication without exposing secrets in logs.
- [ ] Add read-only Microsoft Graph integration for Intune managed-device inventory.
- [ ] Add read-only Entra ID device and ownership information with least-privilege permissions.
- [ ] Build an isolated Windows Server and Windows 11 lab before implementing AD DS, DNS, GPO or hybrid features.
- [ ] Add read-only Active Directory computer, OU and operating-system inventory.
- [ ] Add role-based access control and per-action authorization.
- [ ] Add compliance baselines with evidence, reason and collection timestamp.
- [ ] Add audit export for SIEM/syslog integration and retention controls.
- [ ] Add fleet groups, sites, saved filters and bulk read-only reporting.
- [ ] Add offline/stale-device detection and maintenance-window support.

## Phase 9 — Security and privacy hardening

- [ ] Create and maintain a repository threat model for Console, Helper, pairing and update flows.
- [ ] Commission a security review before enabling remote-changing actions.
- [ ] Fuzz pairing messages, inventory payloads and command parsers.
- [ ] Enforce strict schema validation, payload limits and timeouts on every remote endpoint.
- [ ] Encrypt sensitive local configuration at rest and define secure deletion behavior.
- [ ] Add configurable log retention and privacy redaction for usernames, hostnames and IP addresses.
- [ ] Add Authenticode signing for Console, Helper, installer and updater.
- [ ] Publish provenance, SBOM and checksums for every release artifact.
- [ ] Document vulnerability response, supported versions and security-update expectations.

## Phase 10 — Linux administration console

- [ ] Extract shared models, inventory contracts and validation into `OpenFleetIT.Core`.
- [ ] Create a modern Linux desktop client with Avalonia UI and .NET 10.
- [ ] Implement an agentless, read-only Windows transport over secured WinRM/HTTPS.
- [ ] Evaluate PowerShell Remoting over SSH as an alternative transport when Windows OpenSSH is enabled.
- [ ] Support Kerberos, certificate-based authentication and the Linux system credential vault without storing passwords in OpenFleet.
- [ ] Retrieve Windows version/build, uptime, firewall, restart state, software, security and driver inventory from Linux.
- [ ] Keep remote actions disabled in the first Linux alpha and document all Windows-side prerequisites.
- [ ] Add transport integration tests against disposable Windows Server and Windows 11 virtual machines.
- [ ] Design shared audit records and capability negotiation across the Windows and Linux clients.

## Future ideas — evaluate after the core is stable

- [ ] Optional local web dashboard bound to localhost or a deliberately configured management interface.
- [ ] Network printer inventory and driver/queue health.
- [ ] Hardware warranty and lifecycle metadata from vendor APIs where licensing permits it.
- [ ] Startup-application and scheduled-task inventory.
- [ ] Windows event-log summaries using explicit allow-listed channels and privacy limits.
- [ ] Backup-status integrations for Windows Backup and selected enterprise backup products.
- [ ] Policy templates for Family, Small Business and Enterprise environments.
- [ ] Plugin/provider SDK for additional inventory sources without loading untrusted code in-process.
- [ ] Optional central server for multiple sites, designed separately from the local-first application.
