<#
    Removes the ZplPrintService Windows Service installed by install-service.ps1.
    Run as Administrator.
#>

$ErrorActionPreference = 'Stop'

$ServiceName = 'ZplPrintService'
$NssmExe = Join-Path $env:ProgramData 'nssm\nssm.exe'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    throw "Please re-run this script from an elevated (Administrator) PowerShell window."
}

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "Service '$ServiceName' is not installed — nothing to do."
    return
}

& $NssmExe stop $ServiceName confirm
& $NssmExe remove $ServiceName confirm

Write-Host "Service '$ServiceName' removed."
