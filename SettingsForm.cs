using Microsoft.Win32; // Required for SystemEvents
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Enumeration;

namespace Wintermelon
{
    public partial class SettingsForm : Form
    {
        private ConfigData currentConfig = new ConfigData();

        public SettingsForm()
        {
            InitializeComponent();

            // --- CRASH PREVENTION: Ensure an Icon exists ---
            if (notifyIcon1.Icon == null)
            {
                // Use a built-in Windows System icon as a backup 
                // so the app doesn't crash if your .ico file is missing.
                notifyIcon1.Icon = SystemIcons.Application;
            }
            notifyIcon1.Visible = true;

            // --- Your existing Context Menu logic ---
            ContextMenuStrip trayMenu = new ContextMenuStrip();

            ToolStripMenuItem settingsMenu = new ToolStripMenuItem("Settings", null, (s, e) =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            });

            ToolStripMenuItem exitMenu = new ToolStripMenuItem("Exit", null, (s, e) =>
            {
                Application.Exit();
            });

            trayMenu.Items.Add(settingsMenu);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exitMenu);

            notifyIcon1.ContextMenuStrip = trayMenu;

            // --- Loading Logic ---
            LoadConfigFromFile();

            // Connect the Unlock Event
            Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
        }


        private async void SettingsForm_Load(object sender, EventArgs e)
        {
            // Dark Theme styling
            clbDevices.BackColor = Color.FromArgb(45, 45, 48);
            clbDevices.ForeColor = Color.White;
            clbDevices.BorderStyle = BorderStyle.FixedSingle;
            clbDevices.CheckOnClick = true;

            await PopulateRealDevices();
        }

        private async Task PopulateRealDevices()
        {
            clbDevices.Items.Clear();
            string aqsFilter = "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"";
            string[] properties = { "System.Devices.Aep.IsConnected" };

            try
            {
                var devices = await DeviceInformation.FindAllAsync(aqsFilter, properties, DeviceInformationKind.AssociationEndpoint);
                foreach (var device in devices)
                {
                    if (!string.IsNullOrEmpty(device.Name))
                    {
                        bool isChecked = currentConfig.TargetDevices.Contains(device.Name);
                        clbDevices.Items.Add(device.Name, isChecked);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Bluetooth Error: " + ex.Message); }
        }

        // STEP 2 (Event Handler): Detect Unlock
        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                // Run the check logic on a separate thread to avoid freezing the UI
                Task.Run(() => CheckDevicesAndSwitchDisplay());
            }
        }

        // STEP 3: The Logic (Check Devices & Switch)
        private async Task CheckDevicesAndSwitchDisplay()
        {
            // Reload config to ensure we have the latest user selection
            LoadConfigFromFile();

            string aqsFilter = "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"";
            string[] properties = { "System.Devices.Aep.IsConnected" };

            var devices = await DeviceInformation.FindAllAsync(aqsFilter, properties, DeviceInformationKind.AssociationEndpoint);

            // Find if ANY of our saved devices are currently connected
            bool anyMatch = devices.Any(d =>
                currentConfig.TargetDevices.Contains(d.Name) &&
                d.Properties.ContainsKey("System.Devices.Aep.IsConnected") &&
                (bool)d.Properties["System.Devices.Aep.IsConnected"] == true);

            if (anyMatch)
            {
                ExternalDisplay(); // Call your existing API for TV
            }
            else
            {
                InternalDisplay(); // Call your existing API for Monitor
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            currentConfig.TargetDevices = clbDevices.CheckedItems.Cast<object>().Select(x => x.ToString()).ToList();
            string json = JsonSerializer.Serialize(currentConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("config.json", json);
            Close();
        }
        protected override void OnResize(EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide(); // Hide from taskbar
                notifyIcon1.Visible = true;
            }
            base.OnResize(e);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }


        private void LoadConfigFromFile()
        {
            if (File.Exists("config.json"))
            {
                try
                {
                    string json = File.ReadAllText("config.json");
                    currentConfig = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
                }
                catch { currentConfig = new ConfigData(); }
            }
        }

        // Ensure you detach the event when the form is closed to prevent memory leaks
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 1. Unhook the Windows Unlock listener
            Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;

            // 2. Kill the tray icon immediately (Prevents "ghost icons")
            if (notifyIcon1 != null)
            {
                notifyIcon1.Visible = false;
                notifyIcon1.Dispose();
            }

            base.OnFormClosing(e);
        }


        // --- API PLACEHOLDERS (Ensure these match your existing Wintermelon methods) ---
        private void ExternalDisplay() { /* Your code to switch to TV */ }
        private void InternalDisplay() { /* Your code to switch to Monitor */ }
    }

    public class ConfigData
    {
        public List<string> TargetDevices { get; set; } = new List<string>();
    }

}
