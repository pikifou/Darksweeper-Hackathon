using UnityEngine;

/// <summary>
/// Holds the OpenAI API configuration. This asset must be gitignored.
/// </summary>
[CreateAssetMenu(fileName = "LLMConfig", menuName = "LLM Demo/LLM Config")]
public class LLMConfigSO : ScriptableObject
{
    [Tooltip("Your OpenAI API key. Do NOT commit this.")]
    public string apiKey = "";

    [Tooltip("Model name, e.g. gpt-5")]
    public string model = "gpt-5";

    [Tooltip("OpenAI Chat Completions endpoint")]
    public string endpoint = "https://api.openai.com/v1/chat/completions";

    [Range(0f, 2f)]
    public float temperature = 0.2f;

    public int maxTokens = 4096;
}
