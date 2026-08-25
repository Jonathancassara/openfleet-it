# OpenFleet IT — TODO

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
- [ ] Add tests for malformed, duplicate and incomplete registry entries.

## Phase 2 — Safe local software operations

- [x] Introduce a dedicated, allow-listed action execution service.
- [x] Show an exact action preview and require explicit confirmation.
- [x] Add controlled administrator elevation.
- [x] Implement WinGet updates with exact package-ID validation.
- [x] Implement allow-listed MSI uninstall workflows by ProductCode.
- [x] Implement MSI repair only when Windows Installer metadata advertises it.
- [x] Capture exit code, standard output, timeout and final status when supported by elevation mode.
- [x] Persist a SHA-256 chained tamper-evident local action journal without secrets.
- [ ] Add cancellation and prevent concurrent actions on the same package.

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
- [ ] Add unit, integration and UI tests.
- [x] Add GitHub Actions build and test workflows.
- [x] Add Dependabot and CodeQL scanning workflows; rely on GitHub secret scanning for the public repository.
- [x] Add structured, privacy-safe local audit records.
- [ ] Add accessibility and keyboard-navigation checks.
- [ ] Digitally sign Windows artifacts when a trusted code-signing certificate is available.
- [x] Add self-contained Windows x64 publishing.
- [x] Generate final release checksums and an SPDX software bill of materials.
- [x] Prepare the `v0.1.0-alpha.1` release notes and artifact profile.
- [ ] Merge the validated feature branch into `main`.
