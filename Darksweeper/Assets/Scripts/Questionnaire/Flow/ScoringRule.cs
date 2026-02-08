namespace Questionnaire.Flow
{
    /// <summary>
    /// Global scoring rule: maps answer ID (A/B/C/D) to Action and Empathy deltas.
    /// The rule is uniform across all questions.
    /// </summary>
    public static class ScoringRule
    {
        /// <summary>
        /// Returns the (actionDelta, empathyDelta) for a given answer ID.
        /// A → (+1, +1)  |  B → (+1, −1)  |  C → (−1, +1)  |  D → (−1, −1)
        /// </summary>
        public static (int actionDelta, int empathyDelta) GetDeltas(string answerId)
        {
            switch (answerId)
            {
                case "A": return (1, 1);
                case "B": return (1, -1);
                case "C": return (-1, 1);
                case "D": return (-1, -1);
                default:
                    UnityEngine.Debug.Log($"[ScoringRule] Unknown answer ID: {answerId}");
                    return (0, 0);
            }
        }
    }
}
