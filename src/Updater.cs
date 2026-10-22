using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Nowcast
{
    class Release
    {
        public Version Version;
        public string Tag;
        public string ExeUrl;
        public string PageUrl;
    }

    // Updates from GitHub releases: the new exe replaces this one in place.
    static class Updater
    {
        static readonly HttpClient http = CreateClient();

        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            // GitHub's API refuses requests without one.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("discord-nowcast/" + Program.Version);
            return client;
        }

        static string OldPath
        {
            get { return Application.ExecutablePath + ".old"; }
        }

        // The latest release, or null when it is not newer than this one.
        public static Release FindNewer()
        {
            var text = http.GetStringAsync("https://api.github.com/repos/" + Program.Repo + "/releases/latest").Result;
            var values = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(text);
            var tag = values["tag_name"] as string ?? "";
            Version version;
            if (!Version.TryParse(tag.TrimStart('v'), out version)) return null;
            var release = new Release { Version = version, Tag = tag, PageUrl = values["html_url"] as string };
            foreach (Dictionary<string, object> asset in (ArrayList)values["assets"])
            {
                if (asset["name"] as string == "discord-nowcast.exe") release.ExeUrl = asset["browser_download_url"] as string;
            }
            return version > new Version(Program.Version) ? release : null;
        }

        // Downloads the new exe next to this one, swaps them and starts it.
        // A running exe cannot be overwritten but can be renamed; the .old
        // file is removed by the next start.
        public static void Install(Release release)
        {
            if (release.ExeUrl == null) throw new InvalidOperationException("the release has no discord-nowcast.exe");
            var path = Application.ExecutablePath;
            var newPath = path + ".new";
            var bytes = http.GetByteArrayAsync(release.ExeUrl).Result;
            File.WriteAllBytes(newPath, bytes);
            if (File.Exists(OldPath)) File.Delete(OldPath);
            File.Move(path, OldPath);
            File.Move(newPath, path);
            Log.Write("Updated to " + release.Tag + ", restarting");
            Process.Start(path, "--updated");
        }

        public static void CleanUp()
        {
            // The old exe may still be closing for a moment.
            for (var attempt = 0; attempt < 10 && File.Exists(OldPath); attempt++)
            {
                try
                {
                    File.Delete(OldPath);
                }
                catch (Exception)
                {
                    Thread.Sleep(300);
                }
            }
        }
    }
}
