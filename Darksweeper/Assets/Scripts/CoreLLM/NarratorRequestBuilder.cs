using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayerProfile;

/// <summary>
/// Builds the OpenAI Chat Completions request body for narrator phrase generation.
/// Takes the player profile (scores, gods, questionnaire answers) and produces
/// a request that asks GPT-5 for a cryptic narrator intro.
/// </summary>
public static class NarratorRequestBuilder
{
    /// <summary>
    /// Assembles the full API request body as a JSON string.
    /// </summary>
    public static string Build(PlayerProfileData profile, PromptTemplateSO template, LLMConfigSO config)
    {
        string profileJson = profile.ToJson();

        string userMessage =
            $"Player profile:\n{profileJson}\n\n" +
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
