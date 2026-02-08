using UnityEngine;

/// <summary>
/// Holds the system prompt and JSON schema sent to the LLM.
/// </summary>
[CreateAssetMenu(fileName = "PromptTemplate_v1", menuName = "LLM Demo/Prompt Template")]
public class PromptTemplateSO : ScriptableObject
{
    [TextArea(5, 20)]
    [Tooltip("System prompt instructing the LLM to output only valid JSON.")]
    public string systemPrompt =
        "You are a game state engine. You receive the current game state and a player intent. " +
        "You MUST respond with ONLY valid JSON matching the schema below. No commentary, no markdown, no explanation. " +
        "If you cannot process the intent, return an error JSON object.";

    [TextArea(5, 30)]
    [Tooltip("The JSON schema the LLM must follow.")]
    public string jsonSchema =
        "{\n" +
        "  \"type\": \"game_state_update\",\n" +
        "  \"schema_version\": \"1.0\",\n" +
        "  \"game_state\": {\n" +
        "    \"hp\": <int 0-100>,\n" +
        "    \"energy\": <int 0-100>,\n" +
        "    \"objective\": \"<string>\",\n" +
        "    \"flags\": [\"<string>\", ...]\n" +
        "  }\n" +
        "}\n\n" +
        "On error:\n" +
        "{\n" +
        "  \"type\": \"error\",\n" +
        "  \"schema_version\": \"1.0\",\n" +
        "  \"message\": \"<string>\",\n" +
        "  \"code\": \"<string>\"\n" +
        "}";

    public string schemaVersion = "1.0";
}
