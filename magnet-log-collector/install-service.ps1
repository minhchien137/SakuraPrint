<#
    Cai dat magnet-log-collector (src/index.js) thanh Windows Service bang NSSM,
    de tu dong chay khi khoi dong may va tu restart neu bi crash.

    CACH DUNG:
      1. Chuot phai PowerShell -> "Run as Administrator"
      2. cd toi thu muc nay (D:\PrintService\magnet-log-collector)
      3. Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
      4. .\install-service.ps1

    Chay lai script nay an toan - se tu phat hien service/NSSM da co san va bo
    qua buoc tai lai neu khong can thiet.
#>

$ErrorActionPreference = 'Stop'

# -- Config -------------------------------------------------------------
$ServiceName = 'MagnetLogCollector'
$ProjectDir  = $PSScriptRoot
$LogDir      = Join-Path $ProjectDir 'service-logs'
$NssmDir     = Join-Path $env:ProgramData 'nssm'
$NssmExe     = Join-Path $NssmDir 'nssm.exe'
$NssmZipUrl  = 'https://nssm.cc/release/nssm-2.24.zip'
$NssmZipPath = Join-Path $env:TEMP 'nssm-2.24.zip'

# -- 0. Phai chay quyen Administrator -------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    throw "Vui long chay lai script nay tu cua so PowerShell quyen Administrator."
}

# -- 1. Tim node.exe --------------------------------------------------------
$nodeCmd = Get-Command node -ErrorAction SilentlyContinue
if (-not $nodeCmd) {
    throw "Khong tim thay node.exe trong PATH. Hay cai Node.js truoc (https://nodejs.org)."
}
$NodeExe = $nodeCmd.Source
Write-Host "Dung Node: $NodeExe"

# -- 2. Tai va giai nen NSSM neu chua co --------------------------------
if (-not (Test-Path $NssmExe)) {
    Write-Host "Chua co NSSM - dang tai ve..."
    New-Item -ItemType Directory -Force -Path $NssmDir | Out-Null
    Invoke-WebRequest -Uri $NssmZipUrl -OutFile $NssmZipPath
    Expand-Archive -Path $NssmZipPath -DestinationPath $NssmDir -Force

    $arch = if ([Environment]::Is64BitOperatingSystem) { 'win64' } else { 'win32' }
    $extractedExe = Join-Path $NssmDir "nssm-2.24\$arch\nssm.exe"
    Copy-Item $extractedExe $NssmExe -Force
    Write-Host "Da cai NSSM tai $NssmExe"
} else {
    Write-Host "NSSM da co san tai $NssmExe"
}

# -- 3. Cai npm dependencies neu chua co --------------------------------
if (-not (Test-Path (Join-Path $ProjectDir 'node_modules'))) {
    Write-Host "Dang cai npm dependencies..."
    Push-Location $ProjectDir
    & npm install
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $ProjectDir 'logs') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $ProjectDir 'temp') | Out-Null

# -- 4. (Cai lai) service -------------------------------------------
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service '$ServiceName' da ton tai - dung va go bo truoc..."
    & $NssmExe stop $ServiceName confirm | Out-Null
    & $NssmExe remove $ServiceName confirm | Out-Null
}

& $NssmExe install $ServiceName $NodeExe "src\index.js"
& $NssmExe set $ServiceName AppDirectory $ProjectDir
& $NssmExe set $ServiceName DisplayName "Magnet Log Collector"
& $NssmExe set $ServiceName Description "Doc dinh ky file Excel ket qua kiem tra kich thuoc magnet (D:\MagnetLogfile_Summary) va luu dong moi vao SQL Server (SVN_MiddleDimensionCheckResult)."
& $NssmExe set $ServiceName Start SERVICE_AUTO_START

& $NssmExe set $ServiceName AppStdout (Join-Path $LogDir 'stdout.log')
& $NssmExe set $ServiceName AppStderr (Join-Path $LogDir 'stderr.log')
& $NssmExe set $ServiceName AppRotateFiles 1
& $NssmExe set $ServiceName AppRotateOnline 1
& $NssmExe set $ServiceName AppRotateBytes 1048576

# Tu dong restart neu process thoat/crash
& $NssmExe set $ServiceName AppExit Default Restart
& $NssmExe set $ServiceName AppRestartDelay 3000

# -- 5. Khoi dong -----------------------------------------------------------
Start-Service $ServiceName
Start-Sleep -Seconds 2
Get-Service $ServiceName | Format-Table -AutoSize

Write-Host ""
Write-Host "Xong. Xem log ung dung tai: $(Join-Path $ProjectDir 'logs')"
Write-Host "Xem log stdout/stderr cua NSSM tai: $LogDir"
