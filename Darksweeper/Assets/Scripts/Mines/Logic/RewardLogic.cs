using Mines.Data;
using UnityEngine;

namespace Mines.Logic
{
    /// <summary>
    /// Applies rewards from mine events to the game state.
    /// Static class — no state of its own.
    /// </summary>
    public static class RewardLogic
    {
        /// <summary>
        /// Apply a reward. Returns a human-readable description of what happened.
        /// The caller is responsible for passing the correct references.
        /// </summary>
        /// <param name="type">The reward type</param>
        /// <param name="value">The reward value (amount of HP, radius increase, buff duration)</param>
        /// <param name="applyHpDelta">Delegate to apply HP change on SweeperGameController</param>
        /// <param name="currentRevealRadius">Current reveal radius (for VisionGain)</param>
        /// <param name="setRevealRadius">Delegate to set new reveal radius</param>
        /// <param name="setBuff">Delegate to set buff (number of combats remaining)</param>
        /// <returns>Description string for UI display</returns>
        public static string ApplyReward(
            RewardType type,
            int value,
            System.Action<int> applyHpDelta = null,
            int currentRevealRadius = 0,
            System.Action<int> setRevealRadius = null,
            System.Action<int> setBuff = null)
        {
            switch (type)
            {
                case RewardType.HpGain:
                    applyHpDelta?.Invoke(value);
                    return $"+{value} PV";

                case RewardType.VisionGain:
                    int newRadius = currentRevealRadius + value;
                    setRevealRadius?.Invoke(newRadius);
                    return $"Rayon de vision +{value}";

                case RewardType.Buff:
                    setBuff?.Invoke(value);
                    return $"Protection pour {value} combat(s)";

                case RewardType.None:
                default:
                    return "";
            }
        }
    }
}
