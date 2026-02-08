using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Questionnaire.Presentation
{
    /// <summary>
    /// Manages the fullscreen background layer.
    /// Uses two overlapping Image components to crossfade between states.
    /// </summary>
    public class BackgroundController : MonoBehaviour
    {
        [Header("Two overlapping fullscreen images for crossfade")]
        [SerializeField] private Image imageFront;
        [SerializeField] private Image imageBack;

        [Header("Background sprites (BG1–BG6 + BG_Black)")]
        [SerializeField] private Sprite[] backgroundSprites;

        [Header("Crossfade Settings")]
        [SerializeField] private float crossfadeDuration = 1.5f;

        private Coroutine activeCoroutine;

        /// <summary>
        /// Sets the initial background immediately (no fade). Call at start.
        /// </summary>
        public void SetImmediate(int index)
        {
            if (!IsValidIndex(index)) return;

            imageFront.sprite = backgroundSprites[index];
            imageFront.color = Color.white;

            imageBack.sprite = backgroundSprites[index];
            imageBack.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>
        /// Crossfades from the current background to the target index.
        /// onComplete is called when the fade finishes.
        /// </summary>
        public void CrossfadeTo(int targetIndex, Action onComplete = null)
        {
            if (!IsValidIndex(targetIndex)) return;

            if (activeCoroutine != null)
                StopCoroutine(activeCoroutine);

            activeCoroutine = StartCoroutine(CrossfadeCoroutine(targetIndex, onComplete));
        }

        /// <summary>
        /// Crossfades to black (last sprite in the array).
        /// </summary>
        public void CrossfadeToBlack(Action onComplete = null)
        {
            CrossfadeTo(backgroundSprites.Length - 1, onComplete);
        }

        private IEnumerator CrossfadeCoroutine(int targetIndex, Action onComplete)
        {
            // Set up: back shows current, front will fade in with new sprite
            imageBack.sprite = imageFront.sprite;
            imageBack.color = Color.white;

            imageFront.sprite = backgroundSprites[targetIndex];
            imageFront.color = new Color(1f, 1f, 1f, 0f);

            float elapsed = 0f;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / crossfadeDuration);

                imageFront.color = new Color(1f, 1f, 1f, t);

                yield return null;
            }

            imageFront.color = Color.white;
            imageBack.color = new Color(1f, 1f, 1f, 0f);

            activeCoroutine = null;
            onComplete?.Invoke();
        }

        private bool IsValidIndex(int index)
        {
            if (backgroundSprites == null || index < 0 || index >= backgroundSprites.Length)
            {
                Debug.Log($"[BackgroundController] Invalid index {index}. Array length: {backgroundSprites?.Length ?? 0}");
                return false;
            }
            return true;
        }
    }
}
