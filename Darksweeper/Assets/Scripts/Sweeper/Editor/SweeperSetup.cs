#if UNITY_EDITOR
using Sweeper.Data;
using Sweeper.Flow;
using UnityEditor;
using UnityEngine;

namespace Sweeper.Editor
{
    /// <summary>
    /// Editor utility: creates default ScriptableObject assets for DarkSweeper.
    /// Run via menu: DarkSweeper > Create Default Assets.
    /// </summary>
    public static class SweeperSetup
    {
        [MenuItem("DarkSweeper/Create Default Assets")]
        public static void CreateDefaultAssets()
        {
            // Ensure folders
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Levels");

            // SweeperConfig_Default
            if (AssetDatabase.LoadAssetAtPath<SweeperConfig>("Assets/Data/SweeperConfig_Default.asset") == null)
            {
                var config = ScriptableObject.CreateInstance<SweeperConfig>();
                config.gridWidth = 10;
                config.gridHeight = 10;
                config.mineCount = 15;
                config.hpStart = 100;
                config.revealRadius = 3;
                AssetDatabase.CreateAsset(config, "Assets/Data/SweeperConfig_Default.asset");
                Debug.Log("[SweeperSetup] Created SweeperConfig_Default.asset");
            }

            // Level_Test_5x5 (LevelDataSO)
            if (AssetDatabase.LoadAssetAtPath<LevelDataSO>("Assets/Data/Levels/Level_Test_5x5.asset") == null)
            {
                var level = ScriptableObject.CreateInstance<LevelDataSO>();
                level.width = 5;
                level.height = 5;
                level.cellSize = 1.05f;
                level.InitCells();
                // Place 3 mines
                level.SetCell(2, 4, CellTag.Mine);
                level.SetCell(1, 2, CellTag.Mine);
                level.SetCell(3, 1, CellTag.Mine);
                AssetDatabase.CreateAsset(level, "Assets/Data/Levels/Level_Test_5x5.asset");
                Debug.Log("[SweeperSetup] Created Level_Test_5x5.asset (5x5, 3 mines)");
            }

            // Level_Tutorial (10x10)
            if (AssetDatabase.LoadAssetAtPath<LevelDataSO>("Assets/Data/Levels/Level_Tutorial.asset") == null)
            {
                var level = ScriptableObject.CreateInstance<LevelDataSO>();
                level.width = 10;
                level.height = 10;
                level.cellSize = 1.05f;
                level.InitCells();
                // Place 5 mines
                level.SetCell(2, 7, CellTag.Mine);
                level.SetCell(7, 7, CellTag.Mine);
                level.SetCell(4, 5, CellTag.Mine);
                level.SetCell(1, 2, CellTag.Mine);
                level.SetCell(8, 2, CellTag.Mine);
                AssetDatabase.CreateAsset(level, "Assets/Data/Levels/Level_Tutorial.asset");
                Debug.Log("[SweeperSetup] Created Level_Tutorial.asset (10x10, 5 mines)");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SweeperSetup] All default assets created.");
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif
