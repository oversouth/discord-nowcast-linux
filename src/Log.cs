using System;
using System.IO;
using System.Text;

namespace Nowcast
{
    // %APPDATA%\discord-nowcast\log.txt: the current run only.
    static class Log
    {
        public static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord-nowcast");
        public static readonly string FilePath = Path.Combine(Folder, "log.txt");

        static readonly object gate = new object();

        public static void Start()
        {
            Directory.CreateDirectory(Folder);
            lock (gate)
            {
                try
                {
                    File.WriteAllText(FilePath, "", Encoding.UTF8);
                }
                catch (IOException)
                {
                }
            }
        }

        public static void Write(string text)
        {
            lock (gate)
            {
                try
                {
                    // A run of weeks should not grow it without end.
                    var info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > 1024 * 1024) File.WriteAllText(FilePath, "", Encoding.UTF8);
                    File.AppendAllText(FilePath, DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
