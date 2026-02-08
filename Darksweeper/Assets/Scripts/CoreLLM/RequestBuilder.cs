using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Builds the JSON request body for the OpenAI Chat Completions API.
/// </summary>
public static class RequestBuilder
{
    /// <summary>
    /// Assembles the full API request body as a JSON string.
    /// </summary>
    public static string Build(string userIntent, GameStateModel currentState, PromptTemplateSO template, LLMConfigSO config)
    {
        string currentStateJson = JsonConvert.SerializeObject(new
        {
            hp = currentState.Hp,
            energy = currentState.Energy,
            objective = currentState.Objective,
            flags = currentState.Flags
        }, Formatting.Indented);

        string userMessage =
            $"Player intent: {userIntent}\n\n" +
            $"Current game state:\n{currentStateJson}\n\n" +
            $"JSON schema (version {template.schemaVersion}):\n{template.jsonSchema}";

        var requestBody = new JObject
        {
            ["model"] = config.model,
            ["max_completion_tokens"] = config.maxTokens,
            ["response_format"] = new JObject { ["type"] = "json_object" },
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = template.systemPrompt },
                new JObject { ["role"] = "user", ["content"] = userMessage }
            }
        };

        return requestBody.ToString(Formatting.None);
    }
}
