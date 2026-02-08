#if UNITY_EDITOR
using Mines.Flow;
using Sweeper.Data;
using Sweeper.Flow;
using UnityEditor;
using UnityEngine;

namespace Sweeper.Editor
{
    /// <summary>
    /// Custom Inspector for LevelDataSO.
    /// Shows grid stats, init/resize buttons, and a shortcut to the Scene Painter.
    /// </summary>
    [CustomEditor(typeof(LevelDataSO))]
    public class LevelDataSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var levelData = (LevelDataSO)target;

            // Draw default fields (width, height, backgroundTexture, cellSize)
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Cell Grid", EditorStyles.boldLabel);

            bool cellsExist = levelData.cells != null && levelData.cells.Length > 0;
            int expectedSize = levelData.width * levelData.height;

            if (!cellsExist)
            {
                EditorGUILayout.HelpBox(
                    $"Cell array is not initialized. Click 'Initialize Grid' to create a {levelData.width}x{levelData.height} grid.",
                    MessageType.Warning);

                if (GUILayout.Button("Initialize Grid", GUILayout.Height(30)))
                {
                    Undo.RecordObject(levelData, "Initialize Grid");
                    levelData.InitCells();
                    EditorUtility.SetDirty(levelData);
                    Debug.Log($"[LevelData] Grid initialized: {levelData.width}x{levelData.height} = {levelData.cells.Length} cells");
                }
            }
            else
            {
                // Check if dimensions match
                if (levelData.cells.Length != expectedSize)
                {
                    EditorGUILayout.HelpBox(
                        $"Cell array size ({levelData.cells.Length}) doesn't match dimensions ({levelData.width}x{levelData.height} = {expectedSize}).\n" +
                        "Click 'Resize Grid' to fix (preserves existing data).",
                        MessageType.Warning);

                    if (GUILayout.Button("Resize Grid (preserve data)", GUILayout.Height(30)))
                    {
                        Undo.RecordObject(levelData, "Resize Grid");
                        levelData.ResizeCells(levelData.width, levelData.height);
                        EditorUtility.SetDirty(levelData);
                        Debug.Log($"[LevelData] Grid resized to {levelData.width}x{levelData.height}");
                    }
                }
                else
                {
                    // Stats
                    int mineGeneric = levelData.CountTag(CellTag.Mine);
                    int combat = levelData.CountTag(CellTag.Combat);
                    int chest = levelData.CountTag(CellTag.Chest);
                    int dialogue = levelData.CountTag(CellTag.Dialogue);
                    int shrine = levelData.CountTag(CellTag.Shrine);
                    int sentence = levelData.CountTag(CellTag.Sentence);
                    int mines = mineGeneric + combat + chest + dialogue + shrine + sentence; // total encounters
                    int inactive = levelData.CountTag(CellTag.Inactive);
                    int safe = levelData.CountTag(CellTag.Safe);
                    int entry = levelData.CountTag(CellTag.Entry);
                    int empty = expectedSize - mines - inactive - safe - entry;

                    EditorGUILayout.LabelField($"Grid: {levelData.width} x {levelData.height} = {expectedSize} cells");

                    // Row 1: basic cell types
                    EditorGUILayout.BeginHorizontal();
                    DrawStatLabel("Empty", empty, new Color(0.8f, 0.8f, 0.8f));
                    DrawStatLabel("Entry", entry, new Color(0.3f, 1f, 0.5f));
                    DrawStatLabel("Inactive", inactive, new Color(0.5f, 0.5f, 0.5f));
                    DrawStatLabel("Safe", safe, new Color(0.4f, 0.9f, 0.9f));
                    EditorGUILayout.EndHorizontal();

                    // Row 2: encounter types (painted)
                    EditorGUILayout.BeginHorizontal();
                    DrawStatLabel("Mine(rnd)", mineGeneric, new Color(1f, 0.3f, 0.3f));
                    DrawStatLabel("Combat", combat, new Color(1f, 0.4f, 0.4f));
                    DrawStatLabel("Chest", chest, new Color(1f, 0.85f, 0.3f));
                    DrawStatLabel("Dialogue", dialogue, new Color(0.5f, 0.8f, 1f));
                    DrawStatLabel("Shrine", shrine, new Color(0.8f, 0.5f, 1f));
                    DrawStatLabel("Sentence", sentence, new Color(1f, 0.5f, 0f));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField($"Total painted encounters: {mines}",
                        EditorStyles.miniLabel);

                    // ---- Reconciliation Preview ----
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Reconciliation Preview", EditorStyles.boldLabel);

                    // Mine reconciliation
                    int target = levelData.targetMineCount;
                    if (target > 0)
                    {
                        string mineStatus;
                        Color mineColor;
                        if (mines == target)
                        {
                            mineStatus = $"Mines: {mines} painted = {target} target (exact match)";
                            mineColor = new Color(0.5f, 1f, 0.5f);
                        }
                        else if (mines > target)
                        {
                            mineStatus = $"Mines: {mines} painted > {target} target → {mines - target} will be randomly removed";
                            mineColor = new Color(1f, 0.7f, 0.3f);
                        }
                        else
                        {
                            mineStatus = $"Mines: {mines} painted < {target} target → {target - mines} will be randomly added";
                            mineColor = new Color(0.7f, 0.7f, 1f);
                        }
                        var mineStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = mineColor }, wordWrap = true };
                        EditorGUILayout.LabelField(mineStatus, mineStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"Mines: {mines} painted (no target — all will be used)",
                            new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } });
                    }

                    // Entry point
                    if (entry == 0)
                    {
                        EditorGUILayout.LabelField("Entry: none painted → random cell will be chosen at runtime",
                            new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.7f, 0.3f) } });
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"Entry: {entry} painted",
                            new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.5f, 1f, 0.5f) } });
                    }

                    // ---- Encounter Summary ----
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Encounter Distribution", EditorStyles.boldLabel);

                    int effectiveMines = target > 0 ? target : mines;
                    int playableCells = expectedSize - inactive; // includes Safe cells (playable but mine-free)
                    int minableCells = playableCells - safe - entry; // cells where mines CAN be placed
                    float encounterPct = playableCells > 0 ? (effectiveMines * 100f / playableCells) : 0f;

                    // Ratio encounters / playable cells
                    EditorGUILayout.LabelField(
                        $"Encounters: {effectiveMines} / {playableCells} playable ({encounterPct:F1}%) — {minableCells} cells available for mines",
                        EditorStyles.label);

                    // Encounter type breakdown (always show, with 0s)
                    int totalTargets = levelData.TotalEncounterTargets;
                    int fromWeights = Mathf.Max(0, effectiveMines - totalTargets);

                    EditorGUILayout.BeginHorizontal();
                    DrawStatLabel("Combat", levelData.targetCombat, new Color(1f, 0.4f, 0.4f));
                    DrawStatLabel("Chest", levelData.targetChest, new Color(1f, 0.85f, 0.3f));
                    DrawStatLabel("Dialogue", levelData.targetDialogue, new Color(0.5f, 0.8f, 1f));
                    DrawStatLabel("Shrine", levelData.targetShrine, new Color(0.8f, 0.5f, 1f));
                    DrawStatLabel("Sentence", levelData.targetSentence, new Color(1f, 0.5f, 0f));
                    EditorGUILayout.EndHorizontal();

                    // Sum line
                    string sumLabel = $"Sum: {totalTargets} / {effectiveMines} encounters";
                    if (totalTargets == effectiveMines)
                    {
                        EditorGUILayout.LabelField(sumLabel + " (exact match)",
                            new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.5f, 1f, 0.5f) } });
                    }
                    else if (totalTargets > effectiveMines)
                    {
                        EditorGUILayout.HelpBox(
                            $"{sumLabel} — targets exceed encounter count! Types will be randomly truncated at runtime.",
                            MessageType.Warning);
                    }
                    else if (totalTargets > 0)
                    {
                        EditorGUILayout.LabelField(sumLabel + $" → {fromWeights} filled from distribution weights",
                            new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.7f, 0.7f, 1f) } });
                    }
                    else
                    {
                        EditorGUILayout.LabelField(sumLabel + " (all from distribution weights)",
                            new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } });
                    }
                }

                // ---- Encounter Pool Preview ----
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Encounter Pool", EditorStyles.boldLabel);

                var pool = levelData.encounterPool;
                if (pool == null)
                {
                    EditorGUILayout.HelpBox(
                        "No Encounter Pool assigned. Encounters will use fallback content from MineDistributionSO.",
                        MessageType.Info);
                }
                else
                {
                    int poolC = pool.combatPool != null ? pool.combatPool.Length : 0;
                    int poolCh = pool.chestPool != null ? pool.chestPool.Length : 0;
                    int poolD = pool.dialoguePool != null ? pool.dialoguePool.Length : 0;
                    int poolS = pool.shrinePool != null ? pool.shrinePool.Length : 0;
                    int poolSe = pool.sentencePool != null ? pool.sentencePool.Length : 0;

                    EditorGUILayout.BeginHorizontal();
                    DrawStatLabel("Pool Combat", poolC, poolC > 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f));
                    DrawStatLabel("Pool Chest", poolCh, poolCh > 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f));
                    DrawStatLabel("Pool Dialogue", poolD, poolD > 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f));
                    DrawStatLabel("Pool Shrine", poolS, poolS > 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f));
                    DrawStatLabel("Pool Sentence", poolSe, poolSe > 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(5);

                // Action buttons
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Open Scene Painter", GUILayout.Height(28)))
                {
                    LevelPainterTool.Activate(levelData);
                    // Focus Scene View
                    SceneView sceneView = SceneView.lastActiveSceneView;
                    if (sceneView != null)
                    {
                        sceneView.Focus();
                        // Zoom to grid
                        float gridSize = Mathf.Max(levelData.width, levelData.height) * levelData.cellSize;
                        sceneView.LookAt(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), gridSize * 0.7f);
                    }
                }

                if (GUILayout.Button("Clear All", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("Clear Level",
                        "This will reset ALL cells to Empty. Are you sure?",
                        "Yes, clear all", "Cancel"))
                    {
                        Undo.RecordObject(levelData, "Clear All Cells");
                        levelData.InitCells();
                        EditorUtility.SetDirty(levelData);
                    }
                }

                EditorGUILayout.EndHorizontal();

                // Resize section
                EditorGUILayout.Space(5);
                if (levelData.cells.Length != expectedSize)
                {
                    if (GUILayout.Button($"Resize to {levelData.width}x{levelData.height} (preserve data)"))
                    {
                        Undo.RecordObject(levelData, "Resize Grid");
                        levelData.ResizeCells(levelData.width, levelData.height);
                        EditorUtility.SetDirty(levelData);
                    }
                }
            }
        }

        private void DrawStatLabel(string label, int count, Color color)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color }
            };
            EditorGUILayout.LabelField($"{label}: {count}", style);
        }
    }
}
#endif
