using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace MECCG_Deck_Builder
{
    public sealed class UserSettings
    {
        public List<string> SelectedSets { get; set; } = [];

        // Active mode
        public int LastExtractionMode { get; set; } = 1; // 1 = Harvest, 2 = Relational, 3 = Quick Regex, 4 = File

        // Mode 2 Relational Defaults
        public string LastRelationalCategory { get; set; } = "Home Region";
        public string LastRelationalSourceAttr { get; set; } = "Home";
        public string LastRelationalLookupAttr { get; set; } = "Primary";
        public string LastRelationalLookupVal { get; set; } = "Site";
        public string LastRelationalTargetAttr { get; set; } = "Region";

        // Mode 3 Regex Defaults
        public string LastExtractionCategory { get; set; } = "Custom Trait";
        public string LastExtractionTag { get; set; } = "$1";
        public string LastExtractionPattern { get; set; } = @"(?i)Dwarf factions?";
    }

    public static class UserSettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(Application.StartupPath, "user_settings.json");
        private static UserSettings s_current;

        public static UserSettings Current
        {
            get
            {
                s_current ??= LoadSettings();
                return s_current;
            }
        }

        public static UserSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<UserSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
                // Graceful fallback to default instance on I/O or JSON read errors
            }

            return new UserSettings();
        }

        public static void SaveSettings()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Non-fatal if disk write fails due to permissions
            }
        }
    }
}