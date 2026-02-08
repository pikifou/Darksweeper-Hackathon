using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Orchestrates the LLM tech demo pipeline.
/// Minimal UI: one TMP input field + one send button.
/// All feedback goes to Debug.Log in the Console.
/// </summary>
public class Orchestrator : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField intentInput;
    [SerializeField] private Button sendButton;

    [Header("Data")]
    [SerializeField] private GameStateSO gameStateSO;
    [SerializeField] private LLMConfigSO llmConfig;
    [SerializeField] private PromptTemplateSO promptTemplate;

    private GameStateModel currentState;
    private bool isSending;

    private void Awake()
    {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendClicked);
    }

    private void Start()
    {
        if (gameStateSO == null)
        {
            Debug.LogWarning("[Orchestrator] No GameStateSO assigned.");
            return;
        }

        // Try loading from save file first, fall back to SO defaults
        currentState = SaveManager.Load();
        if (currentState != null)
        {
            GameStateMapper.ApplyToSO(currentState, gameStateSO);
            Debug.Log($"[GameState] Loaded from save: {currentState}");
        }
        else
        {
            currentState = GameStateMapper.FromSO(gameStateSO);
            Debug.Log($"[GameState] Loaded from SO defaults: {currentState}");
        }
    }

    private void Update()
    {
        // Press R to reset state to SO defaults (deletes save)
        if (Input.GetKeyDown(KeyCode.R) && !isSending && !intentInput.isFocused)
        {
            ResetState();
        }
    }

    private void ResetState()
    {
        SaveManager.DeleteSave();
        currentState = GameStateMapper.FromSO(gameStateSO);
        Debug.Log($"[Orchestrator] State reset to SO defaults: {currentState}");
    }

    private void OnSendClicked()
    {
        if (isSending)
        {
            Debug.LogWarning("[Orchestrator] Already sending. Please wait.");
            return;
        }

        string intent = intentInput != null ? intentInput.text : "";

        if (currentState == null)
        {
            Debug.LogError("[Orchestrator] No game state loaded. Cannot send.");
            return;
        }

        if (llmConfig == null || promptTemplate == null)
        {
            Debug.LogError("[Orchestrator] LLM Config or Prompt Template not assigned.");
            return;
        }

        lastIntent = intent;
        string requestBody = RequestBuilder.Build(intent, currentState, promptTemplate, llmConfig);
        Debug.Log($"[Orchestrator] Status: Sending intent: \"{intent}\"");

        isSending = true;
        StartCoroutine(LLMClient.SendRequest(requestBody, llmConfig, OnLLMSuccess, OnLLMError));
    }

    private void OnLLMSuccess(string rawResponse)
    {
        isSending = false;
        Debug.Log($"[Orchestrator] Raw API response:\n{rawResponse}");

        if (ResponseParser.TryParse(rawResponse, out GameStateModel newState, out string parseError))
        {
            currentState = newState;
            GameStateMapper.ApplyToSO(currentState, gameStateSO);
            SaveManager.Save(currentState);
            LogResult("Success", null);
        }
        else
        {
            LogResult("Failed", parseError);
            Debug.Log($"[GameState] Unchanged: {currentState}");
        }
    }

    private void OnLLMError(string error)
    {
        isSending = false;
        LogResult("Failed", error);
        Debug.Log($"[GameState] Unchanged: {currentState}");
    }

    private string lastIntent;

    private void LogResult(string status, string error)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string summary = $"[{timestamp}] Intent: \"{lastIntent}\" → {status}";
        if (error != null)
            summary += $" — {error}";

        Debug.Log(summary);

        if (status == "Success")
            Debug.Log($"[GameState] Updated: {currentState}");
    }
}
