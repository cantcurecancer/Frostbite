/*
Author:  qwert
Project: Frostbite
Purpose: - Clone display on lockscreen so you can see what you're doing when using PC monitor or TV
         - Then restore display topology based on Bluetooth keyboard/controller connection on unlock
Notes:   - Based on the original Wintermelon by arun-goud, heavily modified to resolve Issue #11
         - Simplified and compacted for readability and maintainability (vibed w/ GPT-5 and some Gemini 3 Pro)
         - Really only tested BLE with the Rii i4 keyboard, but should work with others
         - DualSense and other classic Bluetooth controllers should be detectable now too
         - Uses JSON for config and window positions for easier manual editing
         - Logs to C:\Frostbite\debug_log.txt for troubleshooting
         - Configurable keyboard/controller name tokens in config.json
         - I'll probably work on support for USB devices/controllers later
License: GNU General Public License (GPL), Version 2
Version : 0.2.1
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Frostbite
{
    internal static class Program
    {
        // Config holder: list of Bluetooth keyboard/controller name tokens and behavior flags.
        private sealed class UserConfig
        {
            public List<string> KeyboardNames { get; set; } = new();
            public bool CloneOnLock { get; set; } = false;          // default: do NOT clone on lock
            public bool CloneOnUnlockOnly { get; set; } = true;     // default: clone on unlock
            public bool PowerSaveOnLock { get; set; } = false;      // optional: put monitors to power-save after cloning on lock
            public int PowerSaveDelayMs { get; set; } = 500;        // delay before power-save signal
        }

        // P/Invoke: set window placement and enumerate desktop windows.
        [DllImport("user32.dll")] private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);
        [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);
        [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDelegate lpEnumCallbackFunction, IntPtr lParam);
        [DllImport("User32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        public delegate bool EnumDelegate(IntPtr hWnd, int lParam);

        // WM_SYSCOMMAND / monitor power helpers
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private const uint WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        private static void SetMonitorsPower(int state)
        {
            // state: 1 = low power, 2 = shut off. Passing -1 or 0 often interpreted as "on".
            try
            {
                SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(state));
                Log($"SetMonitorsPower({state}) called.");
            }
            catch (Exception ex) { Log($"SetMonitorsPower error: {ex.Message}"); }
        }

        // New helper: launch a detached, hidden PowerShell process to send the monitor power broadcast after a delay.
        // This avoids keeping the main console process open and prevents a lingering black command window.
        private static void LaunchDetachedMonitorPower(int delayMs, int state)
        {
            try
            {
                // Build a PowerShell command that sleeps then invokes SendMessage via Add-Type.
                // Use -NoProfile and -WindowStyle Hidden; Start process CreateNoWindow = true.
                var psCommand = $"Start-Sleep -Milliseconds {Math.Max(0, delayMs)}; " +
                                "Add-Type -Namespace Win32 -Name NativeMethods -MemberDefinition @'\n" +
                                "[System.Runtime.InteropServices.DllImport(\"user32.dll\")]\n" +
                                "public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);\n" +
                                "'@; " +
                                $"[Win32.NativeMethods]::SendMessage([IntPtr]0xffff, 0x0112, [IntPtr]0xF170, [IntPtr]{state});";

                var args = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand.Replace("\"", "\\\"")}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                Process.Start(psi);
                Log($"Launched detached power command (state={state}, delayMs={delayMs}).");
            }
            catch (Exception ex)
            {
                Log($"LaunchDetachedMonitorPower error: {ex.Message}");
            }
        }

        // Display topology via SetDisplayConfig.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern long SetDisplayConfig(uint numPathArrayElements, IntPtr pathArray, uint numModeArrayElements, IntPtr modeArray, uint flags);
        private enum SDC_FLAGS : uint
        {
            SDC_TOPOLOGY_INTERNAL = 1, SDC_TOPOLOGY_CLONE = 2, SDC_TOPOLOGY_EXTEND = 4,
            SDC_TOPOLOGY_EXTERNAL = 8, SDC_APPLY = 128
        }
        private static void SetTopology(SDC_FLAGS topology) => SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, (uint)SDC_FLAGS.SDC_APPLY | (uint)topology);
        public static void CloneDisplays() => SetTopology(SDC_FLAGS.SDC_TOPOLOGY_CLONE);
        public static void ExtendDisplays() => SetTopology(SDC_FLAGS.SDC_TOPOLOGY_EXTEND);
        public static void ExternalDisplay() => SetTopology(SDC_FLAGS.SDC_TOPOLOGY_EXTERNAL);
        public static void InternalDisplay() => SetTopology(SDC_FLAGS.SDC_TOPOLOGY_INTERNAL);

        // Lightweight file logger (best-effort).
        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(@"C:\Frostbite\debug_log.txt",
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* do not throw on logging failure */ }
        }

        // Native structs (minimal).
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public RECT(int l, int t, int r, int b)
            { Left = l; Top = t; Right = r; Bottom = b; }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
            public POINT(int x, int y)
            { X = x; Y = y; }
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length, flags, showCmd;
            public POINT minPosition, maxPosition;
            public RECT normalPosition;
        }

        // Entry point
        private static async Task Main(string[] args)
        {
            var exeDir = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frostbite");

            var (config, configDir) = LoadUserConfig(new[] { exeDir, localAppDir });

            var settingsDir = @"C:\Frostbite";
            try { Directory.CreateDirectory(settingsDir); } catch { /* ignore */ }
            var settingsFile = Path.Combine(settingsDir, "winpos.json");

            if (args.Length == 0) { return; }

            var handleInfo = new List<IntPtr>();

            // Enumerate top-level visible windows; skip Frostbite and protected processes.
            EnumDelegate filter = (hWnd, _) =>
            {
                try
                {
                    var sb = new StringBuilder(255);
                    GetWindowText(hWnd, sb, sb.Capacity + 1);
                    var title = sb.ToString();
                    if (!IsWindowVisible(hWnd) || string.IsNullOrEmpty(title)) { return true; }

                    GetWindowThreadProcessId(hWnd, out var pid);
                    using (var p = Process.GetProcessById((int)pid))
                    {
                        if (p.MainModule?.FileName?.IndexOf("Frostbite", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
                    }
                    handleInfo.Add(hWnd);
                }
                catch (System.ComponentModel.Win32Exception) { /* access denied - skip */ }
                catch { /* other intermittent errors - skip */ }
                return true;
            };

            if (!EnumDesktopWindows(IntPtr.Zero, filter, IntPtr.Zero))
            {
                var err = Marshal.GetLastWin32Error();
                throw new Exception($"EnumDesktopWindows failed with code {err}.");
            }

            if (string.Equals(args[0], "save", StringComparison.OrdinalIgnoreCase))
            {
                // Save positions (legacy wrote "[]"); keep same behavior.
                try { File.WriteAllText(settingsFile, "[]"); } catch { /* ignore */ }

                // Only clone on lock if configured to do so.
                if (config.CloneOnLock && !config.CloneOnUnlockOnly)
                {
                    try
                    {
                        CloneDisplays();
                        Log("CloneDisplays invoked on lock (CloneOnLock=true).");
                        if (config.PowerSaveOnLock)
                        {
                            // Fire-and-forget the power-save broadcast from a detached process so the console does not linger.
                            LaunchDetachedMonitorPower(config.PowerSaveDelayMs, 2); // 2 = shut off
                            Log("PowerSaveOnLock: scheduled detached power-save (2).");
                        }
                    }
                    catch (Exception ex) { Log($"CloneOnLock error: {ex.Message}"); }
                }

                return;
            }

            if (string.Equals(args[0], "restore", StringComparison.OrdinalIgnoreCase))
            {
                Log("--- RESTORE TRIGGERED ---");

                // If configured to clone only on unlock, do it here so TV receives clone only when waking.
                if (config.CloneOnUnlockOnly)
                {
                    try
                    {
                        CloneDisplays();
                        Log("CloneDisplays invoked on unlock (CloneOnUnlockOnly=true).");
                        if (config.PowerSaveOnLock)
                        {
                            // Wake monitors using a detached process so restore doesn't block
                            LaunchDetachedMonitorPower(config.PowerSaveDelayMs, -1); // -1 often interpreted as "on"
                            Log("PowerSaveOnLock: scheduled detached wake attempt (-1).");
                        }
                    }
                    catch (Exception ex) { Log($"CloneOnUnlockOnly error: {ex.Message}"); }
                }

                bool isDeviceConnected = false;

                // Simplified / consolidated device scanning:
                // iterate "modes" for BLE and Classic in one combined loop to reduce duplicated code.
                foreach (var isLe in new[] { true, false })
                {
                    try
                    {
                        var selector = isLe ? BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)
                                            : BluetoothDevice.GetDeviceSelectorFromPairingState(true);

                        var devices = await DeviceInformation.FindAllAsync(selector);
                        if (devices == null || devices.Count == 0) { continue; }

                        foreach (var d in devices)
                        {
                            var name = d.Name ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(name)) { continue; }

                            foreach (var t in config.KeyboardNames)
                            {
                                if (string.IsNullOrWhiteSpace(t)) { continue; }
                                if (!name.Contains(t, StringComparison.OrdinalIgnoreCase)) { continue; }

                                try
                                {
                                    if (isLe)
                                    {
                                        using var ble = await BluetoothLEDevice.FromIdAsync(d.Id);
                                        if (ble != null)
                                        {
                                            var status = ble.ConnectionStatus;
                                            Log($"MATCH (LE): Found device '{name}' matching token '{t}'. LIVE Connection Status: {status}");
                                            if (status == BluetoothConnectionStatus.Connected) { isDeviceConnected = true; break; }
                                        }
                                    }
                                    else
                                    {
                                        using var bt = await BluetoothDevice.FromIdAsync(d.Id);
                                        if (bt != null)
                                        {
                                            var status = bt.ConnectionStatus;
                                            Log($"MATCH (Classic): Found device '{name}' matching token '{t}'. LIVE Connection Status: {status}");
                                            if (status == BluetoothConnectionStatus.Connected) { isDeviceConnected = true; break; }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"Device.FromIdAsync error for '{name}': {ex.Message}");
                                }
                            }

                            if (isDeviceConnected) { break; }
                        }

                        if (isDeviceConnected) { break; }
                    }
                    catch (Exception ex)
                    {
                        Log($"{(isLe ? "BLE" : "Classic")} scan error: {ex.Message}");
                    }
                }

                if (isDeviceConnected)
                {
                    Log("Decision: Switch to TV.");
                    ExternalDisplay();
                }
                else
                {
                    Log($"Decision: configured devices [{string.Join(", ", config.KeyboardNames)}] not found/connected. Stay on PC.");
                    InternalDisplay();
                }

                await Task.Delay(2000); // allow topology to settle

                var processInfo = ReadSavedPlacementsJson(settingsFile);

                foreach (var h in handleInfo)
                {
                    try
                    {
                        var sb = new StringBuilder(255);
                        GetWindowText(h, sb, sb.Capacity + 1);
                        var title = sb.ToString();
                        GetWindowThreadProcessId(h, out var pid);
                        using (var p = Process.GetProcessById((int)pid))
                        {
                            var key = $"{pid}_{h}_{title}";
                            if (processInfo.TryGetValue(key, out var wp))
                            {
                                wp.length = Marshal.SizeOf<WINDOWPLACEMENT>();
                                wp.showCmd = 1;
                                SetWindowPlacement(h, ref wp);
                            }
                        }
                    }
                    catch (System.ComponentModel.Win32Exception) { /* skip protected windows */ }
                    catch { /* ignore transient errors */ }
                }
                return;
            }
        }

        // Load or create config.json.
        private static (UserConfig config, string configDir) LoadUserConfig(IEnumerable<string> candidateDirs)
        {
            var cfgFileName = "config.json";
            var def = new UserConfig
            {
                KeyboardNames = new List<string> { "i4" },
                CloneOnLock = false,
                CloneOnUnlockOnly = true,
                PowerSaveOnLock = false,
                PowerSaveDelayMs = 500
            };

            // 1) Look for existing config
            foreach (var dir in candidateDirs)
            {
                try
                {
                    var path = Path.Combine(dir, cfgFileName);
                    if (!File.Exists(path)) { continue; }
                    var parsed = TryReadConfigFile(path);
                    if (parsed != null) { return (parsed, dir); }
                }
                catch { /* continue */ }
            }

            // 2) Create default config in first writable candidate
            foreach (var dir in candidateDirs)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, cfgFileName);
                    var jsonObj = new
                    {
                        KeyboardNames = def.KeyboardNames,
                        CloneOnLock = def.CloneOnLock,
                        CloneOnUnlockOnly = def.CloneOnUnlockOnly,
                        PowerSaveOnLock = def.PowerSaveOnLock,
                        PowerSaveDelayMs = def.PowerSaveDelayMs
                    };
                    File.WriteAllText(path, JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true }));
                    return (def, dir);
                }
                catch { /* try next */ }
            }

            // 3) Fallback to LocalAppData
            try
            {
                var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frostbite");
                Directory.CreateDirectory(fallback);
                var path = Path.Combine(fallback, cfgFileName);
                var jsonObj = new
                {
                    KeyboardNames = def.KeyboardNames,
                    CloneOnLock = def.CloneOnLock,
                    CloneOnUnlockOnly = def.CloneOnUnlockOnly,
                    PowerSaveOnLock = def.PowerSaveOnLock,
                    PowerSaveDelayMs = def.PowerSaveDelayMs
                };
                File.WriteAllText(path, JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true }));
                return (def, fallback);
            }
            catch (Exception ex)
            {
                Log($"Failed to create config.json: {ex.Message}. Using defaults in-memory.");
                return (def, Path.GetFullPath("."));
            }
        }

        // Parse config.json (new schema preferred, fallback to legacy).
        private static UserConfig? TryReadConfigFile(string path)
        {
            try
            {
                var text = File.ReadAllText(path);
                try
                {
                    var cfg = JsonSerializer.Deserialize<UserConfig>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cfg != null)
                    {
                        if (cfg.KeyboardNames == null || cfg.KeyboardNames.Count == 0) { cfg.KeyboardNames ??= new List<string> { "i4" }; }
                        return cfg;
                    }
                }
                catch { /* fall through to legacy parsing */ }

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var list = new List<string>();

                if (root.TryGetProperty("KeyboardNames", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                        {
                            list.Add(el.GetString()!);
                        }
                    }
                }
                else if (root.TryGetProperty("KeyboardName", out var single) && single.ValueKind == JsonValueKind.String)
                {
                    var s = single.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) { list.Add(s); }
                }

                return new UserConfig { KeyboardNames = list.Count > 0 ? list : new List<string> { "i4" } };
            }
            catch (Exception ex)
            {
                Log($"Failed to parse config.json at '{path}': {ex.Message}");
                return null;
            }
        }

        // Bluetooth detection (consolidated above).

        // DTO helpers and read/write for window placements (unchanged).
        private sealed class WindowPlacementDto
        {
            public string Key { get; set; } = string.Empty;
            public int Flags { get; set; }
            public int Cmd { get; set; }
            public int MinX { get; set; }
            public int MinY { get; set; }
            public int MaxX { get; set; }
            public int MaxY { get; set; }
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
            public bool Primary { get; set; }
        }

        private static Dictionary<string, WINDOWPLACEMENT> ReadSavedPlacementsJson(string path)
        {
            var result = new Dictionary<string, WINDOWPLACEMENT>();
            if (!File.Exists(path)) { return result; }

            try
            {
                var text = File.ReadAllText(path);
                var arr = JsonSerializer.Deserialize<WindowPlacementDto[]?>(text);
                if (arr == null) { return result; }

                foreach (var d in arr)
                {
                    if (d == null || string.IsNullOrWhiteSpace(d.Key) || d.Primary) { continue; }

                    var wp = new WINDOWPLACEMENT
                    {
                        length = Marshal.SizeOf<WINDOWPLACEMENT>(),
                        flags = d.Flags,
                        showCmd = d.Cmd,
                        minPosition = new POINT(d.MinX, d.MinY),
                        maxPosition = new POINT(d.MaxX, d.MaxY),
                        normalPosition = new RECT(d.Left, d.Top, d.Right, d.Bottom)
                    };
                    result[d.Key] = wp;
                }
            }
            catch (Exception ex) { Log($"ReadSavedPlacementsJson error: {ex.Message}"); }
            return result;
        }

        private static void WriteSavedPlacementsJson(IEnumerable<KeyValuePair<string, WINDOWPLACEMENT>> placements, string path)
        {
            try
            {
                var dtoList = placements.Select(kv =>
                {
                    var key = kv.Key;
                    var wp = kv.Value;
                    return new WindowPlacementDto
                    {
                        Key = key,
                        Flags = wp.flags,
                        Cmd = wp.showCmd,
                        MinX = wp.minPosition.X,
                        MinY = wp.minPosition.Y,
                        MaxX = wp.maxPosition.X,
                        MaxY = wp.maxPosition.Y,
                        Left = wp.normalPosition.Left,
                        Top = wp.normalPosition.Top,
                        Right = wp.normalPosition.Right,
                        Bottom = wp.normalPosition.Bottom,
                        Primary = false
                    };
                }).ToArray();
                var json = JsonSerializer.Serialize(dtoList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex) { Log($"WriteSavedPlacementsJson error: {ex.Message}"); }
        }
    }
}
