using Mines.Presentation;
using Sweeper.Data;
using Sweeper.Flow;
using TMPro;
using UnityEngine;

namespace Sweeper.Presentation
{
    /// <summary>
    /// Spawns and manages the 3D grid of Quad cells in the XZ plane.
    /// The background plane is a PERSISTENT scene object (not created at runtime).
    /// At runtime, only the fog-of-war material is applied to it.
    /// No per-cell Unity Lights — brightness is shader-driven.
    /// </summary>
    public class GridRenderer : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material baseMaterial; // CellOverlay shader material, instanced per cell
        [SerializeField] private Material fogOfWarMaterial; // FogOfWar shader material for background plane

        [Header("Background Plane (Scene Object)")]
        [Tooltip("Drag your background plane from the Hierarchy here. It must exist in the scene.")]
        [SerializeField] private MeshRenderer backgroundPlaneRenderer;

        [Header("Text")]
        [SerializeField] private TMP_FontAsset fontAsset;

        [Header("Grid Layout")]
        [Tooltip("Distance between cell centers in world units.")]
        [SerializeField] private float cellSize = 1.05f;
        [Tooltip("Scale of each quad. Set equal to cellSize for no gaps, or smaller for visible grid lines.")]
        [SerializeField] private float quadScale = 1f;
        [Tooltip("Offset the grid origin relative to the background plane center.")]
        [SerializeField] private Vector2 gridOffset = Vector2.zero;

        [Header("Camera")]
        [Tooltip("If true, the camera will be auto-positioned and resized to fit the grid at runtime. Disable if you position the camera manually.")]
        [SerializeField] private bool autoFitCamera = false;

        [Header("Level Data (for editor preview)")]
        [Tooltip("Optional: assign a LevelDataSO to see grid gizmos in editor before play.")]
        [SerializeField] private LevelDataSO levelDataPreview;

        [Header("Hover Colors (tweak in Inspector)")]
        [Tooltip("WHITE — outline when hovering a visible (lit) cell")]
        [SerializeField] private Color hoverBorderVisible = new Color(1f, 1f, 1f, 1f);
        [Tooltip("GREEN — outline when hovering a dark cell you can flag")]
        [SerializeField] private Color hoverBorderActionable = new Color(0.3f, 1f, 0.3f, 1f);
        [Tooltip("GREEN glow — emission for dark hover visibility")]
        [SerializeField] private Color hoverEmissionActionable = new Color(0.1f, 0.4f, 0.1f, 1f);
        [Tooltip("RED fill — base color for disabled/inactive hover")]
        [SerializeField] private Color hoverFillDisabled = new Color(0.8f, 0.15f, 0.15f, 0.7f);
        [Tooltip("RED border — outline for disabled/inactive hover")]
        [SerializeField] private Color hoverBorderDisabled = new Color(1f, 0.2f, 0.2f, 1f);
        [Tooltip("RED glow — emission for disabled hover visibility")]
        [SerializeField] private Color hoverEmissionDisabled = new Color(0.4f, 0.05f, 0.05f, 1f);
        [Tooltip("ORANGE glow — emission for flagged cells in the dark")]
        [SerializeField] private Color flagDarkEmission = new Color(0.5f, 0.25f, 0.05f, 1f);

        [Header("Mine Event Icons")]
        [Tooltip("Assign a MineIconsSO to display sprite icons on resolved mine cells. Create via Assets > Create > DarkSweeper/UI/Mine Icons.")]
        [SerializeField] private MineIconsSO mineIcons;

        private const float GridMargin = 1.5f;
        private const float TextYOffset = 0.02f;

        private CellView[,] cellViews;
        private Vector3 gridOrigin;
        private Material backgroundMaterialInstance;

        // Tracked values for live update
        private float prevCellSize;
        private float prevQuadScale;
        private Vector2 prevGridOffset;

        public Vector3 GridOrigin => gridOrigin;
        public float CellSize => cellSize;
        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }

        /// <summary>
        /// The instanced background material (for FogOfWarManager to bind lightmap texture).
        /// </summary>
        public Material BackgroundMaterial => backgroundMaterialInstance;

        /// <summary>
        /// Set the background texture on the existing background plane.
        /// </summary>
        public void SetBackgroundTexture(Texture2D texture)
        {
            if (backgroundMaterialInstance != null)
                backgroundMaterialInstance.SetTexture("_MainTex", texture);
        }

        /// <summary>
        /// Create the visual grid from a GridModel. Cells are Quads in the XZ plane.
        /// The background plane must already exist in the scene — we just apply the fog material.
        /// </summary>
        public void CreateGrid(GridModel model)
        {
            GridWidth = model.Width;
            GridHeight = model.Height;

            gridOrigin = CalculateGridOrigin(model.Width, model.Height);

            // Push hover colors to CellView (configurable from Inspector)
            CellView.SetHoverColors(
                hoverBorderVisible, hoverBorderActionable, hoverEmissionActionable,
                hoverFillDisabled, hoverBorderDisabled, hoverEmissionDisabled,
                flagDarkEmission
            );

            // Push mine event icon config to CellView
            CellView.SetMineIcons(mineIcons);

            // Apply fog-of-war material to the existing background plane
            SetupBackgroundMaterial();

            cellViews = new CellView[model.Width, model.Height];

            for (int x = 0; x < model.Width; x++)
            {
                for (int y = 0; y < model.Height; y++)
                {
                    Vector3 pos = gridOrigin + new Vector3(x * cellSize, 0f, y * cellSize);
                    GameObject cellGO = CreateCellGameObject(x, y, pos);
                    cellViews[x, y] = cellGO.GetComponent<CellView>();
                }
            }

            if (autoFitCamera)
                FitCamera(model.Width, model.Height);

            SnapshotValues();
        }

        /// <summary>
        /// Destroy all runtime cell GameObjects. Does NOT touch the background plane.
        /// </summary>
        public void DestroyGrid()
        {
            if (cellViews != null)
            {
                for (int x = 0; x < cellViews.GetLength(0); x++)
                {
                    for (int y = 0; y < cellViews.GetLength(1); y++)
                    {
                        if (cellViews[x, y] != null)
                            Destroy(cellViews[x, y].gameObject);
                    }
                }
                cellViews = null;
            }

            if (backgroundMaterialInstance != null)
            {
                // Restore original material on the plane
                Destroy(backgroundMaterialInstance);
                backgroundMaterialInstance = null;
            }
        }

        /// <summary>
        /// Synchronize ALL cell views from the grid model in one pass.
        /// Sets both brightness (_Brightness shader property) and visual state
        /// (colors, borders, quad visibility) from cell.light and cell state.
        /// This is the SINGLE method to call after any model mutation.
        /// </summary>
        public void SyncAllCells(GridModel model)
        {
            for (int x = 0; x < model.Width; x++)
            {
                for (int y = 0; y < model.Height; y++)
                {
                    CellData cell = model.GetCell(x, y);
                    CellView view = cellViews[x, y];
                    view.UpdateBrightness(cell.light);
                    view.UpdateVisual(cell);
                }
            }
        }

        /// <summary>
        /// Refresh a single cell's visual + brightness (e.g. after flag toggle).
        /// </summary>
        public void SyncCell(int x, int y, CellData data)
        {
            if (cellViews == null || x < 0 || x >= cellViews.GetLength(0) || y < 0 || y >= cellViews.GetLength(1))
                return;
            cellViews[x, y].UpdateBrightness(data.light);
            cellViews[x, y].UpdateVisual(data);
        }

        public CellView GetCellView(int x, int y)
        {
            if (cellViews == null) return null;
            if (x < 0 || x >= cellViews.GetLength(0) || y < 0 || y >= cellViews.GetLength(1)) return null;
            return cellViews[x, y];
        }

        // ---- Live update ----

        private void LateUpdate()
        {
            if (cellViews == null) return;

            bool layoutChanged = !Mathf.Approximately(cellSize, prevCellSize)
                              || !Mathf.Approximately(quadScale, prevQuadScale)
                              || !Mathf.Approximately(gridOffset.x, prevGridOffset.x)
                              || !Mathf.Approximately(gridOffset.y, prevGridOffset.y);

            if (layoutChanged)
            {
                RefreshGridLayout();
                SnapshotValues();
            }
        }

        private void RefreshGridLayout()
        {
            gridOrigin = CalculateGridOrigin(GridWidth, GridHeight);

            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    CellView view = cellViews[x, y];
                    if (view == null) continue;

                    view.transform.position = gridOrigin + new Vector3(x * cellSize, 0f, y * cellSize);
                    view.transform.localScale = new Vector3(quadScale, quadScale, 1f);
                }
            }

            if (autoFitCamera)
                FitCamera(GridWidth, GridHeight);
        }

        private Vector3 CalculateGridOrigin(int w, int h)
        {
            return new Vector3(
                -w * cellSize / 2f + cellSize / 2f + gridOffset.x,
                0f,
                -h * cellSize / 2f + cellSize / 2f + gridOffset.y
            );
        }

        private void SnapshotValues()
        {
            prevCellSize = cellSize;
            prevQuadScale = quadScale;
            prevGridOffset = gridOffset;
        }

        // ---- Background material ----

        private void SetupBackgroundMaterial()
        {
            if (backgroundPlaneRenderer == null) return;

            // Save the editor texture BEFORE we replace the material
            Texture existingTex = null;
            if (backgroundPlaneRenderer.sharedMaterial != null)
                existingTex = backgroundPlaneRenderer.sharedMaterial.mainTexture;

            if (fogOfWarMaterial != null)
            {
                backgroundMaterialInstance = new Material(fogOfWarMaterial);
                backgroundPlaneRenderer.material = backgroundMaterialInstance;
            }

            // Restore the background texture onto the fog material
            if (existingTex != null && backgroundMaterialInstance != null)
                backgroundMaterialInstance.SetTexture("_MainTex", existingTex);

            // Set global shader variable for lightmap world-space mapping
            SetGlobalGridBounds();
        }

        /// <summary>
        /// Set global shader variables for the fog shader:
        /// - _DSGridBounds: world-space grid bounds (min corner + size)
        /// - _DSGridSize: grid dimensions in cells (for texel size computation)
        /// </summary>
        private void SetGlobalGridBounds()
        {
            float gridWorldW = GridWidth * cellSize;
            float gridWorldH = GridHeight * cellSize;
            if (gridWorldW <= 0f || gridWorldH <= 0f) return;

            // Grid bottom-left corner in world XZ
            float gridMinX = gridOffset.x - gridWorldW / 2f;
            float gridMinZ = gridOffset.y - gridWorldH / 2f;

            // _DSGridBounds: xy = min corner, zw = world size
            Vector4 bounds = new Vector4(gridMinX, gridMinZ, gridWorldW, gridWorldH);
            Shader.SetGlobalVector("_DSGridBounds", bounds);

            // _DSGridSize: xy = grid dimensions in cells (for texel size in shader)
            Shader.SetGlobalVector("_DSGridSize", new Vector4(GridWidth, GridHeight, 0f, 0f));
        }

        // ---- Cell creation ----

        private GameObject CreateCellGameObject(int x, int y, Vector3 position)
        {
            var cellGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cellGO.name = $"Cell_{x}_{y}";
            cellGO.transform.SetParent(transform);
            cellGO.transform.position = position;
            cellGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cellGO.transform.localScale = new Vector3(quadScale, quadScale, 1f);

            var collider = cellGO.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var meshRend = cellGO.GetComponent<MeshRenderer>();

            var textGO = new GameObject("NumberText");
            textGO.transform.SetParent(cellGO.transform, false);
            textGO.transform.localPosition = new Vector3(0f, 0f, -TextYOffset);
            textGO.transform.localRotation = Quaternion.identity;
            textGO.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

            var tmp = textGO.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36;
            tmp.fontStyle = FontStyles.Bold;
            if (fontAsset != null)
                tmp.font = fontAsset;
            tmp.sortingOrder = 1;

            var cellView = cellGO.AddComponent<CellView>();
            cellView.Initialize(meshRend, tmp, baseMaterial != null ? baseMaterial : meshRend.sharedMaterial);

            return cellGO;
        }

        private void FitCamera(int gridW, int gridH)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = true;
            float screenAspect = (float)Screen.width / Screen.height;
            float gridAspect = (float)gridW / gridH;

            if (gridAspect > screenAspect)
                cam.orthographicSize = (gridW * cellSize / screenAspect) / 2f + GridMargin;
            else
                cam.orthographicSize = (gridH * cellSize) / 2f + GridMargin;

            cam.transform.position = new Vector3(gridOffset.x, 15f, gridOffset.y);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // ---- Editor Gizmos ----

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Use levelDataPreview if assigned, otherwise use runtime dimensions
            int gw = GridWidth > 0 ? GridWidth : (levelDataPreview != null ? levelDataPreview.width : 0);
            int gh = GridHeight > 0 ? GridHeight : (levelDataPreview != null ? levelDataPreview.height : 0);
            float cs = levelDataPreview != null ? levelDataPreview.cellSize : cellSize;

            if (gw <= 0 || gh <= 0) return;

            Vector3 origin = new Vector3(
                -gw * cs / 2f + cs / 2f + gridOffset.x,
                0f,
                -gh * cs / 2f + cs / 2f + gridOffset.y
            );

            // Grid bounds
            Gizmos.color = new Color(0.4f, 0.8f, 0.4f, 0.5f);
            float totalW = gw * cs;
            float totalH = gh * cs;
            Vector3 center = new Vector3(gridOffset.x, 0.001f, gridOffset.y);
            Gizmos.DrawWireCube(center, new Vector3(totalW, 0f, totalH));

            // Individual cell outlines (skip if playing — cells are visible)
            if (cellViews != null) return;

            Gizmos.color = new Color(1f, 1f, 1f, 0.08f);
            for (int x = 0; x < gw; x++)
            {
                for (int y = 0; y < gh; y++)
                {
                    Vector3 pos = origin + new Vector3(x * cs, 0.001f, y * cs);
                    Gizmos.DrawWireCube(pos, new Vector3(cs * 0.95f, 0f, cs * 0.95f));
                }
            }
        }
#endif
    }
}
