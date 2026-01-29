#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Register Frostbite scheduled tasks for lock/unlock events and install UNWISE.ps1.
#>

# --- configuration ---------------------------------------------------------
$scriptdir = Split-Path -Parent $PSCommandPath
$xml1 = Join-Path $scriptdir 'Screen_lock_Save_window_position_Clone_display.xml'
$xml2 = Join-Path $scriptdir 'Screen_unlock_Extend_display_Restore_window.xml'

$installDir = 'C:\Frostbite'
$preferredExe = Join-Path $installDir 'Frostbite.exe'
$manifest = Join-Path $installDir 'install_manifest.txt'

# Uninstall helper settings
$uninstallCandidates = @('UNWISE.ps1','uninstall_frostbite.ps1')
$installedUninstallName = 'UNWISE.ps1'

$user = "$env:USERDOMAIN\$env:USERNAME"
$timestamp = (Get-Date).ToString("s")

Write-Host "--- Frostbite Installer ---" -ForegroundColor Cyan

# 1. FIND THE PUBLISH FOLDER (flattened)
$relPaths = @('bin\Release\net8.0\publish', 'Frostbite\bin\Release\net8.0\publish', 'Wintermelon\bin\Release\net8.0\publish')
$repoPublishDir = $relPaths | ForEach-Object { $p = Join-Path $scriptdir $_; if (Test-Path $p) { $p; break } } | Select-Object -First 1

if (-not $repoPublishDir) {
    $foundExe = Get-ChildItem -Path $scriptdir -Filter "Frostbite.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    $repoPublishDir = if ($foundExe) { $foundExe.DirectoryName } else { $null }
}

if (-not $repoPublishDir) { throw "Could not find publish folder or Frostbite.exe. Please build the project first." }

# Ensure install dir exists
if (-not (Test-Path $installDir)) { New-Item -Path $installDir -ItemType Directory -Force | Out-Null }

$copiedList = @()

# 2. COPY RUNTIME FILES (guard then simple loop) - EXCLUDE config.json
$files = Get-ChildItem -Path $repoPublishDir -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'config.json' }
if ($files) {
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($repoPublishDir.Length).TrimStart('\','/')
        $dest = Join-Path $installDir $relative
        $destDir = Split-Path -Parent $dest
        if (-not (Test-Path $destDir)) { New-Item -Path $destDir -ItemType Directory -Force | Out-Null }
        Copy-Item -Path $file.FullName -Destination $dest -Force
        $copiedList += (Resolve-Path -LiteralPath $dest).Path
    }
    Write-Host "Copied $($files.Count) runtime files to ${installDir}." -ForegroundColor Green
}
else {
    Write-Warning "No runtime files found at $repoPublishDir"
}

# 3. COPY XML TEMPLATES (single-level loop)
@($xml1, $xml2) | ForEach-Object {
    if (Test-Path $_) {
        $dst = Join-Path $installDir (Split-Path $_ -Leaf)
        try {
            Copy-Item -Path $_ -Destination $dst -Force
            $copiedList += (Resolve-Path -LiteralPath $dst).Path
        } catch { Write-Warning "Failed copying XML ${_}: $_"}
    }
}

# 4. COPY UNINSTALLER (straightforward)
$uninstallSource = $uninstallCandidates | ForEach-Object { $p = Join-Path $scriptdir $_; if (Test-Path $p) { $p; break } } | Select-Object -First 1
if ($uninstallSource) {
    try {
        $destUninstall = Join-Path $installDir $installedUninstallName
        Copy-Item -Path $uninstallSource -Destination $destUninstall -Force
        $copiedList += (Resolve-Path -LiteralPath $destUninstall).Path
        Write-Host "Installed uninstall helper as: ${destUninstall}" -ForegroundColor Green
    } catch { Write-Warning "Failed to copy uninstall helper: $_" }
}

# 4.5 CREATE DEFAULT config.json (flat logic)
$cfgPath = Join-Path $installDir 'config.json'
$defaultCfg = @{
    KeyboardNames = @('i4', 'DualSense', 'YourBluetoothDeviceHere')
    CloneOnLock = $true
    CloneOnUnlockOnly = $false
    PowerSaveOnLock = $true
    PowerSaveDelayMs = 500
}

if (-not (Test-Path $cfgPath)) {
    try {
        $json = $defaultCfg | ConvertTo-Json -Depth 5
        Set-Content -Path $cfgPath -Value $json -Encoding UTF8
        $copiedList += (Resolve-Path -LiteralPath $cfgPath).Path
        Write-Host "Wrote default config.json to ${cfgPath}" -ForegroundColor Green
    } catch { Write-Warning "Failed to write default config.json: $_" }
} else {
    Write-Host "config.json already exists at ${cfgPath}; not overwriting." -ForegroundColor Yellow
}

# 5. PERSIST MANIFEST (one-level guard)
if ($copiedList -and $copiedList.Count -gt 0) {
    try { $copiedList | Sort-Object -Unique | Set-Content -Path $manifest -Force } catch { Write-Warning "Failed to write manifest: $_" }
}

# 6. TASK REGISTRATION (early-exit inside function to avoid nesting)
function Update-And-RegisterTask {
    param([string]$xmlFile, [string]$taskName, [string]$exeCommand, [string]$exeArgs = '')

    if (-not (Test-Path $xmlFile)) { Write-Warning "Task XML not found: $xmlFile"; return }
    try {
        [xml]$schtask = Get-Content -Path $xmlFile -ErrorAction Stop
    } catch { Write-Error "Failed to read task XML ${xmlFile}: $_"; return }

    if (-not $schtask -or -not $schtask.Task) { Write-Warning "Invalid task XML: $xmlFile"; return }

    try {
        $schtask.Task.RegistrationInfo.Date = $timestamp
        $schtask.Task.RegistrationInfo.Author = $user

        if ($schtask.Task.Principals -and $schtask.Task.Principals.Principal) {
            $schtask.Task.Principals.Principal.UserId = $user
        }

        if ($schtask.Task.Actions -and $schtask.Task.Actions.Exec) {
            $schtask.Task.Actions.Exec.Command = $exeCommand
            $schtask.Task.Actions.Exec.Arguments = $exeArgs
        }

        $schtask.Save($xmlFile)
        Register-ScheduledTask -Xml (Get-Content $xmlFile | Out-String) -TaskName $taskName -Force
        Write-Host "Registered Task: ${taskName}" -ForegroundColor Green
    } catch { Write-Error "Failed to register task '${taskName}': $_" }
}

$task1Name = 'Frostbite screen lock save window position clone display'
$task2Name = 'Frostbite screen unlock extend display restore window'

# --- MANUAL OVERRIDE START ---
# We force the script to use the files in the current folder ($PSScriptRoot)

$destXml1 = Join-Path $PSScriptRoot "Screen_lock_Save_window_position_Clone_display.xml"
$destXml2 = Join-Path $PSScriptRoot "Screen_unlock_Extend_display_Restore_window.xml"

Write-Host "Manual Override: Targeting XMLs in $PSScriptRoot" -ForegroundColor Green
# --- MANUAL OVERRIDE END ---

Update-And-RegisterTask -xmlFile $destXml1 -taskName $task1Name -exeCommand $preferredExe -exeArgs 'save'
Update-And-RegisterTask -xmlFile $destXml2 -taskName $task2Name -exeCommand $preferredExe -exeArgs 'restore'

Write-Host "`nInstallation Complete." -ForegroundColor Cyan
