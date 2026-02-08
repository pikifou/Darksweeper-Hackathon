# LLM Tech Demo — Unity 6 + GPT-5

A minimal Unity 6 scene that sends a player intent to GPT-5 and receives a full game state update as strict JSON.

## Setup

1. Open the project in **Unity 6**.
2. In `Assets/Data/`, create the **LLMConfig** asset if it doesn't exist:
   - Right-click `Assets/Data/` → `Create > LLM Demo > LLM Config`.
   - Rename to `LLMConfig`.
3. Select `LLMConfig.asset`, paste your **OpenAI API key** in the `Api Key` field.
4. Set `Max Tokens` to at least **4096** (GPT-5 is a reasoning model and needs headroom).
5. Open the scene: `Assets/Scenes/LLM_TechDemo.unity`.
6. Press **Play**.

## Usage

- Type an intent in the input field (e.g. "Player rests at a campfire").
- Click **Send**.
- Check the **Console** for results (game state updates, errors, etc.).
- Press **R** (when not typing) to reset state to defaults.

## JSON Contract v1.0

### Success response from LLM

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

### Error response from LLM

```json
{
  "type": "error",
  "schema_version": "1.0",
  "message": "Cannot process intent",
  "code": "invalid_intent"
}
```

## Project structure

```
Assets/
  Scenes/
    LLM_TechDemo.unity
  Scripts/
    CoreLLM/
      LLMClient.cs          — UnityWebRequest POST to OpenAI
      RequestBuilder.cs      — builds the API request JSON
      ResponseParser.cs      — parses response into GameStateModel
    Domain/
      GameStateModel.cs      — pure C# runtime game state
      GameStateDTO.cs        — JSON deserialization target
    UnityData/
      GameStateSO.cs         — ScriptableObject for game state
      GameStateMapper.cs     — maps between Model and SO
      LLMConfigSO.cs         — API key + config (gitignored)
      PromptTemplateSO.cs    — system prompt + JSON schema
      SaveManager.cs         — save/load to persistentDataPath
    Presentation/
      Orchestrator.cs        — wires everything together
  Data/
    GameState_Runtime.asset  — default game state
    PromptTemplate_v1.asset  — prompt + schema
    LLMConfig.asset          — API key (DO NOT COMMIT)
```

## Key notes

- **GPT-5 specifics:** uses `max_completion_tokens` (not `max_tokens`), does not support custom `temperature`.
- **Pipeline:** JSON → DTO → GameStateModel → ScriptableObject. Never deserialize directly into a SO.
- **Persistence:** game state auto-saves to `persistentDataPath/save.json` on each successful update. Press R to reset.
- **Security:** API key is stored in a gitignored ScriptableObject asset. Never logged.
