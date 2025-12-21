#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Unregister Frostbite scheduled tasks and remove installed files and templates.
#>

param(
    [switch]$RemoveTemplates
)

$ErrorActionPreference = 'Continue'
$scriptdir = Split-Path -Parent $PSCommandPath

$xml1 = Join-Path $scriptdir 'Screen_lock_Save_window_position_Clone_display.xml'
$xml2 = Join-Path $scriptdir 'Screen_unlock_Extend_display_Restore_window.xml'
$bak1 = "$xml1.bak"
$bak2 = "$xml2.bak"

$installDir = 'C:\Frostbite'
$preferredExe = Join-Path $installDir 'Frostbite.exe'
$manifest = Join-Path $installDir 'install_manifest.txt'
$user = "$env:USERDOMAIN\$env:USERNAME"

$task1Name = 'Frostbite screen lock save window position clone display'
$task2Name = 'Frostbite screen unlock extend display restore window'

Write-Host "--- Frostbite Uninstaller ---" -ForegroundColor Cyan

function Try-UnregisterTask {
    param([string]$name)
    try {
        if (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue) {
            Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction Stop
            Write-Host "Unregistered: $name" -ForegroundColor Green
        }
    }
    catch {
        Write-Warning "Failed to unregister task '${name}': $_"
    }
}

# 1. UNREGISTER TASKS
Try-UnregisterTask -name $task1Name
Try-UnregisterTask -name $task2Name

# 2. REMOVE FILES FROM MANIFEST
if (Test-Path $manifest) {
    try {
        $paths = Get-Content -Path $manifest
        foreach ($p in $paths) {
            $resolved = $p.Trim()
            # Do not delete the script currently running (UNWISE.ps1) yet
            if ($resolved -eq $PSCommandPath) { continue }
            
            if (Test-Path $resolved) {
                Remove-Item -Path $resolved -Force
                Write-Host "Removed: $resolved"
            }
        }
        Remove-Item -Path $manifest -Force
    }
    catch {
        Write-Warning "Manifest removal encountered issues: $_"
    }
}

# 3. RESTORE OR REMOVE TEMPLATES
function Restore-XmlIfBackup {
    param([string]$xmlFile, [string]$backupFile)
    if (Test-Path $backupFile) {
        Copy-Item -Path $backupFile -Destination $xmlFile -Force
        try {
            [xml]$doc = Get-Content -Path $xmlFile
            if ($doc.Task.Actions.Exec) { $doc.Task.Actions.Exec.Command = $preferredExe }
            $doc.Save($xmlFile)
        } catch {}
        Remove-Item -Path $backupFile -Force
        Write-Host "Restored template: $(Split-Path $xmlFile -Leaf)"
    }
}

if ($RemoveTemplates) {
    foreach ($f in @($xml1, $xml2, $bak1, $bak2)) {
        if (Test-Path $f) { Remove-Item -Path $f -Force }
    }
} else {
    Restore-XmlIfBackup -xmlFile $xml1 -backupFile $bak1
    Restore-XmlIfBackup -xmlFile $xml2 -backupFile $bak2
}

# 4. FINAL CLEANUP (Install Directory)
Write-Host "`nUninstall logic complete." -ForegroundColor Cyan

if ($RemoveTemplates -and (Test-Path $installDir)) {
    Write-Host "To fully remove the C:\Frostbite folder, please delete it manually,"
    Write-Host "as this script is currently running from within that folder." -ForegroundColor Yellow
} else {
    # Try to remove install dir if empty
    try {
        if (Test-Path $installDir) {
            $remaining = Get-ChildItem -Path $installDir -Exclude (Split-Path $PSCommandPath -Leaf)
            if (-not $remaining) {
                Write-Host "Install directory is empty (except for this script)."
            }
        }
    } catch {}
}
