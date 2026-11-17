using DeviceMonitor.Models;
using System;
using System.IO;
using System.Text.Json;

namespace DeviceMonitor.Services
{
    public class SettingsService : ISettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public SettingsService()
        {
            FilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeviceMonitor",
                "settings.json");

            Current = Load();
        }

        public string FilePath { get; }

        public AppSettings Current { get; private set; }

        public void Save(AppSettings settings)
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);

            // 先写临时文件再替换，避免写一半断电留下半个 JSON
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Copy(tmp, FilePath, overwrite: true);
            File.Delete(tmp);

            Current = settings;
        }

        private AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new AppSettings();

                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
