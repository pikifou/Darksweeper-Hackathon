using Mines.Data;
using UnityEngine;
using UnityEngine.Video;

namespace Mines.Flow
{
    /// <summary>
    /// A single shrine encounter template.
    /// Create instances via Assets > Create > DarkSweeper/Encounters/Shrine.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Encounters/Shrine")]
    public class ShrineEncounterSO : ScriptableObject
    {
        [TextArea(2, 4)]
        public string shrineDescription = "Un autel silencieux.";

        [TextArea(1, 3)]
        public string offerText = "Offrez votre vitalite en echange d'un pouvoir.";

        [Tooltip("HP cost if the player accepts the sacrifice.")]
        public int sacrificeCost = 10;

        [Header("Reward (granted on sacrifice)")]
        public RewardType reward = RewardType.VisionGain;
        public int rewardValue = 1;

        [Header("Visuals")]
        [Tooltip("Video clip shown in the toast on a normal right-click resolve.")]
        public VideoClip videoClip;

        [Tooltip("Video clip shown when the player left-clicks by mistake (interaction destroyed).")]
        public VideoClip penaltyVideoClip;
    }
}
