using System.Collections.Generic;
using Newtonsoft.Json;

namespace Questionnaire.Flow
{
    /// <summary>
    /// Full result of the questionnaire, ready for JSON serialization and AI prompting.
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class QuestionnaireResult
    {
        [JsonProperty("questions")]
        public List<AnswerRecord> Questions { get; set; } = new List<AnswerRecord>();

        [JsonProperty("finalScores")]
        public FinalScores FinalScores { get; set; } = new FinalScores();

        /// <summary>
        /// Serializes this result to a formatted JSON string (Newtonsoft).
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// One recorded answer: question + chosen answer + deltas.
    /// </summary>
    public sealed class AnswerRecord
    {
        [JsonProperty("questionId")]
        public string QuestionId { get; set; }

        [JsonProperty("questionText")]
        public string QuestionText { get; set; }

        [JsonProperty("answerId")]
        public string AnswerId { get; set; }

        [JsonProperty("answerText")]
        public string AnswerText { get; set; }

        [JsonProperty("actionDelta")]
        public int ActionDelta { get; set; }

        [JsonProperty("empathyDelta")]
        public int EmpathyDelta { get; set; }
    }

    /// <summary>
    /// Cumulative scores after all questions.
    /// </summary>
    public sealed class FinalScores
    {
        [JsonProperty("action")]
        public int Action { get; set; }

        [JsonProperty("empathy")]
        public int Empathy { get; set; }
    }
}
