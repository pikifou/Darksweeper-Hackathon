using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends a JSON request to the OpenAI Chat Completions API via UnityWebRequest.
/// Coroutine-based. Handles timeout, network errors, and non-200 responses.
/// </summary>
public static class LLMClient
{
    private const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// Sends the request and calls onSuccess with the raw response body,
    /// or onError with an error message.
    /// </summary>
    /// <param name="timeoutSeconds">Request timeout in seconds. 0 = use default (30s).</param>
    public static IEnumerator SendRequest(string jsonBody, LLMConfigSO config,
        Action<string> onSuccess, Action<string> onError, int timeoutSeconds = 0)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        int timeout = timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds;

        using var request = new UnityWebRequest(config.endpoint, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {config.apiKey}");
        request.timeout = timeout;

        Debug.Log("[LLMClient] Sending request...");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error = $"[LLMClient] Error: {request.error}";
            if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
            {
                error += $"\nResponse: {request.downloadHandler.text}";
            }
            Debug.Log(error);
            onError?.Invoke(error);
            yield break;
        }

        string responseText = request.downloadHandler.text;
        Debug.Log($"[LLMClient] Response received ({responseText.Length} chars)");
        onSuccess?.Invoke(responseText);
    }
}
