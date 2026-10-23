using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Nowcast
{
    // The cover is always the picture the player handed to Windows, exactly
    // what the player shows. Discord only shows pictures by URL, so it goes to
    // litterbox.catbox.moe, a temporary image host that keeps it three days.
    static class Covers
    {
        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        // Tracks without a cover yet: when to look again and how many looks are left.
        static readonly Dictionary<string, DateTime> retryAt = new Dictionary<string, DateTime>();
        static readonly Dictionary<string, int> triesLeft = new Dictionary<string, int>();

        // The URL of the track's cover, or null when there is none yet.
        public static string Find(Track track)
        {
            string url;
            if (cache.TryGetValue(track.Key, out url)) return url;
            if (!IsDue(track.Key)) return null;
            if (cache.Count > 500)
            {
                cache.Clear();
                retryAt.Clear();
                triesLeft.Clear();
            }
            try
            {
                var image = Media.ReadThumbnail(track);
                if (image == null)
                {
                    // Browsers hand the picture to Windows a few seconds after
                    // the track starts, so no cover now does not mean none.
                    Later(track.Key, 3);
                    return null;
                }
                url = Upload(image);
            }
            catch (Exception e)
            {
                Log.Write("Cover not uploaded: " + e.Message);
                Later(track.Key, 10);
                return null;
            }
            cache[track.Key] = url;
            return url;
        }

        // Whether a track that had no cover may be looked at again.
        public static bool IsDue(string key)
        {
            DateTime at;
            int left;
            if (!retryAt.TryGetValue(key, out at)) return true;
            return DateTime.Now >= at && triesLeft.TryGetValue(key, out left) && left > 0;
        }

        static void Later(string key, int seconds)
        {
            int left;
            if (!triesLeft.TryGetValue(key, out left)) left = 10;
            triesLeft[key] = left - 1;
            retryAt[key] = DateTime.Now.AddSeconds(seconds);
        }

        static string Upload(byte[] image)
        {
            // PNG starts with 0x89 'P'; players otherwise give JPEG.
            var isPng = image[0] == 0x89 && image[1] == 0x50;
            using (var form = new MultipartFormDataContent())
            {
                form.Add(new StringContent("fileupload"), "reqtype");
                form.Add(new StringContent("72h"), "time");
                var file = new ByteArrayContent(image);
                file.Headers.ContentType = new MediaTypeHeaderValue(isPng ? "image/png" : "image/jpeg");
                form.Add(file, "fileToUpload", isPng ? "cover.png" : "cover.jpg");
                var response = http.PostAsync("https://litterbox.catbox.moe/resources/internals/api.php", form).Result;
                var text = response.Content.ReadAsStringAsync().Result.Trim();
                if (!response.IsSuccessStatusCode || !text.StartsWith("https://") || text.Contains(" "))
                {
                    throw new InvalidOperationException("litterbox: " + text);
                }
                return text;
            }
        }
    }
}
