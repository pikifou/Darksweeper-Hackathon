using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Questionnaire.Data
{
    /// <summary>
    /// Loads question data from the questions.json file in StreamingAssets.
    /// </summary>
    public static class QuestionLoader
    {
        private const string FileName = "questions.json";

        /// <summary>
        /// Reads and deserializes questions.json into a typed QuestionSetData.
        /// Returns null and logs an error if the file is missing or malformed.
        /// </summary>
        public static QuestionSetData Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, FileName);

            if (!File.Exists(path))
            {
                Debug.Log($"[QuestionLoader] File not found: {path}");
                return null;
            }

            string json = File.ReadAllText(path);

            try
            {
                QuestionSetData data = JsonConvert.DeserializeObject<QuestionSetData>(json);

                if (data?.Questions == null || data.Questions.Count == 0)
                {
                    Debug.Log("[QuestionLoader] JSON parsed but contains no questions.");
                    return null;
                }

                Debug.Log($"[QuestionLoader] Loaded {data.Questions.Count} questions.");
                return data;
            }
            catch (JsonException ex)
            {
                Debug.Log($"[QuestionLoader] Failed to parse JSON: {ex.Message}");
                return null;
            }
        }
    }
}
