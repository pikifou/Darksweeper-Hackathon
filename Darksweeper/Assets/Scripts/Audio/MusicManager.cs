using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Audio
{
    /// <summary>
    /// Persistent singleton that manages background music across all scenes.
    /// Auto-bootstraps via [RuntimeInitializeOnLoadMethod] — works regardless of which scene
    /// is loaded first. Uses two AudioSources for seamless crossfading.
    ///
    /// ── SETUP (Unity Editor) ──────────────────────────────────────────────
    /// 1. Let the scripts compile.
    /// 2. Create a MusicConfig asset:
    ///      Assets > Create > DarkSweeper > Music Config
    ///    Fill in the scene-to-clip entries (e.g. "Questionnaire" → cosmic-indifference,
    ///    "Sweeper_LV1" → obsidian-rites). Save it in Assets/Data/.
    /// 3. Create an empty GameObject, add the MusicManager component, assign the
    ///    MusicConfig asset to its "Config" field.
    /// 4. Drag that GameObject into Assets/Resources/ to create a prefab named
    ///    "MusicManager". Then delete the instance from the scene.
    /// 5. Done. The manager will self-instantiate before any scene loads.
    /// ──────────────────────────────────────────────────────────────────────
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────

        public static MusicManager Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────

        [SerializeField] private MusicConfigSO config;

        // ── Private state ────────────────────────────────────────

        private AudioSource sourceA;
        private AudioSource sourceB;
        private AudioSource activeSource;
        private Coroutine fadeCoroutine;

        // ── Auto-bootstrap ───────────────────────────────────────

        /// <summary>
        /// Called automatically before the first scene loads (even in a build).
        /// Instantiates the MusicManager from a Resources prefab if none exists yet.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;

            var prefab = Resources.Load<MusicManager>("MusicManager");
            if (prefab != null)
            {
                Instantiate(prefab);
            }
            else
            {
                Debug.LogWarning("[MusicManager] No prefab found at Resources/MusicManager. " +
                                 "Music will not play. See setup instructions in MusicManager.cs.");
            }
        }

        // ── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            // Singleton guard
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Create and configure the two AudioSources
            sourceA = gameObject.AddComponent<AudioSource>();
            sourceB = gameObject.AddComponent<AudioSource>();
            ConfigureSource(sourceA);
            ConfigureSource(sourceB);

            activeSource = sourceA;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Scene callback ───────────────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (config == null)
            {
                Debug.LogWarning("[MusicManager] No MusicConfigSO assigned.");
                return;
            }

            AudioClip clip = config.GetClipForScene(scene.name);

            // No mapping for this scene → keep current music playing
            if (clip == null) return;

            // Same clip already playing → no-op (e.g. Intro → Questionnaire)
            if (activeSource.clip == clip && activeSource.isPlaying) return;

            float volume = config.GetVolumeForScene(scene.name);
            CrossfadeTo(clip, volume);
        }

        // ── Crossfade ────────────────────────────────────────────

        /// <summary>
        /// Start a crossfade from the active source to the other source with a new clip.
        /// </summary>
        private void CrossfadeTo(AudioClip newClip, float targetVolume)
        {
            // Determine which source is incoming
            AudioSource outgoing = activeSource;
            AudioSource incoming = (activeSource == sourceA) ? sourceB : sourceA;

            // If a fade is already running, stop it cleanly
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                // Snap the previous transition: silence the non-active source
                if (!outgoing.isPlaying) outgoing.Stop();
            }

            fadeCoroutine = StartCoroutine(FadeCoroutine(outgoing, incoming, newClip, targetVolume,
                                                          config.fadeDuration));
        }

        private IEnumerator FadeCoroutine(AudioSource outgoing, AudioSource incoming,
                                           AudioClip newClip, float targetVolume, float duration)
        {
            // Prepare incoming source
            incoming.clip = newClip;
            incoming.volume = 0f;
            incoming.Play();

            float startVolume = outgoing.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // Use unscaledDeltaTime so the fade works even when Time.timeScale == 0
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                outgoing.volume = Mathf.Lerp(startVolume, 0f, t);
                incoming.volume = Mathf.Lerp(0f, targetVolume, t);

                yield return null;
            }

            // Snap final values
            outgoing.Stop();
            outgoing.clip = null;
            outgoing.volume = 0f;
            incoming.volume = targetVolume;

            activeSource = incoming;
            fadeCoroutine = null;
        }

        // ── Helpers ──────────────────────────────────────────────

        private static void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D — no spatial attenuation
            source.priority = 0;      // highest priority (music should never be culled)
        }
    }
}
