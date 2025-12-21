namespace Wintermelon
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            bluetooth = new System.Windows.Forms.TabControl();
            tabPage1 = new System.Windows.Forms.TabPage();
            label2 = new System.Windows.Forms.Label();
            clbDevices = new System.Windows.Forms.CheckedListBox();
            button1 = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            tabPage2 = new System.Windows.Forms.TabPage();
            notifyIcon1 = new System.Windows.Forms.NotifyIcon(components);
            ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(components);
            Settings = new System.Windows.Forms.ToolStripMenuItem();
            Exit = new System.Windows.Forms.ToolStripMenuItem();
            bluetooth.SuspendLayout();
            tabPage1.SuspendLayout();
            ContextMenuStrip.SuspendLayout();
            SuspendLayout();
            // 
            // bluetooth
            // 
            bluetooth.Controls.Add(tabPage1);
            bluetooth.Controls.Add(tabPage2);
            bluetooth.Location = new System.Drawing.Point(12, 12);
            bluetooth.Name = "bluetooth";
            bluetooth.SelectedIndex = 0;
            bluetooth.Size = new System.Drawing.Size(725, 511);
            bluetooth.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
            tabPage1.Controls.Add(label2);
            tabPage1.Controls.Add(clbDevices);
            tabPage1.Controls.Add(button1);
            tabPage1.Controls.Add(label1);
            tabPage1.Location = new System.Drawing.Point(4, 37);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new System.Windows.Forms.Padding(3);
            tabPage1.Size = new System.Drawing.Size(717, 470);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Bluetooth";
            // 
            // label2
            // 
            label2.Font = new System.Drawing.Font("Segoe UI", 8F);
            label2.Location = new System.Drawing.Point(6, 102);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(242, 61);
            label2.TabIndex = 4;
            label2.Text = "Note: The display will switch to TV if any checked device is detected on unlock.";
            // 
            // clbDevices
            // 
            clbDevices.BackColor = System.Drawing.Color.FromArgb(69, 69, 72);
            clbDevices.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            clbDevices.CheckOnClick = true;
            clbDevices.ForeColor = System.Drawing.SystemColors.InactiveBorder;
            clbDevices.FormattingEnabled = true;
            clbDevices.Location = new System.Drawing.Point(254, 14);
            clbDevices.Name = "clbDevices";
            clbDevices.Size = new System.Drawing.Size(457, 147);
            clbDevices.TabIndex = 3;
            // 
            // button1
            // 
            button1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            button1.Font = new System.Drawing.Font("Segoe UI", 9F);
            button1.ForeColor = System.Drawing.Color.MidnightBlue;
            button1.Location = new System.Drawing.Point(6, 427);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(705, 37);
            button1.TabIndex = 2;
            button1.Text = "Save";
            button1.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.ForeColor = System.Drawing.Color.White;
            label1.Location = new System.Drawing.Point(6, 14);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(242, 28);
            label1.TabIndex = 0;
            label1.Text = "Select Bluetooth Device[s]:";
            // 
            // tabPage2
            // 
            tabPage2.Location = new System.Drawing.Point(4, 29);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new System.Windows.Forms.Padding(3);
            tabPage2.Size = new System.Drawing.Size(717, 478);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Display";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // notifyIcon1
            // 
            notifyIcon1.Icon = (System.Drawing.Icon)resources.GetObject("notifyIcon1.Icon");
            notifyIcon1.Text = "notifyIcon1";
            notifyIcon1.Visible = true;
            // 
            // ContextMenuStrip
            // 
            ContextMenuStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            ContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { Settings, Exit });
            ContextMenuStrip.Name = "Settings";
            ContextMenuStrip.Size = new System.Drawing.Size(212, 80);
            // 
            // Settings
            // 
            Settings.Name = "Settings";
            Settings.Size = new System.Drawing.Size(211, 24);
            Settings.Text = "toolStripMenuItem1";
            // 
            // Exit
            // 
            Exit.Name = "Exit";
            Exit.Size = new System.Drawing.Size(211, 24);
            Exit.Text = "toolStripMenuItem2";
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(11F, 28F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            ClientSize = new System.Drawing.Size(749, 535);
            Controls.Add(bluetooth);
            Font = new System.Drawing.Font("Segoe UI", 12F);
            ForeColor = System.Drawing.Color.Linen;
            Margin = new System.Windows.Forms.Padding(4);
            Name = "SettingsForm";
            Text = "SettingsForm";
            bluetooth.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            ContextMenuStrip.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl bluetooth;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.CheckedListBox clbDevices;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip ContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem Settings;
        private System.Windows.Forms.ToolStripMenuItem Exit;
    }
}