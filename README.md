# Frostbite

Frostbite is a small C# utility that makes the lockscreen appear on all displays by temporarily switching the display projection mode to Duplicate on lock, then restoring your original topology on unlock. It targets Windows 11, .NET 8 and C# 12.

Why use it
- Windows 11 shows the lockscreen only on the primary display. Frostbite mirrors the lockscreen to other displays so your TV or second monitor shows the lockscreen too.
- On unlock Frostbite restores your previous display topology and window positions automatically.
- Detects Bluetooth devices (BLE and classic) so you can use a couch keyboard or controller to decide whether to switch back to an external display.

How it works (summary)
1. On lock: records top-level window placements, switches projection mode to Duplicate.
2. On unlock: polls paired Bluetooth devices for configured name tokens (supports BLE and classic controllers), chooses internal or external topology, waits for the topology to settle, then restores saved window positions.

Requirements
- Windows 11 (tested target)
- .NET 8 SDK / runtime
- C# 12 (project set to use C# 12)
- Recommended IDE: Visual Studio 2026 (with .NET 8 workload)

Quick start (build & publish)
1. Open the solution `Frostbite.sln` in Visual Studio 2026. Main code: `Program.cs`.  
2. Build: use __Build > Build Solution__.  
3. Publish: use __Build > Publish Frostbite__ with recommended settings:
   - Target framework: `net8.0`  
   - Configuration: `Release`  
   - Target runtime: `win-x64`  
   - Deployment mode: Framework-dependent (or Self-contained for a standalone binary)  
   - Target location: `bin\Release\net8.0\publish\`  
   - Optional: enable `Produce single file` or tweak trimming/ReadyToRun as needed.  
4. The published binary will be `bin\Release\net8.0\publish\Frostbite.exe`.

Configuration & files
- Saved window placements: `C:\Frostbite\winpos.json`  
- Config file (created on first run if missing): `config.json` in the app folder (or local app data `...\Frostbite`). It contains the list of device name tokens Frostbite will search for.  
- Logs (best-effort): `C:\Frostbite\debug_log.txt`

Scheduled tasks (recommended)
Frostbite is intended to run via two scheduled tasks:
- `Frostbite screen lock save window position clone display` — runs `Frostbite.exe save` on lock.  
- `Frostbite screen unlock extend display restore window` — runs `Frostbite.exe restore` on unlock.

Install:
1. Open PowerShell as Administrator
2. Navigate to the folder containing the scripts (e.g. `cd C:\Users\YourName\Downloads\Frostbite`)
3. Install using `.\install_frostbite.ps1` to create the scheduled tasks and copy files to `C:\Frostbite`
4. Update config.json in `C:\Frostbite` to add your Bluetooth device name found in Settings > Bluetooth & devices. Example entry:
	- `{ "KeyboardNames": [ "i4", "DualSense", "yourBluetoothDeviceName" ] }`

Uninstall:
1. Open PowerShell as Administrator
2. Navigate to install folder (`cd C:\Frostbite`)
3. Uninstall using `.\UNWISE.ps1` to delete the scheduled tasks and remove installed files from `C:\Frostbite`

Usage notes & troubleshooting
- The app polls paired Bluetooth devices briefly on unlock; if your controller/keyboard is slow to reconnect, increase the polling window in code.  
- Bluetooth detection uses `Windows.Devices.*` APIs — make sure your runtime supports these APIs.  
- If you rename the executable or solution, update the scheduled tasks and script names accordingly.  
- Logs are best-effort and may not always be written (permission/environment dependent).

Attribution
- This project was adapted from earlier work (originally named Wintermelon). See repository history for details.

# Development notes

# clone your newly renamed fork and copy files into it
git clone git@github.com:YOUR_USER/Frostbite.git
cd Frostbite
# copy your project files into this folder (or move the working tree contents here)
# then:
git add .
git commit -m "Initial Frostbite import — forked from Wintermelon; rename and installer updates"
git push -u origin main