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
- [ ] Add Microsoft Store/MSIX package inventory.
- [x] Add read-only update availability detection through WinGet with graceful unavailable/error states.
- [x] Add installed Plug-and-Play device and driver-version inventory through WMI.
- [ ] Add automated parser tests against representative WinGet outputs.
- [ ] Add tests for malformed, duplicate and incomplete registry entries.

## Phase 2 — Safe local software operations

- [ ] Introduce a dedicated, allow-listed action execution service.
- [ ] Show an exact action preview and require explicit confirmation.
- [ ] Add controlled administrator elevation.
- [ ] Implement WinGet updates with package-ID validation.
- [ ] Implement MSI and registered uninstaller workflows.
- [ ] Implement repair only when a trusted repair mechanism is advertised.
- [ ] Capture exit code, standard output, timeout and final status.
- [ ] Persist a tamper-evident local action journal without secrets.
- [ ] Add cancellation and prevent concurrent actions on the same package.

## Phase 3 — Remote administration

- [ ] Keep remote inventory read-only by default.
- [ ] Document WMI, Remote Registry and firewall prerequisites.
- [ ] Design a signed OpenFleet agent or constrained WinRM transport.
- [ ] Add mutual authentication and certificate rotation.
- [ ] Add role-based access control and per-action authorization.
- [ ] Add remote operation audit trails.
- [ ] Avoid remote process creation through unrestricted WMI commands.

## Phase 4 — Engineering quality

- [ ] Refactor presentation logic to MVVM.
- [ ] Add unit, integration and UI tests.
- [ ] Add GitHub Actions build and test workflows.
- [ ] Add dependency, secret and code scanning.
- [ ] Add structured logs with privacy-safe fields.
- [ ] Add accessibility and keyboard-navigation checks.
- [ ] Add signed, self-contained Windows publishing.
- [ ] Publish checksums and a software bill of materials.
- [ ] Prepare the `v0.1.0-preview` release.
- [ ] Merge the validated feature branch into `main`.
