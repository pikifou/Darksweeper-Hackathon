#if UNITY_EDITOR
using Sweeper.Data;
using Sweeper.Flow;
using Sweeper.Presentation;
using UnityEditor;
using UnityEngine;

namespace Sweeper.Editor
{
    /// <summary>
    /// Scene View paint tool for designing DarkSweeper levels.
    /// Select a LevelDataSO in the Project window, then activate this tool
    /// from the menu DarkSweeper > Level Painter.
    /// 
    /// The tool looks for a GridRenderer in the scene to pick up the gridOffset
    /// and background plane. If no GridRenderer is found, the grid is centered
    /// at origin.
    /// 
    /// Controls:
    ///   Left click/drag  = paint current brush
    ///   Right click/drag  = erase (set to Empty)
    ///   Shift + drag      = box fill
    ///   1=Empty, 2=Entry, 3=Inactive, 4=Safe, 5=Mine, 6=Combat, 7=Chest, 8=Dialogue, 9=Shrine
    ///   Escape            = deactivate painter
    /// </summary>
    public static class LevelPainterTool
    {
        private static LevelDataSO activeLevelData;
        private static CellTag currentBrush = CellTag.Mine;
        private static bool isActive;
        private static Vector2Int boxStart = new Vector2Int(-1, -1);
        private static bool isDraggingBox;

        // Cached scene references
        private static GridRenderer cachedGridRenderer;

        // Preview material for rendering the background texture in Scene View
        private static Material previewMaterial;

        // Colors for each CellTag
        private static readonly Color ColorEmpty    = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color ColorEntry    = new Color(0.2f, 1f, 0.4f, 0.6f);
        private static readonly Color ColorInactive = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        private static readonly Color ColorSafe     = new Color(0.3f, 0.9f, 0.9f, 0.35f);
        private static readonly Color ColorMine     = new Color(1f, 0.1f, 0.1f, 0.5f);
        private static readonly Color ColorCombat   = new Color(0.9f, 0.2f, 0.2f, 0.55f);
        private static readonly Color ColorChest    = new Color(1f, 0.85f, 0.2f, 0.55f);
        private static readonly Color ColorDialogue = new Color(0.3f, 0.7f, 1f, 0.55f);
        private static readonly Color ColorShrine   = new Color(0.7f, 0.3f, 1f, 0.55f);
        private static readonly Color GridLineColor   = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color GridBoundsColor = new Color(0.4f, 0.9f, 0.4f, 0.6f);

        // Label shortcuts for cell overlays (indexed by CellTag int value)
        private static readonly string[] TagLabels = { "", "E", "", "S", "M", "Co", "Ch", "Di", "Sh" };

        [MenuItem("DarkSweeper/Level Painter (Toggle)")]
        public static void TogglePainter()
        {
            if (isActive)
            {
                Deactivate();
                Debug.Log("[LevelPainter] Deactivated.");
            }
            else
            {
                var selected = Selection.activeObject as LevelDataSO;
                if (selected == null)
                {
                    Debug.LogWarning("[LevelPainter] Select a LevelDataSO asset in the Project window first.");
                    return;
                }
                Activate(selected);
                Debug.Log($"[LevelPainter] Activated for: {selected.name} ({selected.width}x{selected.height})");
            }
        }

        public static void Activate(LevelDataSO levelData)
        {
            activeLevelData = levelData;

            if (activeLevelData.cells == null || activeLevelData.cells.Length != activeLevelData.width * activeLevelData.height)
            {
                activeLevelData.InitCells();
                EditorUtility.SetDirty(activeLevelData);
            }

            // Cache the GridRenderer from the scene (if any)
            cachedGridRenderer = Object.FindFirstObjectByType<GridRenderer>();

            isActive = true;
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.RepaintAll();
        }

        public static void Deactivate()
        {
            isActive = false;
            activeLevelData = null;
            cachedGridRenderer = null;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!isActive || activeLevelData == null) return;

            HandleKeyboardShortcuts();
            DrawBackgroundPreview();
            DrawGrid();
            DrawCellOverlays();
            DrawToolbarOverlay(sceneView);
            HandleMouseInput();

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
                sceneView.Repaint();
        }

        // ---- Background Texture Preview ----

        private static void DrawBackgroundPreview()
        {
            if (activeLevelData.backgroundTexture == null) return;

            float w = activeLevelData.width * activeLevelData.cellSize;
            float h = activeLevelData.height * activeLevelData.cellSize;
            Vector2 offset = GetGridOffset();
            Vector3 center = new Vector3(offset.x, 0f, offset.y);

            Handles.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Handles.DrawWireCube(center + new Vector3(0f, -0.01f, 0f), new Vector3(w, 0f, h));

            bool planeExistsInScene = cachedGridRenderer != null
                && GetBackgroundPlaneRenderer() != null
                && GetBackgroundPlaneRenderer().gameObject.activeInHierarchy;

            if (!planeExistsInScene)
            {
                DrawTexturePreview(activeLevelData.backgroundTexture, center, w, h);
            }
        }

        private static MeshRenderer GetBackgroundPlaneRenderer()
        {
            if (cachedGridRenderer == null) return null;
            var so = new SerializedObject(cachedGridRenderer);
            var prop = so.FindProperty("backgroundPlaneRenderer");
            if (prop != null)
                return prop.objectReferenceValue as MeshRenderer;
            return null;
        }

        private static void DrawTexturePreview(Texture2D texture, Vector3 center, float width, float height)
        {
            if (previewMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) return;
                previewMaterial = new Material(shader);
                previewMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            previewMaterial.mainTexture = texture;
            previewMaterial.color = new Color(1f, 1f, 1f, 0.4f);
            previewMaterial.SetPass(0);

            float halfW = width / 2f;
            float halfH = height / 2f;
            float yPos = center.y - 0.005f;

            GL.PushMatrix();
            GL.Begin(GL.QUADS);

            GL.TexCoord2(1, 1);
            GL.Vertex3(center.x - halfW, yPos, center.z - halfH);
            GL.TexCoord2(0, 1);
            GL.Vertex3(center.x + halfW, yPos, center.z - halfH);
            GL.TexCoord2(0, 0);
            GL.Vertex3(center.x + halfW, yPos, center.z + halfH);
            GL.TexCoord2(1, 0);
            GL.Vertex3(center.x - halfW, yPos, center.z + halfH);

            GL.End();
            GL.PopMatrix();
        }

        // ---- Grid Drawing ----

        private static void DrawGrid()
        {
            int w = activeLevelData.width;
            int h = activeLevelData.height;
            float cs = activeLevelData.cellSize;

            Vector3 origin = GetGridOrigin();

            Handles.color = GridBoundsColor;
            Vector3 bl = origin + new Vector3(-cs / 2f, 0f, -cs / 2f);
            Vector3 br = bl + new Vector3(w * cs, 0f, 0f);
            Vector3 tr = bl + new Vector3(w * cs, 0f, h * cs);
            Vector3 tl = bl + new Vector3(0f, 0f, h * cs);
            Handles.DrawLine(bl, br);
            Handles.DrawLine(br, tr);
            Handles.DrawLine(tr, tl);
            Handles.DrawLine(tl, bl);

            Handles.color = GridLineColor;
            for (int x = 0; x <= w; x++)
            {
                Vector3 start = bl + new Vector3(x * cs, 0f, 0f);
                Vector3 end = start + new Vector3(0f, 0f, h * cs);
                Handles.DrawLine(start, end);
            }
            for (int y = 0; y <= h; y++)
            {
                Vector3 start = bl + new Vector3(0f, 0f, y * cs);
                Vector3 end = start + new Vector3(w * cs, 0f, 0f);
                Handles.DrawLine(start, end);
            }
        }

        private static void DrawCellOverlays()
        {
            if (activeLevelData.cells == null) return;

            int w = activeLevelData.width;
            int h = activeLevelData.height;
            float cs = activeLevelData.cellSize;
            Vector3 origin = GetGridOrigin();

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    CellTag tag = activeLevelData.GetCell(x, y);
                    if (tag == CellTag.Empty) continue;

                    Color color = GetTagColor(tag);
                    Vector3 center = origin + new Vector3(x * cs, 0.002f, y * cs);
                    float halfSize = cs * 0.45f;

                    Handles.color = color;
                    Vector3[] verts = new Vector3[]
                    {
                        center + new Vector3(-halfSize, 0, -halfSize),
                        center + new Vector3(halfSize, 0, -halfSize),
                        center + new Vector3(halfSize, 0, halfSize),
                        center + new Vector3(-halfSize, 0, halfSize)
                    };
                    Handles.DrawSolidRectangleWithOutline(verts, color, Color.clear);

                    // Draw label for non-empty cells
                    int tagIdx = (int)tag;
                    if (tagIdx >= 0 && tagIdx < TagLabels.Length && TagLabels[tagIdx].Length > 0)
                    {
                        Handles.color = Color.white;
                        Handles.Label(center + new Vector3(-cs * 0.15f, 0f, -cs * 0.15f),
                            TagLabels[tagIdx], labelStyle);
                    }
                }
            }
        }

        // ---- Mouse Input ----

        private static void HandleMouseInput()
        {
            Event e = Event.current;
            if (e == null) return;

            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlID);
                return;
            }

            if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
            {
                if (e.button == 0)
                {
                    Vector2Int cell = MouseToCell(e);
                    if (cell.x >= 0)
                    {
                        if (e.shift && e.type == EventType.MouseDown)
                        {
                            boxStart = cell;
                            isDraggingBox = true;
                        }
                        else if (!e.shift || !isDraggingBox)
                        {
                            PaintCell(cell.x, cell.y, currentBrush);
                        }
                    }
                    e.Use();
                }
                else if (e.button == 1)
                {
                    Vector2Int cell = MouseToCell(e);
                    if (cell.x >= 0)
                        PaintCell(cell.x, cell.y, CellTag.Empty);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && isDraggingBox)
            {
                Vector2Int cell = MouseToCell(e);
                if (cell.x >= 0 && boxStart.x >= 0)
                    FillBox(boxStart, cell, currentBrush);
                isDraggingBox = false;
                boxStart = new Vector2Int(-1, -1);
                e.Use();
            }
        }

        private static Vector2Int MouseToCell(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (Mathf.Abs(ray.direction.y) < 0.001f) return new Vector2Int(-1, -1);
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) return new Vector2Int(-1, -1);

            Vector3 hit = ray.origin + ray.direction * t;
            Vector3 origin = GetGridOrigin();
            float cs = activeLevelData.cellSize;

            int gx = Mathf.FloorToInt((hit.x - origin.x + cs / 2f) / cs);
            int gy = Mathf.FloorToInt((hit.z - origin.z + cs / 2f) / cs);

            if (gx >= 0 && gx < activeLevelData.width && gy >= 0 && gy < activeLevelData.height)
                return new Vector2Int(gx, gy);

            return new Vector2Int(-1, -1);
        }

        private static void PaintCell(int x, int y, CellTag tag)
        {
            if (activeLevelData.GetCell(x, y) != tag)
            {
                Undo.RecordObject(activeLevelData, $"Paint cell ({x},{y}) = {tag}");
                activeLevelData.SetCell(x, y, tag);
                EditorUtility.SetDirty(activeLevelData);
            }
        }

        private static void FillBox(Vector2Int start, Vector2Int end, CellTag tag)
        {
            int xMin = Mathf.Min(start.x, end.x);
            int xMax = Mathf.Max(start.x, end.x);
            int yMin = Mathf.Min(start.y, end.y);
            int yMax = Mathf.Max(start.y, end.y);

            Undo.RecordObject(activeLevelData, $"Fill box ({xMin},{yMin})-({xMax},{yMax}) = {tag}");
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                    activeLevelData.SetCell(x, y, tag);

            EditorUtility.SetDirty(activeLevelData);
        }

        // ---- Keyboard ----

        private static void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            switch (e.keyCode)
            {
                case KeyCode.Alpha1: case KeyCode.Keypad1:
                    currentBrush = CellTag.Empty;
                    Debug.Log("[LevelPainter] Brush: Empty");
                    e.Use(); break;
                case KeyCode.Alpha2: case KeyCode.Keypad2:
                    currentBrush = CellTag.Entry;
                    Debug.Log("[LevelPainter] Brush: Entry");
                    e.Use(); break;
                case KeyCode.Alpha3: case KeyCode.Keypad3:
                    currentBrush = CellTag.Inactive;
                    Debug.Log("[LevelPainter] Brush: Inactive");
                    e.Use(); break;
                case KeyCode.Alpha4: case KeyCode.Keypad4:
                    currentBrush = CellTag.Safe;
                    Debug.Log("[LevelPainter] Brush: Safe (no mine allowed)");
                    e.Use(); break;
                case KeyCode.Alpha5: case KeyCode.Keypad5:
                    currentBrush = CellTag.Mine;
                    Debug.Log("[LevelPainter] Brush: Mine (random encounter)");
                    e.Use(); break;
                case KeyCode.Alpha6: case KeyCode.Keypad6:
                    currentBrush = CellTag.Combat;
                    Debug.Log("[LevelPainter] Brush: Combat");
                    e.Use(); break;
                case KeyCode.Alpha7: case KeyCode.Keypad7:
                    currentBrush = CellTag.Chest;
                    Debug.Log("[LevelPainter] Brush: Chest");
                    e.Use(); break;
                case KeyCode.Alpha8: case KeyCode.Keypad8:
                    currentBrush = CellTag.Dialogue;
                    Debug.Log("[LevelPainter] Brush: Dialogue");
                    e.Use(); break;
                case KeyCode.Alpha9: case KeyCode.Keypad9:
                    currentBrush = CellTag.Shrine;
                    Debug.Log("[LevelPainter] Brush: Shrine");
                    e.Use(); break;
                case KeyCode.Escape:
                    Deactivate();
                    Debug.Log("[LevelPainter] Deactivated.");
                    e.Use(); break;
            }
        }

        // ---- Toolbar Overlay ----

        private static void DrawToolbarOverlay(SceneView sceneView)
        {
            Handles.BeginGUI();

            float toolbarWidth = 580f;
            float toolbarHeight = 120f;
            Rect rect = new Rect(10, 10, toolbarWidth, toolbarHeight);

            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            GUILayout.BeginArea(new Rect(15, 15, toolbarWidth - 10, toolbarHeight - 10));

            GUILayout.Label($"Level Painter: {activeLevelData.name} ({activeLevelData.width}x{activeLevelData.height})",
                EditorStyles.boldLabel);

            // Row 1: basic cell types
            GUILayout.BeginHorizontal();
            DrawBrushButton("1:Empty", CellTag.Empty);
            DrawBrushButton("2:Entry", CellTag.Entry);
            DrawBrushButton("3:Inactv", CellTag.Inactive);
            DrawBrushButton("4:Safe", CellTag.Safe);
            GUILayout.EndHorizontal();

            // Row 2: encounter types
            GUILayout.BeginHorizontal();
            DrawBrushButton("5:Mine", CellTag.Mine);
            DrawBrushButton("6:Combat", CellTag.Combat);
            DrawBrushButton("7:Chest", CellTag.Chest);
            DrawBrushButton("8:Dialog", CellTag.Dialogue);
            DrawBrushButton("9:Shrine", CellTag.Shrine);
            GUILayout.EndHorizontal();

            // Stats
            int totalEncounters = activeLevelData.MineCount;
            int safe = activeLevelData.CountTag(CellTag.Safe);
            int inactive = activeLevelData.CountTag(CellTag.Inactive);
            int entry = activeLevelData.CountTag(CellTag.Entry);
            GUILayout.Label($"Enc: {totalEncounters} (M:{activeLevelData.CountTag(CellTag.Mine)} " +
                           $"Co:{activeLevelData.CountTag(CellTag.Combat)} " +
                           $"Ch:{activeLevelData.CountTag(CellTag.Chest)} " +
                           $"Di:{activeLevelData.CountTag(CellTag.Dialogue)} " +
                           $"Sh:{activeLevelData.CountTag(CellTag.Shrine)}) | " +
                           $"Safe: {safe} | Inact: {inactive} | Entry: {entry}",
                EditorStyles.miniLabel);

            GUILayout.EndArea();

            Handles.EndGUI();
        }

        private static void DrawBrushButton(string label, CellTag tag)
        {
            bool isSelected = currentBrush == tag;
            var style = isSelected ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold } : GUI.skin.button;
            if (isSelected)
            {
                var bg = GUI.backgroundColor;
                GUI.backgroundColor = GetTagColor(tag) + new Color(0.3f, 0.3f, 0.3f, 0f);
                if (GUILayout.Button(label, style, GUILayout.Width(70)))
                    currentBrush = tag;
                GUI.backgroundColor = bg;
            }
            else
            {
                if (GUILayout.Button(label, style, GUILayout.Width(70)))
                    currentBrush = tag;
            }
        }

        // ---- Helpers ----

        private static Vector2 GetGridOffset()
        {
            if (cachedGridRenderer == null)
                cachedGridRenderer = Object.FindFirstObjectByType<GridRenderer>();

            if (cachedGridRenderer != null)
            {
                var so = new SerializedObject(cachedGridRenderer);
                var prop = so.FindProperty("gridOffset");
                if (prop != null)
                    return prop.vector2Value;
            }

            return Vector2.zero;
        }

        private static Vector3 GetGridOrigin()
        {
            float cs = activeLevelData.cellSize;
            Vector2 offset = GetGridOffset();
            return new Vector3(
                -activeLevelData.width * cs / 2f + cs / 2f + offset.x,
                0f,
                -activeLevelData.height * cs / 2f + cs / 2f + offset.y
            );
        }

        private static Color GetTagColor(CellTag tag)
        {
            return tag switch
            {
                CellTag.Entry    => ColorEntry,
                CellTag.Inactive => ColorInactive,
                CellTag.Safe     => ColorSafe,
                CellTag.Mine     => ColorMine,
                CellTag.Combat   => ColorCombat,
                CellTag.Chest    => ColorChest,
                CellTag.Dialogue => ColorDialogue,
                CellTag.Shrine   => ColorShrine,
                _                => ColorEmpty,
            };
        }
    }
}
#endif
