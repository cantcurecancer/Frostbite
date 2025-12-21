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

# 1. FIND THE PUBLISH FOLDER (Robust detection)
$relPaths = @('bin\Release\net8.0\publish', 'Frostbite\bin\Release\net8.0\publish', 'Wintermelon\bin\Release\net8.0\publish')
$repoPublishDir = ""

foreach ($rel in $relPaths) {
    $testPath = Join-Path $scriptdir $rel
    if (Test-Path $testPath) {
        $repoPublishDir = $testPath
        break
    }
}

if (-not $repoPublishDir) {
    $foundExe = Get-ChildItem -Path $scriptdir -Filter "Frostbite.exe" -Recurse | Select-Object -First 1
    if ($foundExe) { $repoPublishDir = $foundExe.DirectoryName }
}

if (-not $repoPublishDir) {
    throw "Could not find publish folder or Frostbite.exe. Please build the project first."
}

# 2. PREPARE INSTALL DIR & COPY RUNTIME FILES
if (-not (Test-Path $installDir)) {
    New-Item -Path $installDir -ItemType Directory -Force | Out-Null
}

$copiedList = @()
try {
    $files = Get-ChildItem -Path $repoPublishDir -File -Recurse
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
catch {
    Write-Warning "Failed to copy runtime files: $_"
}

# 3. COPY XML TEMPLATES
$destXml1 = Join-Path $installDir (Split-Path $xml1 -Leaf)
$destXml2 = Join-Path $installDir (Split-Path $xml2 -Leaf)

foreach ($xml in @($xml1, $xml2)) {
    if (Test-Path $xml) {
        $d = Join-Path $installDir (Split-Path $xml -Leaf)
        try { 
            Copy-Item -Path $xml -Destination $d -Force
            $copiedList += (Resolve-Path -LiteralPath $d).Path 
        } catch { Write-Warning "Failed copying XML ${xml}: $_" }
    }
}

# 4. COPY UNINSTALLER AS UNWISE.ps1
$uninstallSource = $null
foreach ($candidate in $uninstallCandidates) {
    $p = Join-Path $scriptdir $candidate
    if (Test-Path $p) { $uninstallSource = $p; break }
}

if ($uninstallSource) {
    try {
        $destUninstall = Join-Path $installDir $installedUninstallName
        Copy-Item -Path $uninstallSource -Destination $destUninstall -Force
        $copiedList += (Resolve-Path -LiteralPath $destUninstall).Path
        Write-Host "Installed uninstall helper as: ${destUninstall}" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to copy uninstall helper: $_"
    }
}

# 5. PERSIST MANIFEST
try {
    $copiedList | Sort-Object -Unique | Set-Content -Path $manifest -Force
} catch { Write-Warning "Failed to write manifest: $_" }

# 6. TASK REGISTRATION
function Update-And-RegisterTask {
    param([string]$xmlFile, [string]$taskName, [string]$exeCommand, [string]$exeArgs = '')
    try {
        [xml]$schtask = Get-Content -Path $xmlFile -ErrorAction Stop
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
    }
    catch {
        Write-Error "Failed to register task '${taskName}': $_"
    }
}

$task1Name = 'Frostbite screen lock save window position clone display'
$task2Name = 'Frostbite screen unlock extend display restore window'

Update-And-RegisterTask -xmlFile $destXml1 -taskName $task1Name -exeCommand $preferredExe -exeArgs 'save'
Update-And-RegisterTask -xmlFile $destXml2 -taskName $task2Name -exeCommand $preferredExe -exeArgs 'restore'

Write-Host "`nInstallation Complete." -ForegroundColor Cyan
