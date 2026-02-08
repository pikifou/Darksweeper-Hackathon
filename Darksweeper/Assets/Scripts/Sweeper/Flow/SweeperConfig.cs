using UnityEngine;

namespace Sweeper.Flow
{
    /// <summary>
    /// Exposes all tuning parameters for the DarkSweeper grid in the Unity Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Sweeper Config")]
    public class SweeperConfig : ScriptableObject
    {
        [Header("Grid (used in Random mode when no LevelDataSO is assigned)")]
        public int gridWidth = 10;
        public int gridHeight = 10;
        public int mineCount = 15;

        [Header("Player")]
        public int hpStart = 100;

        [Header("Reveal + Light")]
        [Tooltip("Cells within this distance are fully illuminated (binary: 1.0). Fog edge is handled by the shader.")]
        public int revealRadius = 3;
    }
}
