[CmdletBinding()]
param(
    [string]$SourceDirectory = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
$serviceName = 'OpenFleetIT.Helper'
$firewallRuleName = 'OpenFleetIT.Helper.Private'
$installDirectory = Join-Path $env:ProgramFiles 'OpenFleet IT\Helper'
$sourceExecutable = Join-Path $SourceDirectory 'OpenFleetIT.Helper.exe'
$installedExecutable = Join-Path $installDirectory 'OpenFleetIT.Helper.exe'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this installer from an elevated PowerShell window (Run as administrator).'
}
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw "OpenFleetIT.Helper.exe was not found in '$SourceDirectory'."
}

$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    if ($existingService.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
        $existingService.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))
    }
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceExecutable -Destination $installedExecutable -Force

if ($existingService) {
    & sc.exe config $serviceName binPath= ('"{0}"' -f $installedExecutable) start= auto obj= 'NT AUTHORITY\LocalService' password= '' | Out-Null
} else {
    & sc.exe create $serviceName binPath= ('"{0}"' -f $installedExecutable) start= auto obj= 'NT AUTHORITY\LocalService' password= '' DisplayName= 'OpenFleet IT Helper' | Out-Null
}
if ($LASTEXITCODE -ne 0) { throw "Service configuration failed with exit code $LASTEXITCODE." }

& sc.exe description $serviceName 'Read-only OpenFleet inventory helper for trusted private networks.' | Out-Null
& sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/none/0 | Out-Null

Remove-NetFirewallRule -Name $firewallRuleName -ErrorAction SilentlyContinue
New-NetFirewallRule -Name $firewallRuleName -DisplayName 'OpenFleet IT Helper (Private local subnet)' `
    -Description 'Read-only OpenFleet Helper HTTPS endpoint.' -Direction Inbound -Action Allow `
    -Protocol TCP -LocalPort 47831 -Profile Private -RemoteAddress LocalSubnet -Program $installedExecutable | Out-Null

Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(20))

Write-Host 'OpenFleet IT Helper was installed and started.' -ForegroundColor Green
Write-Host 'Firewall scope: Private profile, local subnet, TCP 47831.'
Write-Host 'Pairing information:'
$pairingReady = $false
for ($attempt = 1; $attempt -le 10; $attempt++) {
    & $installedExecutable --pairing-info
    if ($LASTEXITCODE -eq 0) {
        $pairingReady = $true
        break
    }
    Start-Sleep -Seconds 1
}
if (-not $pairingReady) {
    throw 'The service is running but its HTTPS endpoint did not become ready within 10 seconds.'
}
