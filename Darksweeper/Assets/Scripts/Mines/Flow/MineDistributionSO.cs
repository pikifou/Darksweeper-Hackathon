using UnityEngine;

namespace Mines.Flow
{
    /// <summary>
    /// Gameplay tuning: how event types are distributed across mine cells
    /// and their balance parameters.
    /// Editable in Inspector, duplicatable for presets per level.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Mine Distribution")]
    public class MineDistributionSO : ScriptableObject
    {
        [Header("Event Type Weights")]
        [Range(0, 100)] public int combatWeight = 60;
        [Range(0, 100)] public int chestWeight = 15;
        [Range(0, 100)] public int dialogueWeight = 15;
        [Range(0, 100)] public int shrineWeight = 10;

        [Header("Combat")]
        public int normalCreatureForce = 3;
        public int eliteCreatureForce = 8;
        [Range(0f, 1f)] public float eliteChance = 0.2f;

        [Header("Chest")]
        [Range(0f, 1f)] public float trapChance = 0.4f;
        public int trapDamage = 8;

        [Header("Shrine")]
        public int sacrificeCost = 10;

        [Header("Rewards")]
        public int hpGainAmount = 5;
        public int visionGainAmount = 1;
        public int buffDuration = 2; // number of combats the buff lasts

        [Header("Player")]
        [Tooltip("Player's Force stat (F) for combat resolution.")]
        public int playerForce = 1;

        public int TotalWeight => combatWeight + chestWeight + dialogueWeight + shrineWeight;
    }
}
