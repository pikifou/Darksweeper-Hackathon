using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sweeper.Presentation
{
    /// <summary>
    /// UGUI overlay HUD for DarkSweeper.
    /// Displays HP (heart icon + rolling number), mines remaining, and game status.
    ///
    /// HP display is JRPG-style:
    /// - Large heart icon + just the number (no "/ max")
    /// - Rolling counter animation on change
    /// - Floating damage/heal popups via <see cref="HPPopup"/>
    /// - Popup queue with configurable buffer delay to avoid overlap
    /// </summary>
    public class SweeperHUD : MonoBehaviour
    {
        [Header("HP Display")]
        [SerializeField] private RectTransform hpGroup;
        [SerializeField] private Image heartIcon;
        [SerializeField] private TextMeshProUGUI hpValueText;

        [Header("Other UI")]
        [SerializeField] private TextMeshProUGUI minesText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Rolling Counter")]
        [Tooltip("Duration of the rolling counter animation in seconds.")]
        [SerializeField] private float rollDuration = 0.4f;

        [Header("Popup Buffering")]
        [Tooltip("Minimum delay between two consecutive HP popups (seconds). " +
                 "If multiple HP changes arrive faster than this, they are queued.")]
        [SerializeField] private float popupBufferDelay = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private Color healFlashColor = new Color(0.3f, 1f, 0.4f);

        // Keep old field name for backward compat with existing scenes that have it wired
        [SerializeField, HideInInspector] private TextMeshProUGUI hpText;

        // ================================================================
        // Runtime state
        // ================================================================

        private int displayedHP;
        private int targetHP;
        private Coroutine rollCoroutine;
        private Canvas parentCanvas;

        // Popup queue
        private readonly Queue<int> popupQueue = new Queue<int>();
        private Coroutine popupDrainCoroutine;

        private void Awake()
        {
            // Migrate: if old hpText is wired but new hpValueText is not, use the old one
            if (hpValueText == null && hpText != null)
                hpValueText = hpText;

            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
                parentCanvas = GetComponent<Canvas>();
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Reset the HUD to starting values. Hides status text.
        /// No popup, no rolling animation on reset.
        /// </summary>
        public void ResetHUD(int startHP, int totalMines)
        {
            // Flush any pending popups
            popupQueue.Clear();

            UpdateHP(startHP, startHP, 0);
            UpdateMines(totalMines);

            if (statusText != null)
            {
                statusText.text = "";
                statusText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Update the HP display.
        /// When delta != 0, queues a floating popup and plays a rolling counter animation.
        /// </summary>
        /// <param name="current">Current HP after the change.</param>
        /// <param name="max">Maximum HP (kept for API compat, not displayed).</param>
        /// <param name="delta">HP change that triggered this update. 0 = silent update.</param>
        public void UpdateHP(int current, int max, int delta = 0)
        {
            if (hpValueText == null) return;

            targetHP = current;

            if (delta != 0)
            {
                // Enqueue the popup (will be shown with buffer delay)
                EnqueuePopup(delta);

                // Start/restart rolling counter toward the new target
                if (rollCoroutine != null)
                    StopCoroutine(rollCoroutine);
                rollCoroutine = StartCoroutine(RollCounter(displayedHP, current, delta < 0));
            }
            else
            {
                // Instant update (init / reset)
                displayedHP = current;
                hpValueText.text = current.ToString();
                hpValueText.color = normalColor;
            }
        }

        public void UpdateMines(int remaining)
        {
            if (minesText != null)
                minesText.text = $"Mines: {remaining}";
        }

        public void ShowVictory()
        {
            if (statusText != null)
            {
                statusText.text = "VICTORY";
                statusText.color = new Color(0.2f, 1f, 0.3f);
                statusText.gameObject.SetActive(true);
            }
        }

        public void ShowDefeat()
        {
            if (statusText != null)
            {
                statusText.text = "DEFEAT";
                statusText.color = new Color(1f, 0.2f, 0.2f);
                statusText.gameObject.SetActive(true);
            }
        }

        // ================================================================
        // Rolling counter animation
        // ================================================================

        private IEnumerator RollCounter(int from, int to, bool isDamage)
        {
            Color flashColor = isDamage ? damageFlashColor : healFlashColor;
            hpValueText.color = flashColor;

            float elapsed = 0f;
            while (elapsed < rollDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rollDuration);

                // Interpolate displayed value
                int value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                displayedHP = value;
                hpValueText.text = value.ToString();

                // Fade color from flash back to normal
                hpValueText.color = Color.Lerp(flashColor, normalColor, t);

                yield return null;
            }

            // Ensure final value is exact
            displayedHP = to;
            hpValueText.text = to.ToString();
            hpValueText.color = normalColor;
            rollCoroutine = null;
        }

        // ================================================================
        // Popup queue with buffer delay
        // ================================================================

        private void EnqueuePopup(int delta)
        {
            popupQueue.Enqueue(delta);

            // Start draining if not already running
            if (popupDrainCoroutine == null)
                popupDrainCoroutine = StartCoroutine(DrainPopupQueue());
        }

        private IEnumerator DrainPopupQueue()
        {
            while (popupQueue.Count > 0)
            {
                int delta = popupQueue.Dequeue();
                SpawnPopup(delta);

                // Wait the buffer delay before showing the next one
                if (popupQueue.Count > 0)
                    yield return new WaitForSeconds(popupBufferDelay);
            }

            popupDrainCoroutine = null;
        }

        // ================================================================
        // Floating popup
        // ================================================================

        private void SpawnPopup(int delta)
        {
            if (parentCanvas == null) return;

            // Get screen-space position of the HP value text
            Vector2 screenPos;
            if (hpValueText != null)
            {
                RectTransform textRect = hpValueText.rectTransform;
                Vector3 worldPos = textRect.TransformPoint(textRect.rect.center);
                screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
            }
            else
            {
                screenPos = new Vector2(Screen.width * 0.1f, Screen.height * 0.9f);
            }

            var popupGO = new GameObject($"HPPopup_{(delta > 0 ? "+" : "")}{delta}");
            var popup = popupGO.AddComponent<HPPopup>();
            popup.Play(delta, screenPos, parentCanvas.transform);
        }
    }
}
