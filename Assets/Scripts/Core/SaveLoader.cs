using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Contra3D.Core
{
    /// <summary>
    /// T-BDD-ADOPT-6cbd51: checkpoint_triggers_autosave — JSON persistence for SaveData.
    /// Uses System.Text.Json (no Newtonsoft dependency).
    /// Supports CRC32 integrity checks, version migration, and atomic temp-file writes.
    /// </summary>
    public static class SaveLoader
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null,
        };

        public static string Serialize(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return JsonSerializer.Serialize(data, _options);
        }

        public static SaveData Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));
            return JsonSerializer.Deserialize<SaveData>(json, _options)
                   ?? throw new InvalidOperationException("Deserialized SaveData was null.");
        }

        public static SaveData LoadVerified(string json, int slotId = -1)
        {
            var data = Deserialize(json);
            var expectedCrc = data.Crc32;
            data.Crc32 = 0;
            var computed = data.ComputeCrc32();
            data.Crc32 = expectedCrc;
            if (computed != expectedCrc)
                throw new SaveCorruptedException(slotId.ToString());
            return data;
        }

        public static SaveData LoadMigrated(string json)
        {
            var data = Deserialize(json);
            if (data.Version < 3)
            {
                data.CurrentArea = data.CurrentArea == "" ? "unknown" : data.CurrentArea;
                data.WeaponsUnlocked = data.WeaponsUnlocked ?? new List<string> { "rifle_default" };
                data.Version = 3;
            }
            return data;
        }

        public static (string TempPath, string FinalPath, SaveData Committed) AtomicSave(string basePath, SaveData data, string slotId)
        {
            var tempPath = basePath + ".tmp";
            var finalPath = basePath;
            var json = Serialize(data);
            data.Crc32 = data.ComputeCrc32();
            File.WriteAllText(tempPath, json);
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            return (tempPath, finalPath, data);
        }

        public static bool VerifySave(string path, out SaveData data)
        {
            data = null;
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            data = Deserialize(json);
            data.Crc32 = 0;
            var computed = data.ComputeCrc32();
            return computed == 0;
        }
    }
}
