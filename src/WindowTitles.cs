using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Nowcast
{
    // Titles of the visible windows on the desktop, for telling the site in a browser.
    static class WindowTitles
    {
        delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr window, StringBuilder text, int size);

        public static List<string> Read()
        {
            var titles = new List<string>();
            EnumWindows((window, parameter) =>
            {
                if (!IsWindowVisible(window)) return true;
                var length = GetWindowTextLength(window);
                if (length == 0) return true;
                var text = new StringBuilder(length + 1);
                GetWindowText(window, text, text.Capacity);
                titles.Add(text.ToString());
                return true;
            }, IntPtr.Zero);
            return titles;
        }
    }
}
