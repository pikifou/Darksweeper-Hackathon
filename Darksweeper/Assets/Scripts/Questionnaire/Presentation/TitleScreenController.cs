using System;
using System.Collections;
using Audio;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Questionnaire.Presentation
{
    /// <summary>
    /// Manages the title screen phase: looping background video, title image,
    /// "Press to Start" label, and intro transition into the questionnaire.
    /// </summary>
    public class TitleScreenController : MonoBehaviour
    {
        [Header("Video")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoRawImage;
        [SerializeField] private RenderTexture renderTexture;

        [Header("Video Clips")]
        [SerializeField] private VideoClip titleLoopClip;

        [Tooltip("Sequence of transition clips played after input. Title UI hides during the first clip.")]
        [SerializeField] private VideoClip[] transitionClips;

        [Header("Title UI")]
        [SerializeField] private Image titleImage;
        [SerializeField] private TextMeshProUGUI pressToStartText;

        [Header("Fade From Black")]
        [Tooltip("Fullscreen black Image that sits on top of everything. Fades out on start.")]
        [SerializeField] private Image fadeOverlay;
        [SerializeField] private float fadeDuration = 1.5f;

        /// <summary>
        /// Fired when the intro transition finishes and the questionnaire should begin.
        /// </summary>
        public event Action OnTitleComplete;

        private bool waitingForInput;
        #pragma warning disable CS0414
        private bool isFading;
        #pragma warning restore CS0414
        private bool playingTransition;
        private int currentTransitionIndex;
        private AudioSource videoAudioSource;

        private void Start()
        {
            // Start with everything hidden behind the black overlay
            waitingForInput = false;
            isFading = true;

            // Ensure overlay is fully opaque
            if (fadeOverlay != null)
            {
                fadeOverlay.gameObject.SetActive(true);
                fadeOverlay.color = Color.black;
            }

            // Hide title UI during fade (will reveal after)
            if (titleImage != null) titleImage.gameObject.SetActive(false);
            if (pressToStartText != null) pressToStartText.gameObject.SetActive(false);

            // Start looping title video (plays behind the overlay)
            if (titleLoopClip != null && videoPlayer != null)
            {
                videoRawImage.texture = renderTexture;
                videoRawImage.gameObject.SetActive(true);

                // Create/find the AudioSource once (used for clips that have audio)
                videoAudioSource = videoPlayer.GetComponent<AudioSource>();
                if (videoAudioSource == null)
                {
                    videoAudioSource = videoPlayer.gameObject.AddComponent<AudioSource>();
                    videoAudioSource.playOnAwake = false;
                    videoAudioSource.spatialBlend = 0f;
                    videoAudioSource.volume = 1f;
                }
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

                // Loop clip (may not have audio — that's OK)
                videoPlayer.clip = titleLoopClip;
                videoPlayer.targetTexture = renderTexture;
                videoPlayer.isLooping = true;
                videoPlayer.Play();
            }

            // Subscribe to video end (used for the intro transition)
            videoPlayer.loopPointReached += HandleVideoEnd;

            // Start the fade-from-black
            StartCoroutine(FadeFromBlack());
        }

        private IEnumerator FadeFromBlack()
        {
            Debug.Log("[TitleScreen] Fading from black...");

            // Brief delay to let the video player initialize
            yield return new WaitForSeconds(0.2f);

            // Show title UI behind the fade
            if (titleImage != null) titleImage.gameObject.SetActive(true);
            if (pressToStartText != null) pressToStartText.gameObject.SetActive(true);

            // Fade out the black overlay
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                if (fadeOverlay != null)
                    fadeOverlay.color = new Color(0f, 0f, 0f, 1f - t);
                yield return null;
            }

            // Fully transparent — disable overlay and accept input
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
                fadeOverlay.gameObject.SetActive(false);
            }

            isFading = false;
            waitingForInput = true;

            // Play ambient loop sound for the title screen
            SFXManager.Instance.Play("loop_inferno");

            Debug.Log("[TitleScreen] Fade complete. Waiting for input...");
        }

        private void Update()
        {
            if (!waitingForInput) return;

            // Any key or mouse click starts the game (new Input System)
            bool keyPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool mouseClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (keyPressed || mouseClicked)
            {
                waitingForInput = false;

                // Play the "steel" sound via the SFX singleton
                SFXManager.Instance.Play("steel");

                StartIntroTransition();
            }
        }

        private void StartIntroTransition()
        {
            Debug.Log("[TitleScreen] Input received. Starting transition sequence...");

            // Hide title UI during the first transition clip
            if (titleImage != null) titleImage.gameObject.SetActive(false);
            if (pressToStartText != null) pressToStartText.gameObject.SetActive(false);

            // Play the chain of transition clips
            if (transitionClips != null && transitionClips.Length > 0)
            {
                videoPlayer.isLooping = false;
                currentTransitionIndex = 0;
                PlayTransitionClip(currentTransitionIndex);
            }
            else
            {
                // No transition clips — go straight to questionnaire
                Debug.Log("[TitleScreen] No transition clips assigned. Completing immediately.");
                Complete();
            }
        }

        private void PlayTransitionClip(int index)
        {
            playingTransition = true;
            videoPlayer.clip = transitionClips[index];

            // Wire audio AFTER clip is set (clip may have audio track)
            WireAudioForCurrentClip();

            videoPlayer.Play();
            Debug.Log($"[TitleScreen] Playing transition clip {index + 1}/{transitionClips.Length}");
        }

        private void HandleVideoEnd(VideoPlayer vp)
        {
            if (!playingTransition) return;

            currentTransitionIndex++;

            if (currentTransitionIndex < transitionClips.Length)
            {
                // Play the next clip in the chain
                PlayTransitionClip(currentTransitionIndex);
            }
            else
            {
                // All transition clips done
                playingTransition = false;
                Complete();
            }
        }

        private void Complete()
        {
            Debug.Log("[TitleScreen] Title screen complete.");

            // Stop the video player so the questionnaire can take over
            videoPlayer.Stop();
            videoPlayer.isLooping = false;

            // Unsubscribe
            videoPlayer.loopPointReached -= HandleVideoEnd;

            // Fire completion event
            OnTitleComplete?.Invoke();

            // Disable this controller
            enabled = false;
        }

        /// <summary>
        /// Wires the audio track of the CURRENT clip to the AudioSource.
        /// Must be called AFTER videoPlayer.clip is set, because
        /// EnableAudioTrack / SetTargetAudioSource only work when the
        /// current clip actually has an audio track.
        /// </summary>
        private void WireAudioForCurrentClip()
        {
            if (videoAudioSource == null || videoPlayer.clip == null) return;

            // Only wire if the clip has at least one audio track
            if (videoPlayer.clip.audioTrackCount > 0)
            {
                videoPlayer.EnableAudioTrack(0, true);
                videoPlayer.SetTargetAudioSource(0, videoAudioSource);
                Debug.Log($"[TitleScreen] Wired audio track for clip '{videoPlayer.clip.name}' → AudioSource");
            }
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
                videoPlayer.loopPointReached -= HandleVideoEnd;
        }
    }
}
