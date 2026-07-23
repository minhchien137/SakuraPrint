<#
    Installs the ZPL print bridge (server.js) as a Windows Service using NSSM,
    so it auto-starts on boot and restarts itself if it ever crashes.

    USAGE:
      1. Right-click PowerShell -> "Run as Administrator"
      2. cd to this folder (D:\PrintService\PrintService)
      3. Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
      4. .\install-service.ps1

    Re-running this script is safe - it detects an existing service/NSSM install
    and skips re-downloading / re-installing.
#>

$ErrorActionPreference = 'Stop'

# -- Config -------------------------------------------------------------
$ServiceName = 'ZplPrintService'
$ProjectDir  = $PSScriptRoot
$LogDir      = Join-Path $ProjectDir 'service-logs'
$NssmDir     = Join-Path $env:ProgramData 'nssm'
$NssmExe     = Join-Path $NssmDir 'nssm.exe'
$NssmZipUrl  = 'https://nssm.cc/release/nssm-2.24.zip'
$NssmZipPath = Join-Path $env:TEMP 'nssm-2.24.zip'

# -- 0. Must run elevated -------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    throw "Please re-run this script from an elevated (Administrator) PowerShell window."
}

# -- 1. Locate node.exe ---------------------------------------------------
$nodeCmd = Get-Command node -ErrorAction SilentlyContinue
if (-not $nodeCmd) {
    throw "node.exe not found on PATH. Install Node.js first (https://nodejs.org)."
}
$NodeExe = $nodeCmd.Source
Write-Host "Using Node: $NodeExe"

# -- 2. Download & extract NSSM if missing --------------------------------
if (-not (Test-Path $NssmExe)) {
    Write-Host "NSSM not found - downloading..."
    New-Item -ItemType Directory -Force -Path $NssmDir | Out-Null
    Invoke-WebRequest -Uri $NssmZipUrl -OutFile $NssmZipPath
    Expand-Archive -Path $NssmZipPath -DestinationPath $NssmDir -Force

    $arch = if ([Environment]::Is64BitOperatingSystem) { 'win64' } else { 'win32' }
    $extractedExe = Join-Path $NssmDir "nssm-2.24\$arch\nssm.exe"
    Copy-Item $extractedExe $NssmExe -Force
    Write-Host "NSSM installed at $NssmExe"
} else {
    Write-Host "NSSM already present at $NssmExe"
}

# -- 3. Make sure npm dependencies are installed --------------------------
if (-not (Test-Path (Join-Path $ProjectDir 'node_modules'))) {
    Write-Host "Installing npm dependencies..."
    Push-Location $ProjectDir
    & npm install
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

# -- 4. (Re)install the service -------------------------------------------
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service '$ServiceName' already exists - stopping and removing it first..."
    & $NssmExe stop $ServiceName confirm | Out-Null
    & $NssmExe remove $ServiceName confirm | Out-Null
}

& $NssmExe install $ServiceName $NodeExe "server.js"
& $NssmExe set $ServiceName AppDirectory $ProjectDir
& $NssmExe set $ServiceName DisplayName "Zebra ZPL Print Bridge"
& $NssmExe set $ServiceName Description "Local bridge (localhost:8021) forwarding ZPL from the browser to Zebra printers via TCP/IP or USB."
& $NssmExe set $ServiceName Start SERVICE_AUTO_START

& $NssmExe set $ServiceName AppStdout (Join-Path $LogDir 'stdout.log')
& $NssmExe set $ServiceName AppStderr (Join-Path $LogDir 'stderr.log')
& $NssmExe set $ServiceName AppRotateFiles 1
& $NssmExe set $ServiceName AppRotateOnline 1
& $NssmExe set $ServiceName AppRotateBytes 1048576

# Restart automatically if the process ever exits/crashes
& $NssmExe set $ServiceName AppExit Default Restart
& $NssmExe set $ServiceName AppRestartDelay 3000

# -- 5. Start it -----------------------------------------------------------
Start-Service $ServiceName
Start-Sleep -Seconds 2
Get-Service $ServiceName | Format-Table -AutoSize

Write-Host ""
Write-Host "Done. Verify with:  Invoke-RestMethod http://localhost:8021/health"
Write-Host "Logs at: $LogDir"
