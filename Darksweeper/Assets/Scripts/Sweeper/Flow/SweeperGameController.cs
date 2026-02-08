using System;
using System.Collections.Generic;
using Sweeper.Data;
using Sweeper.Logic;
using Sweeper.Presentation;
using UnityEngine;

namespace Sweeper.Flow
{
    public enum GameState
    {
        WaitingForFirstClick,
        Playing,
        Won,
        Lost
    }

    /// <summary>
    /// Orchestrates the full DarkSweeper game loop.
    /// Startup, input routing, game state, win/lose conditions.
    ///
    /// UNIFIED MODEL: Light and Reveal are the SAME concept.
    /// MinesweeperLogic.Discover() is the single operation that handles
    /// both visibility (fog of war) and Minesweeper reveal in one BFS.
    /// After every model mutation, call SyncPresentationState() ONCE.
    /// </summary>
    public class SweeperGameController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private SweeperConfig config;
        [SerializeField] private LevelDataSO levelData; // null = random mode with config dimensions

        [Header("References")]
        [SerializeField] private GridRenderer gridRenderer;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private SweeperHUD hud;
        [SerializeField] private FogOfWarManager fogOfWar;
        [SerializeField] private SparseLightGrid sparseLights;

        // Events for external systems
        public event Action<int> OnHPChanged;
        public event Action<bool> OnGameOver; // true = won, false = lost
        public event Action<int> OnMinesRemainingChanged;

        // Events for the Mine Event system
        public event Action<GridModel> OnGridReady;           // fired after mines are placed and grid is ready
        public event Action<int, int, CellData> OnLeftClickMine;  // fired when left-click hits a mine (replaces defeat)
        public event Action<int, int, CellData> OnRightClickMine; // fired when right-click targets a mine cell

        // Private state
        private GridModel grid;
        private GameState currentState;
        private int currentHP;
        private int maxHP;
        private int buffCombatsRemaining; // combat damage reduction buff from mine events

        // Public accessors for Mine Event system
        public GridModel Grid => grid;
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public GameState CurrentState => currentState;
        public SweeperConfig Config => config;

        /// <summary>
        /// Trigger victory from an external system (e.g. MineEventController when all mines are resolved).
        /// </summary>
        public void TriggerVictory()
        {
            if (currentState == GameState.Won || currentState == GameState.Lost) return;

            currentState = GameState.Won;
            OnGameOver?.Invoke(true);
            if (hud != null) hud.ShowVictory();
            Debug.Log("[Sweeper] VICTORY — all mines resolved.");
        }

        /// <summary>
        /// Apply an HP change from an external system (mine events).
        /// Positive = heal, negative = damage. Clamps to [0, maxHP].
        /// </summary>
        public void ApplyHPDelta(int delta)
        {
            currentHP = Mathf.Clamp(currentHP + delta, 0, maxHP);
            OnHPChanged?.Invoke(currentHP);
            if (hud != null) hud.UpdateHP(currentHP, maxHP, delta);

            if (currentHP <= 0)
            {
                currentState = GameState.Lost;
                OnGameOver?.Invoke(false);
                if (hud != null) hud.ShowDefeat();
                Debug.Log("[Sweeper] DEFEAT — HP reached 0 from mine event.");
            }
        }

        /// <summary>
        /// Lift the fog of war on a single cell.
        /// The mine stays in the grid, but the cell becomes lit so the icon
        /// and background match. Also sets cell.light in the model so that
        /// subsequent SyncAllCells feeds the correct brightness.
        /// </summary>
        public void RevealCellFog(int x, int y)
        {
            // Update the grid model so SyncAllCells sends brightness=1
            CellData cell = grid.GetCell(x, y);
            if (cell != null)
                cell.light = 1f;

            // Update the lightmap texture (background fog of war)
            if (fogOfWar != null)
                fogOfWar.RevealCell(x, y);
        }

        /// <summary>
        /// Called after a mine event is resolved.
        /// Removes the mine flag, recalculates neighbour counts,
        /// runs Discover (light + reveal) from that cell, and syncs visuals.
        /// This lets the player walk through resolved mines.
        /// </summary>
        public void ClearResolvedMine(int x, int y)
        {
            CellData cell = grid.GetCell(x, y);
            if (cell == null) return;

            // 1. Remove the mine
            cell.hasMine = false;
            grid.MineCount--;

            // 2. Recalculate adjacentMines for all neighbours (and the cell itself)
            RecalcAdjacencyAround(x, y);

            // 3. The cell is now walkable — mark it revealed and lit
            cell.isRevealed = true;
            cell.light = 1f;

            // 4. Trigger Discover from this position so light floods through
            var litCells = MinesweeperLogic.Discover(grid, x, y, config.revealRadius);

            // 5. Full sync — lightmap, cell overlays, sparse lights
            SyncPresentationState(litCells);

            // 6. Update mines remaining display
            int remaining = grid.MineCount - grid.FlagCount;
            OnMinesRemainingChanged?.Invoke(remaining);
            if (hud != null) hud.UpdateMines(remaining);

            // Victory is now triggered by MineEventController when all mines are resolved.

            Debug.Log($"[Sweeper] Mine at ({x},{y}) cleared after resolution. " +
                      $"Remaining mines: {grid.MineCount}, lit {litCells.Count} new cells.");
        }

        /// <summary>
        /// Recalculate adjacentMines for the cell at (cx,cy) and all 8 neighbours.
        /// Used after removing a mine to update the number display.
        /// </summary>
        private void RecalcAdjacencyAround(int cx, int cy)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (!grid.IsInBounds(nx, ny)) continue;

                    CellData cell = grid.GetCell(nx, ny);
                    int count = 0;
                    for (int ddx = -1; ddx <= 1; ddx++)
                    {
                        for (int ddy = -1; ddy <= 1; ddy++)
                        {
                            if (ddx == 0 && ddy == 0) continue;
                            int nnx = nx + ddx;
                            int nny = ny + ddy;
                            if (grid.IsInBounds(nnx, nny) && grid.GetCell(nnx, nny).hasMine)
                                count++;
                        }
                    }
                    cell.adjacentMines = count;
                }
            }
        }

        /// <summary>
        /// Get/set combat buff remaining from mine event rewards.
        /// </summary>
        public int BuffCombatsRemaining
        {
            get => buffCombatsRemaining;
            set => buffCombatsRemaining = value;
        }

        private void Start()
        {
            InitializeGame();
        }

        private void OnEnable()
        {
            if (inputHandler != null)
            {
                inputHandler.OnLeftClick += HandleLeftClick;
                inputHandler.OnRightClick += HandleRightClick;
            }
        }

        private void OnDisable()
        {
            if (inputHandler != null)
            {
                inputHandler.OnLeftClick -= HandleLeftClick;
                inputHandler.OnRightClick -= HandleRightClick;
            }
        }

        /// <summary>
        /// Restart the game from any state.
        /// </summary>
        public void RestartGame()
        {
            gridRenderer.DestroyGrid();
            InitializeGame();
        }

        // ==================================================================
        // SINGLE SYNC POINT — call this ONCE after any model mutation
        // ==================================================================

        /// <summary>
        /// Synchronize ALL presentation systems from the grid model.
        /// If lightChangedCells is provided, only those cells are updated in the
        /// lightmap and sparse lights (incremental). Otherwise a full refresh is done.
        /// </summary>
        private void SyncPresentationState(List<(int, int)> lightChangedCells = null)
        {
            // 1. Lightmap texture (fog of war background)
            if (fogOfWar != null)
            {
                if (lightChangedCells != null)
                    fogOfWar.UpdateLightmap(grid, lightChangedCells);
                else
                    fogOfWar.RefreshFullLightmap(grid);
            }

            // 2. Cell overlays — brightness + visual state in one pass
            gridRenderer.SyncAllCells(grid);

            // 3. Sparse lights
            if (sparseLights != null)
            {
                if (lightChangedCells != null)
                    sparseLights.UpdateFromGrid(grid, lightChangedCells);
                else
                    sparseLights.RefreshAll(grid);
            }
        }

        // ==================================================================
        // Initialization
        // ==================================================================

        private void InitializeGame()
        {
            int width, height;
            bool hasLevelLayout = false;

            // Determine mode: level data or random
            if (levelData != null && levelData.cells != null && levelData.cells.Length > 0)
            {
                width = levelData.width;
                height = levelData.height;
                hasLevelLayout = true;
            }
            else
            {
                width = config.gridWidth;
                height = config.gridHeight;
            }

            // Create grid
            grid = new GridModel(width, height);

            if (hasLevelLayout)
            {
                // Layout mode — place mines from LevelDataSO cell tags
                PlaceMinesFromLevelData(grid, levelData);
                MinesweeperLogic.ComputeAdjacency(grid);
                currentState = GameState.Playing;
                Debug.Log($"[Sweeper] Grid created: {width}x{height}, mode: LevelData ({grid.MineCount} mines), state: Playing");
            }
            else
            {
                // Random mode — mines placed on first click
                currentState = GameState.WaitingForFirstClick;
                Debug.Log($"[Sweeper] Grid created: {width}x{height}, mode: Random, state: WaitingForFirstClick");
            }

            // Initialize HP
            maxHP = config.hpStart;
            currentHP = maxHP;

            // Initialize fog of war lightmap
            if (fogOfWar != null)
                fogOfWar.InitLightmap(width, height);

            // Build visuals (background plane already exists in scene — GridRenderer applies fog material)
            gridRenderer.CreateGrid(grid);

            // Pre-light the entry point so the player has somewhere to start
            LightEntryPoint(width, height, hasLevelLayout);

            // Set background texture from level data onto the fog material
            if (hasLevelLayout && levelData.backgroundTexture != null)
                gridRenderer.SetBackgroundTexture(levelData.backgroundTexture);

            // Bind lightmap to background material
            if (fogOfWar != null && gridRenderer.BackgroundMaterial != null)
                fogOfWar.BindToMaterial(gridRenderer.BackgroundMaterial);

            // Initialize sparse lights
            if (sparseLights != null)
                sparseLights.InitLights(width, height, gridRenderer.CellSize, gridRenderer.GridOrigin);

            // Now sync everything from the model (entry light + initial state)
            SyncPresentationState();

            // Initialize input handler
            if (inputHandler != null)
                inputHandler.Initialize(gridRenderer);

            // Initialize HUD
            if (hud != null)
                hud.ResetHUD(maxHP, grid.MineCount);

            // Fire initial events
            OnHPChanged?.Invoke(currentHP);
            OnMinesRemainingChanged?.Invoke(grid.MineCount - grid.FlagCount);

            // Notify mine event system that grid is ready (mines are placed in layout mode)
            if (hasLevelLayout)
                OnGridReady?.Invoke(grid);
        }

        // ==================================================================
        // Left Click Pipeline
        // ==================================================================

        private void HandleLeftClick(int x, int y)
        {
            // 1. Ignore if game is over
            if (currentState == GameState.Won || currentState == GameState.Lost)
                return;

            CellData clickedCell = grid.GetCell(x, y);
            if (clickedCell == null) return;

            // Can't click inactive or flagged cells
            if (!clickedCell.isActive || clickedCell.isFlagged)
                return;

            // 2. First click in random mode — place mines
            if (currentState == GameState.WaitingForFirstClick)
            {
                MinesweeperLogic.PlaceMinesRandom(grid, config.mineCount, x, y);
                MinesweeperLogic.ComputeAdjacency(grid);
                currentState = GameState.Playing;
                Debug.Log($"[Sweeper] First click at ({x},{y}). Mines placed: {grid.MineCount}. State: Playing");

                // Update HUD with actual mine count
                if (hud != null)
                    hud.UpdateMines(grid.MineCount - grid.FlagCount);
                OnMinesRemainingChanged?.Invoke(grid.MineCount - grid.FlagCount);

                // Notify mine event system that grid is ready (mines now placed)
                OnGridReady?.Invoke(grid);
            }

            // 3. Check HP
            if (currentHP <= 0) return;

            // 4. Spend HP (cost per reveal from LevelDataSO, default 0)
            int revealCost = (levelData != null) ? levelData.revealHPCost : 0;
            if (revealCost > 0)
            {
                currentHP = Mathf.Max(0, currentHP - revealCost);
                OnHPChanged?.Invoke(currentHP);
                if (hud != null) hud.UpdateHP(currentHP, maxHP, -revealCost);
            }

            // 5. Mine check BEFORE discover — delegate to Mine Event system instead of defeat
            if (!clickedCell.isRevealed && clickedCell.hasMine)
            {
                // Fire event for MineEventController to handle (penalty + interaction)
                OnLeftClickMine?.Invoke(x, y, clickedCell);
                Debug.Log($"[Sweeper] Left-click on mine at ({x},{y}) — delegated to Mine Event system.");
                return;
            }

            // 6. Single unified Discover: light + reveal in one BFS
            //    - All cells within radius get light=1 (fog lifts)
            //    - Minesweeper reveal follows standard rules inside the BFS
            //    - Clicking revealed cell: BFS propagates through known territory
            //    - Clicking unrevealed cell: standard Minesweeper flood fill
            var litCells = MinesweeperLogic.Discover(grid, x, y, config.revealRadius);

            // 7. SINGLE sync point — updates lightmap, cell overlays, sparse lights
            SyncPresentationState(litCells);

            // Victory is now triggered by MineEventController when all mines are resolved.
        }

        // ==================================================================
        // Right Click Pipeline
        // ==================================================================

        private void HandleRightClick(int x, int y)
        {
            if (currentState != GameState.Playing && currentState != GameState.WaitingForFirstClick)
                return;

            CellData cell = grid.GetCell(x, y);
            if (cell == null) return;

            Debug.Log($"[Sweeper] HandleRightClick({x},{y}) — hasMine={cell.hasMine}, light={cell.light:F2}, isRevealed={cell.isRevealed}, isFlagged={cell.isFlagged}, OnRightClickMine_subscribers={OnRightClickMine?.GetInvocationList()?.Length ?? 0}");

            // If cell is a mine, delegate to Mine Event system.
            // Clicking in the dark is allowed but carries penalties (handled by MineEventController).
            if (cell.hasMine && !cell.isRevealed)
            {
                OnRightClickMine?.Invoke(x, y, cell);
                Debug.Log($"[Sweeper] Right-click on mine at ({x},{y}) — delegated to Mine Event system.");
                return;
            }

            // If cell has no mine and is not yet revealed → false flag: reveal + HP penalty
            if (!cell.hasMine && !cell.isRevealed)
            {
                int penalty = (levelData != null) ? levelData.falseFlagPenalty : 3;

                // Reveal the cell via Discover BFS (lights + minesweeper reveal)
                var litCells = MinesweeperLogic.Discover(grid, x, y, config.revealRadius);
                SyncPresentationState(litCells);

                // Apply HP penalty
                if (penalty > 0)
                {
                    currentHP = Mathf.Max(0, currentHP - penalty);
                    OnHPChanged?.Invoke(currentHP);
                    if (hud != null) hud.UpdateHP(currentHP, maxHP, -penalty);

                    Debug.Log($"[Sweeper] False flag at ({x},{y}) — no mine! Penalty: -{penalty} HP. HP: {currentHP}");

                    if (currentHP <= 0)
                    {
                        currentState = GameState.Lost;
                        OnGameOver?.Invoke(false);
                        if (hud != null) hud.ShowDefeat();
                        Debug.Log("[Sweeper] DEFEAT — HP reached 0 from false flag penalty.");
                    }
                }

                // Victory is now triggered by MineEventController when all mines are resolved.
                return;
            }

            // Default: toggle flag on already-revealed cells (existing behavior)
            MinesweeperLogic.ToggleFlag(grid, x, y);
            gridRenderer.SyncCell(x, y, grid.GetCell(x, y));

            int remaining = grid.MineCount - grid.FlagCount;
            OnMinesRemainingChanged?.Invoke(remaining);
            if (hud != null) hud.UpdateMines(remaining);
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        /// <summary>
        /// Returns true if at least one of the 8 neighbours of (x,y) has light > 0.
        /// Used to determine whether the player has enough information to deduce a mine.
        /// </summary>
        private bool HasLitNeighbour(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    CellData neighbour = grid.GetCell(x + dx, y + dy);
                    if (neighbour != null && neighbour.light > 0f)
                        return true;
                }
            }
            return false;
        }

        private void RevealAllMines()
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    CellData c = grid.GetCell(x, y);
                    if (c.hasMine)
                    {
                        c.isRevealed = true;
                        c.light = 1f;
                    }
                }
            }

            // Full sync — no incremental list, refresh everything
            SyncPresentationState();
        }

        /// <summary>
        /// Pre-light the entry point cell so the player can see where to start.
        /// In level data mode: looks for CellTag.Entry. In random mode: lights the center.
        /// </summary>
        private void LightEntryPoint(int width, int height, bool hasLevelLayout)
        {
            int entryX = width / 2;
            int entryY = height / 2;

            if (hasLevelLayout && levelData != null)
            {
                // Find the first Entry-tagged cell
                bool found = false;
                for (int x = 0; x < levelData.width && !found; x++)
                {
                    for (int y = 0; y < levelData.height && !found; y++)
                    {
                        if (levelData.GetCell(x, y) == CellTag.Entry)
                        {
                            entryX = x;
                            entryY = y;
                            found = true;
                        }
                    }
                }
                if (!found)
                {
                    // No Entry painted — pick a random active, non-mine cell
                    var candidates = new List<(int x, int y)>();
                    for (int x = 0; x < width; x++)
                        for (int y = 0; y < height; y++)
                        {
                            CellData cell = grid.GetCell(x, y);
                            if (cell != null && cell.isActive && !cell.hasMine)
                                candidates.Add((x, y));
                        }
                    if (candidates.Count > 0)
                    {
                        var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                        entryX = pick.x;
                        entryY = pick.y;
                    }
                    Debug.LogWarning($"[Sweeper] No Entry cell found in LevelDataSO — picked random cell ({entryX}, {entryY}).");
                }
            }

            if (hasLevelLayout)
            {
                // Layout mode: mines already placed → full Discover (light + reveal)
                // Player sees the starting area with numbers and can click at the boundary.
                MinesweeperLogic.Discover(grid, entryX, entryY, config.revealRadius);
            }
            else
            {
                // Random mode: no mines yet → light-only (no reveal)
                // Player sees clickable tiles; first click places mines + triggers Discover.
                MinesweeperLogic.ExpandVisibility(grid, entryX, entryY, config.revealRadius);
            }

            // NOTE: SyncPresentationState is called by InitializeGame after this returns
            Debug.Log($"[Sweeper] Entry point at ({entryX}, {entryY}), mode: {(hasLevelLayout ? "Layout (discovered)" : "Random (lit only)")}");
        }

        /// <summary>
        /// Place mines in the GridModel from a LevelDataSO's cell tags,
        /// then reconcile with targetMineCount if specified.
        ///
        /// Reconciliation rules:
        /// - targetMineCount &lt;= 0 → use all painted mines as-is (backward compatible)
        /// - painted == target → use all painted mines
        /// - painted &gt; target → randomly keep 'target' mines from the painted ones
        /// - painted &lt; target → keep all painted mines + add random mines on empty cells
        /// </summary>
        private static void PlaceMinesFromLevelData(GridModel grid, LevelDataSO level)
        {
            // ---- Step 1: process all manually painted cells ----
            // Mine, Combat, Chest, Dialogue, Shrine all count as mine/encounter cells.
            var paintedMines = new List<(int x, int y)>();
            int entryX = -1, entryY = -1;

            for (int x = 0; x < level.width; x++)
            {
                for (int y = 0; y < level.height; y++)
                {
                    CellTag tag = level.GetCell(x, y);
                    CellData cell = grid.GetCell(x, y);
                    if (cell == null) continue;

                    if (tag.IsMineOrEncounter())
                    {
                        paintedMines.Add((x, y));
                    }
                    else if (tag == CellTag.Inactive)
                    {
                        cell.isActive = false;
                        grid.InactiveCount++;
                    }
                    else if (tag == CellTag.Entry)
                    {
                        entryX = x;
                        entryY = y;
                    }
                }
            }

            // ---- Step 2: reconcile mine count ----
            int target = level.targetMineCount;
            List<(int x, int y)> finalMines;

            if (target <= 0)
            {
                // No target set — use all painted mines as-is (backward compatible)
                finalMines = paintedMines;
            }
            else if (paintedMines.Count == target)
            {
                finalMines = paintedMines;
            }
            else if (paintedMines.Count > target)
            {
                // Too many painted mines — randomly keep 'target' of them
                ShuffleList(paintedMines);
                finalMines = paintedMines.GetRange(0, target);
                Debug.Log($"[Sweeper] Mine reconciliation: {paintedMines.Count} painted > {target} target — kept {target} random mines.");
            }
            else
            {
                // Not enough painted mines — add random ones on empty active cells
                finalMines = new List<(int x, int y)>(paintedMines);
                var mineSet = new HashSet<(int x, int y)>(paintedMines);
                var candidates = new List<(int x, int y)>();

                for (int x = 0; x < grid.Width; x++)
                {
                    for (int y = 0; y < grid.Height; y++)
                    {
                        CellData cell = grid.GetCell(x, y);
                        if (cell == null || !cell.isActive) continue;
                        if (mineSet.Contains((x, y))) continue;
                        // Don't place mines on protected cells (Entry, Safe)
                        CellTag tag = level.GetCell(x, y);
                        if (tag.IsProtectedFromMines()) continue;
                        candidates.Add((x, y));
                    }
                }

                ShuffleList(candidates);
                int toAdd = target - finalMines.Count;
                for (int i = 0; i < toAdd && i < candidates.Count; i++)
                    finalMines.Add(candidates[i]);

                if (toAdd > candidates.Count)
                    Debug.LogWarning($"[Sweeper] Not enough empty cells to reach target {target} mines. Placed {finalMines.Count}.");
                else
                    Debug.Log($"[Sweeper] Mine reconciliation: {paintedMines.Count} painted < {target} target — added {toAdd} random mines.");
            }

            // ---- Step 3: place final mines on grid ----
            foreach (var (x, y) in finalMines)
            {
                grid.GetCell(x, y).hasMine = true;
                grid.MineCount++;
            }
        }

        /// <summary>Fisher-Yates shuffle for a generic list.</summary>
        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
