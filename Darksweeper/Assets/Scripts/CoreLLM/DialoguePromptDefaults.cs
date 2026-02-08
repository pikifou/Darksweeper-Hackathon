/// <summary>
/// Default prompt values for dialogue generation via the LLM.
/// Used as fallback when no PromptTemplateSO is assigned.
/// </summary>
public static class DialoguePromptDefaults
{
    public const string SystemPrompt =
        "You are a trial designer for a dark fantasy game. " +
        "The player has completed a moral questionnaire. Two gods now watch them. " +
        "Your role is to craft 12 dialogue encounters that TEST the player's declared identity.\n\n" +
        "RULES:\n" +
        "- Each encounter uses one of the 8 provided characters (referenced by characterId).\n" +
        "- Use each character at least once; you may reuse characters for a second encounter.\n" +
        "- Each encounter has a \"promptText\": 1-3 sentences, cryptic and solemn, in the voice of the character.\n" +
        "- Each encounter has 2 or 3 \"choices\" from this list ONLY: Help, Harm, Ignore, Sacrifice, Refuse.\n" +
        "- Choices must create genuine moral tension. The \"right\" answer should never be obvious.\n" +
        "- If the player declared Action+Empathy, present situations where helping costs dearly or causes harm elsewhere.\n" +
        "- If the player declared Action+Detachment, test whether they maintain cold logic when a sympathetic figure suffers.\n" +
        "- If the player declared Inaction+Empathy, test whether they can truly stand witness without intervening.\n" +
        "- If the player declared Inaction+Detachment, test whether their detachment is authentic or just avoidance.\n" +
        "- hpDelta: negative = damage, positive = healing. Range: -15 to +10. Helping should often cost HP.\n" +
        "- reward must be one of: None, HpGain, VisionGain, Buff. rewardValue: 0-5.\n" +
        "- Write in English. Tone: fragments, no modern language, like the Skull Knight in Berserk.\n" +
        "- The first 4 dialogues (indices 0-3) are for Level 1 (simpler dilemmas).\n" +
        "- The last 8 dialogues (indices 4-11) are for Level 2 (harder, more layered).\n\n" +
        "Respond with ONLY valid JSON matching the schema. No commentary, no markdown.";

    public const string JsonSchema =
        "{\n" +
        "  \"type\": \"dialogue_batch\",\n" +
        "  \"schema_version\": \"2.0\",\n" +
        "  \"dialogues\": [\n" +
        "    {\n" +
        "      \"characterId\": \"<string, one of the 8 character IDs>\",\n" +
        "      \"promptText\": \"<string, 1-3 sentences, character voice>\",\n" +
        "      \"choices\": [\n" +
        "        {\n" +
        "          \"choiceType\": \"<Help|Harm|Ignore|Sacrifice|Refuse>\",\n" +
        "          \"label\": \"<string, short action label, 2-5 words>\",\n" +
        "          \"resultText\": \"<string, 1-2 sentences, what happens>\",\n" +
        "          \"hpDelta\": \"<int, -15 to +10>\",\n" +
        "          \"reward\": \"<None|HpGain|VisionGain|Buff>\",\n" +
        "          \"rewardValue\": \"<int, 0-5>\"\n" +
        "        }\n" +
        "      ]\n" +
        "    }\n" +
        "  ]\n" +
        "}";

    public const string SchemaVersion = "2.0";
}
