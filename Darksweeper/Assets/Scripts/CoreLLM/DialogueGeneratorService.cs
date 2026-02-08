using System;
using System.Collections;
using Mines.Flow;
using PlayerProfile;
using UnityEngine;

/// <summary>
/// Orchestrates LLM-based dialogue generation.
/// Sends the player profile + 8 character descriptions to the LLM,
/// parses the response into 12 <see cref="DialogueEncounterSO"/> instances.
/// Falls back to pre-made assets if the LLM fails (timeout, offline, parse error).
///
/// Attach to a persistent GameObject (or the same one as QuestionnaireFlowController).
/// </summary>
public class DialogueGeneratorService : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────

    [Header("Characters (the 8 fixed NPCs)")]
    [Tooltip("Drag the 8 DialogueCharacterSO assets here.")]
    [SerializeField] private DialogueCharacterSO[] characters;

    [Header("LLM Config")]
    [SerializeField] private LLMConfigSO llmConfig;

    [Tooltip("Prompt template for dialogue generation. Leave empty to use built-in defaults.")]
    [SerializeField] private PromptTemplateSO dialoguePromptTemplate;

    [Tooltip("Max tokens override for dialogue generation (default 8192). Set to 0 to use the LLMConfig value.")]
    [SerializeField] private int maxTokensOverride = 8192;

    [Header("Fallback (offline / LLM failure)")]
    [Tooltip("12 pre-made DialogueEncounterSO assets (4 for level 1, 8 for level 2).")]
    [SerializeField] private DialogueEncounterSO[] fallbackDialogues;

    // ── Events ─────────────────────────────────────────────────

    /// <summary>
    /// Fired when dialogue generation is complete.
    /// The array contains 12 dialogues (indices 0-3 = level 1, 4-11 = level 2).
    /// The bool indicates whether these are LLM-generated (true) or fallback (false).
    /// </summary>
    public event Action<DialogueEncounterSO[], bool> OnDialoguesReady;

    // ── Runtime ────────────────────────────────────────────────

    private DialogueEncounterSO[] generatedDialogues;
    private bool isGenerating;

    private void Awake()
    {
        // Persist across scene loads so the level scene can read generated dialogues
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>The generated (or fallback) dialogues, available after <see cref="OnDialoguesReady"/>.</summary>
    public DialogueEncounterSO[] Dialogues => generatedDialogues;

    /// <summary>True if the last generation used the LLM (not fallback).</summary>
    public bool UsedLLM { get; private set; }

    // ── Public API ─────────────────────────────────────────────

    /// <summary>
    /// Starts generating 12 dialogues via the LLM.
    /// Call this after the questionnaire is complete and the profile is ready.
    /// </summary>
    public void Generate(PlayerProfileData profile)
    {
        if (isGenerating)
        {
            Debug.LogWarning("[DialogueGen] Generation already in progress. Ignoring duplicate call.");
            return;
        }

        if (profile == null)
        {
            Debug.LogError("[DialogueGen] Cannot generate dialogues without a player profile.");
            UseFallback();
            return;
        }

        if (llmConfig == null || string.IsNullOrEmpty(llmConfig.apiKey))
        {
            Debug.LogWarning("[DialogueGen] No LLM config or API key. Using fallback.");
            UseFallback();
            return;
        }

        isGenerating = true;
        StartCoroutine(GenerateCoroutine(profile));
    }

    /// <summary>
    /// Returns the level-1 dialogues (indices 0-3).
    /// Available after <see cref="OnDialoguesReady"/>.
    /// </summary>
    public DialogueEncounterSO[] GetLevel1Dialogues()
    {
        if (generatedDialogues == null) return new DialogueEncounterSO[0];
        int count = Mathf.Min(4, generatedDialogues.Length);
        var result = new DialogueEncounterSO[count];
        Array.Copy(generatedDialogues, 0, result, 0, count);
        return result;
    }

    /// <summary>
    /// Returns the level-2 dialogues (indices 4-11).
    /// Available after <see cref="OnDialoguesReady"/>.
    /// </summary>
    public DialogueEncounterSO[] GetLevel2Dialogues()
    {
        if (generatedDialogues == null || generatedDialogues.Length <= 4)
            return new DialogueEncounterSO[0];

        int count = generatedDialogues.Length - 4;
        var result = new DialogueEncounterSO[count];
        Array.Copy(generatedDialogues, 4, result, 0, count);
        return result;
    }

    // ── Generation Coroutine ───────────────────────────────────

    private IEnumerator GenerateCoroutine(PlayerProfileData profile)
    {
        Debug.Log("[DialogueGen] Building LLM request for 12 dialogues...");

        string requestBody = DialogueRequestBuilder.Build(
            profile,
            characters,
            dialoguePromptTemplate,
            llmConfig,
            maxTokensOverride);

        Debug.Log($"[DialogueGen] Request built ({requestBody.Length} chars). Sending to LLM...");

        string rawResponse = null;
        string error = null;
        bool done = false;

        yield return LLMClient.SendRequest(requestBody, llmConfig,
            onSuccess: (response) => { rawResponse = response; done = true; },
            onError: (err) => { error = err; done = true; });

        // Wait for completion (should already be done after yield)
        while (!done) yield return null;

        isGenerating = false;

        if (error != null)
        {
            Debug.LogWarning($"[DialogueGen] LLM request failed: {error}");
            UseFallback();
            yield break;
        }

        // Parse the response
        if (DialogueResponseParser.TryParse(rawResponse, characters,
            out DialogueEncounterSO[] dialogues, out string parseError))
        {
            generatedDialogues = dialogues;
            UsedLLM = true;
            Debug.Log($"[DialogueGen] Successfully generated {dialogues.Length} dialogues from LLM.");
            OnDialoguesReady?.Invoke(generatedDialogues, true);
        }
        else
        {
            Debug.LogWarning($"[DialogueGen] LLM response parse failed: {parseError}");
            UseFallback();
        }
    }

    // ── Fallback ───────────────────────────────────────────────

    private void UseFallback()
    {
        isGenerating = false;
        UsedLLM = false;

        if (fallbackDialogues != null && fallbackDialogues.Length > 0)
        {
            generatedDialogues = fallbackDialogues;
            Debug.Log($"[DialogueGen] Using {fallbackDialogues.Length} fallback dialogue assets.");
        }
        else
        {
            generatedDialogues = new DialogueEncounterSO[0];
            Debug.LogWarning("[DialogueGen] No fallback dialogues assigned! Dialogues will be empty.");
        }

        OnDialoguesReady?.Invoke(generatedDialogues, false);
    }
}
