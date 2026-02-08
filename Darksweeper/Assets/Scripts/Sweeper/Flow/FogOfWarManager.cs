using System.Collections.Generic;
using Sweeper.Data;
using UnityEngine;

namespace Sweeper.Flow
{
    /// <summary>
    /// Manages a Texture2D lightmap for the fog of war system.
    ///
    /// Two channels:
    ///   R = raw light (0 = dark, 1 = lit) — drives the fog mask
    ///   G = normalized distance to nearest dark cell (0 at fog edge, 1 deep inside)
    ///       — drives the brightness gradient
    ///
    /// The distance field is computed via BFS every time the lightmap changes.
    /// For typical grid sizes (20×30) this is negligible.
    /// </summary>
    public class FogOfWarManager : MonoBehaviour
    {
        [Tooltip("How many cells the light gradient spans from the fog edge to full brightness.")]
        [SerializeField] private int falloffRadius = 5;

        private Texture2D lightmapTexture;
        private Color[] pixelBuffer;
        private int texWidth;
        private int texHeight;

        // Reusable BFS buffers (avoid GC)
        private int[] distField;
        private Queue<int> bfsQueue;

        public Texture2D LightmapTexture => lightmapTexture;

        /// <summary>
        /// Create the lightmap texture. Call once when the grid is created.
        /// </summary>
        public void InitLightmap(int width, int height)
        {
            texWidth = width;
            texHeight = height;

            lightmapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            pixelBuffer = new Color[width * height];
            for (int i = 0; i < pixelBuffer.Length; i++)
                pixelBuffer[i] = Color.black;

            distField = new int[width * height];
            bfsQueue = new Queue<int>(width * height);

            lightmapTexture.SetPixels(pixelBuffer);
            lightmapTexture.Apply();

            Debug.Log($"[FogOfWar] Lightmap created: {width}x{height}, falloff radius: {falloffRadius}");
        }

        /// <summary>
        /// Update the lightmap for cells whose light value changed.
        /// Recomputes the full distance field (fast for small grids).
        /// </summary>
        public void UpdateLightmap(GridModel grid, List<(int x, int y)> changedCells)
        {
            if (lightmapTexture == null) return;

            // Write raw light values to R channel
            foreach (var (x, y) in changedCells)
            {
                CellData cell = grid.GetCell(x, y);
                if (cell == null) continue;
                float v = Mathf.Clamp01(cell.light);
                int idx = y * texWidth + x;
                pixelBuffer[idx] = new Color(v, pixelBuffer[idx].g, 0f, 1f);
            }

            // Recompute distance field and write G channel
            ComputeDistanceField();

            lightmapTexture.SetPixels(pixelBuffer);
            lightmapTexture.Apply();
        }

        /// <summary>
        /// Force-update all pixels from the grid (e.g., after RevealAllMines).
        /// </summary>
        public void RefreshFullLightmap(GridModel grid)
        {
            if (lightmapTexture == null) return;

            // Write raw light values to R channel
            for (int x = 0; x < texWidth; x++)
            {
                for (int y = 0; y < texHeight; y++)
                {
                    CellData cell = grid.GetCell(x, y);
                    if (cell == null) continue;
                    float v = Mathf.Clamp01(cell.light);
                    int idx = y * texWidth + x;
                    pixelBuffer[idx] = new Color(v, 0f, 0f, 1f);
                }
            }

            // Recompute distance field and write G channel
            ComputeDistanceField();

            lightmapTexture.SetPixels(pixelBuffer);
            lightmapTexture.Apply();
        }

        /// <summary>
        /// BFS from all dark cells simultaneously.
        /// Computes Chebyshev distance (king-moves) to the nearest dark cell.
        /// Writes the normalized distance (0..1) into the G channel of pixelBuffer.
        ///
        /// Also treats cells just outside the grid boundary as dark,
        /// so lit cells at the grid edge naturally dim toward the outside.
        /// </summary>
        private void ComputeDistanceField()
        {
            int w = texWidth, h = texHeight;
            int total = w * h;

            bfsQueue.Clear();

            // Init: dark cells = 0, lit cells = -1 (unvisited)
            for (int i = 0; i < total; i++)
            {
                if (pixelBuffer[i].r < 0.5f)
                {
                    distField[i] = 0;
                    bfsQueue.Enqueue(i);
                }
                else
                {
                    distField[i] = -1;
                }
            }

            // Also seed from grid edges: treat just outside as dark.
            // This makes lit cells at the grid boundary dimmer (natural edge fade).
            for (int x = 0; x < w; x++)
            {
                // Bottom edge
                int idxBot = x; // y=0
                if (distField[idxBot] == -1)
                {
                    distField[idxBot] = 1; // distance 1 from virtual outside dark cell
                    bfsQueue.Enqueue(idxBot);
                }
                // Top edge
                int idxTop = (h - 1) * w + x; // y=h-1
                if (distField[idxTop] == -1)
                {
                    distField[idxTop] = 1;
                    bfsQueue.Enqueue(idxTop);
                }
            }
            for (int y = 1; y < h - 1; y++)
            {
                // Left edge
                int idxLeft = y * w; // x=0
                if (distField[idxLeft] == -1)
                {
                    distField[idxLeft] = 1;
                    bfsQueue.Enqueue(idxLeft);
                }
                // Right edge
                int idxRight = y * w + (w - 1); // x=w-1
                if (distField[idxRight] == -1)
                {
                    distField[idxRight] = 1;
                    bfsQueue.Enqueue(idxRight);
                }
            }

            // BFS — Chebyshev distance (8-directional, each step = +1)
            while (bfsQueue.Count > 0)
            {
                int idx = bfsQueue.Dequeue();
                int cx = idx % w;
                int cy = idx / w;
                int currentDist = distField[idx];

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        int nidx = ny * w + nx;
                        if (distField[nidx] != -1) continue; // already visited
                        distField[nidx] = currentDist + 1;
                        bfsQueue.Enqueue(nidx);
                    }
                }
            }

            // Write G channel: normalized distance (0 at edge, 1 deep inside)
            float invRadius = 1f / Mathf.Max(falloffRadius, 1);
            for (int i = 0; i < total; i++)
            {
                float d = distField[i] >= 0 ? distField[i] : 0;
                float normalized = Mathf.Clamp01(d * invRadius);
                Color c = pixelBuffer[i];
                pixelBuffer[i] = new Color(c.r, normalized, 0f, 1f);
            }
        }

        /// <summary>
        /// Force a single cell to "fully lit" in the lightmap, without touching
        /// the grid model. Used for resolved mine events.
        /// </summary>
        public void RevealCell(int x, int y)
        {
            if (lightmapTexture == null) return;
            if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) return;

            int idx = y * texWidth + x;
            pixelBuffer[idx] = new Color(1f, 1f, 0f, 1f); // R=lit, G=max distance (full brightness)

            lightmapTexture.SetPixels(pixelBuffer);
            lightmapTexture.Apply();
        }

        /// <summary>
        /// Assign the lightmap texture to a material's _LightmapTex property.
        /// </summary>
        public void BindToMaterial(Material material)
        {
            if (material != null && lightmapTexture != null)
                material.SetTexture("_LightmapTex", lightmapTexture);
        }

        private void OnDestroy()
        {
            if (lightmapTexture != null)
                Destroy(lightmapTexture);
        }
    }
}
