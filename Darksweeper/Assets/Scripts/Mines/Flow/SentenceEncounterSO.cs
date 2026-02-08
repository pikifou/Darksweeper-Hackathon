using UnityEngine;
using UnityEngine.Video;

namespace Mines.Flow
{
    /// <summary>
    /// A sentence encounter — the final judgement in LV3.
    /// Each sentence mine plays a fullscreen ending video on right-click.
    /// No dialogue panel, no choices — just the cinematic.
    ///
    /// Create instances via Assets > Create > DarkSweeper/Encounters/Sentence.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Encounters/Sentence")]
    public class SentenceEncounterSO : ScriptableObject
    {
        [Header("Ending Video")]
        [Tooltip("The fullscreen video played when the player right-clicks this sentence mine.")]
        public VideoClip videoClip;

        [Header("Description (optional)")]
        [TextArea(2, 4)]
        public string description = "The final sentence.";
    }
}
