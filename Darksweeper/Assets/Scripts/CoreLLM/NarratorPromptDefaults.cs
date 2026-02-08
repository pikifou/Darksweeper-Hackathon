/// <summary>
/// Default prompt values for the narrator PromptTemplateSO asset.
/// 
/// SETUP: In Unity, create a new PromptTemplate asset:
///   Right-click Assets/Data/ → Create > LLM Demo > Prompt Template
///   Rename to "PromptTemplate_Narrator"
///   Copy the values from this class into the asset fields.
///   Or leave the asset blank — QuestionnaireFlowController will use these defaults
///   if the asset fields are empty.
/// </summary>
public static class NarratorPromptDefaults
{
    public const string SystemPrompt =
        "You are a narrator in a dark fantasy world. " +
        "You speak in cryptic, solemn fragments — like the Skull Knight in Berserk. " +
        "Never explain directly. Never use modern language. " +
        "You are addressing a player who has just declared their moral identity through a ritual questionnaire. " +
        "Two gods now watch them: a primary god (their dominant nature) and a secondary god (their tension). " +
        "Based on the player's answers, scores, and assigned gods, describe in 2-3 sentences what these gods expect " +
        "and what awaits the player. Be specific to the gods assigned — reference their nature. " +
        "Respond with ONLY valid JSON matching the schema. No commentary, no markdown.";

    public const string JsonSchema =
        "{\n" +
        "  \"type\": \"narrator_intro\",\n" +
        "  \"schema_version\": \"1.0\",\n" +
        "  \"text\": \"<string, 2-3 sentences, cryptic narrator voice>\"\n" +
        "}";

    public const string SchemaVersion = "1.0";
}
