using System;
using System.IO;

namespace UtilitiesPP
{
    public static class BlueprintBuildersState
    {
        private static bool s_blueprintMode;
        private static string s_baseSettingsDir;
        private static string s_perSaveDir;

        public static event Action<bool> OnBlueprintModeChanged;

        public static bool BlueprintMode => s_blueprintMode;

        public static void Initialize(string gameName)
        {
            s_blueprintMode = false;

            s_baseSettingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Captain of Industry", "Mods", "utilities-plus-plus", "Saved Settings");
            s_perSaveDir = Path.Combine(s_baseSettingsDir, SanitizeFolderName(gameName));
            LoadFromDisk();
            try { OnBlueprintModeChanged?.Invoke(s_blueprintMode); }
            catch { }
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "default";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        public static void SetBlueprintMode(bool value)
        {
            if (s_blueprintMode == value) return;
            s_blueprintMode = value;
            SaveToDisk();
            try { OnBlueprintModeChanged?.Invoke(s_blueprintMode); }
            catch { }
        }

        private static string FilePath => Path.Combine(s_perSaveDir, "blueprint_builders.txt");

        private static void LoadFromDisk()
        {
            try
            {
                if (string.IsNullOrEmpty(s_perSaveDir)) return;
                if (!File.Exists(FilePath)) return;
                var lines = File.ReadAllLines(FilePath);
                if (lines.Length >= 1) s_blueprintMode = lines[0].Trim() == "1";
            }
            catch
            {
            }
        }

        private static void SaveToDisk()
        {
            try
            {
                if (string.IsNullOrEmpty(s_perSaveDir)) return;
                Directory.CreateDirectory(s_perSaveDir);
                File.WriteAllText(FilePath, s_blueprintMode ? "1" : "0");
            }
            catch
            {
            }
        }
    }
}
