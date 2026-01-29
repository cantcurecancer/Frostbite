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

    $task = Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
    if (-not $task) { return }

    try {
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction Stop
        Write-Host "Unregistered: $name" -ForegroundColor Green
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
        Get-Content -Path $manifest |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ -and $_ -ne $PSCommandPath } |
            ForEach-Object {
                $resolved = $_
                if (Test-Path $resolved) {
                    try {
                        Remove-Item -LiteralPath $resolved -Force -ErrorAction Stop
                        Write-Host "Removed: $resolved"
                    } catch {
                        Write-Warning "Failed to remove: $resolved ($_)"
                    }
                }
            }

        Remove-Item -LiteralPath $manifest -Force -ErrorAction SilentlyContinue
    }
    catch {
        Write-Warning "Manifest removal encountered issues: $_"
    }
}

# 3. RESTORE OR REMOVE TEMPLATES
function Restore-XmlIfBackup {
    param([string]$xmlFile, [string]$backupFile)

    if (-not (Test-Path $backupFile)) { return }

    try {
        Copy-Item -LiteralPath $backupFile -Destination $xmlFile -Force
        try {
            [xml]$doc = Get-Content -Path $xmlFile
            if ($doc -and $doc.Task -and $doc.Task.Actions -and $doc.Task.Actions.Exec) {
                $doc.Task.Actions.Exec.Command = $preferredExe
                $doc.Save($xmlFile)
            }
        } catch { }
        Remove-Item -LiteralPath $backupFile -Force -ErrorAction SilentlyContinue
        Write-Host "Restored template: $(Split-Path $xmlFile -Leaf)"
    }
    catch {
        Write-Warning "Failed to restore template for ${xmlFile}: $_"
    }
}

if ($RemoveTemplates) {
    @($xml1, $xml2, $bak1, $bak2) |
        ForEach-Object {
            if (Test-Path $_) {
                try {
                    Remove-Item -LiteralPath $_ -Force -ErrorAction Stop
                    Write-Host "Removed: $_"
                } catch {
                    Write-Warning "Failed to remove: $_ ($_)"
                }
            }
        }
} else {
    Restore-XmlIfBackup -xmlFile $xml1 -backupFile $bak1
    Restore-XmlIfBackup -xmlFile $xml2 -backupFile $bak2
}

# 4. ATTEMPT IMMEDIATE REMOVAL OF COMMON FILES (best-effort)
$extraFiles = @('UNWISE.ps1', 'debug_log.txt', 'winpos.json', 'install_manifest.txt', 'Frostbite.exe', 'config.json')
foreach ($f in $extraFiles) {
    $p = Join-Path $installDir $f
    if (Test-Path $p) {
        try {
            # clear read-only/hidden attributes before deleting
            try { (Get-Item -LiteralPath $p -Force).Attributes = 'Normal' } catch {}
            Remove-Item -LiteralPath $p -Force -ErrorAction Stop
            Write-Host "Removed: $p"
        }
        catch {
            Write-Warning "Failed to remove: $p ($_)"
        }
    }
}

# 5. SCHEDULE SELF-CLEANUP FOR FILES THAT COULD NOT BE DELETED (including this running script)
#   - can't delete the running script; create a temp cleanup PS1 that runs detached after a short delay
try {
    $currentScript = (Get-Item -LiteralPath $PSCommandPath -ErrorAction SilentlyContinue).FullName
    $tempCleanup = Join-Path $env:TEMP ("frostbite_cleanup_{0}.ps1" -f ([guid]::NewGuid()))

    $cleanupContent = @"
Start-Sleep -Seconds 6
# Best-effort remove known files and attempt folder removal when empty
\$paths = @(
    '$installDir\UNWISE.ps1',
    '$installDir\debug_log.txt',
    '$installDir\winpos.json',
    '$installDir\install_manifest.txt',
    '$installDir\Frostbite.exe',
    '$installDir\config.json'
)
foreach (\$p in \$paths) {
    try {
        if (Test-Path \$p) {
            # try clear attributes then remove
            try { (Get-Item -LiteralPath \$p -Force).Attributes = 'Normal' } catch {}
            Remove-Item -LiteralPath \$p -Force -ErrorAction SilentlyContinue
        }
    } catch {}
}

# Remove files listed in manifest if present
try {
    if (Test-Path '$manifest') {
        Get-Content -Path '$manifest' | ForEach-Object { \$it = \$_.Trim(); if (\$it -and \$it -ne '$currentScript' -and Test-Path \$it) { try { (Get-Item -LiteralPath \$it -Force).Attributes = 'Normal' } catch {}; Remove-Item -LiteralPath \$it -Force -ErrorAction SilentlyContinue } }
        Remove-Item -LiteralPath '$manifest' -Force -ErrorAction SilentlyContinue
    }
} catch {}

# Attempt to remove install dir if empty, retry a few times (handles timing/race with processes)
try {
    \$maxAttempts = 8
    for (\$i = 0; \$i -lt \$maxAttempts; \$i++) {
        try {
            if (Test-Path '$installDir') {
                # normalize attributes on any remaining files so they can be removed
                try { Get-ChildItem -Path '$installDir' -Force -Recurse -ErrorAction SilentlyContinue | ForEach-Object { try { \$_.Attributes = 'Normal' } catch {} } } catch {}
                \$remaining = Get-ChildItem -Path '$installDir' -Force -ErrorAction SilentlyContinue
                if (-not \$remaining) {
                    Remove-Item -LiteralPath '$installDir' -Recurse -Force -ErrorAction SilentlyContinue
                    break
                }
            } else {
                break
            }
        } catch {}
        Start-Sleep -Seconds 2
    }
} catch {}

# Remove this cleanup script
try { Remove-Item -LiteralPath '$tempCleanup' -Force -ErrorAction SilentlyContinue } catch {}
"@

    Set-Content -Path $tempCleanup -Value $cleanupContent -Encoding UTF8
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$tempCleanup`"" -WindowStyle Hidden
    Write-Host "Scheduled detached cleanup script: $tempCleanup" -ForegroundColor Green
}
catch {
    Write-Warning "Failed to schedule detached cleanup: $_"
}

# 6. FINAL CLEANUP MESSAGE
Write-Host "`nUninstall logic complete." -ForegroundColor Cyan

if ($RemoveTemplates -and (Test-Path $installDir)) {
    Write-Host "To fully remove the C:\Frostbite folder immediately, please delete it manually,"
    Write-Host "as this script may be running from within that folder. A detached cleanup was scheduled." -ForegroundColor Yellow
    return
}

# If not removing templates, report whether install dir is effectively empty (skip current script)
try {
    if (Test-Path $installDir) {
        $current = (Get-Item -LiteralPath $PSCommandPath -ErrorAction SilentlyContinue).FullName
        $remaining = Get-ChildItem -Path $installDir -Force | Where-Object { $_.FullName -ne $current }
        if (-not $remaining) {
            # Try to remove folder now since it's empty (the running script has been excluded)
            try {
                Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction SilentlyContinue
                Write-Host "Removed install directory: $installDir" -ForegroundColor Green
            } catch {}
        } else {
            Write-Host "Remaining files may be removed by the scheduled detached cleanup." -ForegroundColor Yellow
        }
    }
}
catch { }
