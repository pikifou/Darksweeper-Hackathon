using System.Collections.Generic;
using Newtonsoft.Json;

namespace Questionnaire.Data
{
    /// <summary>
    /// Root object matching the questions.json structure.
    /// </summary>
    public sealed class QuestionSetData
    {
        [JsonProperty("questions")]
        public List<QuestionData> Questions { get; set; }
    }

    /// <summary>
    /// A single question with its possible answers.
    /// </summary>
    public sealed class QuestionData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("answers")]
        public List<AnswerData> Answers { get; set; }
    }

    /// <summary>
    /// A single answer option (A, B, C, or D).
    /// </summary>
    public sealed class AnswerData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
