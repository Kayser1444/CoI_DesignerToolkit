using System;
using System.IO;

namespace UtilitiesPP
{
    public static class HeightFilterState
    {
        public const int MAX_LEVELS = 10;

        private static readonly bool[] s_visible = new bool[MAX_LEVELS];
        private static string s_saveFilePath;
        private static bool s_anyHidden;

        public static event Action OnFilterChanged;

        public static bool AnyHidden => s_anyHidden;

        static HeightFilterState()
        {
            for (int i = 0; i < MAX_LEVELS; i++)
                s_visible[i] = true;
        }

        private static string s_baseSettingsDir;

        public static void Init(string modDirPath)
        {
            s_baseSettingsDir = Path.Combine(modDirPath, "Saved Settings");
            s_saveFilePath = Path.Combine(s_baseSettingsDir, "height_filter.txt");
            Load();
        }

        public static void SetSaveName(string gameName)
        {
            try
            {
                var safeName = SanitizeFolderName(gameName);
                var perSaveDir = Path.Combine(s_baseSettingsDir, safeName);

                if (!Directory.Exists(perSaveDir))
                {
                    var oldPath = Path.Combine(s_baseSettingsDir, "height_filter.txt");
                    if (File.Exists(oldPath))
                    {
                        Directory.CreateDirectory(perSaveDir);
                        File.Move(oldPath, Path.Combine(perSaveDir, "height_filter.txt"));
                    }
                }

                s_saveFilePath = Path.Combine(perSaveDir, "height_filter.txt");
                for (int i = 0; i < MAX_LEVELS; i++)
                    s_visible[i] = true;
                Load();
            }
            catch { }
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        public static bool IsLevelVisible(int level)
        {
            if (level < 1 || level > MAX_LEVELS) return true;
            return s_visible[level - 1];
        }

        public static void SetLevelVisible(int level, bool visible)
        {
            if (level < 1 || level > MAX_LEVELS) return;
            if (s_visible[level - 1] == visible) return;
            s_visible[level - 1] = visible;
            UpdateAnyHidden();
            Save();
            OnFilterChanged?.Invoke();
        }

        public static void ToggleLevel(int level)
        {
            if (level < 1 || level > MAX_LEVELS) return;
            s_visible[level - 1] = !s_visible[level - 1];
            UpdateAnyHidden();
            Save();
            OnFilterChanged?.Invoke();
        }

        public static void HideOneMore()
        {
            for (int i = MAX_LEVELS - 1; i >= 0; i--)
            {
                if (s_visible[i])
                {
                    s_visible[i] = false;
                    UpdateAnyHidden();
                    Save();
                    OnFilterChanged?.Invoke();
                    return;
                }
            }
        }

        public static void ShowOneMore()
        {
            for (int i = 0; i < MAX_LEVELS; i++)
            {
                if (!s_visible[i])
                {
                    s_visible[i] = true;
                    UpdateAnyHidden();
                    Save();
                    OnFilterChanged?.Invoke();
                    return;
                }
            }
        }

        public static void ShowAll()
        {
            bool changed = false;
            for (int i = 0; i < MAX_LEVELS; i++)
            {
                if (!s_visible[i]) { s_visible[i] = true; changed = true; }
            }
            if (changed)
            {
                UpdateAnyHidden();
                Save();
                OnFilterChanged?.Invoke();
            }
        }

        public static void HideAll()
        {
            bool changed = false;
            for (int i = 0; i < MAX_LEVELS; i++)
            {
                if (s_visible[i]) { s_visible[i] = false; changed = true; }
            }
            if (changed)
            {
                UpdateAnyHidden();
                Save();
                OnFilterChanged?.Invoke();
            }
        }

        private static void UpdateAnyHidden()
        {
            s_anyHidden = false;
            for (int i = 0; i < MAX_LEVELS; i++)
            {
                if (!s_visible[i]) { s_anyHidden = true; return; }
            }
        }

        private static void Save()
        {
            if (s_saveFilePath == null) return;
            try
            {
                var dir = Path.GetDirectoryName(s_saveFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var lines = new string[MAX_LEVELS];
                for (int i = 0; i < MAX_LEVELS; i++)
                    lines[i] = $"{i + 1}:{(s_visible[i] ? "1" : "0")}";
                File.WriteAllLines(s_saveFilePath, lines);
            }
            catch { }
        }

        private static void Load()
        {
            if (s_saveFilePath == null || !File.Exists(s_saveFilePath)) return;
            try
            {
                foreach (var line in File.ReadAllLines(s_saveFilePath))
                {
                    var parts = line.Split(':');
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0], out int level) && int.TryParse(parts[1], out int val))
                        {
                            if (level >= 1 && level <= MAX_LEVELS)
                                s_visible[level - 1] = val != 0;
                        }
                    }
                }
                UpdateAnyHidden();
            }
            catch { }
        }
    }
}
