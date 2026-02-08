using System;
using System.Collections.Generic;
using Mines.Data;
using Mines.Flow;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Parses the raw OpenAI response for a dialogue batch.
/// Extracts choices[0].message.content, expects { "type": "dialogue_batch", "dialogues": [...] }.
/// Converts each dialogue DTO into a runtime <see cref="DialogueEncounterSO"/>.
/// </summary>
public static class DialogueResponseParser
{
    /// <summary>
    /// Tries to parse the LLM response into an array of <see cref="DialogueEncounterSO"/>.
    /// Characters are looked up by ID to wire the correct video references.
    /// </summary>
    /// <param name="rawApiResponse">Raw JSON from the OpenAI API.</param>
    /// <param name="characters">The 8 dialogue characters for ID lookup.</param>
    /// <param name="dialogues">Output: 12 runtime DialogueEncounterSO instances.</param>
    /// <param name="error">Output: error message if parsing fails.</param>
    /// <returns>True on success, false on failure.</returns>
    public static bool TryParse(
        string rawApiResponse,
        DialogueCharacterSO[] characters,
        out DialogueEncounterSO[] dialogues,
        out string error)
    {
        dialogues = null;
        error = null;

        try
        {
            string content = ExtractContent(rawApiResponse);
            if (string.IsNullOrEmpty(content))
            {
                error = "Empty or missing content in API response.";
                return false;
            }

            var batch = JsonConvert.DeserializeObject<DialogueBatchDTO>(content);
            if (batch == null)
            {
                error = "Deserialization returned null.";
                return false;
            }

            if (batch.type == "error")
            {
                error = $"LLM returned error type.";
                return false;
            }

            if (batch.type != "dialogue_batch")
            {
                error = $"Unexpected type: \"{batch.type}\". Expected \"dialogue_batch\".";
                return false;
            }

            if (batch.dialogues == null || batch.dialogues.Length == 0)
            {
                error = "No dialogues in response.";
                return false;
            }

            // Build character lookup
            var charLookup = new Dictionary<string, DialogueCharacterSO>(StringComparer.OrdinalIgnoreCase);
            if (characters != null)
            {
                foreach (var c in characters)
                {
                    if (c != null && !string.IsNullOrEmpty(c.characterId))
                        charLookup[c.characterId] = c;
                }
            }

            // Convert DTOs to runtime SOs
            var result = new List<DialogueEncounterSO>();
            for (int i = 0; i < batch.dialogues.Length; i++)
            {
                var dto = batch.dialogues[i];
                if (dto == null) continue;

                var so = ScriptableObject.CreateInstance<DialogueEncounterSO>();

                // Lookup character
                DialogueCharacterSO character = null;
                if (!string.IsNullOrEmpty(dto.characterId))
                    charLookup.TryGetValue(dto.characterId, out character);

                so.character = character;
                so.promptText = dto.promptText ?? "";

                // Convert choices
                var choices = new List<DialogueChoiceEntry>();
                if (dto.choices != null)
                {
                    foreach (var c in dto.choices)
                    {
                        if (c == null) continue;
                        choices.Add(new DialogueChoiceEntry
                        {
                            choiceType = ParseChoiceType(c.choiceType),
                            label = c.label ?? "",
                            resultText = c.resultText ?? "",
                            hpDelta = c.hpDelta,
                            reward = ParseRewardType(c.reward),
                            rewardValue = c.rewardValue
                        });
                    }
                }

                so.choices = choices.ToArray();
                so.name = $"LLM_Dialogue_{i:D2}_{dto.characterId ?? "unknown"}";

                result.Add(so);
            }

            if (result.Count == 0)
            {
                error = "All dialogues were null or invalid.";
                return false;
            }

            dialogues = result.ToArray();
            Debug.Log($"[DialogueParser] Successfully parsed {dialogues.Length} dialogues from LLM response.");
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

    // ================================================================
    // Enum parsing helpers
    // ================================================================

    private static PlayerChoice ParseChoiceType(string value)
    {
        if (string.IsNullOrEmpty(value)) return PlayerChoice.Ignore;
        return value.Trim().ToLowerInvariant() switch
        {
            "help"      => PlayerChoice.Help,
            "harm"      => PlayerChoice.Harm,
            "ignore"    => PlayerChoice.Ignore,
            "sacrifice" => PlayerChoice.Sacrifice,
            "refuse"    => PlayerChoice.Refuse,
            "engage"    => PlayerChoice.Engage,
            "open"      => PlayerChoice.Open,
            _           => PlayerChoice.Ignore
        };
    }

    private static RewardType ParseRewardType(string value)
    {
        if (string.IsNullOrEmpty(value)) return RewardType.None;
        return value.Trim().ToLowerInvariant() switch
        {
            "none"       => RewardType.None,
            "hpgain"     => RewardType.HpGain,
            "visiongain" => RewardType.VisionGain,
            "buff"       => RewardType.Buff,
            _            => RewardType.None
        };
    }

    // ================================================================
    // Content extraction (same pattern as NarratorResponseParser)
    // ================================================================

    private static string ExtractContent(string rawApiResponse)
    {
        try
        {
            var obj = JsonConvert.DeserializeObject<OpenAIResponse>(rawApiResponse);
            if (obj?.choices != null && obj.choices.Length > 0 && obj.choices[0].message != null)
                return obj.choices[0].message.content;
        }
        catch (Exception ex)
        {
            Debug.Log($"[DialogueParser] Failed to extract content: {ex.Message}");
        }
        return null;
    }

    // ================================================================
    // DTOs
    // ================================================================

    private class DialogueBatchDTO
    {
        public string type;
        public string schema_version;
        public DialogueDTO[] dialogues;
    }

    private class DialogueDTO
    {
        public string characterId;
        public string promptText;
        public DialogueChoiceDTO[] choices;
    }

    private class DialogueChoiceDTO
    {
        public string choiceType;
        public string label;
        public string resultText;
        public int hpDelta;
        public string reward;
        public int rewardValue;
    }

    private class OpenAIResponse
    {
        public Choice[] choices;
    }

    private class Choice
    {
        public MessageContent message;
    }

    private class MessageContent
    {
        public string content;
    }
}
