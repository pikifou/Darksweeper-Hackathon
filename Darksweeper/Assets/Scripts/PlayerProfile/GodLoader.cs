using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace PlayerProfile
{
    /// <summary>
    /// Loads god definitions from the gods.json file in StreamingAssets.
    /// </summary>
    public static class GodLoader
    {
        private const string FileName = "gods.json";

        /// <summary>
        /// Reads and deserializes gods.json into a typed GodSetData.
        /// Returns null and logs an error if the file is missing or malformed.
        /// </summary>
        public static GodSetData Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, FileName);

            if (!File.Exists(path))
            {
                Debug.Log($"[GodLoader] File not found: {path}");
                return null;
            }

            string json = File.ReadAllText(path);

            try
            {
                GodSetData data = JsonConvert.DeserializeObject<GodSetData>(json);

                if (data?.Gods == null || data.Gods.Count == 0)
                {
                    Debug.Log("[GodLoader] JSON parsed but contains no gods.");
                    return null;
                }

                Debug.Log($"[GodLoader] Loaded {data.Gods.Count} god definitions.");
                return data;
            }
            catch (JsonException ex)
            {
                Debug.Log($"[GodLoader] Failed to parse JSON: {ex.Message}");
                return null;
            }
        }
    }
}
