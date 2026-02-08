using System.Collections.Generic;
using Sweeper.Data;
using UnityEngine;

namespace Sweeper.Logic
{
    public enum RevealResult
    {
        Safe,
        Mine,
        AlreadyRevealed,
        Flagged
    }

    /// <summary>
    /// Pure static functions for Minesweeper rules.
    /// No state — operates on GridModel passed as argument.
    /// </summary>
    public static class MinesweeperLogic
    {
        // ----- Mine Placement -----

        /// <summary>
        /// Place mines randomly, excluding the first-click cell and its 8 neighbors.
        /// </summary>
        public static void PlaceMinesRandom(GridModel grid, int count, int safeX, int safeY)
        {
            var candidates = new List<(int x, int y)>();

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    // Exclude the safe zone (clicked cell + 8 neighbors)
                    if (Mathf.Abs(x - safeX) <= 1 && Mathf.Abs(y - safeY) <= 1)
                        continue;
                    candidates.Add((x, y));
                }
            }

            // Clamp count to available cells
            int actual = Mathf.Min(count, candidates.Count);

            // Fisher-Yates shuffle, pick first 'actual' elements
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            for (int i = 0; i < actual; i++)
            {
                var (mx, my) = candidates[i];
                grid.GetCell(mx, my).hasMine = true;
            }

            grid.MineCount = actual;
        }

        /// <summary>
        /// Place mines from a pre-parsed layout bool map.
        /// </summary>
        public static void PlaceMinesFromLayout(GridModel grid, bool[,] mineMap)
        {
            int w = mineMap.GetLength(0);
            int h = mineMap.GetLength(1);

            if (w != grid.Width || h != grid.Height)
            {
                Debug.LogError($"[MinesweeperLogic] Layout size ({w}x{h}) does not match grid ({grid.Width}x{grid.Height}).");
                return;
            }

            int mineCount = 0;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (mineMap[x, y])
                    {
                        grid.GetCell(x, y).hasMine = true;
                        mineCount++;
                    }
                }
            }

            grid.MineCount = mineCount;
        }

        // ----- Adjacency -----

        /// <summary>
        /// Compute adjacentMines for every cell in the grid.
        /// </summary>
        public static void ComputeAdjacency(GridModel grid)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    int count = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (grid.IsInBounds(nx, ny) && grid.GetCell(nx, ny).hasMine)
                                count++;
                        }
                    }
                    grid.GetCell(x, y).adjacentMines = count;
                }
            }
        }

        // ----- Reveal -----

        /// <summary>
        /// Attempt to reveal a single cell.
        /// </summary>
        public static RevealResult Reveal(GridModel grid, int x, int y)
        {
            CellData cell = grid.GetCell(x, y);
            if (cell == null) return RevealResult.AlreadyRevealed;

            if (cell.isRevealed) return RevealResult.AlreadyRevealed;
            if (cell.isFlagged) return RevealResult.Flagged;
            if (cell.hasMine) return RevealResult.Mine;

            cell.isRevealed = true;
            grid.RevealedCount++;
            return RevealResult.Safe;
        }

        /// <summary>
        /// Single unified operation: light + reveal from a click position.
        /// Replaces the old separate LightBrush + FloodFill two-step process.
        ///
        /// ONE BFS does everything:
        /// - All cells within radius get light=1 (fog lifts, cell becomes visible)
        /// - Active non-mine non-flagged cells are Minesweeper-revealed
        /// - Mines/flags: lit but not revealed, BLOCK propagation
        /// - Inactive: lit, not revealed, propagate (walls are transparent)
        /// - Already-revealed cells: propagate (expand through known territory)
        /// - Freshly revealed zero-cells: propagate
        /// - Freshly revealed numbered cells: STOP (Minesweeper boundary)
        ///
        /// Returns the list of cells whose light value changed (for lightmap update).
        /// </summary>
        public static List<(int x, int y)> Discover(GridModel grid, int startX, int startY, int maxRadius)
        {
            var lightChanged = new List<(int x, int y)>();
            float maxDistSq = (maxRadius + 0.5f) * (maxRadius + 0.5f);

            var visited = new bool[grid.Width, grid.Height];
            var queue = new Queue<(int x, int y)>();

            queue.Enqueue((startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();

                CellData cell = grid.GetCell(cx, cy);
                if (cell == null) continue;

                // Distance check
                float dx = cx - startX;
                float dy = cy - startY;
                if (dx * dx + dy * dy > maxDistSq) continue;

                // Mines stay DARK: never lit, never revealed, block propagation.
                // The player deduces mine positions from numbers, not from visible tiles.
                if (cell.hasMine) continue;

                // --- LIGHT: fog lifts for every non-mine visited cell ---
                if (cell.light < 1f)
                {
                    cell.light = 1f;
                    lightChanged.Add((cx, cy));
                }

                // --- REVEAL: only active, non-flagged, unrevealed ---
                bool wasAlreadyRevealed = cell.isRevealed;
                if (cell.isActive && !cell.isFlagged && !cell.isRevealed)
                {
                    cell.isRevealed = true;
                    grid.RevealedCount++;
                }

                // --- PROPAGATION ---
                // Inactive cells (walls): lit but block propagation.
                // You see the wall, but not what's behind it.
                if (!cell.isActive) continue;

                // Flags block
                if (cell.isFlagged) continue;

                // Numbered cells ALWAYS stop propagation (Minesweeper boundary).
                // A number is a wall — the BFS never crosses it, whether the cell
                // was just revealed or was already known territory.
                if (cell.adjacentMines > 0) continue;

                // Everything else propagates:
                // - already-revealed zero-cells (known territory)
                // - freshly revealed zero-cells
                for (int ddx = -1; ddx <= 1; ddx++)
                {
                    for (int ddy = -1; ddy <= 1; ddy++)
                    {
                        if (ddx == 0 && ddy == 0) continue;
                        int nx = cx + ddx;
                        int ny = cy + ddy;
                        if (!grid.IsInBounds(nx, ny)) continue;
                        if (visited[nx, ny]) continue;
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            return lightChanged;
        }

        /// <summary>
        /// Light-only version of Discover. Sets light=1 within radius but does NOT reveal.
        /// Used for entry point bootstrap in random mode (mines not placed yet).
        /// Same propagation as Discover: mines block, inactive cells don't.
        /// </summary>
        public static List<(int x, int y)> ExpandVisibility(GridModel grid, int cx, int cy, int radius)
        {
            var changed = new List<(int x, int y)>();
            float radiusSq = (radius + 0.5f) * (radius + 0.5f);

            var visited = new bool[grid.Width, grid.Height];
            var queue = new Queue<(int x, int y)>();

            queue.Enqueue((cx, cy));
            visited[cx, cy] = true;

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                CellData cell = grid.GetCell(x, y);
                if (cell == null) continue;

                float dx = x - cx;
                float dy = y - cy;
                if (dx * dx + dy * dy > radiusSq) continue;

                if (cell.light < 1f)
                {
                    cell.light = 1f;
                    changed.Add((x, y));
                }

                // Mines block propagation
                if (cell.hasMine) continue;

                // Inactive cells (walls): lit but block propagation
                if (!cell.isActive) continue;

                for (int ddx = -1; ddx <= 1; ddx++)
                {
                    for (int ddy = -1; ddy <= 1; ddy++)
                    {
                        if (ddx == 0 && ddy == 0) continue;
                        int nx = x + ddx;
                        int ny = y + ddy;
                        if (!grid.IsInBounds(nx, ny)) continue;
                        if (visited[nx, ny]) continue;
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            return changed;
        }

        // ----- Flags -----

        /// <summary>
        /// Toggle the flag state on an unrevealed cell.
        /// </summary>
        public static void ToggleFlag(GridModel grid, int x, int y)
        {
            CellData cell = grid.GetCell(x, y);
            if (cell == null || cell.isRevealed) return;

            cell.isFlagged = !cell.isFlagged;
            grid.FlagCount += cell.isFlagged ? 1 : -1;
        }

        // ----- Victory -----

        /// <summary>
        /// Returns true if every active non-mine cell has been revealed.
        /// Inactive cells are excluded — they can't be revealed.
        /// </summary>
        public static bool CheckVictory(GridModel grid)
        {
            int totalPlayable = grid.Width * grid.Height - grid.MineCount - grid.InactiveCount;
            return grid.RevealedCount >= totalPlayable;
        }
    }
}
