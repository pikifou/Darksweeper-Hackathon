# Progress

## Step 1 — Create the demo scene and minimal UI ✅

**Date:** 2026-02-05

**Files created:**
- `Assets/Scripts/Presentation/Orchestrator.cs`

**What was done:**
- Created `Orchestrator.cs` MonoBehaviour with two serialized fields: `TMP_InputField` (intent) and `Button` (send).
- On Send click, logs the intent and status to Console via `Debug.Log`.
- All debug feedback (status, errors, raw responses, game state) goes to Console — no extra UI elements.
- Scene `LLM_TechDemo` created manually in Unity with Canvas, TMP InputField, Button, and Orchestrator GameObject.

**Design decision:** Minimal UI approach. Only an input field and a send button. Everything else is logged to Console. No UIManager class needed.

## Step 2 — Create the GameState ScriptableObject ✅

**Date:** 2026-02-05

**Files created:**
- `Assets/Scripts/UnityData/GameStateSO.cs`

**Files modified:**
- `Assets/Scripts/Presentation/Orchestrator.cs` — added `GameStateSO` reference, logs state on Start.

**What was done:**
- Created `GameStateSO` ScriptableObject with fields: `hp`, `energy`, `objective`, `flags`.
- `CreateAssetMenu` attribute allows creation via `Create > LLM Demo > Game State`.
- Orchestrator logs the full game state to Console on Start.
- Asset `GameState_Runtime.asset` created manually in `Assets/Data/`.

## Step 3 — Create the runtime GameStateModel and Mapper ✅

**Date:** 2026-02-05

**Files created:**
- `Assets/Scripts/Domain/GameStateModel.cs` — pure C# class, no Unity dependency.
- `Assets/Scripts/UnityData/GameStateMapper.cs` — static mapper: `FromSO()` and `ApplyToSO()`.

**Files modified:**
- `Assets/Scripts/Presentation/Orchestrator.cs` — uses `GameStateMapper.FromSO()` on Start, holds a `currentState` model at runtime.

**What was done:**
- Runtime model separated from Unity SO. Mapper is the only code reading/writing SO fields.

## Step 4 — Create the API config and prompt template assets ✅

**Date:** 2026-02-05

**Files created:**
- `Assets/Scripts/UnityData/LLMConfigSO.cs` — API key, model, endpoint, temperature, maxTokens.
- `Assets/Scripts/UnityData/PromptTemplateSO.cs` — system prompt, JSON schema, schema version.

**Files modified:**
- `Assets/Scripts/Presentation/Orchestrator.cs` — added `LLMConfigSO` and `PromptTemplateSO` references.

**What was done:**
- Two config ScriptableObjects created. Assets `LLMConfig.asset` and `PromptTemplate_v1.asset` created manually in `Assets/Data/`.
- `LLMConfig.asset` must be gitignored (contains API key).

## Step 5 — Build the LLM request ✅

**Date:** 2026-02-05

**Files created:**
- `Assets/Scripts/CoreLLM/RequestBuilder.cs` — builds OpenAI Chat Completions JSON body with JSON mode.

**Files modified:**
- `Packages/manifest.json` — added `com.unity.nuget.newtonsoft-json` 3.2.1.
- `Assets/Scripts/Presentation/Orchestrator.cs` — on Send, builds request and logs full body to Console.

## Step 6 — Implement the OpenAI transport (UnityWebRequest) ✅

**Date:** 2026-02-05

**Files created:**
- `Assets/Scripts/CoreLLM/LLMClient.cs` — coroutine-based UnityWebRequest POST, auth header, timeout, callbacks.

**Files modified:**
- `Assets/Scripts/Presentation/Orchestrator.cs` — starts coroutine on Send, logs raw response or error. Added `isSending` guard.

## Step 7 — Parse response JSON into GameStateModel ✅

**Date:** 2026-02-05

**Files created:**
- `Assets/Scripts/Domain/GameStateDTO.cs` — DTO matching JSON contract, with `[JsonProperty]` attributes.
- `Assets/Scripts/CoreLLM/ResponseParser.cs` — extracts content from OpenAI response, deserializes DTO, validates type/version, maps to GameStateModel.

## Step 8 — Connect the full pipeline ✅

**Date:** 2026-02-05

**Files modified:**
- `Assets/Scripts/Presentation/Orchestrator.cs` — full pipeline: Send → RequestBuilder → LLMClient → ResponseParser → GameStateMapper → log.
- `Assets/Scripts/CoreLLM/RequestBuilder.cs` — fixed: `max_completion_tokens` (not `max_tokens`), removed `temperature` (GPT-5 only supports default).
- `Assets/Scripts/CoreLLM/ResponseParser.cs` — handles empty content (reasoning model token exhaustion).
- `Assets/Scripts/UnityData/LLMConfigSO.cs` — default maxTokens raised to 4096 (GPT-5 needs headroom for reasoning).

**Lessons learned:**
- GPT-5 is a reasoning model: uses `max_completion_tokens`, doesn't support custom `temperature`, needs high token budget (reasoning_tokens consume from the total).
- All error logs changed to `Debug.Log` instead of `Debug.LogError` for consistent visibility in Console.

## Step 9 — Add runtime persistence (save/load) ✅

**Date:** 2026-02-05

**Files created:**
- `Assets/Scripts/UnityData/SaveManager.cs` — save/load/delete `persistentDataPath/save.json`.

**Files modified:**
- `Assets/Scripts/Presentation/Orchestrator.cs` — loads save on Start, auto-saves on success, press R to reset.

## Step 10 — Add basic logging ✅

**Date:** 2026-02-05

**Files modified:**
- `Assets/Scripts/Presentation/Orchestrator.cs` — structured log per request: `[HH:mm:ss] Intent: "..." → Success/Failed`. API key never logged.

## Step 11 — Package deliverables ✅

**Date:** 2026-02-05

**Files created:**
- `README.md` — setup, usage, JSON contract, project structure, key notes.
- `.gitignore` — Unity standard ignores + `LLMConfig.asset`.
