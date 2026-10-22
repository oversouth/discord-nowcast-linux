using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Nowcast
{
    // Starting with Windows: a value under HKCU\...\Run pointing at this exe.
    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "discord-nowcast";

        static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\""; }
        }

        public static bool IsEnabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return key != null && key.GetValue(Name) is string;
                }
            }
        }

        public static void Enable()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                key.SetValue(Name, Command);
            }
        }

        public static void Disable()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key != null) key.DeleteValue(Name, false);
            }
        }

        // The exe moved since it was added: point the entry at it again.
        public static void Refresh()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                var value = key == null ? null : key.GetValue(Name) as string;
                if (value != null && value != Command) key.SetValue(Name, Command);
            }
        }

        static readonly Regex oldScript = new Regex(@"-File\s+(""[^""]*\\|\S*\\|\.\\)?nowcast\.ps1""?(\s|$)", RegexOptions.IgnoreCase);

        // 1.0.x was a PowerShell script started by a Startup folder shortcut:
        // remove the shortcut and stop the script, so the two never fight.
        public static void RemoveOldVersion()
        {
            try
            {
                var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "discord-nowcast.lnk");
                if (File.Exists(shortcut)) File.Delete(shortcut);

                var query = "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'powershell.exe'";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject process in searcher.Get())
                    {
                        var line = process["CommandLine"] as string ?? "";
                        // Only the script itself: -File, then a path ending in nowcast.ps1.
                        if (oldScript.IsMatch(line))
                        {
                            Process.GetProcessById(Convert.ToInt32(process["ProcessId"])).Kill();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Nothing to clean up, or no rights to; neither stops the start.
            }
        }
    }
}
