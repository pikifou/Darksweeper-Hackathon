# Tech Stack & Architecture — Mine Events (Right-Click Interactions)

*(Unity 6 — Hackathon Scope)*

---

## 1. Technical Objective

The system must:

1. **Intercept right-click** on a revealed mine cell and open an interaction instead of toggling a flag
2. Support **4 event types**: Combat, Chest, Dialogue, Shrine — each with its own choices and resolution
3. Track **per-mine state** (`Hidden → Revealed → Resolved`) independently from the Minesweeper cell state
4. Apply **gameplay effects** on resolution: HP delta, rewards (HP_GAIN, VISION_GAIN, BUFF)
5. Display a **modal interaction panel** that pauses grid input while the player makes a choice
6. **Log every event** into a RunLog for narrative profiling (god alignment)
7. **Distribute event types** across mine cells at level generation time (fixed distribution or weighted random)

Priority: **same as the rest of DarkSweeper — simplicity, performance, readability, zero magic.**

---

## 2. Architecture (same 3 Layers)

```
[Data / Logic]  →  [Flow]  →  [Presentation]
   Pure C#         MonoBehaviour    UI + VFX
```

The mine event system is a **parallel module** that plugs into the existing Sweeper pipeline. It does NOT replace or refactor the Sweeper code — it **extends** it via events and a controller that sits beside `SweeperGameController`.

---

### 2.1 Data Layer — Pure C# (`Assets/Scripts/Mines/Data/`)

#### MineEventType (enum)

```csharp
public enum MineEventType
{
    Combat,
    Chest,
    Dialogue,
    Shrine
}
```

#### MineState (enum)

Per-mine lifecycle:

```csharp
public enum MineState
{
    Hidden,     // cell not yet revealed by the Sweeper
    Revealed,   // cell revealed, event icon visible, awaiting interaction
    Resolved    // interaction completed — cannot be replayed
}
```

#### RewardType (enum)

```csharp
public enum RewardType
{
    None,
    HpGain,       // immediate +PV
    VisionGain,   // increase light radius / falloff
    Buff          // reduce damage for next N combats
}
```

#### PlayerChoice (enum)

Unified enum for all possible player actions across event types:

```csharp
public enum PlayerChoice
{
    // Combat
    Engage,

    // Chest
    Open,
    Ignore,

    // Dialogue
    Help,
    Harm,
    // Ignore is shared

    // Shrine
    Sacrifice,
    Refuse
}
```

#### MineEventData (class, pure C#)

Per-mine-cell event payload. Created at level init, one per mine cell:

```csharp
public class MineEventData
{
    // Identity
    public int x, y;                        // grid coordinates
    public MineEventType eventType;
    public MineState state = MineState.Hidden;

    // Config (set at generation)
    public string eventId;                  // unique id for logging
    public CombatParams combatParams;       // null if not Combat
    public ChestParams chestParams;         // null if not Chest
    public DialogueParams dialogueParams;   // null if not Dialogue
    public ShrineParams shrineParams;       // null if not Shrine

    // Resolution (set after interaction)
    public PlayerChoice? choiceMade;
    public int hpDelta;
    public RewardType rewardGiven;
}
```

#### Event-specific param structs (pure C#)

```csharp
public class CombatParams
{
    public string monsterName;
    public int damage;              // HP lost by the player
    public bool isElite;
    public RewardType reward;
    public int rewardValue;
}

public class ChestParams
{
    public bool isTrapped;
    public int trapDamage;          // HP lost if trapped
    public RewardType reward;
    public int rewardValue;
}

public class DialogueParams
{
    public string dialogueId;
    public string characterName;
    public string promptText;       // what the character says
    public DialogueChoice[] choices; // 2-3 choices
}

public class DialogueChoice
{
    public PlayerChoice choiceType;  // Help / Harm / Ignore
    public string label;             // button text
    public string resultText;        // text shown after choosing
    public int hpDelta;
    public RewardType reward;
    public int rewardValue;
}

public class ShrineParams
{
    public string shrineId;
    public string description;       // what the shrine offers
    public int sacrificeCost;        // HP lost if accepted
    public RewardType reward;
    public int rewardValue;
}
```

#### RunEvent (class, pure C#)

Flat log entry for the PlayerProfile / narrative system:

```csharp
public class RunEvent
{
    public MineEventType eventType;
    public string eventId;
    public int tileX, tileY;
    public string levelId;
    public int hpBefore;
    public int hpAfter;
    public PlayerChoice choice;
    public RewardType reward;
    public int eventIndex;          // sequential counter in the run
}
```

#### RunLog (class, pure C#)

```csharp
public class RunLog
{
    public List<RunEvent> events = new();
    public int nextIndex = 0;

    public void Record(RunEvent e)
    {
        e.eventIndex = nextIndex++;
        events.Add(e);
    }
}
```

#### Why pure C# for data

Same rationale as the Sweeper module:
* Testable without Play Mode
* Serializable to JSON if needed (narrative system, replay, analytics)
* Zero coupling to Unity lifecycle
* Easy to inspect in debugger

---

### 2.2 Logic Layer — Pure C# (`Assets/Scripts/Mines/Logic/`)

#### MineEventLogic (static class)

Pure functions — no state, no Unity:

| Method | Signature | Description |
|--------|-----------|-------------|
| `AssignEvents` | `(GridModel grid, MineDistribution dist) → MineEventData[]` | Iterates mine cells, assigns an event type per distribution weights, creates params. Returns flat array of all mine events |
| `GetInteraction` | `(MineEventData data) → InteractionDescriptor` | Returns what the UI needs to display: event type, available choices, is_resolved. The UI is dumb — it renders whatever this returns |
| `ResolveCombat` | `(MineEventData data, int currentHp) → ResolutionResult` | Applies combat damage, calculates reward. Returns hp_delta + reward |
| `ResolveChest` | `(MineEventData data, PlayerChoice choice, int currentHp) → ResolutionResult` | Open → apply trap/reward; Ignore → no effect |
| `ResolveDialogue` | `(MineEventData data, PlayerChoice choice, int currentHp) → ResolutionResult` | Apply chosen option's effects |
| `ResolveShrine` | `(MineEventData data, PlayerChoice choice, int currentHp) → ResolutionResult` | Sacrifice → cost + reward; Refuse → nothing |

#### ResolutionResult (struct)

```csharp
public struct ResolutionResult
{
    public int hpDelta;
    public RewardType reward;
    public int rewardValue;
    public string resultText;       // narrative feedback to display
    public bool success;            // false if player died
}
```

#### InteractionDescriptor (struct)

What the UI receives — no game logic leaks into presentation:

```csharp
public struct InteractionDescriptor
{
    public MineEventType eventType;
    public string title;
    public string description;
    public ChoiceOption[] choices;
    public bool isResolved;
}

public struct ChoiceOption
{
    public PlayerChoice choice;
    public string label;
    public string riskHint;         // e.g. "dangerous", "uncertain", "" 
}
```

#### RewardLogic (static class)

```csharp
public static class RewardLogic
{
    public static void ApplyReward(RewardType type, int value, /* refs to SweeperConfig/HP */)
    {
        // HP_GAIN: add HP
        // VISION_GAIN: increase radiusFull or radiusFalloff on SweeperConfig
        // BUFF: set a buff flag on a shared state
    }
}
```

#### Why static classes

* Same pattern as `MinesweeperLogic` — proven in the project
* Zero state → zero bugs from stale state
* Deterministic: same input → same output
* Easy to unit test

---

### 2.3 Flow Layer — Orchestration (`Assets/Scripts/Mines/Flow/`)

#### MineEventController (MonoBehaviour)

The **single orchestrator** for all mine interactions. Sits beside `SweeperGameController` in the scene hierarchy.

**Responsibilities:**

1. At level init: call `MineEventLogic.AssignEvents()` to populate mine event data
2. Listen to right-click from `InputHandler.OnRightClick`
3. Route: is this cell a mine? Is the mine revealed? Is it already resolved?
4. If valid: pause grid input → open interaction UI → wait for player choice → resolve → apply effects → log → resume grid input
5. Fire events for the presentation layer

**Integration with SweeperGameController:**

```
SweeperGameController                    MineEventController
        │                                        │
        │  ── OnMineRevealed(x,y) ──────────►    │  update MineState → Revealed
        │                                        │
        │  ◄── ShouldInterceptRightClick(x,y) ── │  returns true if mine + revealed + !resolved
        │                                        │
        │       (skips ToggleFlag)               │  opens interaction panel
        │                                        │
        │  ◄── OnEventResolved(result) ────────  │  apply HP delta, reward
        │                                        │
```

Two options for integration:

**Option A — Event-based (recommended):** `SweeperGameController` fires `OnRightClickCell(x, y, CellData)`. `MineEventController` subscribes. If it handles the event, it signals back to block the default flag toggle. This keeps `SweeperGameController` untouched except for adding the event + a `handled` pattern.

**Option B — Direct injection:** `MineEventController` gets a reference to `SweeperGameController` and calls a method to check/override. More coupled but simpler for hackathon.

**Recommendation: Option A.** Minimal changes to existing code. The interception pattern:

```csharp
// In SweeperGameController.HandleRightClick:
private void HandleRightClick(int x, int y)
{
    if (currentState != GameState.Playing) return;

    // NEW: check if mine event system wants to handle this
    bool handled = false;
    OnRightClickCell?.Invoke(x, y, grid.GetCell(x, y), ref handled);
    if (handled) return;

    // Existing flag toggle logic
    MinesweeperLogic.ToggleFlag(grid, x, y);
    // ...
}
```

#### MineDistributionSO (ScriptableObject)

Configures how event types are distributed across mine cells:

```csharp
[CreateAssetMenu(menuName = "DarkSweeper/Mine Distribution")]
public class MineDistributionSO : ScriptableObject
{
    [Range(0, 100)] public int combatWeight = 60;
    [Range(0, 100)] public int chestWeight = 15;
    [Range(0, 100)] public int dialogueWeight = 15;
    [Range(0, 100)] public int shrineWeight = 10;

    [Header("Combat")]
    public float eliteChance = 0.2f;
    public int normalDamage = 5;
    public int eliteDamage = 12;

    [Header("Chest")]
    public float trapChance = 0.4f;
    public int trapDamage = 8;

    [Header("Shrine")]
    public int sacrificeCost = 10;

    [Header("Rewards")]
    public int hpGainAmount = 5;
    public int visionGainAmount = 1;
}
```

**Why a ScriptableObject:** Same rationale as `SweeperConfig` — editable in Inspector, duplicatable for presets (easy/hard distributions), swappable per level.

#### MineEventContentSO (ScriptableObject)

Holds the **narrative content** for dialogues, shrine descriptions, monster names, etc.:

```csharp
[CreateAssetMenu(menuName = "DarkSweeper/Mine Event Content")]
public class MineEventContentSO : ScriptableObject
{
    public CombatContent[] combats;
    public ChestContent[] chests;
    public DialogueContent[] dialogues;
    public ShrineContent[] shrines;
}
```

Each `XxxContent` is a `[Serializable]` class with text fields. The Logic layer picks randomly from the available pool at level init.

**Why separate from MineDistributionSO:** Distribution = gameplay tuning (designer), content = narrative text (writer). Different people, different cadence of edits.

#### Input Pause System

When a mine event panel is open, grid input must be blocked:

```csharp
// Simple bool flag on InputHandler (or SweeperGameController)
public bool inputBlocked;
```

`MineEventController` sets `inputBlocked = true` when opening a panel, `false` when closing. `InputHandler` checks the flag before firing click events. Minimal, no complex state machine needed.

---

### 2.4 Presentation Layer (`Assets/Scripts/Mines/Presentation/`)

#### MineEventPanel (MonoBehaviour, UGUI)

A **single Canvas panel** that displays all 4 event types. It's a modal overlay.

**Structure:**

```
[Canvas (Screen Space - Overlay)]
  └─ MineEventPanel (CanvasGroup for fade in/out)
       ├─ Background (dark semi-transparent overlay, blocks clicks on grid)
       ├─ EventFrame (central panel)
       │    ├─ HeaderArea
       │    │    ├─ EventIcon (Image — combat sword / chest / speech bubble / altar)
       │    │    └─ TitleText (TMP)
       │    ├─ DescriptionText (TMP)
       │    ├─ ChoiceButtonsContainer (VerticalLayoutGroup)
       │    │    ├─ ChoiceButton_0 (Button + TMP)
       │    │    ├─ ChoiceButton_1 (Button + TMP)
       │    │    └─ ChoiceButton_2 (Button + TMP)
       │    └─ ResultArea (hidden until choice made)
       │         ├─ ResultText (TMP)
       │         ├─ RewardText (TMP, colored)
       │         └─ ContinueButton (Button)
       └─ [optional] CharacterPortrait (Image, for Dialogue type)
```

**Why a single panel (not 4 separate panels):**
* All 4 types share the same structure: title + description + 1-3 buttons + result
* The differences are cosmetic (icon, portrait, colors) and data-driven
* One panel = one prefab = less maintenance for hackathon
* The `InteractionDescriptor` already provides everything the panel needs — it just renders the data

**Population flow:**

1. `MineEventController` calls `MineEventLogic.GetInteraction(data)` → gets `InteractionDescriptor`
2. Controller passes descriptor to `MineEventPanel.Show(descriptor, callback)`
3. Panel populates title, description, buttons from descriptor
4. Player clicks a button → panel fires callback with `PlayerChoice`
5. Controller resolves → calls `MineEventPanel.ShowResult(resultText, rewardText)`
6. Player clicks "Continue" → panel hides → controller resumes grid input

#### Visual Feedback on Grid

When a mine cell is revealed and has an event:

* The cell quad should display an **icon** indicating the event type (sword, chest, bubble, altar)
* Use a `SpriteRenderer` or `TextMeshPro 3D` glyph on top of the mine cell
* Once resolved, the icon changes (dimmed / checkmark / crossed out)

**Integration with CellView:**

`CellView` already manages per-cell visuals. Extend it with an optional `eventIcon` child that `MineEventController` can activate/deactivate. This avoids duplicating rendering logic.

#### Combat-specific: Damage Animation

For hackathon scope, combat resolution is **instant** (no mini-game). Visual feedback:

* Screen flash (red tint via CanvasGroup overlay, 0.2s)
* HP bar shake / number countdown in the HUD
* Short SFX (if available)

No need for a combat scene or turn-based system. The `ResolutionResult` is computed in one call; the animation is purely cosmetic.

---

## 3. Event Flow — Full Sequence (Right-Click on Mine)

```
1. Player right-clicks a cell
2. InputHandler fires OnRightClick(x, y)
3. SweeperGameController.HandleRightClick:
   a. Fires OnRightClickCell(x, y, cellData, ref handled)
   b. MineEventController receives:
      - Is this cell a mine? (cellData.hasMine)
      - Is the mine revealed? (cellData.isRevealed)
      - Is the event already resolved? (mineEventData.state == Resolved)
      → If all conditions met: set handled = true
      → Else: return, let flag toggle proceed
4. MineEventController:
   a. Sets inputBlocked = true
   b. Gets InteractionDescriptor from MineEventLogic
   c. Calls MineEventPanel.Show(descriptor, OnChoiceMade)
5. Player reads description, clicks a choice button
6. MineEventPanel fires callback → MineEventController.OnChoiceMade(choice):
   a. Calls appropriate Resolve method (ResolveCombat / ResolveChest / etc.)
   b. Gets ResolutionResult
   c. Applies HP delta to SweeperGameController (via event or direct ref)
   d. Applies reward via RewardLogic
   e. Sets mineEventData.state = Resolved
   f. Logs RunEvent to RunLog
   g. Calls MineEventPanel.ShowResult(resultText)
7. Player clicks "Continue"
8. MineEventController:
   a. Hides panel
   b. Updates cell visual (resolved icon)
   c. Sets inputBlocked = false
   d. Grid input resumes
```

---

## 4. Data Flow for Event Assignment

At level start (after mines are placed):

```
SweeperGameController.Start()
  → mines placed (from LevelDataSO or random)
  → fires OnGridReady(GridModel)

MineEventController.OnGridReady(grid):
  → iterates all cells where hasMine == true
  → calls MineEventLogic.AssignEvents(grid, distributionSO, contentSO)
  → stores Dictionary<(int,int), MineEventData> for O(1) lookup
```

**Why a Dictionary and not extending CellData:**
* `CellData` is pure Sweeper data — clean separation of concerns
* Mine events are an overlay system; they should not bloat the core data
* The Dictionary maps `(x,y) → MineEventData` and nothing else needs to change in the Sweeper module
* If the mine event system is removed, zero lines of Sweeper code change

---

## 5. Integration Points with Existing Code

| Existing File | Change Required | Nature |
|---------------|----------------|--------|
| `SweeperGameController.cs` | Add `OnRightClickCell` event + `ref handled` pattern in `HandleRightClick` | ~5 lines added, zero logic changed |
| `SweeperGameController.cs` | Add `OnGridReady` event fired after mine placement | ~2 lines |
| `SweeperGameController.cs` | Expose `CurrentHP` getter + `ApplyHPDelta(int)` method | ~10 lines |
| `InputHandler.cs` | Add `inputBlocked` bool, check before firing events | ~3 lines |
| `CellView.cs` | Add optional `eventIconObject` child + `ShowEventIcon(Sprite)` / `ShowResolved()` | ~15 lines |
| `SweeperHUD.cs` | None (HP updates already use events) | 0 lines |
| `CellData.cs` | None | 0 lines |
| `GridModel.cs` | None | 0 lines |
| `MinesweeperLogic.cs` | None | 0 lines |

**Total changes to existing Sweeper code: ~35 lines, all additive.** No refactoring, no renames, no behavior changes.

---

## 6. Asset Organization

```
Assets/
  Scripts/
    Mines/
      Data/
        MineEventType.cs             ← enum (Combat, Chest, Dialogue, Shrine)
        MineState.cs                 ← enum (Hidden, Revealed, Resolved)
        RewardType.cs                ← enum (None, HpGain, VisionGain, Buff)
        PlayerChoice.cs              ← enum (Engage, Open, Ignore, Help, Harm, Sacrifice, Refuse)
        MineEventData.cs             ← per-mine event payload
        CombatParams.cs              ← combat configuration
        ChestParams.cs               ← chest configuration
        DialogueParams.cs            ← dialogue text + choices
        ShrineParams.cs              ← shrine configuration
        RunEvent.cs                  ← log entry
        RunLog.cs                    ← log container
      Logic/
        MineEventLogic.cs            ← event assignment + resolution (static)
        RewardLogic.cs               ← reward application (static)
      Flow/
        MineEventController.cs       ← orchestrator MonoBehaviour
        MineDistributionSO.cs        ← ScriptableObject — distribution weights + gameplay values
        MineEventContentSO.cs        ← ScriptableObject — narrative text pools
      Presentation/
        MineEventPanel.cs            ← UGUI modal panel
        MineEventIcons.cs            ← optional: ScriptableObject mapping event types to sprites
  Art/
    Mines/
      Sprites/
        icon_combat.png
        icon_chest.png
        icon_dialogue.png
        icon_shrine.png
        icon_resolved.png
      UI/
        panel_background.png         ← 9-slice for the event frame
  Prefabs/
    MineEventPanel.prefab            ← the modal UI panel
  Data/
    MineDistribution_Default.asset   ← default distribution preset
    MineContent_Default.asset        ← default content pool
```

---

## 7. Tech Choices Summary

| Component | Choice | Justification |
|-----------|--------|---------------|
| Data model | Pure C# classes in `Mines/Data/` | Same pattern as `CellData`/`GridModel` — testable, decoupled |
| Event storage | `Dictionary<(int,int), MineEventData>` in controller | O(1) lookup, no modification to CellData |
| Logic | Static classes (`MineEventLogic`, `RewardLogic`) | Same pattern as `MinesweeperLogic` — stateless, deterministic |
| Orchestration | `MineEventController` MonoBehaviour | Same pattern as `SweeperGameController` |
| Configuration | Two ScriptableObjects (distribution + content) | Separate tuning from narrative; Inspector-editable, preset-friendly |
| UI framework | UGUI Canvas (Screen Space - Overlay) + TextMeshPro | Same as `SweeperHUD` — consistent, proven in the project |
| UI structure | Single shared panel for all 4 event types | Data-driven, less prefabs, less code |
| Inter-system comm | C# events (`System.Action<T>`) | Same as existing Sweeper events |
| Integration pattern | `ref handled` event on right-click | Minimal invasion — ~35 lines added to Sweeper |
| Input blocking | `bool inputBlocked` on `InputHandler` | Simplest possible modal system |
| Combat resolution | Instant (no mini-game) | Hackathon scope — effect is calculated, animation is cosmetic |
| Logging | `RunLog` with `List<RunEvent>` | Ready for JSON serialization if needed for narrative/god system |
| Sprites for icons | 4 event-type icons + 1 resolved icon | Minimal art budget, high readability |

---

## 8. Key Technical Decisions & Rationale

### Separate module, not extension of Sweeper

The mine event system lives in `Assets/Scripts/Mines/`, not inside `Assets/Scripts/Sweeper/`. This is deliberate:
* The Sweeper module is **done and stable** — touching it risks regression
* Mine events are a **gameplay layer on top**, not a modification of Minesweeper rules
* If mine events are cut (hackathon triage), the Sweeper still works perfectly
* Different people can work on Mines vs Sweeper without merge conflicts

### Dictionary overlay, not CellData extension

Adding fields to `CellData` (like `eventType`, `shrineParams`, etc.) would violate single responsibility. `CellData` knows about Minesweeper. `MineEventData` knows about narrative events. They are linked by grid coordinates, not by inheritance or composition.

### Single UI panel, not 4 panels

All event types follow the same UX pattern: title → description → 1–3 buttons → result. The differences are data, not structure. A single panel driven by `InteractionDescriptor` means:
* 1 prefab to maintain
* No if/else to pick which panel to show
* Adding a 5th event type = adding content, not UI code

### Instant combat (no mini-game)

For hackathon scope, combat is a **stat check**: the player takes fixed damage, gets a reward. The visual feedback (screen flash, HP animation) is enough to create tension. A full combat mini-game would:
* Triple the scope
* Require its own UI, animations, enemy AI
* Distract from the core loop (perception + resource management)

If combat depth is desired post-hackathon, the `ResolveCombat` method is the single point to replace — the rest of the system doesn't care how combat is resolved internally.

### Content separated from distribution

`MineDistributionSO` (gameplay weights) and `MineEventContentSO` (text/narrative) are intentionally separate:
* A designer can tune combat frequency without touching dialogue text
* A writer can add new dialogue options without touching balance numbers
* Both are ScriptableObjects → both editable in Inspector, both versionable

### RunLog ready for god alignment

The `RunLog` captures every player choice with enough detail for the narrative/god system to evaluate:
* Did the player Help or Harm in dialogues? → affects Porteur de Cendres vs Archiviste du Vide
* Did the player Sacrifice at shrines? → affects alignment score
* Did the player Open or Ignore chests? → risk appetite metric
* `hpBefore`/`hpAfter` tracks resource management style

This data can be serialized to JSON and fed to the LLM-based narrative system without any changes to the mine event code.

---

## 9. Performance Notes

* **Mine events are sparse**: typically 10-30 mines on a grid. The Dictionary has 10-30 entries. Zero performance concern.
* **UI is a single Canvas panel**: shown/hidden, not instantiated/destroyed. No GC pressure.
* **Event assignment** happens once at level start: O(n) where n = mine count. Negligible.
* **Resolution** is a single method call with arithmetic: O(1). Negligible.
* **RunLog** is an append-only list: grows linearly with player interactions. For hackathon levels (< 100 events per run), this is trivial.

---

## 10. Implementation Order (Recommended)

| Phase | Tasks | Depends On |
|-------|-------|------------|
| **1. Data** | Create all enums, `MineEventData`, param classes, `RunEvent`, `RunLog` | Nothing |
| **2. Logic** | Implement `MineEventLogic.AssignEvents`, `GetInteraction`, all `ResolveXxx` methods, `RewardLogic` | Phase 1 |
| **3. Flow** | Create `MineEventController`, `MineDistributionSO`, `MineEventContentSO`. Wire to `SweeperGameController` (add events + `ref handled`) | Phase 2 + Sweeper |
| **4. UI** | Build `MineEventPanel` prefab + script. Wire to controller | Phase 3 |
| **5. Polish** | Cell icons, screen flash, result animations, sound hooks | Phase 4 |

Phases 1-2 can be developed and tested in isolation (pure C#, no Unity). Phase 3 is the integration point. Phase 4 is visual. Phase 5 is polish.

---

## 11. Why This Works for a Hackathon

* **Minimal invasion**: ~35 lines changed in existing Sweeper code
* **Familiar patterns**: same architecture, same conventions, same tools as the rest of the project
* **Data-driven**: adding a new event type = new enum value + new param class + new Resolve method + new content in SO. No UI changes.
* **Incrementally shippable**: Phase 1-3 gives a working system with placeholder text. Phase 4-5 makes it look good.
* **Cuttable**: if time runs out, combat-only is a viable MVP (skip Dialogue, Shrine, Chest)
* **Explainable to a jury**: "Right-click on a mine → modal panel → player choice → gameplay effect → logged for narrative AI"
