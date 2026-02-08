using Mines.Data;
using UnityEngine;
using UnityEngine.Video;

namespace Mines.Flow
{
    /// <summary>
    /// A single chest encounter template.
    /// Create instances via Assets > Create > DarkSweeper/Encounters/Chest.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Encounters/Chest")]
    public class ChestEncounterSO : ScriptableObject
    {
        [TextArea(2, 4)]
        public string description = "Un coffre mysterieux repose dans l'obscurite.";

        [Header("Trap")]
        public bool isTrapped;

        [Tooltip("HP lost if the chest is trapped and opened.")]
        public int trapDamage;

        [Header("Reward")]
        public RewardType reward = RewardType.HpGain;
        public int rewardValue = 5;

        [Header("Visuals")]
        [Tooltip("Video clip shown in the toast on a normal right-click resolve.")]
        public VideoClip videoClip;

        [Tooltip("Video clip shown when the player left-clicks by mistake (interaction destroyed).")]
        public VideoClip penaltyVideoClip;
    }
}
