using Mines.Flow;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayerProfile;

/// <summary>
/// Builds the OpenAI Chat Completions request body for dialogue batch generation.
/// Sends the player profile, 8 character descriptions, and the expected JSON schema.
/// </summary>
public static class DialogueRequestBuilder
{
    /// <summary>
    /// Assembles the full API request body as a JSON string.
    /// </summary>
    /// <param name="profile">Player profile (scores, gods, questionnaire).</param>
    /// <param name="characters">The 8 dialogue characters with their descriptions.</param>
    /// <param name="template">Prompt template SO (system prompt + schema). May be null for defaults.</param>
    /// <param name="config">LLM config (model, API key, etc.).</param>
    /// <param name="maxTokensOverride">If > 0, overrides config.maxTokens for this request.</param>
    public static string Build(
        PlayerProfileData profile,
        DialogueCharacterSO[] characters,
        PromptTemplateSO template,
        LLMConfigSO config,
        int maxTokensOverride = 8192)
    {
        // --- System prompt ---
        string systemPrompt = (template != null && !string.IsNullOrWhiteSpace(template.systemPrompt))
            ? template.systemPrompt
            : DialoguePromptDefaults.SystemPrompt;

        // --- JSON schema ---
        string jsonSchema = (template != null && !string.IsNullOrWhiteSpace(template.jsonSchema))
            ? template.jsonSchema
            : DialoguePromptDefaults.JsonSchema;

        string schemaVersion = (template != null && !string.IsNullOrWhiteSpace(template.schemaVersion))
            ? template.schemaVersion
            : DialoguePromptDefaults.SchemaVersion;

        // --- User message: 3 blocks ---

        // 1. Player profile
        string profileJson = profile.ToJson();

        // 2. Available characters
        var charArray = new JArray();
        if (characters != null)
        {
            foreach (var c in characters)
            {
                if (c == null) continue;
                charArray.Add(new JObject
                {
                    ["characterId"] = c.characterId,
                    ["characterName"] = c.characterName,
                    ["description"] = c.descriptionForLLM,
                    ["axisTag"] = c.axisTag
                });
            }
        }

        string userMessage =
            $"Player profile:\n{profileJson}\n\n" +
            $"Available characters ({charArray.Count}):\n{charArray.ToString(Formatting.Indented)}\n\n" +
            $"JSON response schema (version {schemaVersion}):\n{jsonSchema}";

        // --- Request body ---
        int tokens = maxTokensOverride > 0 ? maxTokensOverride : config.maxTokens;

        var requestBody = new JObject
        {
            ["model"] = config.model,
            ["max_completion_tokens"] = tokens,
            ["response_format"] = new JObject { ["type"] = "json_object" },
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = userMessage }
            }
        };

        return requestBody.ToString(Formatting.None);
    }
}
