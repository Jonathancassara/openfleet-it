[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$serviceName = 'OpenFleetIT.Helper'
$firewallRuleName = 'OpenFleetIT.Helper.Private'
$installDirectory = Join-Path $env:ProgramFiles 'OpenFleet IT\Helper'
$expectedInstallDirectory = [System.IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'OpenFleet IT\Helper'))
$resolvedInstallDirectory = [System.IO.Path]::GetFullPath($installDirectory)
if ($resolvedInstallDirectory -ne $expectedInstallDirectory) {
    throw 'Unexpected Helper installation path. Removal was stopped.'
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this uninstaller from an elevated PowerShell window (Run as administrator).'
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))
    }
    & sc.exe delete $serviceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Service removal failed with exit code $LASTEXITCODE." }
}

Remove-NetFirewallRule -Name $firewallRuleName -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $resolvedInstallDirectory) {
    Remove-Item -LiteralPath $resolvedInstallDirectory -Recurse -Force
}

Write-Host 'OpenFleet IT Helper, its firewall rule and installed binaries were removed.' -ForegroundColor Green
Write-Host 'The service identity and pairing state were preserved for a safe reinstall.'
