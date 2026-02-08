using Mines.Data;
using UnityEngine;
using UnityEngine.Video;

namespace Mines.Flow
{
    /// <summary>
    /// A reusable pool of encounter ScriptableObjects.
    /// Referenced by LevelDataSO — the level designer drag-drops encounter SOs here.
    /// At runtime, the mine event system draws from these pools to populate each mine.
    ///
    /// Drawing logic (for N mines of type X with M items in pool):
    /// 1. Shuffle a copy of the pool
    /// 2. Take min(N, M) unique items
    /// 3. If N > M, remaining slots draw randomly (with repetition)
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Encounter Pool")]
    public class EncounterPoolSO : ScriptableObject
    {
        [Header("Combat Encounters")]
        [Tooltip("Pool of combat encounters. Each combat mine draws one.")]
        public CombatEncounterSO[] combatPool = new CombatEncounterSO[0];

        [Header("Chest Encounters")]
        [Tooltip("Pool of chest encounters. Each chest mine draws one.")]
        public ChestEncounterSO[] chestPool = new ChestEncounterSO[0];

        [Header("Dialogue Encounters")]
        [Tooltip("Pool of dialogue encounters. Each dialogue mine draws one.")]
        public DialogueEncounterSO[] dialoguePool = new DialogueEncounterSO[0];

        [Header("Shrine Encounters")]
        [Tooltip("Pool of shrine encounters. Each shrine mine draws one.")]
        public ShrineEncounterSO[] shrinePool = new ShrineEncounterSO[0];

        [Header("Generic Videos (shared across all encounters)")]
        [Tooltip("Penalty video shown when the player left-clicks a dialogue mine by mistake.")]
        public VideoClip dialoguePenaltyVideoClip;

        [Header("Action Result Videos")]
        [Tooltip("One video per player action type. Shared across all encounters.")]
        public VideoClip engageVideoClip;
        public VideoClip openVideoClip;
        public VideoClip ignoreVideoClip;
        public VideoClip helpVideoClip;
        public VideoClip harmVideoClip;
        public VideoClip sacrificeVideoClip;
        public VideoClip refuseVideoClip;

        /// <summary>
        /// Get the result video clip for a given player action.
        /// Returns null if no clip is assigned for that action.
        /// </summary>
        public VideoClip GetActionResultClip(PlayerChoice choice)
        {
            return choice switch
            {
                PlayerChoice.Engage    => engageVideoClip,
                PlayerChoice.Open      => openVideoClip,
                PlayerChoice.Ignore    => ignoreVideoClip,
                PlayerChoice.Help      => helpVideoClip,
                PlayerChoice.Harm      => harmVideoClip,
                PlayerChoice.Sacrifice => sacrificeVideoClip,
                PlayerChoice.Refuse    => refuseVideoClip,
                _ => null
            };
        }

        /// <summary>True if at least one pool has at least one entry.</summary>
        public bool HasAnyContent =>
            (combatPool != null && combatPool.Length > 0) ||
            (chestPool != null && chestPool.Length > 0) ||
            (dialoguePool != null && dialoguePool.Length > 0) ||
            (shrinePool != null && shrinePool.Length > 0);
    }
}
