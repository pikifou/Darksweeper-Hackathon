using System;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Parses the raw OpenAI response for a narrator phrase.
/// Extracts choices[0].message.content, expects { "type": "narrator_intro", "text": "..." }.
/// </summary>
public static class NarratorResponseParser
{
    /// <summary>
    /// Tries to extract the narrator text from the raw API response.
    /// Returns true on success (text is set), false on failure (error is set).
    /// </summary>
    public static bool TryParse(string rawApiResponse, out string narratorText, out string error)
    {
        narratorText = null;
        error = null;

        try
        {
            string content = ExtractContent(rawApiResponse);
            if (string.IsNullOrEmpty(content))
            {
                error = "Empty or missing content in API response. The model may have used all tokens for reasoning.";
                return false;
            }

            var dto = JsonConvert.DeserializeObject<NarratorDTO>(content);
            if (dto == null)
            {
                error = "Deserialization returned null.";
                return false;
            }

            // Check for LLM error response
            if (dto.Type == "error")
            {
                error = $"LLM returned error: {dto.Text}";
                return false;
            }

            // Validate type
            if (dto.Type != "narrator_intro")
            {
                error = $"Unexpected type: \"{dto.Type}\". Expected \"narrator_intro\".";
                return false;
            }

            if (string.IsNullOrEmpty(dto.Text))
            {
                error = "Narrator text is empty or missing.";
                return false;
            }

            narratorText = dto.Text;
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
            Debug.Log($"[NarratorParser] Failed to extract content: {ex.Message}");
        }

        return null;
    }

    // ── DTOs ──────────────────────────────────────────────

    private class NarratorDTO
    {
        [JsonProperty("type")]
        public string Type;

        [JsonProperty("schema_version")]
        public string SchemaVersion;

        [JsonProperty("text")]
        public string Text;
    }

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
