using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Nowcast
{
    // The work itself, on its own thread: once a second reads what plays and,
    // when it changed, sends the status to Discord.
    class Engine
    {
        readonly Discord discord = new Discord();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly FileSystemWatcher watcher;
        Thread thread;
        volatile bool stopping;
        volatile bool reload;
        Settings settings;

        // What is in the status now: track key, or "none".
        string sentKey;
        long? sentStart;
        bool? sentPlaying;
        DateTime sentAt = DateTime.MinValue;
        DateTime nextConnect = DateTime.MinValue;
        string shownKey;

        public bool IsFirstRun { get; private set; }
        public DateTime HiddenUntil { get; private set; }
        // For the tray: one line about the status and the sessions Windows reports.
        public string Status { get; private set; }
        public List<Track> Sessions { get; private set; }
        public event Action Changed;

        public Engine()
        {
            bool isNew;
            settings = Settings.Load(out isNew);
            IsFirstRun = isNew;
            Status = Text.Starting;
            Sessions = new List<Track>();

            watcher = new FileSystemWatcher(Log.Folder, "config.json");
            watcher.Changed += delegate { reload = true; wake.Set(); };
            watcher.Created += delegate { reload = true; wake.Set(); };
            watcher.EnableRaisingEvents = true;
        }

        public Settings Settings
        {
            get { return settings; }
        }

        public void Start()
        {
            // STA, like the PowerShell this grew from: the Start menu lookup is a shell object.
            thread = new Thread(Loop) { IsBackground = true, Name = "engine" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public void Stop()
        {
            stopping = true;
            wake.Set();
            if (thread != null) thread.Join(5000);
            watcher.Dispose();
        }

        public void Hide(TimeSpan time)
        {
            HiddenUntil = DateTime.Now + time;
            wake.Set();
        }

        public void Show()
        {
            HiddenUntil = DateTime.MinValue;
            wake.Set();
        }

        void Loop()
        {
            try
            {
                Media.Open();
            }
            catch (Exception e)
            {
                Log.Write("Media sessions unavailable: " + e.Message);
                Report("Windows media sessions unavailable");
                return;
            }
            while (!stopping)
            {
                try
                {
                    Tick();
                }
                catch (Exception e)
                {
                    Log.Write("Error: " + e);
                }
                wake.WaitOne(1000);
            }
            if (discord.IsConnected)
            {
                try
                {
                    discord.SetActivity(null);
                }
                catch (Exception)
                {
                }
            }
            discord.Dispose();
        }

        void Tick()
        {
            if (reload)
            {
                reload = false;
                bool isNew;
                settings = Settings.Load(out isNew);
                sentKey = null;
                Log.Write("Settings reloaded");
            }

            if (!discord.IsConnected && DateTime.Now >= nextConnect)
            {
                if (discord.Connect(settings.ClientId)) sentKey = null;
                else nextConnect = DateTime.Now.AddSeconds(15);
            }

            var sessions = Media.Read(settings);
            var isHidden = DateTime.Now < HiddenUntil;
            var track = isHidden ? null : Pick(sessions);
            Sessions = sessions;
            Report(!discord.IsConnected ? Text.DiscordOff
                : isHidden ? string.Format(Text.HiddenUntil, HiddenUntil)
                : track == null ? Text.NothingPlaying
                : Describe(track));
            if (!discord.IsConnected) return;

            var key = track == null ? "none" : track.Key;
            var start = track == null ? null : StartedAt(track);
            bool? playing = track == null ? (bool?)null : track.IsPlaying;
            // A seek: the start of the track moved by more than 3 seconds.
            var jumped = start.HasValue && sentStart.HasValue && Math.Abs(start.Value - sentStart.Value) > 3000;
            var changed = key != sentKey || playing != sentPlaying || jumped;
            // Discord takes at most 5 updates per 20 seconds.
            if (!changed || (DateTime.Now - sentAt).TotalSeconds < 4) return;

            try
            {
                if (track == null)
                {
                    discord.SetActivity(null);
                    Log.Write("Status cleared");
                }
                else
                {
                    var activity = Activity.Build(track, start, settings);
                    discord.SetActivity(activity);
                    Log.Write("Status: " + Describe(track) + (activity.ContainsKey("assets") ? "" : " (no cover)"));
                }
                shownKey = track == null ? null : key;
                sentKey = key;
                sentStart = start;
                sentPlaying = playing;
                sentAt = DateTime.Now;
            }
            catch (Exception e)
            {
                Log.Write("Lost Discord: " + e.Message);
                discord.Dispose();
                nextConnect = DateTime.Now.AddSeconds(5);
            }
        }

        // What to show: what plays, a track with a known length first (a video
        // feed in the browser usually has none), then the one already shown,
        // then the one Windows calls current. On pause: only with showPaused.
        Track Pick(List<Track> sessions)
        {
            var visible = sessions.Where(t => !IsHidden(t)).ToList();
            var pool = visible.Where(t => t.IsPlaying).ToList();
            if (pool.Count == 0 && settings.ShowPaused) pool = visible;
            if (pool.Count == 0) return null;
            var timed = pool.Where(t => t.Duration > 0).ToList();
            if (timed.Count > 0) pool = timed;
            return pool.FirstOrDefault(t => t.Key == shownKey)
                ?? pool.FirstOrDefault(t => t.IsCurrent)
                ?? pool[0];
        }

        bool IsHidden(Track track)
        {
            return settings.Hide.Any(word =>
                track.App.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 ||
                track.Source.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // When the track started, Unix ms; null when the app gives no length.
        static long? StartedAt(Track track)
        {
            if (track.Duration <= 0) return null;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var position = track.Position * 1000;
            // The position is as of UpdatedAt; it has played on since.
            if (track.IsPlaying && track.UpdatedAt > 0) position += Math.Max(0, now - track.UpdatedAt);
            position = Math.Min(position, track.Duration * 1000);
            return now - (long)position;
        }

        public static string Describe(Track track)
        {
            var line = track.Artist != "" ? track.Artist + " — " + track.Title : track.Title;
            return line + " · " + track.Source.Name;
        }

        void Report(string status)
        {
            Status = status;
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
