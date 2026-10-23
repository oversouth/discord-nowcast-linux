using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Nowcast
{
    // The cover is always the picture the player handed to Windows, exactly
    // what the player shows. Discord only shows pictures by URL, so it goes to
    // litterbox.catbox.moe, a temporary image host that keeps it three days,
    // or uguu.se when litterbox is down.
    static class Covers
    {
        static readonly HttpClient http = Client();

        // Hosts behind a firewall turn away requests that do not say what sends them.
        static HttpClient Client()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "discord-nowcast/" + Program.Version);
            client.DefaultRequestHeaders.ExpectContinue = false;
            return client;
        }
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
                Log.Write("Cover not uploaded: " + Message(e));
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

        // A host that failed is skipped for a while, so every track does not
        // wait for it to time out first.
        static DateTime litterboxDownUntil = DateTime.MinValue;

        static string Upload(byte[] image)
        {
            if (DateTime.Now >= litterboxDownUntil)
            {
                try
                {
                    return ToLitterbox(image);
                }
                catch (Exception e)
                {
                    Log.Write("litterbox failed, trying uguu.se: " + Message(e));
                    litterboxDownUntil = DateTime.Now.AddMinutes(30);
                }
            }
            return ToUguu(image);
        }

        // litterbox.catbox.moe: kept for three days.
        static string ToLitterbox(byte[] image)
        {
            var fields = new Dictionary<string, string> { { "reqtype", "fileupload" }, { "time", "72h" } };
            var text = Post("https://litterbox.catbox.moe/resources/internals/api.php", Form(fields, "fileToUpload", image)).Trim();
            if (!text.StartsWith("https://") || text.Contains(" ")) throw new InvalidOperationException(Short(text));
            return text;
        }

        // uguu.se: kept for three hours, enough for a track; Discord keeps its own copy.
        static string ToUguu(byte[] image)
        {
            var text = Post("https://uguu.se/upload", Form(new Dictionary<string, string>(), "files[]", image));
            try
            {
                var reply = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(text);
                var files = (ArrayList)reply["files"];
                var url = (string)((Dictionary<string, object>)files[0])["url"];
                if (url.StartsWith("https://")) return url;
            }
            catch (Exception)
            {
            }
            throw new InvalidOperationException("uguu.se: " + Short(text));
        }

        static string Post(string url, HttpContent form)
        {
            using (form)
            {
                var response = http.PostAsync(url, form).Result;
                var text = response.Content.ReadAsStringAsync().Result;
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException((int)response.StatusCode + " " + Short(text));
                return text;
            }
        }

        // multipart/form-data written by hand, as a browser sends it: the one
        // MultipartFormDataContent writes (a quoted boundary, an extra
        // filename*) is not understood by every server.
        static HttpContent Form(Dictionary<string, string> fields, string fileField, byte[] image)
        {
            var boundary = "----nowcast" + Guid.NewGuid().ToString("N");
            var head = new StringBuilder();
            foreach (var pair in fields)
            {
                head.Append("--" + boundary + "\r\n");
                head.Append("Content-Disposition: form-data; name=\"" + pair.Key + "\"\r\n\r\n");
                head.Append(pair.Value + "\r\n");
            }
            head.Append("--" + boundary + "\r\n");
            head.Append("Content-Disposition: form-data; name=\"" + fileField + "\"; filename=\"" + (IsPng(image) ? "cover.png" : "cover.jpg") + "\"\r\n");
            head.Append("Content-Type: " + (IsPng(image) ? "image/png" : "image/jpeg") + "\r\n\r\n");
            var start = Encoding.UTF8.GetBytes(head.ToString());
            var end = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");
            var body = new byte[start.Length + image.Length + end.Length];
            start.CopyTo(body, 0);
            image.CopyTo(body, start.Length);
            end.CopyTo(body, start.Length + image.Length);
            var content = new ByteArrayContent(body);
            content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);
            return content;
        }

        // PNG starts with 0x89 'P'; players otherwise give JPEG.
        static bool IsPng(byte[] image)
        {
            return image.Length > 1 && image[0] == 0x89 && image[1] == 0x50;
        }

        // An error page's title, or the start of the reply: never the whole page in the log.
        static string Short(string text)
        {
            var title = Regex.Match(text, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            text = title.Success ? title.Groups[1].Value : text;
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > 120 ? text.Substring(0, 120) + "…" : text;
        }

        static string Message(Exception e)
        {
            return e.GetBaseException().Message;
        }
    }
}
