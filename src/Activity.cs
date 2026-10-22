using System;
using System.Collections.Generic;

namespace Nowcast
{
    // The Discord activity for a track: "Listening to …" (type 2).
    static class Activity
    {
        public static Dictionary<string, object> Build(Track track, long? startedAt, Settings settings)
        {
            var source = track.Source.Name;
            var artist = track.Artist != "" ? track.Artist : source;
            var activity = new Dictionary<string, object> { { "type", 2 }, { "instance", false } };

            // What the member list shows after "Listening to": status_display_type
            // 0 is the application name, 1 the state line, 2 the details line.
            switch (settings.InList)
            {
                case "artist":
                    Lines(activity, track.Title, artist, 1);
                    break;
                case "title":
                    Lines(activity, track.Title, artist, 2);
                    break;
                case "app":
                    Lines(activity, track.Title, artist, 0);
                    break;
                default:
                    Lines(activity, track.Artist != "" ? track.Artist + " — " + track.Title : track.Title, source, 1);
                    break;
            }
            var state = (string)activity["state"];
            if (!track.IsPlaying) activity["state"] = Limit(state + " · " + Text.Paused);

            var assets = new Dictionary<string, object>();
            var cover = settings.Covers ? Covers.Find(track) : null;
            var image = cover ?? (settings.Image != "" ? settings.Image : null);
            if (image != null)
            {
                assets["large_image"] = image;
                // The line under the others: the album, or the source if not written above.
                var caption = track.Album != "" ? track.Album : (state.StartsWith(source) ? null : source);
                if (caption != null) assets["large_text"] = Limit(caption);
                // The source's logo in the corner of the cover.
                if (settings.SourceIcons && track.Source.IconUrl != null)
                {
                    assets["small_image"] = track.Source.IconUrl;
                    assets["small_text"] = Limit(source);
                }
            }
            if (assets.Count > 0) activity["assets"] = assets;

            if (startedAt.HasValue && track.IsPlaying)
            {
                activity["timestamps"] = new Dictionary<string, object>
                {
                    { "start", startedAt.Value },
                    { "end", startedAt.Value + (long)(track.Duration * 1000) },
                };
            }

            if (settings.Buttons) activity["buttons"] = new[] { Button(track) };
            return activity;
        }

        static void Lines(Dictionary<string, object> activity, string details, string state, int inList)
        {
            activity["details"] = Limit(details);
            activity["state"] = Limit(state);
            activity["status_display_type"] = inList;
        }

        // "Listen on SoundCloud": a search for the track in the same service,
        // since players do not tell Windows the track's link. YouTube otherwise.
        static Dictionary<string, object> Button(Track track)
        {
            var query = Uri.EscapeDataString((track.Artist + " " + track.Title).Trim());
            var hasOwn = track.Source.Search != null;
            var label = hasOwn ? string.Format(Text.ListenOn, track.Source.Name) : Text.FindOnYouTube;
            var url = (hasOwn ? track.Source.Search : "https://www.youtube.com/results?search_query=") + query;
            return new Dictionary<string, object> { { "label", Limit(label, 32) }, { "url", url } };
        }

        // Discord takes strings of 2 to 128 characters (button labels: 32).
        static string Limit(string text, int max = 128)
        {
            text = text.Trim();
            if (text.Length > max) text = text.Substring(0, max - 1) + "…";
            while (text.Length < 2) text += "⠀";
            return text;
        }
    }
}
