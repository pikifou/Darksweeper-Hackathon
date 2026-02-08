using Mines.Data;
using UnityEngine;
using UnityEngine.Video;

namespace Mines.Flow
{
    /// <summary>
    /// A single combat encounter template.
    /// Create instances via Assets > Create > DarkSweeper/Encounters/Combat.
    /// At runtime, the system copies these fields into CombatParams for each mine.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Encounters/Combat")]
    public class CombatEncounterSO : ScriptableObject
    {
        public string monsterName = "Creature";

        [TextArea(2, 4)]
        public string description = "Une creature se dresse devant vous.";

        [Tooltip("Creature Force (Fc) — also its HP.")]
        public int creatureForce = 3;

        public bool isElite;

        [Header("Reward (granted on victory)")]
        public RewardType reward = RewardType.None;
        public int rewardValue;

        [Header("Visuals")]
        [Tooltip("Video clip shown in the toast on a normal right-click resolve.")]
        public VideoClip videoClip;

        [Tooltip("Video clip shown when the player left-clicks by mistake (penalty).")]
        public VideoClip penaltyVideoClip;
    }
}
