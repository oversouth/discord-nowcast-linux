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

        // The URL of the track's cover, or null when the player gives none.
        public static string Find(Track track)
        {
            string url;
            if (cache.TryGetValue(track.Key, out url)) return url;
            if (cache.Count > 500) cache.Clear();
            try
            {
                var image = Media.ReadThumbnail(track);
                url = image == null ? null : Upload(image);
            }
            catch (Exception e)
            {
                // Not cached: tried again with the next update.
                Log.Write("Cover not uploaded: " + e.Message);
                return null;
            }
            cache[track.Key] = url;
            return url;
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
