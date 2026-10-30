using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MECCG_Deck_Builder
{
    public sealed class UserSettings
    {
        public List<string> SelectedSets { get; set; } = [];

        // Persisted folder path for Tag Set Save / Load / Merge
        public string LastTagSetDirectory { get; set; } = string.Empty;

        // Active mode
        public int LastExtractionMode { get; set; } = 1; // 1 = Harvest, 2 = Relational, 3 = Quick Regex, 4 = File

        // Mode 1 Entity Harvest Defaults
        public string LastHarvestAttribute { get; set; } = "Secondary";
        public string LastHarvestValue { get; set; } = "Faction";
        public string LastHarvestCategory { get; set; } = "Faction Focus";

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
                string path = StoragePathService.GetSettingsFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonConvert.DeserializeObject<UserSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
                else if (!StoragePathService.IsPortableMode)
                {
                    // Check legacy path in app directory for seamless migration
                    string legacyPath = Path.Combine(StoragePathService.AppBaseDirectory, "user_settings.json");
                    if (File.Exists(legacyPath))
                    {
                        string json = File.ReadAllText(legacyPath);
                        var settings = JsonConvert.DeserializeObject<UserSettings>(json);
                        if (settings != null)
                        {
                            s_current = settings; // Assign BEFORE saving to prevent recursion
                            SaveSettings();
                            return settings;
                        }
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
            // Access s_current directly to avoid re-triggering LoadSettings()
            var settingsToSave = s_current;
            if (settingsToSave == null) return;

            try
            {
                string path = StoragePathService.GetSettingsFilePath();
                string json = JsonConvert.SerializeObject(settingsToSave, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Non-fatal if disk write fails due to permissions
            }
        }
    }
}