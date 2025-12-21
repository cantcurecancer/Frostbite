/*
***************************************************************************
Author           :  arun-goud
Github project   :  Wintermelon 
                    (WIN)dows (T)en & (E)leven (R)endering of (M)irrored (E)vent-triggered (L)ockscreens (O)n (N)on-main displays
Description      :  C# program for Win 10 & 11 that toggles projection mode
                    between duplicate and extend setting.
                    Accepts optional arguments 'save' and 'restore' that 
                    store and retrieve positions of open windows, respectively,
                    using a settings file at
                    C:\Users\Username\AppData\Local\Wintermelon\winpos.txt
Use case         :  Allows lockscreen mirroring by registering as a task
                    with task scheduler and using lock, unlock events as
                    the task triggers.
Created on       :  2020/06/19
Last modified on :  2021/07/05
References       :  Code snippets were borrowed from below URLs
                    https://docs.microsoft.com/en-us/windows/win32/gdi/positioning-objects-on-multiple-display-monitors
                    https://docs.microsoft.com/en-us/archive/blogs/davidrickard/saving-window-size-and-location-in-wpf-and-winforms
                    https://bytes.com/topic/net/answers/855274-getting-child-windows-using-process-getprocess
                    https://stackoverflow.com/questions/15448266/how-do-you-find-what-windowhandle-takes-process-ownership-and-get-the-handle
                    http://pinvoke.net/default.aspx/user32.EnumDesktopWindows
                    https://stackoverflow.com/questions/19867402/how-can-i-use-enumwindows-to-find-windows-with-a-specific-caption-title
                    https://stackoverflow.com/questions/38460253/how-to-use-system-windows-forms-in-net-core-class-library
                    https://github.com/BiNZGi/WinPos

***************************************************************************
*/
/*
 * 12/18/2025 version QD25
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using Windows.Devices.Bluetooth; 
using Windows.Devices.Enumeration;
using System.Threading.Tasks;


namespace Wintermelon
{
    class Program
    {
        // PInvoke imports for controlling Window placement
        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);
        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        // PInvoke imports to get handles to multiple windows having the same process name
        public delegate bool EnumDelegate(IntPtr hWnd, int lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);
        [DllImport("user32.dll")]
        public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDelegate lpEnumCallbackFunction, IntPtr lParam);
        [DllImport("User32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, out bool pvAttribute, int cbAttribute);
        [Flags]
        public enum DwmWindowAttribute
        {
            DWMWA_NCRENDERING_ENABLED = 1,
            DWMWA_NCRENDERING_POLICY,
            DWMWA_TRANSITIONS_FORCEDISABLED,
            DWMWA_ALLOW_NCPAINT,
            DWMWA_CAPTION_BUTTON_BOUNDS,
            DWMWA_NONCLIENT_RTL_LAYOUT,
            DWMWA_FORCE_ICONIC_REPRESENTATION,
            DWMWA_FLIP3D_POLICY,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            DWMWA_HAS_ICONIC_BITMAP,
            DWMWA_DISALLOW_PEEK,
            DWMWA_EXCLUDED_FROM_PEEK,
            DWMWA_CLOAK,
            DWMWA_CLOAKED,
            DWMWA_FREEZE_REPRESENTATION,
            DWMWA_LAST
        }

        // Display switch imports
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern long SetDisplayConfig(uint numPathArrayElements,
        IntPtr pathArray, uint numModeArrayElements, IntPtr modeArray, uint flags);

        public enum SDC_FLAGS
        {
            SDC_TOPOLOGY_INTERNAL = 1,
            SDC_TOPOLOGY_CLONE = 2,
            SDC_TOPOLOGY_EXTEND = 4,
            SDC_TOPOLOGY_EXTERNAL = 8,
            SDC_APPLY = 128
        }

        public static void CloneDisplays()
        {
            SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, ((uint) SDC_FLAGS.SDC_APPLY | (uint) SDC_FLAGS.SDC_TOPOLOGY_CLONE));
        }

        public static void ExtendDisplays()
        {
            SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, ((uint) SDC_FLAGS.SDC_APPLY | (uint) SDC_FLAGS.SDC_TOPOLOGY_EXTEND));
        }

        public static void ExternalDisplay()
        {
            SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, ((uint) SDC_FLAGS.SDC_APPLY | (uint) SDC_FLAGS.SDC_TOPOLOGY_EXTERNAL));
        }

        public static void InternalDisplay()
        {
            SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, ((uint) SDC_FLAGS.SDC_APPLY | (uint) SDC_FLAGS.SDC_TOPOLOGY_INTERNAL));
        }
        private static void Log(string message)
        {
            try
            {
                string logPath = @"C:\Wintermelon\debug_log.txt";
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, logEntry);
            }
            catch { /* Avoid crashing if log fails */ }
        }

        // RECT structure required by WINDOWPLACEMENT structure
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public RECT(int left, int top, int right, int bottom)
            {
                this.Left = left;
                this.Top = top;
                this.Right = right;
                this.Bottom = bottom;
            }
            public override string ToString()
            {
                return "(" + this.Left.ToString() + "," + this.Top.ToString() + "," + 
                    this.Right.ToString() + "," + this.Bottom.ToString() + ")";
            }
        }

        // POINT structure required by WINDOWPLACEMENT structure
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
            public override string ToString()
            {
                return "(" + this.X.ToString() + "," + this.Y.ToString() + ")";
            }
        }

        // WINDOWPLACEMENT stores the position, size, and state of a window
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT minPosition;
            public POINT maxPosition;
            public RECT normalPosition;
            public override string ToString()
            {
                return "Length=" + length.ToString() + "\tFlags=" + flags.ToString()+"\tCmd="+showCmd.ToString()+
                    "\tMin=" + minPosition.ToString() + "\tMax=" + maxPosition.ToString() + "\tRect=" + normalPosition.ToString();
            }
        }
        [STAThread]
        static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new SettingsForm());
            }
            catch (Exception ex)
            {
                // This forces a popup even if the window fails to load
                MessageBox.Show($"FATAL ERROR:\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
                                "Wintermelon Crash", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //static async System.Threading.Tasks.Task Main(string[] args)
        //{
        //    if (args.Length == 0)
        //    {
        //        //*Console.WriteLine("Usage: Wintermelon [save | restore]");
        //        return;
        //    }

        //    string appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wintermelon");
        //    if (!Directory.Exists(appDir))
        //        Directory.CreateDirectory(appDir);
        //    string settingsFile = Path.Combine(appDir, "winpos.txt");

        //    Screen[] screens = Screen.AllScreens;

        //    // Get collection of .exe processes with top-level desktop windows and save to handleInfo[]
        //    var procSettings = new List<string>();
        //    var handleInfo = new List<IntPtr>();
        //    EnumDelegate filter = delegate (IntPtr hWnd, int lParam)
        //    {
        //        // 2. Wrap EVERYTHING inside the filter in a try-catch
        //        try
        //        {
        //            StringBuilder strbTitle = new StringBuilder(255);
        //            int nLength = GetWindowText(hWnd, strbTitle, strbTitle.Capacity + 1);
        //            string ptitle = strbTitle.ToString();

        //            if (IsWindowVisible(hWnd) && string.IsNullOrEmpty(ptitle) == false)
        //            {
        //                uint procid = 0;
        //                GetWindowThreadProcessId(hWnd, out procid);

        //                // This is the specific line that triggers the "Access Denied" crash
        //                using (Process p = Process.GetProcessById((int)procid))
        //                {
        //                    // We wrap the check for our own process to avoid moving the Wintermelon window
        //                    if (p.MainModule.FileName.IndexOf("Wintermelon", StringComparison.OrdinalIgnoreCase) >= 0)
        //                    {
        //                        return true;
        //                    }
        //                }
        //                handleInfo.Add(hWnd);
        //            }
        //        }
        //        catch (System.ComponentModel.Win32Exception)
        //        {
        //            // This catches the "Access is denied" error and simply moves to the next window
        //            return true;
        //        }
        //        catch (Exception)
        //        {
        //            // Catches any other rare errors (like a process closing while being checked)
        //            return true;
        //        }

        //        return true;
        //    };
        //    // Enumerate visible windows using filter defined above
        //    if (!EnumDesktopWindows(IntPtr.Zero, filter, IntPtr.Zero))
        //    {
        //        // Get the last Win32 error code
        //        int nErrorCode = Marshal.GetLastWin32Error();
        //        string strErrMsg = String.Format("EnumDesktopWindows failed with code {0}.", nErrorCode);
        //        throw new Exception(strErrMsg);
        //    }

        //    // Take appropriate action based on whether "save" or "restore" was passed in
        //    // 'save' --> Save window positions and then clone displays
        //    if (string.Equals(args[0], "save", StringComparison.OrdinalIgnoreCase))
        //    {               
        //        //*Console.WriteLine("Saving window positions to {0}...", settingsFile);
        //        File.WriteAllText(settingsFile, "");

        //        foreach (var item in procSettings)
        //        {
        //            File.AppendAllText(settingsFile, item + "\n");
        //        }
        //        //*Console.WriteLine("Done.");

        //        // Duplicate screen using DisplaySwitch.exe process (slow approach)
        //        //ProcessStartInfo startInfo = new ProcessStartInfo();
        //        //startInfo.FileName = @"C:\Windows\System32\DisplaySwitch.exe";
        //        //startInfo.Arguments = @"/clone";
        //        //Process.Start(startInfo);

        //        // Duplicate using SetDisplayConfig() (faster approach)
        //        CloneDisplays();
        //    }

        //    else if (string.Equals(args[0], "restore", StringComparison.OrdinalIgnoreCase))
        //    {
        //        Log("--- RESTORE TRIGGERED (Optimized Polling) ---");
        //        bool isKbConnected = false;

        //        // Check every 500ms for up to 7.5 seconds
        //        // Most keyboards connect between 3s and 7s
        //        for (int i = 1; i <= 15; i++)
        //        {
        //            isKbConnected = await IsBluetoothKeyboardConnected();
        //            if (isKbConnected)
        //            {
        //                Log($"i4 connected! Detected at {i * 0.5} seconds.");
        //                break;
        //            }
        //            await System.Threading.Tasks.Task.Delay(500);
        //        }

        //        if (isKbConnected)
        //        {
        //            Log("Decision: Switch to TV.");
        //            ExternalDisplay();
        //        }
        //        else
        //        {
        //            Log("Decision: i4 not found/connected. Stay on PC.");
        //            InternalDisplay();
        //        }

        //        // Settling delay before moving windows
        //        await System.Threading.Tasks.Task.Delay(2000);
        //        // ----------------------------------------------------------------------------------

        //        //*Console.WriteLine("Restoring window positions from {0}...", settingsFile);
        //        // Read settings.txt into a dictionary with pid+"_"+ptitle serving as key
        //        var processInfo = new Dictionary<string, WINDOWPLACEMENT>();
        //        System.IO.StreamReader infile = new System.IO.StreamReader(@settingsFile);
        //        string line;
        //        while ((line = infile.ReadLine()) != null)
        //        {
        //            if (string.IsNullOrWhiteSpace(line))
        //            { 
        //                continue;
        //            }
        //            //System.Console.WriteLine(line);
        //            var dict = line.Split('\t').Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
        //            string pid = dict["PID"];
        //            string id = dict["ID"];
        //            string ptitle = dict["Title"];
        //            bool isPrimary = string.Equals(dict["Primary"], "True", StringComparison.OrdinalIgnoreCase);

        //            // Save only for those that are not on the primary screen
        //            if (!isPrimary)
        //            {
        //                WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
        //                wp.length = Marshal.SizeOf(wp);
        //                wp.flags = int.Parse(dict["Flags"]);
        //                wp.showCmd = int.Parse(dict["Cmd"]);
        //                var minxy = dict["Min"].Replace("(", "").Replace(")", "").Split(",");
        //                wp.minPosition = new POINT(int.Parse(minxy[0]), int.Parse(minxy[1]));
        //                var maxxy = dict["Max"].Replace("(", "").Replace(")", "").Split(",");
        //                wp.maxPosition = new POINT(int.Parse(maxxy[0]), int.Parse(maxxy[1]));
        //                var pqrs = dict["Rect"].Replace("(", "").Replace(")", "").Split(",");
        //                wp.normalPosition = new RECT(int.Parse(pqrs[0]), int.Parse(pqrs[1]),int.Parse(pqrs[2]), int.Parse(pqrs[3]));

        //                processInfo.Add(pid + "_" + id + "_" + ptitle, wp);
        //                //Console.WriteLine("Info='{0}'", pid + "_" + id + "_" + ptitle);
        //            }
        //        }
        //        infile.Close();

        //        //*Console.WriteLine("Restoring {0} windows", handleInfo.Count());

        //        foreach (var handle in handleInfo)
        //        {
        //            try
        //            {
        //                StringBuilder strbTitle = new StringBuilder(255);
        //                GetWindowText(handle, strbTitle, strbTitle.Capacity + 1);
        //                string ptitle = strbTitle.ToString();

        //                uint procid = 0;
        //                GetWindowThreadProcessId(handle, out procid);

        //                // --- THE FIX: This part often causes the "Access Denied" error ---
        //                using (Process p = Process.GetProcessById((int)procid))
        //                {
        //                    // We just need the ID to match our dictionary
        //                    string pid = procid.ToString();
        //                    string id = handle.ToString();

        //                    if (processInfo.ContainsKey(pid + "_" + id + "_" + ptitle))
        //                    {
        //                        WINDOWPLACEMENT wp = processInfo[pid + "_" + id + "_" + ptitle];
        //                        wp.length = Marshal.SizeOf(wp);
        //                        wp.showCmd = 1;
        //                        SetWindowPlacement(handle, ref wp);
        //                    }
        //                }
        //            }
        //            catch (System.ComponentModel.Win32Exception)
        //            {
        //                // Ignore protected system processes (Access Denied)
        //                continue;
        //            }
        //        }


        //        //*Console.WriteLine("Done.");
        //    }
        //    else
        //    {               
        //         //*Console.WriteLine("Usage: Wintermelon [save | restore]");
        //         return;                
        //    }

        //}
        private static async System.Threading.Tasks.Task<bool> IsBluetoothKeyboardConnected()
        {
            try
            {
                // 1. Broadly search for all paired Bluetooth LE devices
                string selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
                var devices = await DeviceInformation.FindAllAsync(selector);

                foreach (var device in devices)
                {
                    // 2. Look for your specific keyboard name
                    if (device.Name.Contains("i4", StringComparison.OrdinalIgnoreCase))
                    {
                        // 3. Attempt to create a BluetoothLEDevice object to get a "Live" status
                        using (var bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id))
                        {
                            if (bleDevice != null)
                            {
                                var status = bleDevice.ConnectionStatus;
                                Log($"MATCH: Found 'i4'. LIVE Connection Status: {status}");
                                return status == BluetoothConnectionStatus.Connected;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Log($"Critical API Error: {ex.Message}"); }
            return false;
        }


    }
}
