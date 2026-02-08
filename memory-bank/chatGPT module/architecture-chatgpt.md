# Architecture — Unity 6 Tech Demo: LLM → JSON → GameState

## Overview

A single Unity scene sends a user intent + current game state to **GPT-5** (JSON mode), receives a strict JSON response with the full updated game state, and applies it back into the game via ScriptableObjects.

This is a quick hackathon tech demo. Simplicity is the priority.

---

## Core Data Flow

```
User types intent
        |
        v
+-------------------+
|   Orchestrator    |  (MonoBehaviour — wires everything, no logic)
+-------------------+
        |e
        v
+-------------------+
|  RequestBuilder   |  Assembles: system prompt + user intent + current state + schema
+-------------------+
        |
        v
+-------------------+
|    LLMClient      |  UnityWebRequest POST to OpenAI Chat Completions (JSON mode)
+-------------------+
        |
        v
   Raw JSON string
        |
        v
+-------------------+
|  ResponseParser   |  Newtonsoft.Json → GameStateDTO → GameStateModel
+-------------------+
        |
        v
+-------------------+
|  GameStateMapper  |  GameStateModel → GameStateSO (in-memory update)
+-------------------+
        |
        v
   UI refreshes + optional save to disk
```

---

## Four-Layer Architecture

| Layer          | Folder                     | Depends on          | Key classes                              |
| -------------- | -------------------------- | ------------------- | ---------------------------------------- |
| **CoreLLM**    | `Scripts/CoreLLM/`         | UnityEngine (web)   | `LLMClient`, `RequestBuilder`, `ResponseParser` |
| **Domain**     | `Scripts/Domain/`          | C# only (no Unity)  | `GameStateModel`, `GameStateDTO`         |
| **UnityData**  | `Scripts/UnityData/`       | UnityEngine          | `GameStateSO`, `GameStateMapper`, `LLMConfigSO`, `PromptTemplateSO` |
| **Presentation** | `Scripts/Presentation/`  | MonoBehaviour        | `Orchestrator`, `UIManager`              |

**Rules:**

* No class may reference a layer below it (Presentation → UnityData → Domain; CoreLLM → Domain).
* No Domain code may reference `UnityEngine`.
* JSON is **never** deserialized directly into a ScriptableObject.
* Mandatory pipeline: **JSON → DTO → GameStateModel → ScriptableObject**.
* Folders only — no Assembly Definitions for this demo.

---

## JSON Contract v1.0

### Success response

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

### Error response (from LLM)

```json
{
  "type": "error",
  "schema_version": "1.0",
  "message": "Cannot process intent",
  "code": "invalid_intent"
}
```

The LLM returns the **full updated state** (not deltas). No reducer pattern.

---

## Key Classes

### Domain Layer

**`GameStateModel`** — Plain C# class, no Unity dependencies.

| Field       | Type           | Default                |
| ----------- | -------------- | ---------------------- |
| `Hp`        | int            | 100                    |
| `Energy`    | int            | 50                     |
| `Objective` | string         | "Explore the area"     |
| `Flags`     | List\<string\> | empty                  |

**`GameStateDTO`** — Mirrors the JSON structure exactly. Used only for deserialization.

### UnityData Layer

**`GameStateSO`** — ScriptableObject with serialized fields matching `GameStateModel`. One default asset: `GameState_Runtime.asset`.

**`GameStateMapper`** — Bidirectional mapping:
* `GameStateModel FromSO(GameStateSO so)`
* `void ApplyToSO(GameStateModel model, GameStateSO so)`

**`LLMConfigSO`** — ScriptableObject holding API key, model name, endpoint, temperature, max tokens. **Gitignored.**

**`PromptTemplateSO`** — ScriptableObject with:
* `systemPrompt` (multiline text) — instructs the LLM to output only valid JSON.
* `jsonSchema` (multiline text) — the contract schema as text.
* `schemaVersion` (string) — e.g. "1.0".

System prompt and schema are separate fields. Both stored as plain text blobs.

### CoreLLM Layer

**`RequestBuilder`** — Assembles the OpenAI API request body:
* System message from `PromptTemplateSO.systemPrompt`.
* User message containing: intent + serialized current state + schema.
* Enables JSON mode: `response_format: { "type": "json_object" }`.

**`LLMClient`** — Sends the request via `UnityWebRequest`. Coroutine-based. Handles timeout and errors. Callbacks: `onSuccess(string rawJson)`, `onError(string errorMessage)`.

**`ResponseParser`** — Deserializes raw JSON with `Newtonsoft.Json`:
* Checks `type == "game_state_update"` and `schema_version == "1.0"`.
* If `type == "error"`, extracts the error message.
* Maps DTO → `GameStateModel` on success.

### Presentation Layer

**`Orchestrator`** — MonoBehaviour on a scene GameObject. Wires the pipeline:
1. On button press: build request → send → parse → apply → log result.
2. On failure: log error, state unchanged.
3. On startup: load from save file or SO defaults.

Minimal UI: only a `TMP_InputField` (intent) and a `Button` (send). All feedback goes to `Debug.Log` in Console. No UIManager class.

---

## Persistence

* On successful update: `GameStateModel` saved as JSON to `Application.persistentDataPath/save.json`.
* On startup: if save file exists, load it instead of SO defaults.
* Reset button in UI deletes the save and reloads from SO.

---

## Error Handling

| Error type      | Behavior                                      |
| --------------- | --------------------------------------------- |
| Network / timeout | Status `Failed`, error message in UI, no freeze |
| Malformed JSON  | Status `Failed`, state unchanged              |
| Wrong type/version | Status `Failed`, state unchanged           |
| LLM returns error object | Status `Failed`, message displayed   |

Policy: **reject** on any failure. No retry, no repair pass. Game state is never corrupted.

---

## Folder Structure

```
Assets/
  Scenes/
    LLM_TechDemo.unity
  Scripts/
    CoreLLM/
      LLMClient.cs
      RequestBuilder.cs
      ResponseParser.cs
    Domain/
      GameStateModel.cs
      GameStateDTO.cs
    UnityData/
      GameStateSO.cs
      GameStateMapper.cs
      LLMConfigSO.cs
      PromptTemplateSO.cs
    Presentation/
      Orchestrator.cs
      UIManager.cs
  Data/
    GameState_Runtime.asset
    PromptTemplate_v1.asset
    LLMConfig.asset              ← gitignored
```

---

## Tech Stack Summary

| Component     | Choice                                                     |
| ------------- | ---------------------------------------------------------- |
| Engine        | Unity 6                                                    |
| LLM           | GPT-5 via OpenAI Chat Completions API                      |
| JSON mode     | `response_format: { "type": "json_object" }`               |
| HTTP client   | UnityWebRequest (coroutine)                                |
| JSON parsing  | Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`)         |
| UI            | UGUI (Canvas)                                              |
| Persistence   | JSON file in `persistentDataPath`                          |
| API key       | LLMConfigSO asset, gitignored                              |
| Temperature   | 0.2                                                        |
| Testing       | Manual only                                                |
