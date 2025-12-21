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
Version : 0.1
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
        // Minimal config holder: list of Bluetooth keyboard/controller name tokens.
        private sealed class UserConfig { public List<string> KeyboardNames { get; set; } = new(); }

        // P/Invoke: set window placement and enumerate desktop windows.
        [DllImport("user32.dll")] private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);
        [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);
        [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDelegate lpEnumCallbackFunction, IntPtr lParam);
        [DllImport("User32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        public delegate bool EnumDelegate(IntPtr hWnd, int lParam);

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
            // Resolve config location without hardcoding to LocalAppData.
            // Prefer executable directory (so build artifacts copied to C:\Frostbite include config.json).
            var exeDir = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frostbite");

            var (config, configDir) = LoadUserConfig(new[] { exeDir, localAppDir });

            // Use C:\Frostbite as the default location for winpos.json (requested).
            var settingsDir = @"C:\Frostbite";
            try { Directory.CreateDirectory(settingsDir); } catch { /* ignore */ }
            var settingsFile = Path.Combine(settingsDir, "winpos.json");

            if (args.Length == 0)
            {
                //*Console.WriteLine("Usage: Frostbite [save | restore]");
                return;
            }

            var handleInfo = new List<IntPtr>();

            // Enumerate top-level visible windows; skip Frostbite and protected processes.
            EnumDelegate filter = (hWnd, _) =>
            {
                try
                {
                    var sb = new StringBuilder(255);
                    GetWindowText(hWnd, sb, sb.Capacity + 1);
                    var title = sb.ToString();
                    if (!IsWindowVisible(hWnd) || string.IsNullOrEmpty(title))
                    {
                        return true;
                    }

                    GetWindowThreadProcessId(hWnd, out var pid);
                    using (var p = Process.GetProcessById((int)pid))
                    {
                        if (p.MainModule?.FileName?.IndexOf("Frostbite", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
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
                //*Console.WriteLine("Saving window positions to {0}...", settingsFile);
                // Preserve prior simple behavior: write an empty JSON array so file is valid JSON.
                try { File.WriteAllText(settingsFile, "[]"); } catch { /* ignore */ }
                //*Console.WriteLine("Done.");
                CloneDisplays();
                return;
            }

            if (string.Equals(args[0], "restore", StringComparison.OrdinalIgnoreCase))
            {
                Log("--- RESTORE TRIGGERED (Optimized Polling) ---");
                bool isDeviceConnected = false;
                // Poll for devices up to 7.5s (500ms x 15)
                for (int i = 0; i < 15; i++)
                {
                    if (await IsBluetoothPairedDeviceConnected(config.KeyboardNames))
                    {
                        Log($"Device match detected at {(i + 1) * 0.5} seconds.");
                        isDeviceConnected = true;
                        break;
                    }
                    await Task.Delay(500);
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

                //*Console.WriteLine("Restoring window positions from {0}...", settingsFile);
                var processInfo = ReadSavedPlacementsJson(settingsFile);

                //*Console.WriteLine("Restoring {0} windows", handleInfo.Count());
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
                                wp.showCmd = 1; // restored as normal
                                SetWindowPlacement(h, ref wp);
                                //Console.WriteLine("Restored placement for {0}", key);
                            }
                        }
                    }
                    catch (System.ComponentModel.Win32Exception) { /* skip protected windows */ }
                    catch { /* ignore transient errors */ }
                }
                //*Console.WriteLine("Done.");
                return;
            }
            //*Console.WriteLine("Usage: Frostbite [save | restore]");
        }

        // Load or create config.json. Checks multiple candidate directories (e.g. exe folder then LocalAppData).
        // Returns both the parsed configuration and the directory where config.json was found/created.
        private static (UserConfig config, string configDir) LoadUserConfig(IEnumerable<string> candidateDirs)
        {
            var cfgFileName = "config.json";
            var def = new UserConfig { KeyboardNames = new List<string> { "i4" } };

            // First, look for an existing config.json in the candidate locations.
            foreach (var dir in candidateDirs)
            {
                try
                {
                    var path = Path.Combine(dir, cfgFileName);
                    if (File.Exists(path))
                    {
                        var parsed = TryReadConfigFile(path);
                        if (parsed != null)
                        {
                            return (parsed, dir);
                        }
                    }
                }
                catch { /* ignore and continue */ }
            }

            // Not found: try to create config.json in the first writable candidate directory.
            foreach (var dir in candidateDirs)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, cfgFileName);
                    var json = JsonSerializer.Serialize(new { KeyboardNames = def.KeyboardNames }, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                    return (def, dir);
                }
                catch { /* cannot write here; try next */ }
            }

            // Final fallback: LocalApplicationData\Frostbite
            try
            {
                var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frostbite");
                Directory.CreateDirectory(fallback);
                var path = Path.Combine(fallback, cfgFileName);
                var json = JsonSerializer.Serialize(new { KeyboardNames = def.KeyboardNames }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return (def, fallback);
            }
            catch (Exception ex)
            {
                Log($"Failed to create config.json in any candidate location: {ex.Message}. Using defaults in-memory.");
                return (def, Path.GetFullPath(".")); // return current directory as a best-effort configDir
            }
        }

        // Try to parse config.json; supports { "KeyboardNames": [...] } or legacy { "KeyboardName": "..." }.
        private static UserConfig? TryReadConfigFile(string path)
        {
            try
            {
                var text = File.ReadAllText(path);
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
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        list.Add(s);
                    }
                }

                return list.Count > 0 ? new UserConfig { KeyboardNames = list } : new UserConfig { KeyboardNames = new List<string> { "i4" } };
            }
            catch (Exception ex)
            {
                Log($"Failed to parse config.json at '{path}': {ex.Message}");
                return null;
            }
        }

        // Find paired BLE *and* classic Bluetooth devices and check if any name contains a configured token and is connected.
        // This expands detection to standard Bluetooth controllers (e.g. DualSense) as well as BLE keyboards.
        private static async Task<bool> IsBluetoothPairedDeviceConnected(IEnumerable<string> tokens)
        {
            try
            {
                // 1) Check Bluetooth LE paired devices (existing behavior)
                try
                {
                    var leSelector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
                    var leDevices = await DeviceInformation.FindAllAsync(leSelector);
                    foreach (var d in leDevices)
                    {
                        var name = d.Name ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        foreach (var t in tokens)
                        {
                            if (string.IsNullOrWhiteSpace(t))
                            {
                                continue;
                            }

                            if (!name.Contains(t, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            using var ble = await BluetoothLEDevice.FromIdAsync(d.Id);
                            if (ble != null)
                            {
                                var status = ble.ConnectionStatus;
                                Log($"MATCH (LE): Found device '{name}' matching token '{t}'. LIVE Connection Status: {status}");
                                if (status == BluetoothConnectionStatus.Connected)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: continue to classic Bluetooth check but log.
                    Log($"BLE scan error: {ex.Message}");
                }

                // 2) Check classic Bluetooth (RFCOMM/paired) devices which include many controllers (e.g. DualSense).
                //    Use BluetoothDevice APIs to enumerate paired devices and inspect live ConnectionStatus.
                try
                {
                    // Use selector for paired Bluetooth devices (classic). BluetoothDevice provides selectors for paired devices.
                    var classicSelector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                    var classicDevices = await DeviceInformation.FindAllAsync(classicSelector);
                    foreach (var d in classicDevices)
                    {
                        var name = d.Name ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        foreach (var t in tokens)
                        {
                            if (string.IsNullOrWhiteSpace(t))
                            {
                                continue;
                            }

                            if (!name.Contains(t, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            using var bt = await BluetoothDevice.FromIdAsync(d.Id);
                            if (bt != null)
                            {
                                var status = bt.ConnectionStatus;
                                Log($"MATCH (Classic): Found device '{name}' matching token '{t}'. LIVE Connection Status: {status}");
                                if (status == BluetoothConnectionStatus.Connected)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: log and return false if nothing else matched.
                    Log($"Classic Bluetooth scan error: {ex.Message}");
                }
            }
            catch (Exception ex) { Log($"Critical API Error: {ex.Message}"); }
            return false;
        }

        // DTO helpers for JSON serialization of WINDOWPLACEMENT
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
            if (!File.Exists(path))
            {
                return result;
            }

            try
            {
                var text = File.ReadAllText(path);
                var arr = JsonSerializer.Deserialize<WindowPlacementDto[]?>(text);
                if (arr == null)
                {
                    return result;
                }

                foreach (var d in arr)
                {
                    if (d == null || string.IsNullOrWhiteSpace(d.Key) || d.Primary)
                    {
                        continue; // preserve previous Primary behavior
                    }

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
