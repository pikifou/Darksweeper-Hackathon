# Implementation Plan — Unity 6 Tech Demo: LLM Call → JSON → GameState → ScriptableObject

## Scope

A single Unity scene demonstrating the core loop:

1. Read current game state from a ScriptableObject.
2. Send user intent + current state to **GPT-5** (JSON mode).
3. Receive a strict JSON response with the full updated game state.
4. Parse the JSON → DTO → GameStateModel → ScriptableObject.
5. Display the result in the UI.

**No** advanced validation, no reducer pattern, no repair passes, no multi-agent, no Addressables.
One loop, end-to-end. Quick demo for a hackathon.

### Key decisions

| Decision             | Choice                                           |
| -------------------- | ------------------------------------------------ |
| LLM                  | GPT-5 with **JSON mode** (`response_format`)     |
| JSON library         | Newtonsoft.Json (Unity default)                   |
| HTTP                 | UnityWebRequest (coroutine-based)                 |
| UI framework         | UGUI (Canvas)                                     |
| Project structure    | Folders only, no Assembly Definitions             |
| Invalid updates      | Reject (status Failed, state unchanged)           |
| API key storage      | ScriptableObject asset, gitignored                |
| Testing              | Manual only                                       |
| LLM response shape   | Full game state (not deltas)                      |

---

## JSON Contract v1.0

The LLM always returns a complete game state:

```json
{
  "type": "game_state_update",
  "schema_version": "1.0",
  "game_state": {
    "hp": 95,
    "energy": 40,
    "objective": "Find the hidden cave",
    "flags": ["campfire_visited", "sword_equipped"]
  }
}
```

If the LLM cannot comply, it returns:

```json
{
  "type": "error",
  "schema_version": "1.0",
  "message": "Cannot process intent",
  "code": "invalid_intent"
}
```

---

## Folder Structure

```
Assets/
  Scenes/
    LLM_TechDemo.unity
  Scripts/
    CoreLLM/
      LLMClient.cs              — UnityWebRequest transport
      RequestBuilder.cs          — assembles the API request
      ResponseParser.cs          — JSON → DTO
    Domain/
      GameStateModel.cs          — plain C# runtime model
      GameStateDTO.cs            — mirrors JSON structure
    UnityData/
      GameStateSO.cs             — ScriptableObject definition
      GameStateMapper.cs         — Model ↔ SO mapping
      LLMConfigSO.cs             — API key + endpoint config
      PromptTemplateSO.cs        — system prompt + schema text
    Presentation/
      Orchestrator.cs            — MonoBehaviour, wires everything
      UIManager.cs               — UGUI references and display logic
  Data/
    GameState_Runtime.asset      — default game state
    PromptTemplate_v1.asset      — prompt + schema
    LLMConfig.asset              — API key (gitignored)
```

---

## Step 1 — Create the demo scene and minimal UI

**Instruction**

1. Create a new scene `LLM_TechDemo`.
2. Add a Canvas with:
   * InputField — user types their intent here.
   * Button — "Send".
3. Create an empty GameObject `Orchestrator` with `Orchestrator.cs`.
4. All feedback (status, raw response, errors, game state) goes to `Debug.Log` in the Console. No extra UI elements.

**Test**

* Enter any text, press Send → Console shows `[Orchestrator] Sending intent: "your text"`.
* No console exceptions.

---

## Step 2 — Create the GameState ScriptableObject

**Instruction**

1. Create `GameStateSO.cs`:
   * `int hp`
   * `int energy`
   * `string objective`
   * `List<string> flags`
2. Create asset `GameState_Runtime.asset` with defaults:
   * hp = 100, energy = 50, objective = "Explore the area", flags = empty.
3. Orchestrator holds a reference to this asset.

**Test**

* Select the asset in Editor → default values visible.
* Enter Play Mode → UI shows these values.

---

## Step 3 — Create the runtime GameStateModel and Mapper

**Instruction**

1. Create `GameStateModel.cs` — plain C# class, **no Unity dependencies**:
   * `int Hp`, `int Energy`, `string Objective`, `List<string> Flags`
2. Create `GameStateMapper.cs`:
   * `GameStateModel FromSO(GameStateSO so)` — reads SO into a new model.
   * `void ApplyToSO(GameStateModel model, GameStateSO so)` — writes model fields into SO.
3. On Play Mode start, the Orchestrator loads the model from the SO and displays it.

**Test**

* Change asset values in Editor → Play Mode → UI reflects changes.
* Mapper is the only code reading/writing SO fields.

---

## Step 4 — Create the API config and prompt template assets

**Instruction**

1. Create `LLMConfigSO.cs` with fields:
   * `string apiKey`
   * `string model` (default `"gpt-5"`)
   * `string endpoint` (default `"https://api.openai.com/v1/chat/completions"`)
   * `float temperature` (default `0.2`)
   * `int maxTokens` (default `512`)
2. Create `PromptTemplateSO.cs` with fields:
   * `string systemPrompt` (multiline text area)
   * `string jsonSchema` (multiline text area)
   * `string schemaVersion` (default `"1.0"`)
3. Create assets:
   * `LLMConfig.asset` — developer fills in their API key. **Gitignored.**
   * `PromptTemplate_v1.asset` — system prompt instructs JSON-only output; schema text describes the contract.
4. Add `LLMConfig.asset` path to `.gitignore`.

**Test**

* Both assets editable in Inspector.
* `LLMConfig.asset` does not appear in version control.

---

## Step 5 — Build the LLM request

**Instruction**

1. Create `RequestBuilder.cs`:
   * Takes: user intent (string), current GameStateModel, PromptTemplateSO.
   * Produces a JSON string for the OpenAI Chat Completions API body:
     * `model` from config
     * `temperature` from config
     * `max_tokens` from config
     * `response_format: { "type": "json_object" }` — enables JSON mode
     * `messages` array:
       * **system**: the system prompt from the template
       * **user**: includes the user intent, the current game state (serialized), and the JSON schema
2. The system prompt must clearly state: return ONLY valid JSON matching the schema, no commentary.

**Test**

* Log the assembled request body to the console.
* Confirm it contains: system prompt, user intent, serialized current state, schema.

---

## Step 6 — Implement the OpenAI transport (UnityWebRequest)

**Instruction**

1. Create `LLMClient.cs`:
   * `IEnumerator SendRequest(string jsonBody, LLMConfigSO config, System.Action<string> onSuccess, System.Action<string> onError)`
   * Uses `UnityWebRequest.Post` with JSON content type.
   * Sets `Authorization: Bearer <apiKey>` header.
   * Handles: timeout, network errors, non-200 status codes.
2. Orchestrator starts this coroutine:
   * Sets status `Sending`.
   * On success: passes raw response text forward.
   * On error: shows error in UI, sets status `Failed`.

**Test**

* Invalid API key → `Failed` with auth error message.
* Valid key → raw response appears in the Raw Response area.
* Tiny timeout → fails gracefully, no freeze.

---

## Step 7 — Parse response JSON into GameStateModel

**Instruction**

1. Create `GameStateDTO.cs` — plain C# class matching the JSON contract:
   * `string type`
   * `string schema_version`
   * `GameStateData game_state` (nested class with `int hp`, `int energy`, `string objective`, `List<string> flags`)
2. Create `ResponseParser.cs`:
   * Deserializes raw JSON string into `GameStateDTO` using Newtonsoft.Json.
   * Checks: `type == "game_state_update"` and `schema_version == "1.0"`.
   * If type is `"error"`, extracts the error message and rejects.
   * Maps `GameStateDTO.game_state` → `GameStateModel`.
   * Returns the model on success, or an error string on failure.
3. On parse failure: do not update state.

**Test**

* Valid JSON → GameStateModel populated correctly.
* JSON with `type: "error"` → rejected with message shown.
* Malformed JSON → fails cleanly, status `Failed`, state unchanged.

---

## Step 8 — Connect the full pipeline

**Instruction**

1. Wire everything in `Orchestrator.cs`:
   * User clicks Send →
   * `RequestBuilder` assembles the request body.
   * `LLMClient` sends the request (coroutine).
   * `ResponseParser` parses the response.
   * If valid:
     * Set runtime `GameStateModel` to the parsed result.
     * `GameStateMapper.ApplyToSO(model, gameStateSO)` — update the SO in memory.
     * Refresh UI display.
     * Status → `Done`.
   * If invalid:
     * Show error message.
     * Status → `Failed`.
     * State unchanged.
2. UI always shows: raw JSON, pretty-printed current state, status, last error.

**Test**

* Send 3 different intents. After each success:
  * State updates in UI.
  * SO updates visible in Inspector during Play Mode.
* Trigger one failure → state remains the previous valid state.

---

## Step 9 — Add runtime persistence (save/load)

**Instruction**

1. On successful state update: save `GameStateModel` as JSON to `Application.persistentDataPath/save.json`.
2. On startup: if `save.json` exists, load it and use it instead of the SO defaults.
3. Add a **Reset** button in the UI:
   * Deletes `save.json`.
   * Reloads state from the SO defaults.
   * Refreshes UI.

**Test**

* Update state, exit Play Mode, re-enter → state persists.
* Click Reset → state returns to SO defaults.
* Delete save file manually → falls back to defaults on next launch.

---

## Step 10 — Add basic logging

**Instruction**

1. After each request, log to console:
   * Timestamp, user intent, result status (success / fail), error message if any.
2. Show a one-line summary of the last result in the UI.
3. Ensure the API key never appears in any log output.

**Test**

* 1 success + 1 failure → console and UI show correct info.
* Grep logs for API key → not found.

---

## Step 11 — Package deliverables

**Instruction**

1. Write a `README.md` at the project root:
   * Setup: create `LLMConfig.asset`, paste API key.
   * How to open and run `LLM_TechDemo` scene.
   * JSON contract v1.0 reference.
2. Ensure `.gitignore` covers:
   * `LLMConfig.asset` (and its .meta)
   * Save files in persistentDataPath
   * Build artifacts, Library/, Temp/, etc.

**Test**

* A new developer can open the project, configure the key, run the scene, and see end-to-end success within minutes.

---

## Step 12 — Final acceptance tests

Run these scenarios:

1. **Happy path** — Intent: "Player rests at a campfire." → hp/energy update, objective changes.
2. **Parse failure** — Manually corrupt the raw response or force bad JSON → `Failed`, state unchanged.
3. **Network error** — Invalid key or no network → `Failed`, no freeze, clear error message.

All scenarios: correct UI states, no unhandled exceptions in console.
