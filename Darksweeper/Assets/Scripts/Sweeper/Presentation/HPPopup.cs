using System.Collections;
using TMPro;
using UnityEngine;

namespace Sweeper.Presentation
{
    /// <summary>
    /// JRPG-style floating HP change popup.
    /// Spawned by <see cref="SweeperHUD"/> when HP changes.
    ///
    /// - Damage (negative): red text "-X" drifts upward from the HP number position and fades out.
    /// - Heal (positive):   green text "+X" drifts upward from slightly below the HP number and fades out.
    ///
    /// Self-destroys after the animation completes.
    /// </summary>
    public class HPPopup : MonoBehaviour
    {
        // ================================================================
        // Configuration — tweak in Inspector or via code
        // ================================================================

        [Header("Animation")]
        [SerializeField] private float duration = 1.0f;
        [SerializeField] private float driftDistance = 80f;

        [Header("Colors")]
        [SerializeField] private Color damageColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color healColor = new Color(0.3f, 1f, 0.4f, 1f);

        // ================================================================
        // Runtime state
        // ================================================================

        private TextMeshProUGUI label;
        private RectTransform rectTransform;
        private Vector2 startPos;

        /// <summary>
        /// Initialize and play the popup animation.
        /// Call immediately after instantiation.
        /// </summary>
        /// <param name="delta">HP change: negative = damage, positive = heal.</param>
        /// <param name="anchorWorldPos">Screen-space position of the HP value text.</param>
        /// <param name="parentCanvas">Canvas transform to parent this popup under.</param>
        public void Play(int delta, Vector2 anchorScreenPos, Transform parentCanvas)
        {
            // --- Create TMP label ---
            label = gameObject.AddComponent<TextMeshProUGUI>();
            rectTransform = GetComponent<RectTransform>();

            transform.SetParent(parentCanvas, false);

            // Text
            bool isDamage = delta < 0;
            string sign = delta > 0 ? "+" : "";
            label.text = $"{sign}{delta}";
            label.fontSize = 42;
            label.fontStyle = FontStyles.Bold;
            label.color = isDamage ? damageColor : healColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableAutoSizing = false;

            // Size & position
            rectTransform.sizeDelta = new Vector2(200, 60);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // Convert screen-space anchor to local canvas position
            if (parentCanvas is RectTransform canvasRect)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, anchorScreenPos, null, out Vector2 localPos);

                // Damage starts at the HP text, heal starts slightly below
                float verticalOffset = isDamage ? 0f : -40f;
                localPos.y += verticalOffset;
                rectTransform.anchoredPosition = localPos;
            }

            startPos = rectTransform.anchoredPosition;

            StartCoroutine(AnimatePopup(isDamage));
        }

        private IEnumerator AnimatePopup(bool isDamage)
        {
            float elapsed = 0f;
            float direction = 1f; // always drift upward

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease-out curve for smooth deceleration
                float eased = 1f - (1f - t) * (1f - t);

                // Drift upward
                Vector2 pos = startPos;
                pos.y += direction * driftDistance * eased;
                rectTransform.anchoredPosition = pos;

                // Fade out in the second half of the animation
                float alpha;
                if (t < 0.4f)
                    alpha = 1f;
                else
                    alpha = Mathf.Lerp(1f, 0f, (t - 0.4f) / 0.6f);

                Color c = label.color;
                c.a = alpha;
                label.color = c;

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
