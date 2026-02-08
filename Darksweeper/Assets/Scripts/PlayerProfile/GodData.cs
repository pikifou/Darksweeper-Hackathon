using System.Collections.Generic;
using Newtonsoft.Json;

namespace PlayerProfile
{
    /// <summary>
    /// Root object matching the gods.json structure.
    /// </summary>
    public sealed class GodSetData
    {
        [JsonProperty("gods")]
        public List<GodData> Gods { get; set; }
    }

    /// <summary>
    /// A single god definition: identity, quadrant, and reveal phrases.
    /// </summary>
    public sealed class GodData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("quadrant")]
        public string Quadrant { get; set; }

        /// <summary>
        /// +1 or -1. Positive = Action, Negative = Inaction.
        /// </summary>
        [JsonProperty("actionSign")]
        public int ActionSign { get; set; }

        /// <summary>
        /// +1 or -1. Positive = Empathy, Negative = Detachment.
        /// </summary>
        [JsonProperty("empathySign")]
        public int EmpathySign { get; set; }

        /// <summary>
        /// Spoken when this god is assigned as the primary god.
        /// </summary>
        [JsonProperty("primaryPhrase")]
        public string PrimaryPhrase { get; set; }

        /// <summary>
        /// Spoken when this god is assigned as the secondary god.
        /// </summary>
        [JsonProperty("secondaryPhrase")]
        public string SecondaryPhrase { get; set; }
    }
}
