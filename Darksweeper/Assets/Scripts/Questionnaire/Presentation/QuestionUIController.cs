using System;
using Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Questionnaire.Presentation
{
    /// <summary>
    /// Manages the question panel: displays question text and 4 answer buttons.
    /// No scoring logic — just displays and reports clicks.
    /// </summary>
    public class QuestionUIController : MonoBehaviour
    {
        [Header("Question Text")]
        [SerializeField] private TextMeshProUGUI questionText;

        [Header("Answer Buttons (A, B, C, D)")]
        [SerializeField] private Button buttonA;
        [SerializeField] private Button buttonB;
        [SerializeField] private Button buttonC;
        [SerializeField] private Button buttonD;

        [Header("Answer Texts")]
        [SerializeField] private TextMeshProUGUI textA;
        [SerializeField] private TextMeshProUGUI textB;
        [SerializeField] private TextMeshProUGUI textC;
        [SerializeField] private TextMeshProUGUI textD;

        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        /// <summary>
        /// Fired when the player clicks an answer button.
        /// Parameter: the answer ID ("A", "B", "C", or "D").
        /// </summary>
        public event Action<string> OnAnswerClicked;

        private void Awake()
        {
            buttonA.onClick.AddListener(() => HandleClick("A"));
            buttonB.onClick.AddListener(() => HandleClick("B"));
            buttonC.onClick.AddListener(() => HandleClick("C"));
            buttonD.onClick.AddListener(() => HandleClick("D"));

            // Add hover SFX to each button
            AddHoverSFX(buttonA);
            AddHoverSFX(buttonB);
            AddHoverSFX(buttonC);
            AddHoverSFX(buttonD);

            Hide();
        }

        /// <summary>
        /// Populates the panel with a question and its 4 answers, then shows it.
        /// </summary>
        public void ShowQuestion(string question, string answerA, string answerB, string answerC, string answerD)
        {
            questionText.text = question;
            textA.text = answerA;
            textB.text = answerB;
            textC.text = answerC;
            textD.text = answerD;

            SetButtonsInteractable(true);
            panelRoot.SetActive(true);
        }

        /// <summary>
        /// Hides the question panel.
        /// </summary>
        public void Hide()
        {
            panelRoot.SetActive(false);
        }

        /// <summary>
        /// Enables or disables all answer buttons (used during transitions).
        /// </summary>
        public void SetButtonsInteractable(bool interactable)
        {
            buttonA.interactable = interactable;
            buttonB.interactable = interactable;
            buttonC.interactable = interactable;
            buttonD.interactable = interactable;
        }

        private void HandleClick(string answerId)
        {
            SFXManager.Instance.Play("ui_onclick");
            SetButtonsInteractable(false);
            Hide();
            OnAnswerClicked?.Invoke(answerId);
        }

        /// <summary>
        /// Adds an EventTrigger to a button that plays "ui_hover" on pointer enter.
        /// </summary>
        private static void AddHoverSFX(Button button)
        {
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => SFXManager.Instance.Play("ui_hover"));
            trigger.triggers.Add(entry);
        }
    }
}
