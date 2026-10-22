using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Nowcast
{
    class SourceInfo
    {
        // As shown in the status: "Яндекс Музыка", "SoundCloud", "Chrome".
        public string Name;
        // File name of its logo in assets/sources, or null for none.
        public string Icon;
        // Search page the status button opens, the query appended; null: YouTube.
        public string Search;

        public string IconUrl
        {
            get
            {
                return Icon == null ? null
                    : "https://raw.githubusercontent.com/" + Program.Repo + "/main/assets/sources/" + Icon + ".png";
            }
        }
    }

    // Which app a session comes from, by its app ID.
    static class Sources
    {
        class Rule
        {
            public Regex Pattern;
            public string Name;
            public string Icon;
            public string Search;

            public Rule(string pattern, string name, string icon, string search)
            {
                Pattern = new Regex(pattern, RegexOptions.IgnoreCase);
                Name = name;
                Icon = icon;
                Search = search;
            }
        }

        // Order matters: Yandex Music before Yandex Browser, YouTube Music before YouTube.
        static readonly Rule[] rules =
        {
            new Rule(@"yandex.*music|music.*yandex|яндекс.?музык", Text.Ru ? "Яндекс Музыка" : "Yandex Music", "yandexmusic", "https://music.yandex.ru/search?text="),
            new Rule(@"spotify", "Spotify", "spotify", "https://open.spotify.com/search/"),
            new Rule(@"soundcloud", "SoundCloud", "soundcloud", "https://soundcloud.com/search?q="),
            new Rule(@"youtube.?music", "YouTube Music", "youtubemusic", "https://music.youtube.com/search?q="),
            new Rule(@"youtube", "YouTube", "youtube", "https://www.youtube.com/results?search_query="),
            new Rule(@"telegram", "Telegram", "telegram", null),
            new Rule(@"vk.?music|vk музык|vkontakte|\bvk\b", Text.Ru ? "VK Музыка" : "VK Music", "vk", "https://vk.com/audio?q="),
            new Rule(@"applemusic|apple music|itunes", "Apple Music", "applemusic", "https://music.apple.com/search?term="),
            new Rule(@"zunemusic", Text.Ru ? "Медиаплеер Windows" : "Windows Media Player", "music", null),
            new Rule(@"aimp", "AIMP", "music", null),
            new Rule(@"foobar", "foobar2000", "music", null),
            new Rule(@"vlc", "VLC", "vlcmediaplayer", null),
            new Rule(@"musicbee", "MusicBee", "music", null),
            new Rule(@"yandex|^browser", Text.Ru ? "Яндекс Браузер" : "Yandex Browser", "yandexbrowser", null),
            new Rule(@"chrome", "Chrome", "googlechrome", null),
            new Rule(@"msedge|microsoftedge", "Edge", "microsoftedge", null),
            new Rule(@"firefox|308046B0AF4A39CB", "Firefox", "firefoxbrowser", null),
            new Rule(@"opera", "Opera", "opera", null),
            new Rule(@"vivaldi", "Vivaldi", "vivaldi", null),
            new Rule(@"brave", "Brave", "brave", null),
        };

        public static SourceInfo Resolve(string app, Settings settings)
        {
            string name = null;
            // Your own names from config.json come first: {"chrome": "YouTube"}.
            foreach (var pair in settings.Names)
            {
                if (app.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0) name = pair.Value;
            }
            // Sites installed as apps (Chrome._crx_…) and Store apps (…!App):
            // the name from the Start menu, so SoundCloud reads as SoundCloud.
            if (name == null && (app.Contains("_crx_") || app.Contains("!"))) name = StartMenuName(app);

            // The logo and the button follow the name when it is known, the app ID otherwise.
            var rule = (name == null ? null : Match(name)) ?? Match(app);
            if (name == null) name = rule != null ? rule.Name : Fallback(app);
            return new SourceInfo
            {
                Name = name,
                Icon = rule != null ? rule.Icon : null,
                Search = rule != null ? rule.Search : null,
            };
        }

        static Rule Match(string text)
        {
            foreach (var rule in rules)
            {
                if (rule.Pattern.IsMatch(text)) return rule;
            }
            return null;
        }

        // Microsoft.Package_8wekyb3d8bbwe!App or Something.exe -> Package / Something.
        static string Fallback(string app)
        {
            var name = Regex.Replace(app, "!.*$", "");
            name = Regex.Replace(name, "_[a-z0-9]{13}$", "");
            name = Regex.Replace(name, @"\.exe$", "", RegexOptions.IgnoreCase);
            var parts = name.Split('.');
            name = parts[parts.Length - 1];
            return name != "" ? name : (Text.Ru ? "Музыка" : "Music");
        }

        // App ID -> name, as the Start menu (shell:AppsFolder) lists them.
        // Read again at most once a minute, when an unknown app shows up.
        static readonly Dictionary<string, string> startMenu = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static DateTime startMenuReadAt = DateTime.MinValue;

        static string StartMenuName(string app)
        {
            string name;
            if (startMenu.TryGetValue(app, out name)) return name;
            if ((DateTime.Now - startMenuReadAt).TotalSeconds < 60) return null;
            startMenuReadAt = DateTime.Now;
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                var shell = Activator.CreateInstance(shellType);
                var folder = shellType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { "shell:AppsFolder" });
                var items = folder.GetType().InvokeMember("Items", BindingFlags.InvokeMethod, null, folder, null);
                foreach (var item in (IEnumerable)items)
                {
                    var id = item.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, item, null) as string;
                    var title = item.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, item, null) as string;
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(title)) startMenu[id] = title;
                }
            }
            catch (Exception e)
            {
                Log.Write("Start menu names unavailable: " + e.Message);
            }
            return startMenu.TryGetValue(app, out name) ? name : null;
        }
    }
}
