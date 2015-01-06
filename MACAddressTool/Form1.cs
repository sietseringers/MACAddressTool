using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Security.Permissions;
using System.Management;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;


namespace MACAddressTool
{
    public partial class Form1 : Form
    {
        public class Adapter
        {
            public ManagementObject adapter;
            public string adaptername;
            public string customname;
            public int devnum;

            public Adapter(ManagementObject a, string aname, string cname, int n)
            {
                this.adapter = a;
                this.adaptername = aname;
                this.customname = cname;
                this.devnum = n;
            }

            public Adapter(NetworkInterface i) : this(i.Description) { }

            public Adapter(string aname)
            {
                this.adaptername = aname;

                var searcher = new ManagementObjectSearcher("select * from win32_networkadapter where Name='" + adaptername + "'");
                var found = searcher.Get();
                this.adapter = found.Cast<ManagementObject>().FirstOrDefault();

                // Extract adapter number; this should correspond to the keys under
                // HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}
                try
                {
                    var match = Regex.Match(adapter.Path.RelativePath, "\\\"(\\d+)\\\"$");
                    this.devnum = int.Parse(match.Groups[1].Value);
                }
                catch
                {
                    return;
                }

                // Find the name the user gave to it in "Network Adapters"
                this.customname = NetworkInterface.GetAllNetworkInterfaces().Where(
                    i => i.Description == adaptername
                ).Select(
                    i => " (" + i.Name + ")"
                ).FirstOrDefault();
            }

            public NetworkInterface ManagedAdapter
            {
                get
                {
                    return NetworkInterface.GetAllNetworkInterfaces().Where(
                        nic => nic.Description == this.adaptername
                    ).FirstOrDefault();
                }
            }

            public string Mac
            {
                get
                {
                    try
                    {
                        return BitConverter.ToString(this.ManagedAdapter.GetPhysicalAddress().GetAddressBytes()).Replace("-", "").ToUpper();
                    }
                    catch { return null; }
                }
            }

            public string RegistryKey
            {
                get
                {
                    return String.Format(@"SYSTEM\ControlSet001\Control\Class\{{4D36E972-E325-11CE-BFC1-08002BE10318}}\{0:D4}", this.devnum);
                }
            }

            public string RegistryMac {
                get
                {
                    try
                    {
                        using (RegistryKey regkey = Registry.LocalMachine.OpenSubKey(this.RegistryKey, false))
                        {
                            return regkey.GetValue("NetworkAddress").ToString();
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            public bool SetRegistryMac(string value)
            {
                bool shouldReenable = false;
                try
                {
                    if (value.Length > 0 && !Adapter.IsValidMac(value, false))
                        throw new Exception(value + " is not a valid mac address");

                    using (RegistryKey regkey = Registry.LocalMachine.OpenSubKey(this.RegistryKey, true))
                    {
                        if (regkey.GetValue("AdapterModel") as string != this.adaptername
                            && regkey.GetValue("DriverDesc") as string != this.adaptername)
                            throw new Exception("Adapter not found in registry");

                        string question = value.Length > 0 ?
                            "Changing MAC-adress of adapter {0} from {1} to {2}. Proceed?" :
                            "Clearing custom MAC-address of adapter {0}. Proceed?";

                        DialogResult proceed = MessageBox.Show(
                            String.Format(question, this.ToString(), this.Mac, value),
                            "Change MAC-address?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (proceed != DialogResult.Yes)
                            return false;

                        var result = (uint)adapter.InvokeMethod("Disable", null);
                        if (result != 0)
                            throw new Exception("Failed to disable network adapter.");

                        // If we're here the adapter has been disabled, so we set the flag that will re-enable it in the finally block
                        shouldReenable = true;

                        if (regkey != null)
                        {
                            if (value.Length > 0)
                                regkey.SetValue("NetworkAddress", value, RegistryValueKind.String);
                            else
                                regkey.DeleteValue("NetworkAddress");
                        }

                        return true;
                    }
                }

                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    return false;
                }

                finally
                {
                    if (shouldReenable)
                    {
                        uint result = (uint)adapter.InvokeMethod("Enable", null);
                        if (result != 0)
                            MessageBox.Show("Failed to re-enable network adapter.");
                    }
                }
            }

            public override string ToString()
            {
                return this.adaptername + this.customname;
            }

            public static string GetNewMac()
            {
                System.Random r = new System.Random();
                ///*  Generate first byte:
                //    - Take 6 random bits
                //    - Shift them two bits to the right
                //    - Make the least significant two bits 10
                //    In hex, the resulting byte should end with 2, 6, A or E */
                //long firstbyte = (r.Next(63) << 2) + 2;

                //// Generate second byte
                //long firsttwobytes = (firstbyte << 8) + r.Next(255);

                //// Shift the first two bytes to be the first two bytes of the mac address
                //long mac = firsttwobytes << 32;

                //// Choose the remaining four bytes randomly
                //mac = mac + r.Next();

                //return mac.ToString("x12").ToUpper();

                byte[] bytes = new byte[6];
                r.NextBytes(bytes);

                // Set second bit to 1
                bytes[0] = (byte)(bytes[0] | 0x02);
                // Set first bit to 0
                bytes[0] = (byte)(bytes[0] & 0xfe);

                return MacToString(bytes);
            }

            public static bool IsValidMac(string mac, bool actual)
            {
                // 6 bytes == 12 hex characters (without dashes/dots/anything else)
                if (mac.Length != 12)
                    return false;

                // Should be uppercase
                if (mac != mac.ToUpper())
                    return false;

                // Should not contain anything other than hexadecimal digits
                if (!Regex.IsMatch(mac, "^[0-9A-F]*$"))
                    return false;

                if (!actual) // The second character should be a 2, 6, A or E
                {
                    char c = mac[1];
                    return (c == '2' || c == '6' || c == 'A' || c == 'E');
                }

                return true;
            }

            public static bool IsValidMac(byte[] bytes, bool actual)
            {
                return IsValidMac(Adapter.MacToString(bytes), actual);
            }

            public static string MacToString(byte[] bytes)
            {
                return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(
                    a => Adapter.IsValidMac(a.GetPhysicalAddress().GetAddressBytes(), true)
                ).OrderByDescending(a => a.Speed))
            {
                AdaptersComboBox.Items.Add(new Adapter(adapter));
            }
            

            AdaptersComboBox.SelectedIndex = 0;
        }

        private void UpdateAddresses()
        {
            Adapter a = AdaptersComboBox.SelectedItem as Adapter;
            this.CurrentMacTextBox.Text = a.RegistryMac;
            this.ActualMacLabel.Text = a.Mac;
        }

        private void AdaptersComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateAddresses();
        }

        private void RandomButton_Click(object sender, EventArgs e)
        {
            CurrentMacTextBox.Text = Adapter.GetNewMac();
            //SetRegistryMac(Adapter.GetNewMac());
        }

        private void UpdateButton_Click(object sender, EventArgs e)
        {
            if (!Adapter.IsValidMac(CurrentMacTextBox.Text, false))
            {
                MessageBox.Show("Entered MAC-address is not valid; will not update.", "Invalid MAC-address specified", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetRegistryMac(CurrentMacTextBox.Text);
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            SetRegistryMac("");
        }

        private void SetRegistryMac(string mac)
        {
            Adapter a = AdaptersComboBox.SelectedItem as Adapter;

            if (a.SetRegistryMac(mac))
            {
                System.Threading.Thread.Sleep(100);
                UpdateAddresses();
                MessageBox.Show("Done!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RereadButton_Click(object sender, EventArgs e)
        {
            UpdateAddresses();
        }

        private void CurrentMacTextBox_TextChanged(object sender, EventArgs e)
        {
            this.UpdateButton.Enabled = Adapter.IsValidMac(this.CurrentMacTextBox.Text, false);
        }
    }
}
