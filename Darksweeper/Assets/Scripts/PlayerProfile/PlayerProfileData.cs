using System.Collections.Generic;
using Newtonsoft.Json;
using Questionnaire.Flow;

namespace PlayerProfile
{
    /// <summary>
    /// Persistent player profile that grows over time.
    /// Stores questionnaire results, god assignments, scores, and future game history.
    /// Pure C# — no Unity dependencies. JSON-serializable for ChatGPT prompting.
    /// </summary>
    public sealed class PlayerProfileData
    {
        [JsonProperty("primaryGod")]
        public GodReference PrimaryGod { get; set; }

        [JsonProperty("secondaryGod")]
        public GodReference SecondaryGod { get; set; }

        [JsonProperty("actionScore")]
        public int ActionScore { get; set; }

        [JsonProperty("empathyScore")]
        public int EmpathyScore { get; set; }

        [JsonProperty("questionnaire")]
        public QuestionnaireResult Questionnaire { get; set; }

        [JsonProperty("gameHistory")]
        public List<string> GameHistory { get; set; } = new List<string>();

        /// <summary>
        /// Serializes this profile to a formatted JSON string.
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// Lightweight reference to a god (id + name only). Used inside PlayerProfileData.
    /// </summary>
    public sealed class GodReference
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        public static GodReference FromGodData(GodData god)
        {
            if (god == null) return null;
            return new GodReference { Id = god.Id, Name = god.Name };
        }
    }
}
