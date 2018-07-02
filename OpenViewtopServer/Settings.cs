using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;

namespace Gosub.Viewtop
{
    class Settings
    {
        const string SETTINGS_FILE_NAME = "Settings.json";

        public string Name = "";

        public static Settings Load()
        {
            try
            {
                var fileName = Path.Combine(Application.CommonAppDataPath, SETTINGS_FILE_NAME);
                if (!File.Exists(fileName))
                    return new Settings();
                var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(fileName));
                return settings == null ? new Settings() : settings;
            }
            catch
            {
                return new Settings();
            }
        }

        public static void Save(Settings settings)
        {
            var fileName = Path.Combine(Application.CommonAppDataPath, SETTINGS_FILE_NAME);
            File.WriteAllText(fileName, JsonConvert.SerializeObject(settings));
        }
    }
}
