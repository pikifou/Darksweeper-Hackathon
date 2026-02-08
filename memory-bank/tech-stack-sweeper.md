# Tech Stack & Architecture — DarkSweeper Grid (Minesweeper + Fog of War)

*(Unity 6 — Hackathon Scope)*

---

## 1. Technical Objective

The system must:

1. Generate a **2D Minesweeper grid** with configurable dimensions and mine count
2. Overlay a **Fog of War** system driven by per-cell light values (`0.0`–`1.0`)
3. Handle **left click** (spend HP → illuminate → reveal) and **right click** (toggle flag)
4. Apply a **circular light brush** with full-radius and linear falloff on each left click
5. Implement **flood fill** (classic Minesweeper zero-propagation)
6. Display a **HUD** with HP, remaining mines, and win/lose status
7. Expose **configurable parameters** in the Unity Inspector for rapid iteration

Priority: **simplicity, performance, readability, zero magic** — consistent with the rest of the project.

---

## 2. Architecture (3 Layers)

```
[Data / Logic] → [Flow] → [Presentation]
```

Same layering philosophy as the Questionnaire module and the ChatGPT module. Pure C# for logic, MonoBehaviour for orchestration, Unity components for rendering.

---

### 2.1 Data & Logic Layer (pure C#, no Unity dependencies)

This layer owns all game rules. It can be tested independently of Unity.

#### CellData (struct or class)

Per-cell state:

| Field           | Type    | Default | Description                                   |
| --------------- | ------- | ------- | --------------------------------------------- |
| `hasMine`       | bool    | false   | True if the cell contains a mine              |
| `adjacentMines` | int     | 0       | Count of mines in the 8 neighbors (0–8)       |
| `isRevealed`    | bool    | false   | True if the cell has been revealed (Minesweeper sense) |
| `isFlagged`     | bool    | false   | True if the player placed a flag              |
| `light`         | float   | 0.0     | Illumination level [0.0, 1.0]                 |

#### GridModel (class)

* Holds a 2D array of `CellData` (`CellData[width, height]`)
* Provides read-only accessors: `GetCell(x, y)`, `Width`, `Height`
* Stores game-wide state: `mineCount`, `flagCount`, `revealedCount`

#### MinesweeperLogic (static class)

Pure functions, no state:

* `PlaceMinesFromLayout(GridModel grid, MineLayoutSO layout)` — places mines at positions defined by the level designer in a ScriptableObject (see §2.2 MineLayoutSO)
* `PlaceMinesRandom(GridModel grid, int count, int safeX, int safeY)` — fallback: places mines randomly, excluding the first click and its neighbors
* `ComputeAdjacency(GridModel grid)` — fills `adjacentMines` for every cell
* `Reveal(GridModel grid, int x, int y)` → returns `RevealResult` (enum: `Safe`, `Mine`, `AlreadyRevealed`)
* `FloodFill(GridModel grid, int x, int y)` → reveals all connected zero-cells and their numbered border (iterative BFS, not recursive)
* `ToggleFlag(GridModel grid, int x, int y)` → toggles `isFlagged`
* `CheckVictory(GridModel grid)` → bool (all non-mine cells revealed)

#### LightBrush (static class)

* `ApplyLight(GridModel grid, int cx, int cy, int radiusFull, int radiusFalloff)` — applies circular light centered on `(cx, cy)`:
  * Within `radiusFull`: light = 1.0
  * Between `radiusFull` and `radiusFull + radiusFalloff`: linear interpolation from 1.0 to 0.0
  * Uses `max(existing, new)` to avoid over-illumination
  * Distance calculated as Euclidean (`sqrt(dx² + dy²)`)

#### Why pure C# for logic

* Testable without Play Mode
* No coupling to rendering
* Easy to debug (log grid state as text)
* Consistent with Domain layer pattern used in ChatGPT module (`GameStateModel`)

---

### 2.2 Flow Layer (game controller + configuration)

#### SweeperGameController (MonoBehaviour)

Single controller that owns the game loop:

* **On Start**: creates `GridModel`. Checks if a `MineLayoutSO` is assigned:
  * **If layout assigned** → places mines from the layout immediately, computes adjacency. Game starts in `Playing` state (no first-click safety needed — the level is designed).
  * **If no layout** → game starts in `WaitingForFirstClick` state. Mines placed on first click (random, with first-click safety).
* **On left click**:
  1. If `WaitingForFirstClick` → place mines randomly (excluding clicked cell), compute adjacency, switch to `Playing`
  2. Check HP > 0 — if not, ignore
  3. Decrement HP by 1
  4. Apply light brush via `LightBrush.ApplyLight()`
  5. If cell is not flagged: call `MinesweeperLogic.Reveal()`
     * If `Mine` → trigger defeat
     * If `Safe` and `adjacentMines == 0` → call `FloodFill()`
  6. Check victory condition
  7. Notify presentation layer to refresh
* **On right click**:
  * If cell is not revealed: `ToggleFlag()`
  * No HP cost, no light effect
  * Notify presentation to refresh
* **Game state enum**: `WaitingForFirstClick → Playing → Won → Lost`
* **Fires events**: `OnCellUpdated(int x, int y)`, `OnGameOver(bool won)`, `OnHPChanged(int current)`

#### SweeperConfig (ScriptableObject)

Exposes tuning parameters in the Inspector:

| Field           | Type  | Default | Description                          |
| --------------- | ----- | ------- | ------------------------------------ |
| `gridWidth`     | int   | 10      | Number of columns                    |
| `gridHeight`    | int   | 10      | Number of rows                       |
| `mineCount`     | int   | 15      | Total mines (used only in random mode) |
| `hpStart`       | int   | 100     | Starting HP                          |
| `radiusFull`    | int   | 1       | Full-brightness radius (cells)       |
| `radiusFalloff` | int   | 3       | Falloff distance beyond full radius  |

Using a ScriptableObject (not a MonoBehaviour with serialized fields) so the config can be shared, duplicated for presets, and swapped at runtime.

#### MineLayoutSO (ScriptableObject) — Manual Level Design

**Purpose**: Allow a level designer to hand-place mines visually, directly in the Unity Inspector.

**Format**: A multiline `[TextArea]` string where each character represents a cell and each line represents a row. The designer "draws" the grid:

```
. . . . X . . . . .
. . X . . . . . X .
. . . . . . X . . .
X . . . X . . . . .
. . . . . . . . X .
. . X . . . . . . .
. . . . . X . . . .
. . . . . . . . . .
X . . . X . . . . X
. . . . . . X . . .
```

**Legend**:

| Character | Meaning                |
| --------- | ---------------------- |
| `.`       | Empty cell             |
| `X`       | Mine                   |
| *(space)* | Separator (ignored)    |

Spaces between characters are optional — they exist purely for readability in the Inspector. The parser strips them.

**ScriptableObject fields**:

| Field      | Type              | Description                                         |
| ---------- | ----------------- | --------------------------------------------------- |
| `layout`   | string [TextArea] | The visual grid (see format above)                  |

**Grid dimensions are implicit** — derived from the text: number of rows = number of lines, number of columns = number of non-space characters on the first line. No need to duplicate `width`/`height` from `SweeperConfig`.

**Mine count is implicit** — counted from the number of `X` characters. The `mineCount` field in `SweeperConfig` is ignored when a layout is assigned.

**Parser** (in `MinesweeperLogic` or a dedicated `LayoutParser` static class):
* Strip spaces from each line
* Validate: all rows same length, only `.` and `X` characters
* Return a `bool[width, height]` mine map
* Log warnings on parse errors (malformed rows, unknown characters)

**Why this format**:

* **Instantly readable** — the level designer sees the grid at a glance in the Inspector
* **Fast to author** — copy-paste, edit characters, done
* **Diffable in Git** — a text change is easy to review
* **Extensible** — new characters can be added later (e.g. `T` for trap, `H` for health pickup) without changing the architecture
* **No custom editor needed** — `[TextArea(10, 30)]` is sufficient for a hackathon

**Wiring to the controller**:

The `SweeperGameController` has an **optional** serialized field:

```csharp
[SerializeField] private MineLayoutSO mineLayout; // null = random mode
```

* If `mineLayout` is assigned → use it. Grid dimensions come from the layout. `SweeperConfig.gridWidth/gridHeight/mineCount` are ignored.
* If `mineLayout` is null → use `SweeperConfig` for dimensions and random mine placement.

This makes it trivial to switch between designed levels and random play: just drag-and-drop or clear the field in the Inspector.

---

### 2.3 Presentation Layer (rendering + UI)

#### Rendering Approach: SpriteRenderer Grid

**Choice: one GameObject per cell, each with SpriteRenderers.**

Each cell is composed of:

| Child / Component  | Role                                                       |
| ------------------ | ---------------------------------------------------------- |
| Base SpriteRenderer | Shows cell content: unrevealed tile, number (1–8), mine, flag, empty |
| Overlay SpriteRenderer | Black square on top, alpha driven by `(1.0 - light)` — this IS the Fog of War |

The overlay is a simple white or black sprite whose `SpriteRenderer.color` alpha is set to `1.0 - cell.light`. At `light = 0.0`, the overlay is fully opaque (black). At `light = 1.0`, the overlay is fully transparent.

##### Why SpriteRenderer (not UGUI, not Tilemap)

| Option         | Pros                              | Cons                                    |
| -------------- | --------------------------------- | --------------------------------------- |
| **SpriteRenderer** | Full per-cell control, simple color/alpha manipulation, good perf for ≤30×30 grids, easy 2D raycasting | One GO per cell (acceptable for hackathon grid sizes) |
| UGUI Grid      | Familiar from other modules       | Poor perf for game grids, awkward input handling, not designed for this |
| Tilemap        | Very efficient, built-in          | Per-cell dynamic alpha is cumbersome, harder to add hover effects |
| Shader/Mesh    | Best performance                  | Overkill for hackathon, complex setup   |

SpriteRenderer is the **sweet spot** for hackathon scope: simple, controllable, debuggable.

#### GridRenderer (MonoBehaviour)

* Creates the cell GameObjects at startup based on grid dimensions
* Pools them (instantiate once, reuse on restart)
* Updates individual cell visuals when notified by the controller
* Maps light values to overlay alpha
* Maps Minesweeper state to base sprite (number sprites, flag icon, mine icon, blank)

#### CellView (MonoBehaviour, on each cell GO)

* Holds references to its two SpriteRenderers (base + overlay)
* `UpdateVisual(CellData data)` — sets the correct sprite and overlay alpha
* Applies visibility thresholds from the spec:
  * `light < 0.15` → content sprite hidden (overlay nearly opaque)
  * `0.15 ≤ light < 0.6` → content dimmed (overlay semi-transparent, numbers/flags hidden or blurred)
  * `light ≥ 0.6` → content fully visible (overlay transparent enough to read)
* Handles hover highlight: slight tint or outline on mouse enter/exit

#### Input Handling

* **Legacy Input System** (`Input.GetMouseButtonDown(0)`, `Input.GetMouseButtonDown(1)`)
  * Simpler than the new Input System for a hackathon
  * Consistent with the rest of the project (no InputActions asset needed)
* **Mouse-to-grid mapping**: `Camera.main.ScreenToWorldPoint()` → divide by cell size → clamp to grid bounds
  * OR use `Physics2D.Raycast` with 2D colliders on cells (simpler but slightly heavier)
  * **Recommended**: direct math conversion (no colliders needed, no physics overhead)

#### Camera Setup

* **Orthographic 2D camera**
* Camera `orthographicSize` calculated from grid dimensions to fit the entire grid in view
* Centered on the grid

#### SweeperHUD (MonoBehaviour, UGUI Canvas)

* Overlay canvas (Screen Space - Overlay) on top of the game view
* Components:
  * `TextMeshProUGUI` — HP display: "PV: 87/100"
  * `TextMeshProUGUI` — Mines remaining: "Mines: 12"
  * `TextMeshProUGUI` — Status message (Victory / Defeat)
  * `Button` — Restart (resets grid, HP, fog)
* Subscribes to controller events (`OnHPChanged`, `OnGameOver`)

---

## 3. Visual Style

* **Dark background**: deep charcoal or black behind the grid
* **Cell sprites**: simple, flat, high-contrast tiles
  * Unrevealed: dark stone/metal texture or flat dark grey
  * Revealed empty: slightly lighter
  * Numbers: classic Minesweeper color coding (1=blue, 2=green, 3=red, etc.) but with sufficient value contrast for accessibility
  * Flag: distinct icon shape (not color-dependent)
  * Mine: skull or geometric shape
* **Fog overlay**: solid black sprite, alpha-blended
* **Hover**: subtle white or colored outline (always visible, even in low light — as specified)

---

## 4. Asset Organization

```
Assets/
  Scenes/
    DarkSweeper.unity                    ← new scene (separate from Questionnaire and LLM_TechDemo)
  Scripts/
    Sweeper/
      Data/
        CellData.cs                      ← per-cell state (pure C#)
        GridModel.cs                     ← 2D grid container (pure C#)
      Logic/
        MinesweeperLogic.cs              ← mine placement, reveal, flood fill, victory check
        LightBrush.cs                    ← circular light application
      Flow/
        SweeperGameController.cs         ← game loop, input routing, state management
        SweeperConfig.cs                 ← ScriptableObject for tuning parameters
        MineLayoutSO.cs                  ← ScriptableObject for hand-designed levels (TextArea grid)
      Presentation/
        GridRenderer.cs                  ← creates and manages cell GameObjects
        CellView.cs                      ← per-cell visual update
        SweeperHUD.cs                    ← UGUI overlay (HP, mines, status, restart)
        InputHandler.cs                  ← mouse input → grid coordinates
  Art/
    Sweeper/
      Sprites/
        cell_unrevealed.png
        cell_revealed.png
        cell_numbers_1to8.png            ← sprite sheet or individual sprites
        cell_mine.png
        cell_flag.png
        fog_overlay.png                  ← simple white/black square
        cell_hover.png                   ← hover outline
  Data/
    SweeperConfig_Default.asset          ← default game parameters
    Levels/
      Level_Tutorial.asset               ← example hand-designed level (MineLayoutSO)
      Level_Test_5x5.asset               ← small test layout
```

---

## 5. Tech Choices Summary

| Component            | Choice                                                          |
| -------------------- | --------------------------------------------------------------- |
| Engine               | Unity 6                                                         |
| Rendering            | SpriteRenderer (one GO per cell, base + overlay)                |
| Camera               | Orthographic 2D                                                 |
| Fog of War           | Per-cell black overlay sprite, alpha = `1.0 - light`            |
| Grid logic           | Pure C# classes (no Unity dependencies)                         |
| Flood fill           | Iterative BFS (not recursive — avoids stack overflow on large grids) |
| Light calculation    | Euclidean distance, linear falloff, `max()` blending            |
| Input                | Legacy Input System (`Input.GetMouseButtonDown`)                |
| Mouse-to-grid        | `Camera.ScreenToWorldPoint()` → math conversion (no colliders) |
| UI framework         | UGUI (Canvas) + TextMeshPro for HUD                             |
| Configuration        | ScriptableObject (`SweeperConfig`)                              |
| Level layout         | ScriptableObject (`MineLayoutSO`) — optional, TextArea grid     |
| Mine placement       | Manual layout (if SO assigned) OR random (if null)              |
| Scene                | `DarkSweeper.unity` (separate from other modules)               |
| Inter-system events  | C# events (`System.Action<T>`)                                  |
| JSON (if needed)     | Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`)              |
| Testing              | Manual only                                                     |

---

## 6. Key Technical Decisions & Rationale

### SpriteRenderer over Tilemap

Tilemap is efficient for static grids but makes per-cell dynamic effects (continuous alpha values for fog) awkward. SpriteRenderer gives direct per-cell control via `SpriteRenderer.color`, which is exactly what the fog system needs. For a hackathon grid (≤ 30×30 = 900 cells × 2 renderers = 1800 SpriteRenderers), performance is not a concern.

### Pure C# logic layer

The Minesweeper logic (mine placement, adjacency, flood fill, victory check) and the light brush calculation have **zero reason** to depend on Unity. Keeping them as pure C# static classes means:
* They can be unit-tested without Play Mode
* They're trivial to debug (log the grid as ASCII)
* They won't break if the rendering approach changes

### Manual layout vs random: opt-in design

The `MineLayoutSO` is an **optional** ScriptableObject. The controller checks at startup:
* **Layout assigned** → designed level. Dimensions and mines come from the text grid. The designer has full control. No first-click safety (the level is intentional).
* **No layout** → random mode. Dimensions and mine count come from `SweeperConfig`. First-click safety applies.

This means random play works out of the box with zero setup, while level designers can create `.asset` files for specific puzzles by simply typing a grid of `.` and `X` in the Inspector. The TextArea format was chosen over a `List<Vector2Int>` because:
* A coordinate list is unreadable for grids larger than 5x5
* The text grid shows the spatial relationship at a glance
* It's trivially copy-pasteable and diffable in Git
* The format is extensible: adding new characters later (e.g. `T` for trap) requires only a parser change, not a data structure change

### First-click safety (random mode only)

In random mode, mines are placed **after** the first left click, excluding the clicked cell and its 8 neighbors. This is standard Minesweeper behavior and avoids instant game-overs. In layout mode, the level designer is responsible for fair placement.

### Iterative BFS for flood fill

Recursive flood fill can overflow the stack on large empty regions. An iterative BFS using a `Queue<(int, int)>` is trivially simple and safe for any grid size.

### ScriptableObject for config (not JSON)

Unlike the Questionnaire module (which uses JSON for content data that benefits from text editing), the Sweeper config is purely numeric tuning parameters. A ScriptableObject is better here:
* Editable directly in the Inspector
* Supports drag-and-drop presets (easy, medium, hard)
* No file I/O needed
* Consistent with `LLMConfigSO` / `GameStateSO` patterns

### Legacy Input System

The new Unity Input System adds indirection (InputActions asset, bindings, callbacks) that provides no benefit for a hackathon prototype with 3 inputs (left click, right click, mouse position). Legacy `Input.GetMouseButtonDown()` is simpler and sufficient.

---

## 7. Performance Notes

* **Grid sizes up to 30×30** (1800 SpriteRenderers) are trivially handled by Unity
* **Light brush** recalculates only cells within `radiusFull + radiusFalloff` on each click — not the whole grid
* **Flood fill** uses `visited` marking to prevent reprocessing
* **Visual updates** are batched: after a click, only affected cells are refreshed (not the whole grid)
* If larger grids are needed later, the rendering can be migrated to a GPU-based approach (compute shader writing to a texture) without changing the logic layer

---

## 8. Why This Works for a Hackathon

* **Few classes, clear responsibilities** — same philosophy as the Questionnaire module
* **No heavy systems** — no Tilemap painters, no shader graph, no physics, no new Input System
* **Logic is decoupled from rendering** — can iterate on visuals without breaking rules
* **ScriptableObject presets** — swap difficulty in one click
* **Consistent with the rest of the project** — same patterns (layered architecture, C# events, UGUI HUD, ScriptableObjects for config)
* **Debuggable** — log the grid state as text, inspect SpriteRenderers in the hierarchy
* **Explainable in 5 minutes to a jury or a dev**
