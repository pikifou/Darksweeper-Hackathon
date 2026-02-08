using Mines.Flow;
using Sweeper.Data;
using UnityEngine;

namespace Sweeper.Flow
{
    /// <summary>
    /// ScriptableObject holding all level design data for a DarkSweeper level.
    /// Replaces the old text-based MineLayoutSO with a structured, editor-friendly format.
    /// Use the LevelPainterTool in the Scene View to paint cell tags visually.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Level Data")]
    public class LevelDataSO : ScriptableObject
    {
        [Header("Grid Dimensions")]
        public int width = 50;
        public int height = 30;

        [Header("Visual")]
        [Tooltip("The background map image displayed under the grid.")]
        public Texture2D backgroundTexture;

        [Tooltip("Distance between cell centers in world units. Should match the background texture.")]
        public float cellSize = 1.05f;

        [Header("Encounter Targets")]
        [Tooltip("Target total number of mines/encounters. If > 0, painted mines are reconciled to this count.\n" +
                 "Painted < Target → random mines added. Painted > Target → random subset kept.\n" +
                 "Set to 0 to use all painted mines as-is.")]
        public int targetMineCount = 0;

        [Tooltip("Target number of Combat encounters. 0 = fill from distribution weights.")]
        [Min(0)] public int targetCombat = 0;
        [Tooltip("Target number of Chest encounters. 0 = fill from distribution weights.")]
        [Min(0)] public int targetChest = 0;
        [Tooltip("Target number of Dialogue encounters. 0 = fill from distribution weights.")]
        [Min(0)] public int targetDialogue = 0;
        [Tooltip("Target number of Shrine encounters. 0 = fill from distribution weights.")]
        [Min(0)] public int targetShrine = 0;

        [Header("Gameplay")]
        [Tooltip("HP cost each time the player reveals a cell (left-click). 0 = free reveals.")]
        [Min(0)] public int revealHPCost = 0;

        [Tooltip("HP penalty when right-clicking a cell that has no mine (false flag). " +
                 "The cell is revealed and this damage is applied. 0 = no penalty.")]
        [Min(0)] public int falseFlagPenalty = 3;

        [Header("Encounter Pool")]
        [Tooltip("Pool of encounter SOs to draw from at runtime. Each mine draws its content from this pool.")]
        public EncounterPoolSO encounterPool;

        /// <summary>True if any encounter type target is explicitly set.</summary>
        public bool HasEncounterTargets => targetCombat > 0 || targetChest > 0 || targetDialogue > 0 || targetShrine > 0;

        /// <summary>Sum of all encounter type targets.</summary>
        public int TotalEncounterTargets => targetCombat + targetChest + targetDialogue + targetShrine;

        [Header("Cell Data")]
        [HideInInspector]
        public CellTag[] cells; // flat array: index = y * width + x

        /// <summary>Get the tag for a cell at (x, y).</summary>
        public CellTag GetCell(int x, int y)
        {
            if (cells == null || x < 0 || x >= width || y < 0 || y >= height)
                return CellTag.Empty;
            int idx = y * width + x;
            if (idx >= cells.Length) return CellTag.Empty;
            return cells[idx];
        }

        /// <summary>Set the tag for a cell at (x, y).</summary>
        public void SetCell(int x, int y, CellTag tag)
        {
            if (cells == null || x < 0 || x >= width || y < 0 || y >= height)
                return;
            int idx = y * width + x;
            if (idx < cells.Length)
                cells[idx] = tag;
        }

        /// <summary>Allocate the cells array and fill with Empty.</summary>
        public void InitCells()
        {
            cells = new CellTag[width * height];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = CellTag.Empty;
        }

        /// <summary>
        /// Resize the cells array preserving existing data where possible.
        /// </summary>
        public void ResizeCells(int newWidth, int newHeight)
        {
            var newCells = new CellTag[newWidth * newHeight];

            if (cells != null)
            {
                int copyW = Mathf.Min(width, newWidth);
                int copyH = Mathf.Min(height, newHeight);

                for (int x = 0; x < copyW; x++)
                {
                    for (int y = 0; y < copyH; y++)
                    {
                        int oldIdx = y * width + x;
                        int newIdx = y * newWidth + x;
                        if (oldIdx < cells.Length)
                            newCells[newIdx] = cells[oldIdx];
                    }
                }
            }

            cells = newCells;
            width = newWidth;
            height = newHeight;
        }

        /// <summary>Count cells with a given tag.</summary>
        public int CountTag(CellTag tag)
        {
            if (cells == null) return 0;
            int count = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] == tag) count++;
            return count;
        }

        /// <summary>Number of mine/encounter cells in the level (Mine + Combat + Chest + Dialogue + Shrine).</summary>
        public int MineCount
        {
            get
            {
                if (cells == null) return 0;
                int count = 0;
                for (int i = 0; i < cells.Length; i++)
                    if (cells[i].IsMineOrEncounter()) count++;
                return count;
            }
        }
    }
}
