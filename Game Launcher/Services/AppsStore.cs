using Game_Launcher.Helpers;
using Game_Launcher.Models;
using System.IO;
using System.Text.Json;

namespace Game_Launcher.Services {
    /// <summary> Reads and writes apps.json in Nexus's own data folder. </summary>
    public static class AppsStore {

        public static AppsData Load() {
            string path = AppPaths.AppsFile;
            if (!File.Exists(path)) {
                return new AppsData();
            }

            try {
                return JsonSerializer.Deserialize<AppsData>(File.ReadAllText(path)) ?? new AppsData();
            }
            catch (JsonException) {
                // Corrupt file: keep it aside rather than lose it silently or crash
                File.Move(path, path + ".bad", overwrite: true);
                return new AppsData();
            }
        }

        public static void Save(AppsData data) {
            string path = AppPaths.AppsFile;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);

            // Temp file first so a crash mid-write can't leave a half-written apps.json
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, overwrite: true);
        }
    }
}
