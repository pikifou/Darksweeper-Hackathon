using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Parses the raw OpenAI response JSON into a GameStateModel.
/// Extracts the assistant message content, deserializes the DTO, validates type/version.
/// </summary>
public static class ResponseParser
{
    /// <summary>
    /// Tries to parse the raw API response into a GameStateModel.
    /// Returns true on success (model is set), false on failure (error is set).
    /// </summary>
    public static bool TryParse(string rawApiResponse, out GameStateModel model, out string error)
    {
        model = null;
        error = null;

        try
        {
            // The raw response is the full OpenAI API response.
            // We need to extract choices[0].message.content first.
            string content = ExtractContent(rawApiResponse);
            if (string.IsNullOrEmpty(content))
            {
                error = "Empty or missing content in API response. The model may have used all tokens for reasoning. Try increasing max_completion_tokens in LLMConfig.";
                return false;
            }

            var dto = JsonConvert.DeserializeObject<GameStateDTO>(content);
            if (dto == null)
            {
                error = "Deserialization returned null.";
                return false;
            }

            // Check for LLM error response
            if (dto.Type == "error")
            {
                error = $"LLM returned error: [{dto.Code}] {dto.Message}";
                return false;
            }

            // Validate type and version
            if (dto.Type != "game_state_update")
            {
                error = $"Unexpected type: \"{dto.Type}\". Expected \"game_state_update\".";
                return false;
            }

            if (dto.SchemaVersion != "1.0")
            {
                error = $"Unexpected schema_version: \"{dto.SchemaVersion}\". Expected \"1.0\".";
                return false;
            }

            if (dto.GameState == null)
            {
                error = "game_state field is missing or null.";
                return false;
            }

            // Map DTO to model
            model = new GameStateModel
            {
                Hp = dto.GameState.Hp,
                Energy = dto.GameState.Energy,
                Objective = dto.GameState.Objective ?? "",
                Flags = dto.GameState.Flags ?? new List<string>()
            };

            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON parse error: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Unexpected error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Extracts choices[0].message.content from the OpenAI API response.
    /// </summary>
    private static string ExtractContent(string rawApiResponse)
    {
        try
        {
            var obj = JsonConvert.DeserializeObject<OpenAIResponse>(rawApiResponse);
            if (obj?.Choices != null && obj.Choices.Length > 0 && obj.Choices[0].Message != null)
            {
                return obj.Choices[0].Message.Content;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ResponseParser] Failed to extract content: {ex.Message}");
        }

        return null;
    }

    // Minimal classes to navigate the OpenAI response structure
    private class OpenAIResponse
    {
        [JsonProperty("choices")]
        public Choice[] Choices;
    }

    private class Choice
    {
        [JsonProperty("message")]
        public MessageContent Message;
    }

    private class MessageContent
    {
        [JsonProperty("content")]
        public string Content;
    }
}
