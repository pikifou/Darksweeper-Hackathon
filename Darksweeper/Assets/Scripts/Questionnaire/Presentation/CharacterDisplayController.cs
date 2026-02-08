using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Questionnaire.Presentation
{
    /// <summary>
    /// Manages the centered character display.
    /// Alternates between static character images and transition videos
    /// rendered via VideoPlayer → RenderTexture → RawImage.
    /// </summary>
    public class CharacterDisplayController : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private RawImage characterRawImage;

        [Header("Static character images (Char1–Char6)")]
        [SerializeField] private Texture2D[] characterImages;

        [Header("Transition video clips (Trans_1to2 ... Trans_5to6, Trans_Outro)")]
        [SerializeField] private VideoClip[] transitionClips;

        [Header("Video")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RenderTexture renderTexture;

        private Action onVideoFinished;
        private AudioSource cachedAudioSource;

        private void Awake()
        {
            videoPlayer.loopPointReached += HandleVideoEnd;
            videoPlayer.isLooping = false;

            // Cache the AudioSource (TitleScreenController.Start creates it first)
            cachedAudioSource = videoPlayer.GetComponent<AudioSource>();
        }

        /// <summary>
        /// Wires the audio track of the CURRENT clip to the cached AudioSource.
        /// Must be called AFTER videoPlayer.clip is set, because EnableAudioTrack
        /// and SetTargetAudioSource only work when the clip has an audio track.
        /// </summary>
        private void WireAudioForCurrentClip()
        {
            if (cachedAudioSource == null)
                cachedAudioSource = videoPlayer.GetComponent<AudioSource>();

            if (cachedAudioSource == null || videoPlayer.clip == null) return;

            if (videoPlayer.clip.audioTrackCount > 0)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.EnableAudioTrack(0, true);
                videoPlayer.SetTargetAudioSource(0, cachedAudioSource);
                Debug.Log($"[CharacterDisplay] Wired audio for clip '{videoPlayer.clip.name}'");
            }
        }

        /// <summary>
        /// Shows a static character image by index (0-based).
        /// </summary>
        public void ShowStaticImage(int index)
        {
            if (characterImages == null || index < 0 || index >= characterImages.Length)
            {
                Debug.Log($"[CharacterDisplay] Invalid image index {index}. Array length: {characterImages?.Length ?? 0}");
                return;
            }

            videoPlayer.Stop();
            characterRawImage.texture = characterImages[index];
            characterRawImage.gameObject.SetActive(true);
        }

        /// <summary>
        /// Plays a transition video by index (0-based).
        /// Index 0 = Trans_1to2, ..., Index 4 = Trans_5to6, Index 5 = Trans_Outro.
        /// onComplete is called when the video finishes.
        /// </summary>
        public void PlayTransition(int transitionIndex, Action onComplete = null)
        {
            if (transitionClips == null || transitionIndex < 0 || transitionIndex >= transitionClips.Length)
            {
                Debug.Log($"[CharacterDisplay] Invalid transition index {transitionIndex}. Array length: {transitionClips?.Length ?? 0}");
                onComplete?.Invoke();
                return;
            }

            VideoClip clip = transitionClips[transitionIndex];
            if (clip == null)
            {
                Debug.Log($"[CharacterDisplay] Transition clip at index {transitionIndex} is null. Skipping.");
                onComplete?.Invoke();
                return;
            }

            onVideoFinished = onComplete;

            // Switch RawImage to show the RenderTexture
            characterRawImage.texture = renderTexture;

            videoPlayer.clip = clip;
            videoPlayer.targetTexture = renderTexture;
            WireAudioForCurrentClip();
            videoPlayer.Play();
        }

        /// <summary>
        /// Plays an arbitrary video clip (not from the transitionClips array).
        /// Useful for one-off clips like the level transition video.
        /// </summary>
        public void PlayClip(VideoClip clip, Action onComplete = null)
        {
            if (clip == null)
            {
                Debug.Log("[CharacterDisplay] PlayClip called with null clip. Skipping.");
                onComplete?.Invoke();
                return;
            }

            onVideoFinished = onComplete;

            characterRawImage.texture = renderTexture;
            characterRawImage.gameObject.SetActive(true);

            videoPlayer.clip = clip;
            videoPlayer.targetTexture = renderTexture;
            WireAudioForCurrentClip();
            videoPlayer.Play();
        }

        /// <summary>
        /// Hides the character display (used after the outro).
        /// </summary>
        public void Hide()
        {
            videoPlayer.Stop();
            characterRawImage.gameObject.SetActive(false);
        }

        private void HandleVideoEnd(VideoPlayer vp)
        {
            Action callback = onVideoFinished;
            onVideoFinished = null;
            callback?.Invoke();
        }

        private void OnDestroy()
        {
            videoPlayer.loopPointReached -= HandleVideoEnd;
        }
    }
}
