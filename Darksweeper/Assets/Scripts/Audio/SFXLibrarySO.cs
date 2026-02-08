using System;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Centralized catalog of all sound effects, mapping string IDs to AudioClips.
    /// Create an asset via: Assets > Create > DarkSweeper > SFX Library
    ///
    /// Example entries:
    ///   "cell_reveal"  → reveal.wav     @ 0.8
    ///   "flag_toggle"  → flag.wav       @ 0.7
    ///   "defeat"       → explosion.wav  @ 1.0
    ///   "victory"      → win.wav        @ 1.0
    ///   "ui_click"     → click.wav      @ 0.6
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/SFX Library")]
    public class SFXLibrarySO : ScriptableObject
    {
        [Serializable]
        public struct SFXEntry
        {
            [Tooltip("Unique identifier used in code, e.g. \"cell_reveal\".")]
            public string id;

            [Tooltip("The audio clip to play.")]
            public AudioClip clip;

            [Range(0f, 1f)]
            [Tooltip("Default playback volume for this sound.")]
            public float volume;
        }

        [Header("Sound Effects Catalog")]
        [Tooltip("Each entry maps a string ID to a clip and volume.")]
        public SFXEntry[] entries;

        /// <summary>
        /// Look up an entry by its string ID.
        /// Returns true if found, with the entry populated.
        /// Returns false if the ID is not in the catalog.
        /// </summary>
        public bool TryGet(string id, out SFXEntry entry)
        {
            entry = default;

            if (entries == null || string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].id, id, StringComparison.Ordinal))
                {
                    entry = entries[i];
                    return true;
                }
            }

            return false;
        }
    }
}
