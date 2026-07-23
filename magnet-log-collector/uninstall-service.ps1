<#
    Go bo Windows Service MagnetLogCollector da cai boi install-service.ps1.
    Chay voi quyen Administrator.
#>

$ErrorActionPreference = 'Stop'

$ServiceName = 'MagnetLogCollector'
$NssmExe = Join-Path $env:ProgramData 'nssm\nssm.exe'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    throw "Vui long chay lai script nay tu cua so PowerShell quyen Administrator."
}

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "Service '$ServiceName' chua duoc cai - khong co gi de lam."
    return
}

& $NssmExe stop $ServiceName confirm
& $NssmExe remove $ServiceName confirm

Write-Host "Da go bo service '$ServiceName'."
