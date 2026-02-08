using System;
using Mines.Data;
using UnityEngine;

namespace Mines.Flow
{
    /// <summary>
    /// A single dialogue encounter template.
    /// References a <see cref="DialogueCharacterSO"/> for identity and visuals,
    /// and holds the situation-specific content (prompt text + choices).
    ///
    /// Create instances via Assets > Create > DarkSweeper/Encounters/Dialogue.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Encounters/Dialogue")]
    public class DialogueEncounterSO : ScriptableObject
    {
        [Header("Character")]
        [Tooltip("The NPC for this dialogue. Provides name, intro video, and LLM description.")]
        public DialogueCharacterSO character;

        [Header("Dialogue Content")]
        [TextArea(2, 4)]
        public string promptText = "A presence watches you.";

        public DialogueChoiceEntry[] choices = new DialogueChoiceEntry[]
        {
            new() { choiceType = PlayerChoice.Help, label = "Help", resultText = "...", hpDelta = 0, reward = RewardType.None, rewardValue = 0 },
            new() { choiceType = PlayerChoice.Ignore, label = "Ignore", resultText = "You walk away.", hpDelta = 0, reward = RewardType.None, rewardValue = 0 },
        };

        /// <summary>
        /// Convenience accessor — returns the character name or a fallback.
        /// </summary>
        public string CharacterName => character != null ? character.characterName : "Unknown";
    }

    /// <summary>
    /// A single dialogue choice entry. Uses enums directly (no string parsing).
    /// </summary>
    [Serializable]
    public class DialogueChoiceEntry
    {
        public PlayerChoice choiceType;
        public string label;

        [TextArea(1, 3)]
        public string resultText;

        public int hpDelta;
        public RewardType reward;
        public int rewardValue;
    }
}
