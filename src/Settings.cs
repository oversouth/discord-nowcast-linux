using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Nowcast
{
    // %APPDATA%\discord-nowcast\config.json. Written with the defaults on the
    // first run; a broken file falls back to the defaults and says so in the log.
    class Settings
    {
        public static readonly string FilePath = Path.Combine(Log.Folder, "config.json");

        const string DefaultText =
@"{
  ""clientId"": ""1562901782579912804"",
  ""inList"": ""source"",
  ""showPaused"": false,
  ""covers"": true,
  ""buttons"": true,
  ""checkUpdates"": true,
  ""image"": """",
  ""hide"": [],
  ""names"": {}
}
";

        public string ClientId = "1562901782579912804";
        // What the member list shows after "Listening to": source, artist, title, app.
        public string InList = "source";
        public bool ShowPaused = false;
        public bool Covers = true;
        public bool Buttons = true;
        public bool CheckUpdates = true;
        public string Image = "";
        public List<string> Hide = new List<string>();
        public Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // isNew: the file did not exist and was written now, that is, a first run.
        public static Settings Load(out bool isNew)
        {
            isNew = false;
            var settings = new Settings();
            try
            {
                Directory.CreateDirectory(Log.Folder);
                if (!File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, DefaultText, new UTF8Encoding(false));
                    isNew = true;
                }
                var values = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(FilePath, Encoding.UTF8));
                if (values != null) settings.Apply(values);
            }
            catch (Exception e)
            {
                Log.Write("config.json could not be read, using the defaults: " + e.Message);
            }
            return settings;
        }

        void Apply(Dictionary<string, object> values)
        {
            var clientId = Get<string>(values, "clientId");
            if (clientId != null && clientId.Trim() != "") ClientId = clientId.Trim();
            InList = Get<string>(values, "inList") ?? InList;
            ShowPaused = Get(values, "showPaused", ShowPaused);
            Covers = Get(values, "covers", Covers);
            Buttons = Get(values, "buttons", Buttons);
            CheckUpdates = Get(values, "checkUpdates", CheckUpdates);
            Image = Get<string>(values, "image") ?? Image;

            var hide = Get<ArrayList>(values, "hide");
            if (hide != null)
            {
                foreach (var word in hide)
                {
                    if (word is string && (string)word != "") Hide.Add((string)word);
                }
            }
            var names = Get<Dictionary<string, object>>(values, "names");
            if (names != null)
            {
                foreach (var pair in names)
                {
                    if (pair.Value is string) Names[pair.Key] = (string)pair.Value;
                }
            }
        }

        static T Get<T>(Dictionary<string, object> values, string key) where T : class
        {
            object value;
            return values.TryGetValue(key, out value) ? value as T : null;
        }

        static bool Get(Dictionary<string, object> values, string key, bool fallback)
        {
            object value;
            return values.TryGetValue(key, out value) && value is bool ? (bool)value : fallback;
        }
    }
}
