[![GitHub Release](https://img.shields.io/github/v/release/cantcurecancer/Frostbite?include_prereleases&style=flat-square)](https://github.com/cantcurecancer/Frostbite/releases/latest)
[![License](https://img.shields.io/github/license/cantcurecancer/Frostbite?style=flat-square)](https://github.com/cantcurecancer/Frostbite/blob/main/LICENSE)
[![Repo Size](https://img.shields.io/github/repo-size/cantcurecancer/Frostbite?style=flat-square)](https://github.com/cantcurecancer/Frostbite)
# Frostbite
Frostbite is a small C# utility that makes the lockscreen appear on all displays by temporarily switching the display projection mode to Duplicate on lock, then restoring your original topology on unlock. It targets Windows 11, .NET 8 and C# 12.

## Why use it
- Windows 11 shows the lockscreen only on the primary display. Frostbite mirrors the lockscreen to other displays so your TV or second monitor shows the lockscreen too.
- On unlock Frostbite restores your previous display topology and window positions automatically.
- Detects Bluetooth devices (BLE and classic) so you can use a couch keyboard or controller to decide whether to switch back to an external display.

## Requirements
- Windows 11 (tested target)
- .NET 8 runtime (included in self-contained releases)

## Install
**Users should double-click one of these files:**

- `install_frostbite.bat` — Install Frostbite
- Edit `C:\Frostbite\config.json` to add your Bluetooth device names

## Uninstall
- Double-click `UNWISE.bat` from `C:\Frostbite` to uninstall and remove files.

**Internal files (do not double-click):**
- `_install_frostbite.ps1` — Internal installer script
- `_UNWISE.ps1` — Internal uninstaller script

## Development (building from source)
If you're a developer and want to build from source:

1. Clone the repo and open `Frostbite.sln` in Visual Studio 2026
2. Ensure you have .NET 8 SDK installed
3. Build: __Build > Build Solution__
4. Publish: __Build > Publish Frostbite__ with these settings:
   - Target framework: `net8.0`
   - Configuration: `Release`
   - Target runtime: `win-x64`
   - Deployment mode: Framework-dependent
   - Target location: `bin\Release\net8.0\publish\`
5. Run the installer from your source folder as usual

## Configuration & files
- Saved window placements: `C:\Frostbite\winpos.json`
- Config file: `C:\Frostbite\config.json` (created on first run if missing)
- Logs: `C:\Frostbite\debug_log.txt` (best-effort)

## Scheduled tasks
Frostbite runs automatically via Windows Scheduled Tasks:
- `Frostbite screen lock save window position clone display` — runs on lock
- `Frostbite screen unlock extend display restore window` — runs on unlock
- `Frostbite screen login clone display` — runs on login
- `Frostbite screen restart clone display` — runs on restart/startup

## Usage notes & troubleshooting
- The app polls paired Bluetooth devices briefly on unlock; if your controller/keyboard is slow to reconnect, increase delays in config.json
- Bluetooth detection uses Windows.Devices.* APIs
- Logs are best-effort; check `debug_log.txt` if something isn't working
- To find your device name: Settings > Bluetooth & devices > Paired devices (copy the exact name)

## Attribution
- Adapted from earlier work (Wintermelon). See repository history for details.