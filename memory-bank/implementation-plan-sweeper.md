# DarkSweeper Grid — Implementation Plan

*(Unity 6 — Hackathon Scope)*

## Scope

This plan covers **only the base Minesweeper + Fog of War game loop**, as described in `memory-bank/feature-sweeper.md` and `memory-bank/tech-stack-sweeper.md`.

Included:

* 2D grid with configurable dimensions
* Minesweeper logic: mines, adjacency numbers, reveal, flood fill, flags
* Fog of War: per-cell light value, circular light brush with falloff
* HP system: each left click costs 1 PV
* HUD: HP, mines remaining, win/lose status
* Manual mine layout via ScriptableObject (optional, fallback to random)
* Restart button

Not included:

* God system integration
* Narrative elements
* ChatGPT module connection
* Audio
* Animations beyond basic state changes
* Save/load

---

## Step 1 — Create CellData and GridModel

### Goal

Define the **pure C# data structures** that represent the grid. No Unity dependencies.

### Instructions

* Create `CellData` as a class (not struct — it will be stored in a 2D array and mutated in place).
* Fields:
  * `bool hasMine` — default `false`
  * `int adjacentMines` — default `0`
  * `bool isRevealed` — default `false`
  * `bool isFlagged` — default `false`
  * `float light` — default `0.0f`
* Create `GridModel` as a class.
* Fields / accessors:
  * `CellData[,] cells` — private 2D array
  * `int Width` — read-only
  * `int Height` — read-only
  * `int MineCount` — total mines placed
  * `int FlagCount` — current number of flags on the grid
  * `int RevealedCount` — current number of revealed cells
* Constructor: `GridModel(int width, int height)` — allocates the 2D array and initializes every cell to defaults.
* Public method: `CellData GetCell(int x, int y)` — returns the cell at `(x, y)`. Returns `null` or throws if out of bounds.
* Public method: `bool IsInBounds(int x, int y)` — returns `true` if coordinates are within the grid.
* Place both files in `Assets/Scripts/Sweeper/Data/`.
* These classes must **not** use `using UnityEngine` or inherit from any Unity type.

### Test

* Create a temporary MonoBehaviour test script.
* In `Start()`, instantiate a `GridModel(5, 5)`.
* Log to console: grid dimensions, and confirm all 25 cells have default values (`hasMine=false`, `light=0.0`, etc.).
* Manually set `cells[2,3].hasMine = true` and read it back via `GetCell(2, 3)` — confirm it returns `true`.
* Confirm the script compiles and runs without errors.
* Delete the test script after validation.

---

## Step 2 — Create SweeperConfig ScriptableObject

### Goal

Create a **single configuration asset** that exposes all tuning parameters in the Unity Inspector.

### Instructions

* Create `SweeperConfig.cs` in `Assets/Scripts/Sweeper/Flow/`.
* It must be a `ScriptableObject` with `[CreateAssetMenu(menuName = "DarkSweeper/Sweeper Config")]`.
* Serialized fields:
  * `int gridWidth` — default `10`
  * `int gridHeight` — default `10`
  * `int mineCount` — default `15`
  * `int hpStart` — default `100`
  * `int radiusFull` — default `1`
  * `int radiusFalloff` — default `3`
* Create an asset instance `SweeperConfig_Default.asset` in `Assets/Data/` via the Unity menu.
* Fill in the default values listed above.

### Test

* Select the asset in the Inspector — all 6 fields are visible and editable.
* Change `gridWidth` to 8, exit and re-enter Play Mode — value persists.
* Duplicate the asset, rename to `SweeperConfig_Hard.asset`, set `mineCount` to 25 — confirm both assets exist independently.

---

## Step 3 — Implement Random Mine Placement and Adjacency Computation

### Goal

Write the core Minesweeper setup logic: **place mines randomly** and **compute adjacency numbers**.

### Instructions

* Create `MinesweeperLogic.cs` as a **static class** in `Assets/Scripts/Sweeper/Logic/`.
* No Unity dependencies (`using UnityEngine` is allowed only for `Debug.Log`).
* Implement `PlaceMinesRandom(GridModel grid, int count, int safeX, int safeY)`:
  * Randomly select `count` cells to place mines.
  * Exclude the cell at `(safeX, safeY)` and its 8 neighbors (first-click safety zone — up to 9 excluded cells).
  * Set `hasMine = true` on selected cells.
  * Update `grid.MineCount`.
  * If `count` exceeds available cells (total cells minus safe zone), clamp to the maximum possible — do not crash.
* Implement `ComputeAdjacency(GridModel grid)`:
  * For every cell in the grid, count how many of its 8 neighbors have `hasMine == true`.
  * Store the result in `cell.adjacentMines`.
* Both methods must handle edge and corner cells correctly (neighbors that fall outside the grid are simply skipped).

### Test

* Create a temporary test script.
* Instantiate a `GridModel(8, 8)`.
* Call `PlaceMinesRandom(grid, 10, 4, 4)`.
* Log total mines placed — should be exactly 10.
* Confirm cell `(4, 4)` and all its neighbors have `hasMine == false`.
* Call `ComputeAdjacency(grid)`.
* Log the grid as ASCII (e.g. `*` for mine, digit for adjacency count, `.` for 0) — visually verify the adjacency numbers match the mine positions.
* Run 3 times — confirm mine positions differ each time (randomness works).
* Delete the test script after validation.

---

## Step 4 — Implement Reveal and Flood Fill

### Goal

Implement the **cell reveal mechanic** and the **zero-propagation flood fill**.

### Instructions

* In `MinesweeperLogic`, define an enum `RevealResult { Safe, Mine, AlreadyRevealed, Flagged }`.
* Implement `Reveal(GridModel grid, int x, int y)`:
  * If the cell is already revealed → return `AlreadyRevealed`.
  * If the cell is flagged → return `Flagged` (do not reveal flagged cells).
  * If `hasMine == true` → return `Mine`.
  * Otherwise → set `isRevealed = true`, increment `grid.RevealedCount`, return `Safe`.
* Implement `FloodFill(GridModel grid, int x, int y)`:
  * Uses **iterative BFS** (not recursion): maintain a `Queue<(int, int)>`.
  * Starting from `(x, y)`, enqueue the cell.
  * While the queue is not empty:
    * Dequeue a cell.
    * If it is out of bounds, already revealed, flagged, or has a mine → skip.
    * Reveal it (`isRevealed = true`, increment `RevealedCount`).
    * If `adjacentMines == 0` → enqueue all 8 neighbors.
  * Result: all connected zero-cells and their numbered border (cells with `adjacentMines > 0`) are revealed.
* The flood fill must **not** reveal mines, even if adjacent to a zero-cell.

### Test

* Create a `GridModel(5, 5)` with a known mine layout (manually set 3 mines at specific positions).
* Call `ComputeAdjacency`.
* Call `Reveal` on a mine cell → returns `Mine`.
* Call `Reveal` on a non-mine cell with `adjacentMines > 0` → returns `Safe`, only that cell is revealed.
* Call `Reveal` on a cell with `adjacentMines == 0` → returns `Safe`. Then call `FloodFill` from that cell.
* Log the grid state — confirm all connected zero-cells and their numbered border are revealed, but mines are untouched.
* Call `Reveal` on an already revealed cell → returns `AlreadyRevealed`.
* Set a cell as flagged, call `Reveal` → returns `Flagged`.

---

## Step 5 — Implement Flag Toggle and Victory Check

### Goal

Complete the Minesweeper logic with **flag placement** and **win condition detection**.

### Instructions

* In `MinesweeperLogic`, implement `ToggleFlag(GridModel grid, int x, int y)`:
  * If the cell is already revealed → do nothing (cannot flag a revealed cell).
  * Otherwise → toggle `isFlagged`. Update `grid.FlagCount` accordingly (+1 or -1).
* Implement `CheckVictory(GridModel grid)` → `bool`:
  * Returns `true` if **every non-mine cell** is revealed.
  * Formula: `grid.RevealedCount == (grid.Width * grid.Height - grid.MineCount)`.
  * Does **not** require flags to be placed on mines — only non-mine cells need to be revealed.

### Test

* Create a `GridModel(3, 3)` with 2 mines at known positions.
* Call `ComputeAdjacency`.
* Toggle flag on an unrevealed cell → `isFlagged == true`, `FlagCount == 1`.
* Toggle again → `isFlagged == false`, `FlagCount == 0`.
* Try to flag a revealed cell → nothing happens.
* Reveal all 7 non-mine cells one by one (use `Reveal` + `FloodFill` as appropriate).
* After each reveal, call `CheckVictory` — should return `false` until the last one.
* After all 7 are revealed → `CheckVictory` returns `true`.

---

## Step 6 — Implement Light Brush

### Goal

Create the **circular light application** with full-radius and linear falloff.

### Instructions

* Create `LightBrush.cs` as a **static class** in `Assets/Scripts/Sweeper/Logic/`.
* Implement `ApplyLight(GridModel grid, int cx, int cy, int radiusFull, int radiusFalloff)`:
  * Iterate over all cells within a bounding box of `(cx - totalRadius, cy - totalRadius)` to `(cx + totalRadius, cy + totalRadius)`, where `totalRadius = radiusFull + radiusFalloff`.
  * Skip cells outside the grid bounds.
  * For each cell, compute Euclidean distance: `dist = sqrt((x - cx)² + (y - cy)²)`.
  * Compute new light value:
    * If `dist <= radiusFull` → `newLight = 1.0`
    * If `dist > radiusFull` and `dist < radiusFull + radiusFalloff` → `newLight = 1.0 - (dist - radiusFull) / radiusFalloff` (linear interpolation)
    * If `dist >= radiusFull + radiusFalloff` → `newLight = 0.0`
  * Apply: `cell.light = max(cell.light, newLight)` — light is persistent and additive (via max).
* The method must also return a list of `(int x, int y)` coordinates of all cells whose light value actually changed (for efficient visual refresh later).

### Test

* Create a `GridModel(10, 10)`, all lights at `0.0`.
* Call `ApplyLight(grid, 5, 5, 1, 3)`.
* Log light values for the entire grid.
* Verify:
  * Cell `(5, 5)` → light = `1.0` (center, within radiusFull).
  * Cell `(6, 5)` → light = `1.0` (distance 1, within radiusFull).
  * Cell `(7, 5)` → light between `0.0` and `1.0` (distance 2, in falloff zone).
  * Cell `(9, 5)` → light = `0.0` (distance 4, outside total radius of 4).
* Call `ApplyLight(grid, 5, 5, 1, 3)` again on the same grid — all values should remain the same (max with existing).
* Call `ApplyLight(grid, 7, 5, 1, 3)` — cell `(7, 5)` should now have a higher light value than before (closer to the new center).
* Confirm no cell ever exceeds `1.0`.

---

## Step 7 — Create MineLayoutSO and Layout Parser

### Goal

Allow a level designer to **hand-place mines** via a ScriptableObject with a visual text grid.

### Instructions

* Create `MineLayoutSO.cs` in `Assets/Scripts/Sweeper/Flow/`.
* It must be a `ScriptableObject` with `[CreateAssetMenu(menuName = "DarkSweeper/Mine Layout")]`.
* Single serialized field:
  * `[TextArea(10, 30)] public string layout` — the visual grid.
* Format rules:
  * Each line = one row of the grid.
  * `.` = empty cell, `X` = mine.
  * Spaces are ignored (optional separators for readability).
  * The parser must strip spaces and trim empty lines.
* Create a static class `LayoutParser` in `Assets/Scripts/Sweeper/Logic/` (or add to `MinesweeperLogic`):
  * `Parse(string layoutText)` → returns a result object containing:
    * `int width` — number of columns (length of the first non-empty row after stripping spaces)
    * `int height` — number of rows
    * `bool[,] mines` — 2D array where `true` = mine
    * `int mineCount` — total mines found
  * Validation:
    * All rows must have the same length after stripping spaces. If not → log a warning, return null.
    * Only `.` and `X` are valid characters. Unknown characters → log a warning, treat as empty.
* In `MinesweeperLogic`, implement `PlaceMinesFromLayout(GridModel grid, bool[,] mineMap)`:
  * Copies the mine positions from the parsed layout into the grid's `CellData` array.
  * Updates `grid.MineCount`.
  * Grid dimensions must match the layout dimensions — if they don't, log an error.
* Create two test assets in `Assets/Data/Levels/`:
  * `Level_Test_5x5.asset` — a 5x5 layout with 4 mines at known positions.
  * `Level_Tutorial.asset` — a 10x10 layout with a simple, fair pattern.

### Test

* Select `Level_Test_5x5.asset` in the Inspector — the TextArea shows the visual grid.
* Create a temporary test script:
  * Read the `layout` string from the SO.
  * Call `LayoutParser.Parse()`.
  * Log: width, height, mine count, and mine positions.
  * Verify they match what was typed in the TextArea.
* Test error handling:
  * Create a layout with inconsistent row lengths → log warning, `Parse` returns null.
  * Create a layout with an unknown character (e.g. `Z`) → log warning, character treated as empty.

---

## Step 8 — Create Scene, Camera, and Grid Renderer

### Goal

Set up the **DarkSweeper scene** with an orthographic camera and a script that **spawns one GameObject per cell** in a grid layout.

### Instructions

* Create a new scene `DarkSweeper.unity` in `Assets/Scenes/`.
* Set up the main camera:
  * Projection: **Orthographic**.
  * Background color: black or very dark grey (#1A1A1A).
  * Clear flags: Solid Color.
  * Position at `(0, 0, -10)` — looking down the Z axis.
* Create a `GridRenderer.cs` MonoBehaviour in `Assets/Scripts/Sweeper/Presentation/`.
* GridRenderer responsibilities:
  * Serialized field: a reference to `SweeperConfig`.
  * Public method: `CreateGrid(GridModel model)`:
    * Instantiate `width * height` GameObjects as children of the GridRenderer's transform.
    * Each cell GO is named `Cell_X_Y`.
    * Position each cell at world coordinates `(x * cellSize, y * cellSize, 0)` where `cellSize` is a constant (e.g. `1.0`).
    * The grid should be centered on the origin: offset = `(-width * cellSize / 2 + cellSize / 2, -height * cellSize / 2 + cellSize / 2)`.
  * Adjust camera `orthographicSize` to fit the grid with a small margin. Formula: `orthographicSize = max(gridHeight, gridWidth * screenHeight / screenWidth) / 2 + margin`.
* For now, each cell GO has a single **SpriteRenderer** with a placeholder white square sprite (1x1 pixel, imported as Sprite). Set the color to dark grey so the grid is visible against the black background.
* Store references to all cell GOs in a `CellView[,]` array (CellView component will be added in Step 9 — for now, just the SpriteRenderer).

### Test

* Enter Play Mode.
* A grid of dark grey squares appears on screen, centered.
* The camera frame fits the entire grid.
* Check the Hierarchy: `GridRenderer` has `width * height` children named `Cell_0_0` through `Cell_9_9` (for a 10x10 grid).
* Change `gridWidth` to 15 in the config asset, re-enter Play Mode → grid is wider, camera adjusts.

---

## Step 9 — Implement CellView (Minesweeper State Rendering)

### Goal

Create the **CellView** component that renders a cell's Minesweeper state (unrevealed, revealed number, flag, mine) using sprites.

### Instructions

* Create `CellView.cs` MonoBehaviour in `Assets/Scripts/Sweeper/Presentation/`.
* Add `CellView` as a component on every cell GO (added by `GridRenderer` during instantiation).
* CellView has:
  * A reference to its **base SpriteRenderer** (the one already on the cell GO from Step 8).
  * Serialized references (set via GridRenderer or a shared sprite atlas reference):
    * Sprite for unrevealed cell
    * Sprites for numbers 1–8 (array of 8 sprites)
    * Sprite for revealed empty cell (0 adjacent mines)
    * Sprite for flag
    * Sprite for mine
* Create placeholder sprites in `Assets/Art/Sweeper/Sprites/`:
  * `cell_unrevealed.png` — dark grey square
  * `cell_revealed_empty.png` — slightly lighter square
  * `cell_number_1.png` through `cell_number_8.png` — squares with centered digits in distinct colors (1=blue, 2=green, 3=red, 4=dark blue, 5=dark red, 6=teal, 7=black, 8=grey)
  * `cell_flag.png` — square with a flag icon or triangle
  * `cell_mine.png` — square with an X or skull shape
  * All sprites should be the same pixel size (e.g. 64x64 or 128x128), imported as Sprite, Pixels Per Unit matching so they fill one grid cell.
* Public method on CellView: `UpdateVisual(CellData data)`:
  * If `!data.isRevealed` and `data.isFlagged` → show flag sprite.
  * If `!data.isRevealed` and `!data.isFlagged` → show unrevealed sprite.
  * If `data.isRevealed` and `data.hasMine` → show mine sprite (for game-over reveal).
  * If `data.isRevealed` and `data.adjacentMines == 0` → show revealed empty sprite.
  * If `data.isRevealed` and `data.adjacentMines > 0` → show the corresponding number sprite.
* GridRenderer gets a public method: `RefreshCell(int x, int y, CellData data)` — calls `CellView.UpdateVisual(data)` on the matching cell.
* GridRenderer gets a public method: `RefreshAllCells(GridModel model)` — updates every cell.

### Test

* In a temporary test script at `Start()`:
  * Create a `GridModel(5, 5)`.
  * Manually set specific cells: one mine, one flag, one revealed with `adjacentMines = 3`, one revealed empty.
  * Call `GridRenderer.CreateGrid(model)` then `RefreshAllCells(model)`.
* Enter Play Mode — visually verify:
  * Most cells show the unrevealed sprite.
  * The flagged cell shows the flag sprite.
  * The revealed cell with 3 adjacent mines shows the number "3".
  * The revealed empty cell shows the lighter empty sprite.
* All sprites are aligned to the grid, no gaps, no overlaps.

---

## Step 10 — Add Fog of War Overlay

### Goal

Add a **second SpriteRenderer per cell** that acts as the Fog of War darkness overlay, driven by the cell's `light` value.

### Instructions

* For each cell GO, GridRenderer now creates a **child GameObject** named `FogOverlay`.
* The child has a `SpriteRenderer` with:
  * A simple **solid black square sprite** (`fog_overlay.png` — 64x64 solid black, imported as Sprite).
  * Sorting order higher than the base SpriteRenderer (so it renders on top).
  * Initial color: `new Color(0, 0, 0, 1)` — fully opaque black.
* CellView gets a reference to this overlay SpriteRenderer.
* Update `CellView.UpdateVisual(CellData data)` to also set the overlay:
  * Overlay alpha = `1.0 - data.light`. At `light = 0.0` → fully black. At `light = 1.0` → fully transparent.
  * Apply visibility thresholds for the **base sprite** content:
    * If `data.light < 0.15` → base sprite is hidden (set base SpriteRenderer `enabled = false` or set its alpha to 0). The cell appears as near-total darkness.
    * If `data.light >= 0.15` and `data.light < 0.6` → base sprite is visible but dimmed. Set base SpriteRenderer color alpha to a reduced value (e.g. `data.light`). Numbers and flags are hard to read (penumbra effect).
    * If `data.light >= 0.6` → base sprite is fully visible (alpha = 1.0). Numbers and flags are perfectly readable.
  * The fog overlay alpha is always `1.0 - data.light` regardless of thresholds — it provides the smooth gradient of darkness.

### Test

* Create a `GridModel(8, 8)`.
* Manually set light values:
  * `cells[4, 4].light = 1.0` (full light)
  * `cells[3, 4].light = 0.7` (readable)
  * `cells[2, 4].light = 0.4` (penumbra)
  * `cells[1, 4].light = 0.1` (near-dark)
  * All other cells remain at `0.0` (total darkness)
* Set a few cells as revealed with numbers, then call `RefreshAllCells`.
* Enter Play Mode — visually verify:
  * Center cell is bright, number clearly visible.
  * Adjacent cell is slightly dimmed, still readable.
  * Penumbra cell has visible fog, content hard to read.
  * Near-dark cell is almost entirely black.
  * All other cells are fully black.
* The gradient from light to dark is smooth and visually convincing.

---

## Step 11 — Implement Input Handling and Hover

### Goal

Convert **mouse position to grid coordinates** and detect left/right clicks. Show a **hover highlight** on the cell under the cursor.

### Instructions

* Create `InputHandler.cs` MonoBehaviour in `Assets/Scripts/Sweeper/Presentation/`.
* InputHandler is responsible for:
  * Reading mouse position every frame.
  * Converting screen position to grid coordinates:
    * `Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition)`
    * Convert `worldPos` to grid coords: `int gx = Floor((worldPos.x - gridOffset.x) / cellSize)`, same for `gy`.
    * Clamp to grid bounds. If outside the grid → no valid cell (return `-1, -1` or a nullable).
  * Detecting left click: `Input.GetMouseButtonDown(0)`.
  * Detecting right click: `Input.GetMouseButtonDown(1)`.
  * Exposing events or callbacks:
    * `System.Action<int, int> OnLeftClick`
    * `System.Action<int, int> OnRightClick`
    * `System.Action<int, int> OnHoverChanged` — fires when the hovered cell changes
  * The InputHandler needs to know the grid's world-space origin and cell size — receive these from GridRenderer.
* Hover highlight:
  * CellView gets a method `SetHovered(bool hovered)`:
    * When hovered → apply a subtle visual change: a slight white tint on the base sprite, or enable a thin outline sprite. This must be visible even in darkness (applied **on top of** the fog overlay, or by slightly brightening the fog overlay).
    * When not hovered → revert to normal.
  * InputHandler tracks the previously hovered cell and calls `SetHovered(false)` on the old cell and `SetHovered(true)` on the new cell when the hover changes.

### Test

* Enter Play Mode with the grid visible.
* Move the mouse across the grid:
  * The currently hovered cell shows a visible highlight.
  * Moving to another cell: old highlight disappears, new one appears.
  * Moving outside the grid: no highlight active.
* Log the grid coordinates of the hovered cell to console — confirm they are correct (match the visual position).
* Click left on a cell → console logs `"Left click: (3, 5)"` (or equivalent).
* Click right on a cell → console logs `"Right click: (3, 5)"`.
* Right-click should not open the browser/system context menu in a build (ensure `Event.current` is consumed if needed, or handle in WebGL build config later).

---

## Step 12 — Build the Game Controller (Startup and State Machine)

### Goal

Create the **SweeperGameController** that orchestrates the full game: initializes the grid, manages game state, and routes input to logic and rendering.

### Instructions

* Create `SweeperGameController.cs` MonoBehaviour in `Assets/Scripts/Sweeper/Flow/`.
* Serialized fields:
  * `SweeperConfig config`
  * `MineLayoutSO mineLayout` — optional (null = random mode)
  * `GridRenderer gridRenderer`
  * `InputHandler inputHandler`
* Define a state enum: `GameState { WaitingForFirstClick, Playing, Won, Lost }`
* Private state:
  * `GridModel grid`
  * `GameState currentState`
  * `int currentHP`
* On `Start()`:
  * Determine grid dimensions:
    * If `mineLayout` is assigned → parse layout, use its width/height.
    * If `mineLayout` is null → use `config.gridWidth` and `config.gridHeight`.
  * Create a new `GridModel(width, height)`.
  * If layout mode:
    * Call `PlaceMinesFromLayout` with the parsed mine map.
    * Call `ComputeAdjacency`.
    * Set state to `Playing`.
  * If random mode:
    * Set state to `WaitingForFirstClick` (mines placed on first click).
  * Set `currentHP = config.hpStart`.
  * Call `gridRenderer.CreateGrid(grid)`.
  * Call `gridRenderer.RefreshAllCells(grid)`.
* Subscribe to InputHandler events:
  * `OnLeftClick` → route to the left-click pipeline (Step 13).
  * `OnRightClick` → route to the right-click pipeline (Step 13).
* Expose C# events for external systems:
  * `event System.Action<int> OnHPChanged`
  * `event System.Action<bool> OnGameOver` — `true` = won, `false` = lost

### Test

* **Random mode**: Leave `mineLayout` field empty in Inspector. Enter Play Mode.
  * Grid renders, all cells are dark (no light).
  * Console logs: `"Grid created: 10x10, mode: Random, HP: 100, state: WaitingForFirstClick"`.
  * No mines are placed yet (verify by logging mine count = 0).
* **Layout mode**: Assign `Level_Test_5x5.asset` to the `mineLayout` field. Enter Play Mode.
  * Grid renders at 5x5 (overrides config dimensions).
  * Console logs: `"Grid created: 5x5, mode: Layout (4 mines), HP: 100, state: Playing"`.
  * Mines are already placed (verify by logging mine count = 4).

---

## Step 13 — Implement Left Click and Right Click Pipelines

### Goal

Wire the **full click behavior**: left click spends HP, applies light, reveals cells, triggers flood fill, and checks win/lose. Right click toggles flags.

### Instructions

#### Left Click Pipeline

When `OnLeftClick(x, y)` fires:

1. If `currentState == Won` or `currentState == Lost` → ignore.
2. If `currentState == WaitingForFirstClick`:
   * Call `PlaceMinesRandom(grid, config.mineCount, x, y)`.
   * Call `ComputeAdjacency(grid)`.
   * Set state to `Playing`.
3. If `currentHP <= 0` → ignore (no HP left).
4. Decrement `currentHP` by 1. Fire `OnHPChanged(currentHP)`.
5. Call `LightBrush.ApplyLight(grid, x, y, config.radiusFull, config.radiusFalloff)` — get back the list of cells whose light changed.
6. Refresh visuals for all cells whose light changed (call `gridRenderer.RefreshCell` for each).
7. If the cell at `(x, y)` is not flagged:
   * Call `MinesweeperLogic.Reveal(grid, x, y)`.
   * If result is `Mine`:
     * Set state to `Lost`.
     * Reveal all mines on the grid (set `isRevealed = true` for all mine cells, set their light to `1.0`).
     * Call `gridRenderer.RefreshAllCells(grid)`.
     * Fire `OnGameOver(false)`.
   * If result is `Safe`:
     * If the revealed cell has `adjacentMines == 0` → call `FloodFill(grid, x, y)`.
     * Refresh all newly revealed cells (call `gridRenderer.RefreshCell` for each revealed cell).
     * Call `CheckVictory(grid)`:
       * If `true` → set state to `Won`, fire `OnGameOver(true)`.
   * If result is `AlreadyRevealed` or `Flagged` → no Minesweeper action (but HP was still spent and light was still applied — this is by design: "each left click costs 1 HP").

#### Right Click Pipeline

When `OnRightClick(x, y)` fires:

1. If `currentState != Playing` and `currentState != WaitingForFirstClick` → ignore.
2. Call `MinesweeperLogic.ToggleFlag(grid, x, y)`.
3. Refresh the cell visual: `gridRenderer.RefreshCell(x, y, grid.GetCell(x, y))`.
4. No HP cost, no light change.

### Test

* **Left click — first click safety (random mode)**:
  * Enter Play Mode (random mode, 10x10, 15 mines).
  * Click on cell `(5, 5)`.
  * Console log: HP decreased from 100 to 99, mines placed (15), adjacency computed.
  * The clicked cell and its light radius are illuminated.
  * The clicked cell is revealed (if not a mine — guaranteed by first-click safety).
  * State is now `Playing`.

* **Left click — reveal a number**:
  * Click on a cell that has `adjacentMines > 0`.
  * Cell shows the number. Light applied around it. HP decremented.

* **Left click — trigger flood fill**:
  * Click on a cell that has `adjacentMines == 0`.
  * Multiple cells are revealed in a flood fill pattern. All connected zeros and their numbered border are now visible.
  * Note: flood-filled cells become revealed but their light values may still be `0.0` — they are revealed in the Minesweeper sense but may be in darkness. This is correct behavior (see feature spec: "Voir n'est pas résoudre").

* **Left click — hit a mine (defeat)**:
  * Click on a mine.
  * All mines are revealed and illuminated.
  * Game state = `Lost`. Further clicks are ignored.

* **Right click — flag**:
  * Right-click on an unrevealed cell → flag appears. No HP change, no light change.
  * Right-click again → flag removed.
  * Right-click on a revealed cell → nothing happens.

* **Left click on a flagged cell**:
  * Left click on a flagged cell → HP is spent, light is applied, but the cell is NOT revealed (flag protects it). This is correct.

* **Victory**:
  * Use a `Level_Test_5x5.asset` with 1 mine.
  * Reveal all 24 non-mine cells → `CheckVictory` returns `true`, state = `Won`.

---

## Step 14 — Implement the HUD

### Goal

Display **HP, remaining mines, and game status** via a UGUI overlay.

### Instructions

* Create `SweeperHUD.cs` MonoBehaviour in `Assets/Scripts/Sweeper/Presentation/`.
* In the DarkSweeper scene, create a Canvas:
  * Render Mode: **Screen Space - Overlay**.
  * Canvas Scaler: Scale With Screen Size, reference resolution 1920x1080.
* Add the following UI elements (using TextMeshPro):
  * **HP Text** — top-left corner. Format: `"PV: 87 / 100"`.
  * **Mines Text** — top-right corner. Format: `"Mines: 12"`. Calculated as `totalMines - flagCount`.
  * **Status Text** — center of screen, large font, initially hidden. Shows `"VICTORY"` or `"DEFEAT"` when the game ends.
  * **Restart Button** — below the status text (or always visible in a corner). Label: `"Restart"`.
* SweeperHUD has serialized references to these 4 UI elements.
* Public methods:
  * `UpdateHP(int current, int max)` — updates HP text.
  * `UpdateMines(int remaining)` — updates mines remaining text.
  * `ShowVictory()` — shows the status text with "VICTORY".
  * `ShowDefeat()` — shows the status text with "DEFEAT".
  * `ResetHUD(int startHP, int totalMines)` — hides status text, resets HP and mines display.
* SweeperGameController subscribes SweeperHUD to its events:
  * `OnHPChanged` → `UpdateHP`
  * `OnGameOver(true)` → `ShowVictory()`
  * `OnGameOver(false)` → `ShowDefeat()`
  * After each flag toggle → `UpdateMines(grid.MineCount - grid.FlagCount)`
* Add a serialized reference from SweeperGameController to SweeperHUD.
* On Start, call `ResetHUD(config.hpStart, mineCount)`.

### Test

* Enter Play Mode.
* HP shows `"PV: 100 / 100"` at start.
* Mines shows `"Mines: 15"` (or the layout's mine count).
* Click a cell → HP updates to `"PV: 99 / 100"`.
* Right-click to place a flag → mines display decreases by 1.
* Remove the flag → mines display increases by 1.
* Trigger a defeat → `"DEFEAT"` appears centered on screen.
* Restart → play and trigger a victory → `"VICTORY"` appears.

---

## Step 15 — Implement Restart

### Goal

Allow the player to **restart the game** cleanly without leaving the scene.

### Instructions

* SweeperGameController gets a public method: `RestartGame()`.
* `RestartGame()`:
  * Destroy all existing cell GameObjects (or reset them).
  * Re-run the full startup sequence from Step 12:
    * Create a new `GridModel`.
    * Place mines (or wait for first click).
    * Reset `currentHP` to `config.hpStart`.
    * Set state to `WaitingForFirstClick` (random) or `Playing` (layout).
    * Call `gridRenderer.CreateGrid(grid)` and `RefreshAllCells(grid)`.
  * Call `SweeperHUD.ResetHUD()`.
* Wire the **Restart button** in SweeperHUD to call `SweeperGameController.RestartGame()`.
* Restart must work from any state: `Playing`, `Won`, or `Lost`.

### Test

* Play a game until defeat → click Restart.
  * Grid regenerates, HP is back to full, status text disappears, all cells are dark again.
  * A new game can be played immediately.
* Play a game until victory → click Restart.
  * Same: clean reset, new game starts.
* During a game (state = Playing) → click Restart.
  * Game resets cleanly, no errors.
* In random mode, after restart, click the same cell as before → different mine layout (randomness works).
* In layout mode, after restart → same mine positions (deterministic).

---

## Step 16 — End-to-End Validation

### Goal

Validate the complete system as a playable game loop.

### Instructions

Run through the following scenarios manually. All must pass.

### Scenario A — Random Mode, Full Happy Path

1. Open DarkSweeper scene with no layout assigned, config 10x10, 15 mines, 100 HP.
2. Grid appears: all cells dark. HUD shows PV: 100/100, Mines: 15.
3. Left click near center. First click is safe (no mine). Light appears around the clicked cell. HP = 99.
4. Continue clicking to reveal cells. Observe:
   * Light accumulates correctly (overlapping brushes produce brighter areas).
   * Numbers are visible in illuminated areas.
   * Numbers are unreadable in dark areas (fog overlay).
   * Flood fill works when clicking a zero-cell.
5. Place a few flags via right-click. Mines counter decreases.
6. Remove a flag. Counter increases.
7. Reveal all non-mine cells → "VICTORY" appears. Clicks are ignored.
8. Click Restart → new game, fresh grid.

### Scenario B — Layout Mode

1. Assign `Level_Test_5x5.asset` to the controller.
2. Enter Play Mode → 5x5 grid, mines at the designed positions.
3. Play through: verify mine positions match the layout exactly.
4. Restart → same mine positions.

### Scenario C — Defeat

1. Random mode. Play until you hit a mine.
2. "DEFEAT" appears. All mines are revealed and lit.
3. Further clicks are ignored (both left and right).
4. Restart works cleanly.

### Scenario D — HP Exhaustion

1. Set `hpStart` to 10 in the config.
2. Click 10 times → HP reaches 0.
3. Left clicks no longer do anything (no light, no reveal, no HP change).
4. Right-click still works (flag toggle).
5. Restart resets HP.

### Scenario E — Edge Cases

1. Click the same already-revealed cell multiple times → HP decreases each time, light re-applies (using max, so no visual change), but the cell is not re-revealed.
2. Left-click a flagged cell → HP decreases, light applies, but the cell is NOT revealed (flag protects it).
3. Set config to 3x3 with 8 mines → only 1 safe cell. First click should reveal it immediately, triggering victory.
4. Set config to 1x1 with 0 mines → first click reveals the only cell, victory.

### Final Checklist

* No errors or warnings in console during normal gameplay.
* All sprites are aligned and correctly sized.
* Hover highlight is visible on all cells, including dark ones.
* HP never goes negative.
* Light values never exceed 1.0.
* Fog overlay looks smooth and convincing.
* Flood fill does not cause lag, even on a 20x20 grid.
* Game can be restarted indefinitely without memory leaks (check that old cell GOs are destroyed or reused).

---

## Design Intent Reminder

> The player does not play Minesweeper.
> The player pays to see — and then decides what to do with what they've learned.
> The darkness is not decoration. It is the cost of knowledge.
