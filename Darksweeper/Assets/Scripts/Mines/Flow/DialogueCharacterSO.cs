using UnityEngine;
using UnityEngine.Video;

namespace Mines.Flow
{
    /// <summary>
    /// Defines a dialogue character — a fixed NPC the player can encounter.
    /// Each character has an ID (for LLM matching), a name, a narrative description
    /// (sent to the LLM), and a single intro video clip.
    ///
    /// Penalty and result videos are generic (they show the player, not the character)
    /// and are handled at the controller/panel level.
    ///
    /// Create instances via Assets > Create > DarkSweeper/Characters/Dialogue Character.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Characters/Dialogue Character")]
    public class DialogueCharacterSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID used to match LLM responses (e.g. 'stone_child').")]
        public string characterId;

        [Tooltip("Display name shown in the dialogue panel.")]
        public string characterName = "Unknown";

        [Header("LLM")]
        [TextArea(3, 8)]
        [Tooltip("Narrative description sent to the LLM so it can write dialogue for this character.")]
        public string descriptionForLLM;

        [Tooltip("Axis tag for reference (e.g. 'empathy', 'action_empathy').")]
        public string axisTag;

        [Header("Visuals")]
        [Tooltip("Intro video — the character appears. Plays once and freezes on last frame.")]
        public VideoClip introClip;
    }
}
