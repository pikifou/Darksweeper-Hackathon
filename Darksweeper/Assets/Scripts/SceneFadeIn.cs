using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a GameObject in ANY scene to get a fade-from-black on load.
/// Requires a fullscreen Image (black, alpha = 1) assigned in the Inspector.
/// The Image fades out over <see cref="fadeDuration"/> seconds, then disables itself.
/// </summary>
public class SceneFadeIn : MonoBehaviour
{
    [Tooltip("Fullscreen black Image overlay (alpha starts at 1, fades to 0).")]
    [SerializeField] private Image fadeOverlay;

    [Tooltip("How long the fade-from-black takes (in seconds).")]
    [SerializeField] private float fadeDuration = 1.5f;

    [Tooltip("Optional delay before the fade starts (in seconds).")]
    [SerializeField] private float delayBeforeFade = 0f;

    private void Start()
    {
        if (fadeOverlay == null)
        {
            Debug.LogWarning("[SceneFadeIn] No fadeOverlay assigned. Disabling.");
            enabled = false;
            return;
        }

        // Make sure the overlay starts fully opaque
        Color c = fadeOverlay.color;
        c.a = 1f;
        fadeOverlay.color = c;
        fadeOverlay.gameObject.SetActive(true);

        StartCoroutine(FadeFromBlack());
    }

    private IEnumerator FadeFromBlack()
    {
        if (delayBeforeFade > 0f)
            yield return new WaitForSeconds(delayBeforeFade);

        Color c = fadeOverlay.color;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            fadeOverlay.color = c;
            yield return null;
        }

        c.a = 0f;
        fadeOverlay.color = c;
        fadeOverlay.gameObject.SetActive(false);

        Debug.Log("[SceneFadeIn] Fade from black complete.");
    }
}
