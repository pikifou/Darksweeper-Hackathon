using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlayerProfile;

namespace Questionnaire.Presentation
{
    /// <summary>
    /// Maps a god ID (from gods.json) to a portrait sprite.
    /// Assign these in the Inspector.
    /// </summary>
    [Serializable]
    public struct GodPortraitEntry
    {
        [Tooltip("Must match the god 'id' field in gods.json (e.g. ash_bearer, iron_judge, …)")]
        public string godId;
        public Sprite portrait;
    }

    /// <summary>
    /// Displays cryptic god phrases and a ChatGPT-generated narrator phrase
    /// on the black screen after the questionnaire outro.
    /// 
    /// Sequence: primary god phrase → secondary god phrase → narrator phrase → fade out all.
    /// The narrator phrase arrives asynchronously (LLM call).
    /// If it hasn't arrived yet when needed, waits with "..." until it does.
    /// </summary>
    public class GodRevealController : MonoBehaviour
    {
        [Header("Panels (CanvasGroup on each — fades text + background together)")]
        [SerializeField] private CanvasGroup primaryPanel;
        [SerializeField] private CanvasGroup secondaryPanel;
        [SerializeField] private CanvasGroup narratorPanel;

        [Header("Text Elements (inside the panels)")]
        [SerializeField] private TextMeshProUGUI primaryPhraseText;
        [SerializeField] private TextMeshProUGUI secondaryPhraseText;
        [SerializeField] private TextMeshProUGUI narratorPhraseText;

        [Header("God Name Labels (optional — inside the panels)")]
        [Tooltip("TMP text to show the primary god's name. Leave empty to skip.")]
        [SerializeField] private TextMeshProUGUI primaryGodNameText;
        [Tooltip("TMP text to show the secondary god's name. Leave empty to skip.")]
        [SerializeField] private TextMeshProUGUI secondaryGodNameText;

        [Header("God Portraits (optional — Image components inside the panels)")]
        [Tooltip("Image component in the primary panel to display the god portrait. Leave empty to skip.")]
        [SerializeField] private Image primaryPortraitImage;
        [Tooltip("Image component in the secondary panel to display the god portrait. Leave empty to skip.")]
        [SerializeField] private Image secondaryPortraitImage;

        [Header("Portrait Lookup")]
        [Tooltip("Map each god ID to its portrait sprite. Must cover all 4 gods.")]
        [SerializeField] private GodPortraitEntry[] godPortraits;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 1.5f;
        [Tooltip("How long the primary god phrase stays visible (in seconds).")]
        [SerializeField] private float pauseAfterPrimary = 5.0f;
        [Tooltip("How long both god phrases stay visible together (in seconds).")]
        [SerializeField] private float pauseAfterSecondary = 5.0f;
        [Tooltip("How long the narrator phrase stays visible (in seconds).")]
        [SerializeField] private float pauseAfterNarrator = 5.0f;
        [SerializeField] private float fadeOutDuration = 1.5f;

        /// <summary>
        /// Fired when the entire reveal sequence is finished (after narrator).
        /// </summary>
        public event Action OnRevealComplete;

        // Narrator text arrives asynchronously from the LLM
        private string narratorText;
        private bool narratorReceived;
        private bool narratorFailed;

        private void Awake()
        {
            HidePanel(primaryPanel);
            HidePanel(secondaryPanel);
            HidePanel(narratorPanel);
        }

        /// <summary>
        /// Called by the flow controller when the LLM narrator response arrives.
        /// Can be called at any time — before or during the reveal sequence.
        /// </summary>
        public void SetNarratorText(string text)
        {
            narratorText = text;
            narratorReceived = true;
            Debug.Log($"[GodReveal] Narrator text received: {text}");
        }

        /// <summary>
        /// Called by the flow controller if the LLM request fails.
        /// The reveal will skip the narrator phrase.
        /// </summary>
        public void SetNarratorFailed()
        {
            narratorFailed = true;
            Debug.Log("[GodReveal] Narrator request failed. Will skip narrator phrase.");
        }

        /// <summary>
        /// Starts the god reveal sequence on the black screen.
        /// Assigns portraits and names based on the god data.
        /// </summary>
        public void ShowReveal(GodData primaryGod, GodData secondaryGod)
        {
            // ── Text ─────────────────────────────────────
            primaryPhraseText.text = primaryGod.PrimaryPhrase;
            secondaryPhraseText.text = secondaryGod.SecondaryPhrase;

            // ── Names (optional) ─────────────────────────
            if (primaryGodNameText != null)
                primaryGodNameText.text = primaryGod.Name;
            if (secondaryGodNameText != null)
                secondaryGodNameText.text = secondaryGod.Name;

            // ── Portraits (optional) ─────────────────────
            if (primaryPortraitImage != null)
            {
                Sprite primarySprite = FindPortrait(primaryGod.Id);
                primaryPortraitImage.sprite = primarySprite;
                primaryPortraitImage.enabled = primarySprite != null;
            }
            if (secondaryPortraitImage != null)
            {
                Sprite secondarySprite = FindPortrait(secondaryGod.Id);
                secondaryPortraitImage.sprite = secondarySprite;
                secondaryPortraitImage.enabled = secondarySprite != null;
            }

            StartCoroutine(RevealSequence());
        }

        /// <summary>
        /// Looks up the portrait sprite for a given god ID from the serialized array.
        /// Returns null if not found.
        /// </summary>
        private Sprite FindPortrait(string godId)
        {
            if (godPortraits == null) return null;
            foreach (var entry in godPortraits)
            {
                if (string.Equals(entry.godId, godId, StringComparison.OrdinalIgnoreCase))
                    return entry.portrait;
            }
            Debug.LogWarning($"[GodReveal] No portrait found for god '{godId}'. " +
                             "Make sure the godPortraits array in the Inspector covers all gods.");
            return null;
        }

        private IEnumerator RevealSequence()
        {
            // ── Phase 1: Primary god phrase ────────────────────
            ShowPanel(primaryPanel);
            yield return FadePanel(primaryPanel, 0f, 1f, fadeInDuration);
            yield return new WaitForSeconds(pauseAfterPrimary);

            // ── Phase 2: Secondary god phrase ──────────────────
            ShowPanel(secondaryPanel);
            yield return FadePanel(secondaryPanel, 0f, 1f, fadeInDuration);
            yield return new WaitForSeconds(pauseAfterSecondary);

            // ── Phase 3: Fade out god phrases ──────────────────
            StartCoroutine(FadePanel(primaryPanel, 1f, 0f, fadeOutDuration));
            yield return FadePanel(secondaryPanel, 1f, 0f, fadeOutDuration);
            HidePanel(primaryPanel);
            HidePanel(secondaryPanel);

            // ── Phase 4: Narrator phrase (from ChatGPT) ────────
            if (!narratorFailed)
            {
                // Wait for the narrator text if it hasn't arrived yet
                if (!narratorReceived)
                {
                    narratorPhraseText.text = "...";
                    ShowPanel(narratorPanel);
                    narratorPanel.alpha = 1f;

                    Debug.Log("[GodReveal] Waiting for narrator text from LLM...");

                    while (!narratorReceived && !narratorFailed)
                    {
                        yield return null;
                    }
                }

                if (narratorReceived && !string.IsNullOrEmpty(narratorText))
                {
                    // Show the narrator phrase
                    narratorPhraseText.text = narratorText;
                    ShowPanel(narratorPanel);
                    narratorPanel.alpha = 0f;

                    yield return FadePanel(narratorPanel, 0f, 1f, fadeInDuration);
                    yield return new WaitForSeconds(pauseAfterNarrator);

                    // Fade out
                    yield return FadePanel(narratorPanel, 1f, 0f, fadeOutDuration);
                    HidePanel(narratorPanel);
                }
            }

            // ── Done ───────────────────────────────────────────
            Debug.Log("[GodReveal] Reveal sequence complete.");
            OnRevealComplete?.Invoke();
        }

        // ── Panel helpers ────────────────────────────────────────

        private IEnumerator FadePanel(CanvasGroup panel, float fromAlpha, float toAlpha, float duration)
        {
            if (panel == null) yield break;

            panel.alpha = fromAlpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                panel.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                yield return null;
            }

            panel.alpha = toAlpha;
        }

        private static void ShowPanel(CanvasGroup panel)
        {
            if (panel == null) return;
            panel.alpha = 0f;
            panel.gameObject.SetActive(true);
        }

        private static void HidePanel(CanvasGroup panel)
        {
            if (panel == null) return;
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }
    }
}
