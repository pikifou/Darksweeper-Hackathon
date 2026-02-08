using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Sweeper.Flow
{
    /// <summary>
    /// Handles the transition from one level to the next on victory.
    /// Place this in LV1 and LV2 scenes. On victory, it plays a fullscreen
    /// video while async-loading the next scene, then fades to black and activates it.
    ///
    /// Pattern follows QuestionnaireFlowController.TransitionToLevelSequence().
    /// </summary>
    public class LevelTransitionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SweeperGameController gameController;

        [Header("Transition Video")]
        [Tooltip("Video clip played fullscreen on victory.")]
        [SerializeField] private VideoClip transitionClip;

        [Header("Next Scene")]
        [Tooltip("Scene name to load (must be in Build Settings). E.g. 'Sweeper_LV2'.")]
        [SerializeField] private string nextSceneName = "Sweeper_LV2";

        [Header("Fullscreen Display")]
        [Tooltip("Canvas (Screen Space - Overlay, high sort order) that covers the screen. Starts disabled.")]
        [SerializeField] private Canvas transitionCanvas;
        [Tooltip("RawImage filling the canvas — receives the VideoPlayer RenderTexture.")]
        [SerializeField] private RawImage videoDisplay;
        [Tooltip("Black Image overlay for fade-to-black after the video ends.")]
        [SerializeField] private Image fadeOverlay;

        [Header("Timing")]
        [Tooltip("Delay after VICTORY is shown before the transition video starts.")]
        [SerializeField] private float victoryDisplayDelay = 3f;
        [SerializeField] private float fadeToBlackDuration = 1.5f;

        private VideoPlayer videoPlayer;
        private RenderTexture renderTexture;

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Awake()
        {
            // Ensure the transition canvas is hidden at start
            if (transitionCanvas != null)
                transitionCanvas.enabled = false;
            if (videoDisplay != null)
                videoDisplay.gameObject.SetActive(false);
            if (fadeOverlay != null)
            {
                Color c = fadeOverlay.color;
                c.a = 0f;
                fadeOverlay.color = c;
            }
        }

        private void OnEnable()
        {
            if (gameController != null)
                gameController.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            if (gameController != null)
                gameController.OnGameOver -= HandleGameOver;
        }

        private void OnDestroy()
        {
            CleanupRenderTexture();
        }

        // ================================================================
        // Event handler
        // ================================================================

        private void HandleGameOver(bool won)
        {
            if (!won) return;
            if (transitionClip == null || string.IsNullOrEmpty(nextSceneName)) return;

            StartCoroutine(TransitionSequence());
        }

        // ================================================================
        // Transition coroutine
        // ================================================================

        private IEnumerator TransitionSequence()
        {
            Debug.Log($"[LevelTransition] Starting transition. Waiting {victoryDisplayDelay}s for VICTORY display...");

            // 0. Let the player see "VICTORY" for a few seconds before taking over
            yield return new WaitForSeconds(victoryDisplayDelay);

            Debug.Log($"[LevelTransition] Victory delay done. Playing video: {transitionClip.name}, Scene: {nextSceneName}");

            // 1. Activate the fullscreen canvas (covers VICTORY text and game)
            if (transitionCanvas != null)
                transitionCanvas.enabled = true;

            // Ensure fade overlay starts transparent
            if (fadeOverlay != null)
            {
                fadeOverlay.gameObject.SetActive(true);
                Color c = fadeOverlay.color;
                c.a = 0f;
                fadeOverlay.color = c;
            }

            // 2. Set up VideoPlayer and RenderTexture
            SetupVideoPlayer();

            // 3. Begin async scene load in the background (don't activate yet)
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextSceneName);
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = false;
                Debug.Log("[LevelTransition] Async scene load started in background.");
            }
            else
            {
                Debug.LogError($"[LevelTransition] Failed to start async load for scene '{nextSceneName}'. Is it in Build Settings?");
            }

            // 4. Play the transition video
            bool videoFinished = false;
            videoPlayer.loopPointReached += _ => videoFinished = true;
            videoPlayer.Play();

            // Wait for video to finish
            while (!videoFinished)
                yield return null;

            Debug.Log("[LevelTransition] Transition video finished.");

            // 5. Fade to black
            if (fadeOverlay != null)
            {
                float elapsed = 0f;
                Color c = fadeOverlay.color;
                while (elapsed < fadeToBlackDuration)
                {
                    elapsed += Time.deltaTime;
                    c.a = Mathf.Clamp01(elapsed / fadeToBlackDuration);
                    fadeOverlay.color = c;
                    yield return null;
                }
                c.a = 1f;
                fadeOverlay.color = c;
                Debug.Log("[LevelTransition] Fade to black complete.");
            }

            // 6. Wait for async load to be ready (progress reaches 0.9) and activate
            if (asyncLoad != null)
            {
                while (asyncLoad.progress < 0.9f)
                {
                    Debug.Log($"[LevelTransition] Scene loading... {asyncLoad.progress * 100:F0}%");
                    yield return null;
                }

                Debug.Log("[LevelTransition] Scene ready. Activating...");
                asyncLoad.allowSceneActivation = true;
            }

            CleanupRenderTexture();
        }

        // ================================================================
        // Video setup helpers
        // ================================================================

        private void SetupVideoPlayer()
        {
            // Create or get VideoPlayer component
            videoPlayer = gameObject.GetComponent<VideoPlayer>();
            if (videoPlayer == null)
                videoPlayer = gameObject.AddComponent<VideoPlayer>();

            // Create RenderTexture at screen resolution
            renderTexture = new RenderTexture(Screen.width, Screen.height, 0);
            renderTexture.Create();

            // Configure VideoPlayer
            videoPlayer.clip = transitionClip;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.isLooping = false;
            videoPlayer.playOnAwake = false;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            // Assign to display
            if (videoDisplay != null)
            {
                videoDisplay.texture = renderTexture;
                videoDisplay.gameObject.SetActive(true);
            }
        }

        private void CleanupRenderTexture()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }
        }
    }
}
