using System;
using System.Collections;
using Mines.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Mines.Presentation
{
    /// <summary>
    /// Non-blocking toast notification for mine event results.
    /// Slides up from the bottom of the screen, displays for a few seconds,
    /// then slides back down. Supports an optional <see cref="VideoClip"/>.
    ///
    /// Used for Combat, Chest, and Shrine events (quick auto-dismiss).
    /// Also used for Dialogue events in two-phase mode:
    ///   Phase 1 — intro video + choice buttons fade in
    ///   Phase 2 — result video + outcome text, then auto-dismiss
    ///
    /// All UI elements are wired via the Inspector from a prefab.
    /// Use <b>DarkSweeper &gt; Create Mine Event Toast Prefab</b> to generate it.
    /// </summary>
    public class MineEventToast : MonoBehaviour
    {
        // ================================================================
        // Inspector References — wire from prefab
        // ================================================================

        [Header("Root")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform toastContainer;

        [Header("Video Area")]
        [SerializeField] private RawImage videoImage;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private Image placeholderIcon;
        [SerializeField] private TextMeshProUGUI placeholderIconText;

        [Header("Cartouche")]
        [SerializeField] private TextMeshProUGUI iconText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI hpDeltaText;
        [SerializeField] private TextMeshProUGUI rewardText;

        [Header("Dialogue Choices")]
        [Tooltip("Up to 3 choice buttons for dialogue events. Hidden for quick events.")]
        [SerializeField] private Button[] choiceButtons = new Button[3];
        [SerializeField] private TextMeshProUGUI[] choiceLabels = new TextMeshProUGUI[3];
        [SerializeField] private CanvasGroup choiceGroup;

        [Header("Timing")]
        [SerializeField] private float displayDuration = 3.5f;
        [SerializeField] private float slideSpeed = 600f; // pixels per second

        [Header("Dialogue Timing")]
        [Tooltip("Delay before choice buttons fade in (seconds).")]
        [SerializeField] private float choiceFadeInDelay = 1.5f;
        [Tooltip("Duration of choice buttons fade-in animation (seconds).")]
        [SerializeField] private float choiceFadeInDuration = 0.5f;

        // ================================================================
        // Runtime state
        // ================================================================

        private Coroutine activeRoutine;
        private float hiddenY;  // Y position when fully hidden (below screen)
        private float shownY;   // Y position when fully visible

        // Dialogue state
        private Action<PlayerChoice, VideoClip> dialogueChoiceCallback;
        private PlayerChoice[] buttonChoiceMap;
        private VideoClip[] buttonResultClips;
        private Action dialogueDismissCallback;

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Awake()
        {
            if (toastContainer != null)
            {
                // Cache positions: shown = current anchored Y, hidden = off-screen below
                shownY = toastContainer.anchoredPosition.y;
                hiddenY = shownY - toastContainer.rect.height - 50f;

                // Start hidden
                var pos = toastContainer.anchoredPosition;
                pos.y = hiddenY;
                toastContainer.anchoredPosition = pos;
            }

            if (canvas != null) canvas.enabled = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            // Wire choice button click handlers
            buttonChoiceMap = new PlayerChoice[3];
            buttonResultClips = new VideoClip[3];
            for (int i = 0; i < choiceButtons.Length && i < 3; i++)
            {
                if (choiceButtons[i] != null)
                {
                    int idx = i; // capture for closure
                    choiceButtons[i].onClick.AddListener(() => OnChoiceButtonClicked(idx));
                }
            }

            HideChoiceButtons();
        }

        // ================================================================
        // Public API — Quick Toast (Combat / Chest / Shrine)
        // ================================================================

        /// <summary>
        /// Show a toast with the event resolution. Non-blocking: the player can
        /// keep playing while the toast is visible. Auto-dismisses after
        /// <see cref="displayDuration"/> seconds.
        /// </summary>
        public void Show(MineEventType eventType, string title,
                         ResolutionResult result, string rewardDesc,
                         bool wasLeftClickPenalty, VideoClip clip = null)
        {
            StopActiveRoutine();
            EnableCanvas(false); // non-blocking for quick events
            HideChoiceButtons();

            PopulateIcon(eventType);
            PopulateTitle(title, wasLeftClickPenalty);
            PopulateResult(result, wasLeftClickPenalty);
            PopulateHPDelta(result.hpDelta);
            PopulateReward(rewardDesc);
            SetupVideo(eventType, clip);

            activeRoutine = StartCoroutine(ToastRoutine(null));
        }

        // ================================================================
        // Public API — Dialogue Toast (two-phase)
        // ================================================================

        /// <summary>
        /// Phase 1: Show the dialogue intro — video + title/description.
        /// Choice buttons fade in after <see cref="choiceFadeInDelay"/>.
        /// Blocks raycasts so the player must choose.
        /// </summary>
        /// <param name="descriptor">Interaction descriptor with title, description, choices.</param>
        /// <param name="introClip">Intro video clip (character's request). Null = placeholder.</param>
        /// <param name="onChoice">Callback when the player clicks a choice. Receives the choice + its result video clip.</param>
        public void ShowDialogue(InteractionDescriptor descriptor, VideoClip introClip,
                                 Action<PlayerChoice, VideoClip> onChoice)
        {
            StopActiveRoutine();
            EnableCanvas(true); // block raycasts during choice phase
            dialogueChoiceCallback = onChoice;

            PopulateIcon(MineEventType.Dialogue);

            // Title = character name
            if (titleText != null)
                titleText.text = descriptor.title ?? "";

            // Description in result area (repurposed for intro text)
            if (resultText != null)
                resultText.text = descriptor.description ?? "";

            // Hide result fields during intro phase
            if (hpDeltaText != null) hpDeltaText.gameObject.SetActive(false);
            if (rewardText != null) rewardText.gameObject.SetActive(false);

            SetupVideo(MineEventType.Dialogue, introClip, freezeOnLastFrame: true);
            SetupChoiceButtons(descriptor);

            activeRoutine = StartCoroutine(DialogueIntroRoutine());
        }

        /// <summary>
        /// Phase 2: Show the dialogue result — switch video, display outcome.
        /// Auto-dismisses after <see cref="displayDuration"/>.
        /// </summary>
        /// <param name="result">Resolution result.</param>
        /// <param name="rewardDesc">Reward description string.</param>
        /// <param name="resultClip">Result video clip. Null = keep current or placeholder.</param>
        /// <param name="onDismiss">Called when the toast finishes dismissing.</param>
        public void ShowDialogueResult(ResolutionResult result, string rewardDesc,
                                       VideoClip resultClip, Action onDismiss = null)
        {
            StopActiveRoutine();
            dialogueDismissCallback = onDismiss;

            HideChoiceButtons();

            // Switch video to result clip
            SetupVideo(MineEventType.Dialogue, resultClip);

            // Populate result content
            PopulateResult(result, false);
            PopulateHPDelta(result.hpDelta);
            PopulateReward(rewardDesc);

            // Unblock raycasts — result phase is non-blocking
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            // Hold for displayDuration then slide down
            activeRoutine = StartCoroutine(ToastRoutine(dialogueDismissCallback));
        }

        /// <summary>
        /// Immediately hide the toast (skip animation).
        /// </summary>
        public void Hide()
        {
            StopActiveRoutine();
            HideChoiceButtons();

            if (toastContainer != null)
            {
                var pos = toastContainer.anchoredPosition;
                pos.y = hiddenY;
                toastContainer.anchoredPosition = pos;
            }

            if (canvas != null) canvas.enabled = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            StopVideo();
            dialogueChoiceCallback = null;
            dialogueDismissCallback = null;
        }

        // ================================================================
        // Choice Buttons
        // ================================================================

        private void SetupChoiceButtons(InteractionDescriptor descriptor)
        {
            // Initially hidden — will fade in via coroutine
            if (choiceGroup != null)
            {
                choiceGroup.alpha = 0f;
                choiceGroup.blocksRaycasts = false;
                choiceGroup.interactable = false;
                choiceGroup.gameObject.SetActive(true);
            }

            int numChoices = descriptor.choices != null ? descriptor.choices.Length : 0;

            for (int i = 0; i < 3; i++)
            {
                if (i < numChoices && choiceButtons[i] != null)
                {
                    choiceButtons[i].gameObject.SetActive(true);
                    buttonChoiceMap[i] = descriptor.choices[i].choice;

                    // Result clip will be set by controller via SetChoiceResultClips (from pool, by action type)
                    buttonResultClips[i] = null;

                    if (choiceLabels[i] != null)
                    {
                        string label = descriptor.choices[i].label;
                        if (!string.IsNullOrEmpty(descriptor.choices[i].riskHint))
                            label += $"  <size=70%><color=#888888>({descriptor.choices[i].riskHint})</color></size>";
                        choiceLabels[i].text = label;
                    }
                }
                else if (choiceButtons[i] != null)
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Set the result video clips for each choice button.
        /// Called by the controller after ShowDialogue, since InteractionDescriptor doesn't carry VideoClips.
        /// </summary>
        public void SetChoiceResultClips(VideoClip[] clips)
        {
            if (clips == null) return;
            for (int i = 0; i < clips.Length && i < 3; i++)
                buttonResultClips[i] = clips[i];
        }

        private void HideChoiceButtons()
        {
            if (choiceGroup != null)
            {
                choiceGroup.alpha = 0f;
                choiceGroup.blocksRaycasts = false;
                choiceGroup.interactable = false;
                choiceGroup.gameObject.SetActive(false);
            }

            for (int i = 0; i < choiceButtons.Length && i < 3; i++)
            {
                if (choiceButtons[i] != null)
                    choiceButtons[i].gameObject.SetActive(false);
            }
        }

        private void OnChoiceButtonClicked(int index)
        {
            if (dialogueChoiceCallback == null) return;

            PlayerChoice choice = buttonChoiceMap[index];
            VideoClip clip = buttonResultClips[index];

            // Disable further clicks
            var callback = dialogueChoiceCallback;
            dialogueChoiceCallback = null;

            callback.Invoke(choice, clip);
        }

        // ================================================================
        // Animation — Quick Toast
        // ================================================================

        private IEnumerator ToastRoutine(Action onDismiss)
        {
            // Slide up (only if not already at shown position)
            float currentY = toastContainer != null ? toastContainer.anchoredPosition.y : shownY;
            if (Mathf.Abs(currentY - shownY) > 1f)
                yield return SlideY(hiddenY, shownY);

            // Hold
            yield return new WaitForSeconds(displayDuration);

            // Slide down
            yield return SlideY(shownY, hiddenY);

            // Fully hidden
            if (canvas != null) canvas.enabled = false;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            StopVideo();
            activeRoutine = null;

            onDismiss?.Invoke();
        }

        // ================================================================
        // Animation — Dialogue Intro (Phase 1)
        // ================================================================

        private IEnumerator DialogueIntroRoutine()
        {
            // Slide up
            yield return SlideY(hiddenY, shownY);

            // Wait before showing choices
            yield return new WaitForSeconds(choiceFadeInDelay);

            // Fade in choice buttons
            if (choiceGroup != null)
            {
                choiceGroup.gameObject.SetActive(true);
                float elapsed = 0f;
                while (elapsed < choiceFadeInDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / choiceFadeInDuration);
                    choiceGroup.alpha = t;
                    yield return null;
                }
                choiceGroup.alpha = 1f;
                choiceGroup.blocksRaycasts = true;
                choiceGroup.interactable = true;
            }

            // Stay visible — wait for player to click a choice
            // (the coroutine ends here; the toast stays up until OnChoiceButtonClicked fires)
            activeRoutine = null;
        }

        // ================================================================
        // Shared Animation
        // ================================================================

        private IEnumerator SlideY(float from, float to)
        {
            if (toastContainer == null) yield break;

            float distance = Mathf.Abs(to - from);
            if (distance < 1f) yield break;

            float duration = distance / slideSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                var pos = toastContainer.anchoredPosition;
                pos.y = Mathf.Lerp(from, to, t);
                toastContainer.anchoredPosition = pos;
                yield return null;
            }

            var final_pos = toastContainer.anchoredPosition;
            final_pos.y = to;
            toastContainer.anchoredPosition = final_pos;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void StopActiveRoutine()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
        }

        private void EnableCanvas(bool blockRaycasts)
        {
            if (canvas != null) canvas.enabled = true;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = blockRaycasts;
                canvasGroup.interactable = blockRaycasts;
            }
        }

        private void PopulateIcon(MineEventType eventType)
        {
            if (iconText != null)
            {
                iconText.text = GetIcon(eventType);
                iconText.color = GetIconColor(eventType);
            }
        }

        private void PopulateTitle(string title, bool wasLeftClickPenalty)
        {
            if (titleText != null)
            {
                string fullTitle = title ?? "";
                if (wasLeftClickPenalty)
                    fullTitle = $"<color=#FF4444>!! </color>{fullTitle}<color=#FF4444> !!</color>";
                titleText.text = fullTitle;
            }
        }

        private void PopulateResult(ResolutionResult result, bool wasLeftClickPenalty)
        {
            if (resultText != null)
            {
                string txt = "";
                if (wasLeftClickPenalty)
                    txt = "<color=#FF4444>Clic gauche accidentel !</color>\n";
                txt += result.resultText ?? "";
                resultText.text = txt;
            }
        }

        private void PopulateHPDelta(int hpDelta)
        {
            if (hpDeltaText != null)
            {
                if (hpDelta != 0)
                {
                    string sign = hpDelta > 0 ? "+" : "";
                    string color = hpDelta > 0 ? "#44FF44" : "#FF4444";
                    hpDeltaText.text = $"<color={color}>{sign}{hpDelta} PV</color>";
                    hpDeltaText.gameObject.SetActive(true);
                }
                else
                {
                    hpDeltaText.gameObject.SetActive(false);
                }
            }
        }

        private void PopulateReward(string rewardDesc)
        {
            if (rewardText != null)
            {
                if (!string.IsNullOrEmpty(rewardDesc))
                {
                    rewardText.text = $"<color=#FFDD44>{rewardDesc}</color>";
                    rewardText.gameObject.SetActive(true);
                }
                else
                {
                    rewardText.gameObject.SetActive(false);
                }
            }
        }

        // ================================================================
        // Video
        // ================================================================

        private void SetupVideo(MineEventType eventType, VideoClip clip, bool freezeOnLastFrame = false)
        {
            // Unsubscribe any previous freeze callback
            if (videoPlayer != null)
                videoPlayer.loopPointReached -= OnVideoFreezeOnLastFrame;

            if (clip != null && videoPlayer != null && videoImage != null)
            {
                // Show video, hide placeholder
                if (placeholderIcon != null) placeholderIcon.gameObject.SetActive(false);
                if (placeholderIconText != null) placeholderIconText.gameObject.SetActive(false);
                videoImage.gameObject.SetActive(true);

                videoPlayer.Stop();
                videoPlayer.clip = clip;
                videoPlayer.time = 0;
                videoPlayer.frame = 0;
                videoPlayer.isLooping = !freezeOnLastFrame;

                if (freezeOnLastFrame)
                    videoPlayer.loopPointReached += OnVideoFreezeOnLastFrame;

                videoPlayer.Play();
            }
            else
            {
                // No video — show large placeholder icon
                if (videoImage != null) videoImage.gameObject.SetActive(false);
                if (videoPlayer != null) videoPlayer.Stop();

                if (placeholderIcon != null) placeholderIcon.gameObject.SetActive(true);
                if (placeholderIconText != null)
                {
                    placeholderIconText.gameObject.SetActive(true);
                    placeholderIconText.text = GetIcon(eventType);
                    placeholderIconText.color = GetIconColor(eventType);
                }
            }
        }

        private void OnVideoFreezeOnLastFrame(VideoPlayer vp)
        {
            vp.Pause();
            vp.loopPointReached -= OnVideoFreezeOnLastFrame;
        }

        private void StopVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFreezeOnLastFrame;
                if (videoPlayer.isPlaying)
                    videoPlayer.Stop();
            }
        }

        // ================================================================
        // Icon Helpers
        // ================================================================

        private static string GetIcon(MineEventType type) => type switch
        {
            MineEventType.Combat  => "\u2694",  // crossed swords
            MineEventType.Chest   => "\u2617",  // lozenge
            MineEventType.Shrine  => "\u2726",  // four-pointed star
            MineEventType.Dialogue => "\u2637", // trigram
            _ => "\u2713"                        // checkmark
        };

        private static Color GetIconColor(MineEventType type) => type switch
        {
            MineEventType.Combat  => new Color(0.9f, 0.35f, 0.3f, 1f),
            MineEventType.Chest   => new Color(1f, 0.85f, 0.3f, 1f),
            MineEventType.Shrine  => new Color(0.7f, 0.5f, 1f, 1f),
            MineEventType.Dialogue => new Color(0.5f, 0.75f, 1f, 1f),
            _ => Color.white
        };
    }
}
