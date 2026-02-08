using System;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Data-driven mapping of scene names to music tracks.
    /// Create an asset via: Assets > Create > DarkSweeper > Music Config
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/Music Config")]
    public class MusicConfigSO : ScriptableObject
    {
        [Serializable]
        public struct SceneMusicEntry
        {
            [Tooltip("Exact scene name (as shown in Build Settings).")]
            public string sceneName;

            [Tooltip("Music clip to play when this scene loads.")]
            public AudioClip clip;

            [Range(0f, 1f)]
            [Tooltip("Target volume for this track.")]
            public float volume;
        }

        [Header("Scene-to-Music Mapping")]
        [Tooltip("Each entry maps a scene name to a music clip and volume.")]
        public SceneMusicEntry[] entries;

        [Header("Transition")]
        [Tooltip("Duration (seconds) of the crossfade between two tracks.")]
        public float fadeDuration = 1.5f;

        /// <summary>
        /// Returns the AudioClip assigned to the given scene, or null if no mapping exists.
        /// A null return means "keep whatever is currently playing".
        /// </summary>
        public AudioClip GetClipForScene(string sceneName)
        {
            if (entries == null) return null;

            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].sceneName, sceneName, StringComparison.Ordinal))
                    return entries[i].clip;
            }

            return null;
        }

        /// <summary>
        /// Returns the target volume for the given scene, or 1f if no mapping exists.
        /// </summary>
        public float GetVolumeForScene(string sceneName)
        {
            if (entries == null) return 1f;

            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].sceneName, sceneName, StringComparison.Ordinal))
                    return entries[i].volume;
            }

            return 1f;
        }
    }
}
