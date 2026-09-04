using System;
using System.Text.Json;

namespace Contra3D.Core
{
    /// <summary>
    /// T-BDD-ADOPT-6cbd51: checkpoint_triggers_autosave — JSON persistence for SaveData.
    /// Uses System.Text.Json (no Newtonsoft dependency).
    /// </summary>
    public static class SaveLoader
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = false,
            // Keep property name casing consistent with SaveData [JsonPropertyName] attributes.
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
    }
}
