# install-node.ps1
#
# Installs Node.js automatically (via winget, falling back to a direct MSI
# download) and refreshes PATH for this process so a caller can verify the
# install right away.
#
# Kept in its own file (instead of an inline "powershell -Command ... ^"
# block inside INSTALL_ALL.bat) because multi-line caret-continued
# -Command invocations are fragile: under some Windows locales/codepages
# the continuation gets corrupted and PowerShell fails to parse the
# command (e.g. "TerminatorExpectedAtEndOfString").
#
# ASCII-only on purpose so it behaves the same under any system codepage
# (including Chinese GBK / codepage 936).
#
# Exit code 0 = Node.js is installed and usable, 1 = failed.

$ErrorActionPreference = 'Stop'

function Test-NodeInstalled {
    if (Get-Command node -ErrorAction SilentlyContinue) { return $true }
    $fallback = Join-Path $env:ProgramFiles 'nodejs\node.exe'
    if (Test-Path $fallback) { return $true }
    return $false
}

if (Test-NodeInstalled) {
    Write-Host "Node.js is already installed."
    exit 0
}

Write-Host "Node.js not found. Installing automatically..."

$winget = Get-Command winget -ErrorAction SilentlyContinue
if ($winget) {
    Write-Host "Trying winget..."
    try {
        & winget install OpenJS.NodeJS.LTS -e --silent --accept-package-agreements --accept-source-agreements
    } catch {
        Write-Host ("winget install failed: " + $_.Exception.Message)
    }
} else {
    Write-Host "winget is not available on this machine."
}

if (-not (Test-NodeInstalled)) {
    Write-Host "Downloading the Node.js installer directly..."
    $url = 'https://nodejs.org/dist/v20.17.0/node-v20.17.0-x64.msi'
    $out = Join-Path $env:TEMP 'node-installer.msi'
    try {
        Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing
        Start-Process msiexec.exe -ArgumentList ('/i "' + $out + '" /qn /norestart') -Wait
        Remove-Item $out -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Host ("Direct download/install failed: " + $_.Exception.Message)
    }
}

# Refresh PATH in THIS process from the registry so Test-NodeInstalled can
# see a node.exe that winget/msiexec just installed (a new install does not
# update the PATH of processes that were already running).
$machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
$userPath    = [Environment]::GetEnvironmentVariable('Path', 'User')
$nodeDir     = Join-Path $env:ProgramFiles 'nodejs'
$env:Path    = "$machinePath;$userPath;$nodeDir"

if (-not (Test-NodeInstalled)) {
    Write-Host ""
    Write-Host "ERROR: Automatic Node.js install did not succeed."
    Write-Host "Please install manually from https://nodejs.org then run INSTALL_ALL.bat again."
    exit 1
}

Write-Host "Node.js installed successfully."
exit 0
