using System.Collections.Generic;
using Sweeper.Data;
using UnityEngine;

namespace Sweeper.Flow
{
    /// <summary>
    /// Sparse grid of real Unity Point Lights for illuminating 3D objects.
    /// Instead of 1 light per cell (which blows the 256 limit), we place 1 light
    /// per NxN block of cells. Each sparse light's intensity is the average
    /// of its block's cell light values.
    /// </summary>
    public class SparseLightGrid : MonoBehaviour
    {
        [Header("Sparse Grid")]
        [Tooltip("One light per sparseStep x sparseStep block of cells.")]
        [SerializeField] private int sparseStep = 4;

        [Header("Light Settings")]
        [SerializeField] private float maxIntensity = 3f;
        [SerializeField] private float lightRange = 8f;
        [SerializeField] private float lightHeight = 3f;
        [SerializeField] private Color lightColor = new Color(1f, 0.95f, 0.85f);

        private Light[,] sparseLights;
        private int sparseW;
        private int sparseH;
        private int gridW;
        private int gridH;

        /// <summary>
        /// Create the sparse light grid. Call once when the game grid is created.
        /// </summary>
        public void InitLights(int gridWidth, int gridHeight, float cellSize, Vector3 gridOrigin)
        {
            // Cleanup previous lights
            DestroyLights();

            gridW = gridWidth;
            gridH = gridHeight;
            sparseW = Mathf.CeilToInt((float)gridWidth / sparseStep);
            sparseH = Mathf.CeilToInt((float)gridHeight / sparseStep);

            sparseLights = new Light[sparseW, sparseH];

            for (int sx = 0; sx < sparseW; sx++)
            {
                for (int sy = 0; sy < sparseH; sy++)
                {
                    // Position at the center of the block
                    float centerX = (sx * sparseStep + Mathf.Min(sparseStep, gridWidth - sx * sparseStep) * 0.5f - 0.5f);
                    float centerY = (sy * sparseStep + Mathf.Min(sparseStep, gridHeight - sy * sparseStep) * 0.5f - 0.5f);

                    Vector3 worldPos = gridOrigin + new Vector3(centerX * cellSize, lightHeight, centerY * cellSize);

                    var lightGO = new GameObject($"SparseLight_{sx}_{sy}");
                    lightGO.transform.SetParent(transform);
                    lightGO.transform.position = worldPos;

                    var pointLight = lightGO.AddComponent<Light>();
                    pointLight.type = LightType.Point;
                    pointLight.range = lightRange;
                    pointLight.intensity = 0f;
                    pointLight.color = lightColor;
                    pointLight.shadows = LightShadows.None;
                    pointLight.enabled = false;

                    sparseLights[sx, sy] = pointLight;
                }
            }

            Debug.Log($"[SparseLightGrid] Created {sparseW}x{sparseH} = {sparseW * sparseH} sparse lights (step={sparseStep})");
        }

        /// <summary>
        /// Update sparse light intensities from cells whose light value changed.
        /// Only recalculates blocks that contain changed cells.
        /// </summary>
        public void UpdateFromGrid(GridModel grid, List<(int x, int y)> changedCells)
        {
            if (sparseLights == null) return;

            // Track which sparse blocks need updating
            var dirtyBlocks = new HashSet<(int, int)>();

            foreach (var (cx, cy) in changedCells)
            {
                int bx = cx / sparseStep;
                int by = cy / sparseStep;
                if (bx < sparseW && by < sparseH)
                    dirtyBlocks.Add((bx, by));
            }

            // Recalculate each dirty block
            foreach (var (bx, by) in dirtyBlocks)
            {
                float sum = 0f;
                int count = 0;

                int xStart = bx * sparseStep;
                int yStart = by * sparseStep;
                int xEnd = Mathf.Min(xStart + sparseStep, gridW);
                int yEnd = Mathf.Min(yStart + sparseStep, gridH);

                for (int x = xStart; x < xEnd; x++)
                {
                    for (int y = yStart; y < yEnd; y++)
                    {
                        CellData cell = grid.GetCell(x, y);
                        if (cell != null)
                        {
                            sum += cell.light;
                            count++;
                        }
                    }
                }

                float average = count > 0 ? sum / count : 0f;
                Light light = sparseLights[bx, by];

                if (average <= 0f)
                {
                    light.enabled = false;
                }
                else
                {
                    light.enabled = true;
                    light.intensity = average * maxIntensity;
                }
            }
        }

        /// <summary>
        /// Recalculate ALL sparse light blocks from the full grid (no changedCells list).
        /// Used for full-refresh scenarios (e.g. RevealAllMines).
        /// </summary>
        public void RefreshAll(GridModel grid)
        {
            if (sparseLights == null) return;

            for (int bx = 0; bx < sparseW; bx++)
            {
                for (int by = 0; by < sparseH; by++)
                {
                    float sum = 0f;
                    int count = 0;

                    int xStart = bx * sparseStep;
                    int yStart = by * sparseStep;
                    int xEnd = Mathf.Min(xStart + sparseStep, gridW);
                    int yEnd = Mathf.Min(yStart + sparseStep, gridH);

                    for (int x = xStart; x < xEnd; x++)
                    {
                        for (int y = yStart; y < yEnd; y++)
                        {
                            CellData cell = grid.GetCell(x, y);
                            if (cell != null) { sum += cell.light; count++; }
                        }
                    }

                    float average = count > 0 ? sum / count : 0f;
                    Light light = sparseLights[bx, by];
                    if (average <= 0f) { light.enabled = false; }
                    else { light.enabled = true; light.intensity = average * maxIntensity; }
                }
            }
        }

        /// <summary>
        /// Destroy all sparse lights.
        /// </summary>
        public void DestroyLights()
        {
            if (sparseLights == null) return;

            for (int sx = 0; sx < sparseLights.GetLength(0); sx++)
            {
                for (int sy = 0; sy < sparseLights.GetLength(1); sy++)
                {
                    if (sparseLights[sx, sy] != null)
                        Destroy(sparseLights[sx, sy].gameObject);
                }
            }

            sparseLights = null;
        }

        private void OnDestroy()
        {
            DestroyLights();
        }
    }
}
