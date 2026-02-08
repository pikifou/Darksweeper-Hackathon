using System;
using Mines.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Mines.Presentation
{
    /// <summary>
    /// Single UGUI modal panel for all 4 mine event types.
    /// Data-driven: renders whatever <see cref="InteractionDescriptor"/> provides.
    ///
    /// All UI elements are wired via the Inspector from a prefab.
    /// Use <b>DarkSweeper &gt; Create Mine Event Panel Prefab</b> to generate the
    /// initial prefab, then customise it freely in the editor.
    /// </summary>
    public class MineEventPanel : MonoBehaviour
    {
        // ================================================================
        // Inspector References — wire these from your prefab
        // ================================================================

        [Header("Root")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Overlay")]
        [SerializeField] private Image darkOverlay;

        [Header("Event Frame")]
        [SerializeField] private RectTransform eventFrame;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Choices")]
        [Tooltip("Assign up to 3 buttons. Only visible choices will be active.")]
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TextMeshProUGUI[] choiceLabels;

        [Header("Video")]
        [Tooltip("Optional VideoPlayer for character intro clips. Plays once and pauses on last frame.")]
        [SerializeField] private VideoPlayer videoPlayer;
        [Tooltip("Optional RawImage to display the VideoPlayer's RenderTexture output.")]
        [SerializeField] private RawImage videoDisplay;

        [Header("Result Area")]
        [SerializeField] private RectTransform resultArea;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI hpDeltaText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Button continueButton;

        // ================================================================
        // Runtime state
        // ================================================================

        private Action<PlayerChoice> onChoiceCallback;
        private Action onContinueCallback;
        private PlayerChoice[] buttonChoices;

        private int MaxChoices => choiceButtons != null ? choiceButtons.Length : 0;

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Awake()
        {
            EnsureEventSystem();
            WireButtonCallbacks();
            Hide();
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>Show the interaction panel with choices, plus an optional intro video.</summary>
        public void Show(InteractionDescriptor descriptor, Action<PlayerChoice> onChoice, VideoClip introClip = null)
        {
            onChoiceCallback = onChoice;

            if (canvas != null) canvas.enabled = true;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            // Title & description
            if (titleText != null) titleText.text = descriptor.title ?? "";
            if (descriptionText != null) descriptionText.text = descriptor.description ?? "";

            // Play character intro video (if any)
            PlayIntroVideo(introClip);

            // Choice buttons
            int numChoices = descriptor.choices != null ? Mathf.Min(descriptor.choices.Length, MaxChoices) : 0;
            for (int i = 0; i < MaxChoices; i++)
            {
                if (i < numChoices)
                {
                    choiceButtons[i].gameObject.SetActive(true);
                    choiceLabels[i].text = descriptor.choices[i].label;
                    if (!string.IsNullOrEmpty(descriptor.choices[i].riskHint))
                        choiceLabels[i].text += $"  <size=80%><color=#888>({descriptor.choices[i].riskHint})</color></size>";
                    buttonChoices[i] = descriptor.choices[i].choice;
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }

            // Hide result area
            if (resultArea != null) resultArea.gameObject.SetActive(false);
        }

        /// <summary>Show the result after resolution.</summary>
        public void ShowResult(ResolutionResult result, string rewardDescription, bool wasLeftClickPenalty, Action onContinue)
        {
            onContinueCallback = onContinue;

            // Hide choice buttons
            for (int i = 0; i < MaxChoices; i++)
                choiceButtons[i].gameObject.SetActive(false);

            // Show result area
            if (resultArea != null) resultArea.gameObject.SetActive(true);

            // Result text
            string fullResult = "";
            if (wasLeftClickPenalty)
                fullResult = "<color=#FF4444>Clic gauche accidentel !</color>\n\n";
            fullResult += result.resultText ?? "";
            if (resultText != null) resultText.text = fullResult;

            // HP delta
            if (hpDeltaText != null)
            {
                if (result.hpDelta != 0)
                {
                    string sign = result.hpDelta > 0 ? "+" : "";
                    string color = result.hpDelta > 0 ? "#44FF44" : "#FF4444";
                    hpDeltaText.text = $"<color={color}>{sign}{result.hpDelta} PV</color>";
                    hpDeltaText.gameObject.SetActive(true);
                }
                else
                {
                    hpDeltaText.gameObject.SetActive(false);
                }
            }

            // Reward
            if (rewardText != null)
            {
                if (!string.IsNullOrEmpty(rewardDescription))
                {
                    rewardText.text = $"<color=#FFDD44>{rewardDescription}</color>";
                    rewardText.gameObject.SetActive(true);
                }
                else
                {
                    rewardText.gameObject.SetActive(false);
                }
            }

            // Continue button
            if (continueButton != null) continueButton.gameObject.SetActive(true);
        }

        /// <summary>Hide the panel.</summary>
        public void Hide()
        {
            StopVideo();

            if (canvas != null) canvas.enabled = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            onChoiceCallback = null;
            onContinueCallback = null;
        }

        // ================================================================
        // Video: intro clip plays once, then freezes on last frame
        // ================================================================

        /// <summary>
        /// Play the character intro clip. Pauses on the last frame so the
        /// character visual stays on screen while the player reads and chooses.
        /// </summary>
        private void PlayIntroVideo(VideoClip clip)
        {
            if (videoPlayer == null) return;

            if (clip == null)
            {
                // No clip — hide the video area
                if (videoDisplay != null) videoDisplay.gameObject.SetActive(false);
                videoPlayer.Stop();
                return;
            }

            // Show the video display
            if (videoDisplay != null) videoDisplay.gameObject.SetActive(true);

            // Configure: play once, no loop
            videoPlayer.isLooping = false;
            videoPlayer.clip = clip;

            // Reset to beginning
            videoPlayer.Stop();
            videoPlayer.time = 0;
            videoPlayer.frame = 0;

            // Subscribe to end-of-clip to pause on last frame
            videoPlayer.loopPointReached -= OnIntroVideoFinished;
            videoPlayer.loopPointReached += OnIntroVideoFinished;

            videoPlayer.Play();
        }

        private void OnIntroVideoFinished(VideoPlayer vp)
        {
            // Pause so the last frame stays visible
            vp.Pause();
            vp.loopPointReached -= OnIntroVideoFinished;
        }

        private void StopVideo()
        {
            if (videoPlayer == null) return;
            videoPlayer.loopPointReached -= OnIntroVideoFinished;
            videoPlayer.Stop();
        }

        // ================================================================
        // Button wiring
        // ================================================================

        private void WireButtonCallbacks()
        {
            if (choiceButtons == null) return;
            buttonChoices = new PlayerChoice[choiceButtons.Length];

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                int idx = i; // capture
                choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(idx));
            }

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void OnChoiceClicked(int index)
        {
            if (buttonChoices == null || index < 0 || index >= buttonChoices.Length) return;
            onChoiceCallback?.Invoke(buttonChoices[index]);
        }

        private void OnContinueClicked()
        {
            onContinueCallback?.Invoke();
        }

        // ================================================================
        // EventSystem safety net
        // ================================================================

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[MineEventPanel] Created EventSystem (was missing from scene).");
        }
    }
}
