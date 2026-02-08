using Mines.Flow;

namespace Mines.Data
{
    /// <summary>
    /// Configuration for a Dialogue mine event.
    /// A dialogue presents a character with 2-3 choices (Help / Harm / Ignore / Sacrifice / Refuse).
    /// </summary>
    public class DialogueParams
    {
        public string dialogueId;
        public string characterName;
        public string promptText;       // what the character says
        public DialogueChoice[] choices; // 2-3 choices

        /// <summary>Reference to the character SO for video lookup at display time.</summary>
        public DialogueCharacterSO character;
    }

    /// <summary>
    /// A single choice in a dialogue event.
    /// </summary>
    public class DialogueChoice
    {
        public PlayerChoice choiceType; // Help / Harm / Ignore / Sacrifice / Refuse
        public string label;            // button text
        public string resultText;       // text shown after choosing
        public int hpDelta;
        public RewardType reward;
        public int rewardValue;
    }
}
