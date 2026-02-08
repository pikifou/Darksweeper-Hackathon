using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Persistent singleton that plays one-shot sound effects.
    /// Auto-bootstraps via [RuntimeInitializeOnLoadMethod] — works regardless of which scene
    /// is loaded first. Uses a pool of AudioSources for overlapping sounds.
    ///
    /// ── SETUP (Unity Editor) ──────────────────────────────────────────────
    /// 1. Let the scripts compile.
    /// 2. Create an SFX Library asset:
    ///      Assets > Create > DarkSweeper > SFX Library
    ///    Add entries (e.g. id:"cell_reveal", clip:reveal.wav, volume:0.8).
    ///    Save it in Assets/Data/.
    /// 3. Create an empty GameObject, add the SFXManager component, assign the
    ///    SFX Library asset to its "Library" field.
    /// 4. Drag that GameObject into Assets/Resources/ to create a prefab named
    ///    "SFXManager". Then delete the instance from the scene.
    /// 5. Done. The manager will self-instantiate before any scene loads.
    /// ──────────────────────────────────────────────────────────────────────
    ///
    /// ── USAGE ─────────────────────────────────────────────────────────────
    ///   // By library ID:
    ///   SFXManager.Instance.Play("cell_reveal");
    ///
    ///   // By direct clip reference:
    ///   [SerializeField] AudioClip myClip;
    ///   SFXManager.Instance.Play(myClip);
    ///   SFXManager.Instance.Play(myClip, 0.5f);
    /// ──────────────────────────────────────────────────────────────────────
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────

        public static SFXManager Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────

        [SerializeField] private SFXLibrarySO library;

        [Header("Pool")]
        [SerializeField]
        [Tooltip("Number of AudioSources in the pool. More = more overlapping sounds.")]
        private int poolSize = 8;

        // ── Private state ────────────────────────────────────────

        private AudioSource[] pool;
        private int nextIndex;

        // ── Auto-bootstrap ───────────────────────────────────────

        /// <summary>
        /// Called automatically before the first scene loads (even in a build).
        /// Instantiates the SFXManager from a Resources prefab if none exists yet.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;

            var prefab = Resources.Load<SFXManager>("SFXManager");
            if (prefab != null)
            {
                Instantiate(prefab);
            }
            else
            {
                Debug.LogWarning("[SFXManager] No prefab found at Resources/SFXManager. " +
                                 "SFX will not play. See setup instructions in SFXManager.cs.");
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

            // Build the AudioSource pool
            pool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                ConfigureSource(pool[i]);
            }

            nextIndex = 0;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Public API ───────────────────────────────────────────

        /// <summary>
        /// Play a sound effect by direct AudioClip reference.
        /// </summary>
        /// <param name="clip">The clip to play.</param>
        /// <param name="volume">Playback volume (0-1). Defaults to 1.</param>
        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            pool[nextIndex].PlayOneShot(clip, volume);
            nextIndex = (nextIndex + 1) % pool.Length;
        }

        /// <summary>
        /// Play a sound effect by its string ID from the SFXLibrarySO catalog.
        /// </summary>
        /// <param name="id">The ID of the entry in the library (e.g. "cell_reveal").</param>
        public void Play(string id)
        {
            if (library == null)
            {
                Debug.LogWarning("[SFXManager] No SFXLibrarySO assigned. Cannot play by ID.");
                return;
            }

            if (library.TryGet(id, out SFXLibrarySO.SFXEntry entry))
            {
                Play(entry.clip, entry.volume);
            }
            else
            {
                Debug.LogWarning($"[SFXManager] SFX ID \"{id}\" not found in library.");
            }
        }

        // ── Helpers ──────────────────────────────────────────────

        private static void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;        // one-shot, not looping
            source.spatialBlend = 0f;   // 2D — no spatial attenuation
            source.priority = 128;      // default priority (music at 0 takes precedence)
        }
    }
}
