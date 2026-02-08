using System;
using System.Collections;
using System.Collections.Generic;
using Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Questionnaire.Data;
using Questionnaire.Presentation;
using PlayerProfile;

namespace Questionnaire.Flow
{
    /// <summary>
    /// Master controller for the questionnaire sequence.
    /// Loads questions, drives the UI / background / character display,
    /// accumulates scores, assigns gods, shows the reveal, and fires a completion event.
    /// </summary>
    public class QuestionnaireFlowController : MonoBehaviour
    {
        // ── State ──────────────────────────────────────────────
        private enum State
        {
            WaitingForTitle,
            Questionnaire,   // questions + videos run in parallel
            PlayingOutro,
            RevealingGods,
            TransitionToLevel,
            Completed
        }

        // ── Serialized References ──────────────────────────────
        [Header("Title Screen")]
        [SerializeField] private TitleScreenController titleScreen;

        [Header("Presentation")]
        [SerializeField] private QuestionUIController questionUI;
        [SerializeField] private BackgroundController background;
        [SerializeField] private CharacterDisplayController characterDisplay;
        [SerializeField] private GodRevealController godReveal;

        [Header("LLM Narrator")]
        [SerializeField] private LLMConfigSO llmConfig;
        [SerializeField] private PromptTemplateSO narratorPromptTemplate;

        [Header("LLM Dialogue Generation")]
        [Tooltip("Generates 12 dialogue encounters via the LLM. Optional — if null, dialogues must already be in the pool.")]
        [SerializeField] private DialogueGeneratorService dialogueGenerator;

        [Header("LLM Status")]
        [Tooltip("Small text in a corner to show LLM request status. Optional.")]
        [SerializeField] private TextMeshProUGUI llmStatusText;
        [SerializeField] private float statusDisplayDuration = 3f;
        [SerializeField] private float statusFadeDuration = 1f;

        [Header("Transition to Level")]
        [Tooltip("Video clip played after the god reveal, while the next scene loads in the background.")]
        [SerializeField] private VideoClip levelTransitionClip;
        [Tooltip("Name of the scene to load (must be in Build Settings). E.g. 'Sweeper_LV1'.")]
        [SerializeField] private string targetSceneName = "Sweeper_LV1";
        [Tooltip("Fullscreen black Image used for fade-to-black before scene activation.")]
        [SerializeField] private Image fadeToBlackOverlay;
        [Tooltip("Duration of the fade-to-black after the transition video ends.")]
        [SerializeField] private float fadeToBlackDuration = 1.5f;

        [Header("LLM Status Messages")]
        [Tooltip("Message shown when the LLM request is sent.")]
        [SerializeField] private string messageSending = "Sending data to the gods...";
        [Tooltip("Message shown when the LLM response is received successfully.")]
        [SerializeField] private string messageSuccess = "The gods have spoken.";
        [Tooltip("Message shown when the LLM request fails or parsing errors.")]
        [SerializeField] private string messageFailure = "The gods remain silent.";

        // ── Runtime ────────────────────────────────────────────
        private QuestionSetData questionData;
        private GodSetData godData;
        private QuestionnaireResult result;
        private State currentState;

        // Decoupled question / video progress
        private int answeredCount;          // 0–6: how many questions have been answered
        private int videosStarted;          // 0–5: how many transition clips have been started
        private bool isTransitionPlaying;   // is a transition clip currently playing

        /// <summary>
        /// Fired once when the full sequence is complete (after god reveal).
        /// Carries the PlayerProfileData, ready for AI prompting.
        /// </summary>
        public event Action<PlayerProfileData> OnQuestionnaireCompleted;

        // ── Lifecycle ──────────────────────────────────────────

        private void Start()
        {
            // Load questions from JSON
            questionData = QuestionLoader.Load();
            if (questionData == null || questionData.Questions.Count != 6)
            {
                Debug.Log("[Flow] Failed to load questions or unexpected count. Aborting.");
                return;
            }

            // Load god definitions from JSON
            godData = GodLoader.Load();
            if (godData == null || godData.Gods.Count != 4)
            {
                Debug.Log("[Flow] Failed to load gods or unexpected count. Aborting.");
                return;
            }

            // Initialize runtime result
            result = new QuestionnaireResult();

            // Subscribe to UI events
            questionUI.OnAnswerClicked += HandleAnswerClicked;

            // Wait for title screen to finish before starting the questionnaire
            if (titleScreen != null)
            {
                currentState = State.WaitingForTitle;
                titleScreen.OnTitleComplete += HandleTitleComplete;
                Debug.Log("[Flow] Data loaded. Waiting for title screen...");
            }
            else
            {
                // No title screen — start immediately
                BeginQuestionnaire();
            }
        }

        private void HandleTitleComplete()
        {
            titleScreen.OnTitleComplete -= HandleTitleComplete;
            BeginQuestionnaire();
        }

        /// <summary>
        /// Starts the questionnaire sequence. Called after the title screen completes,
        /// or immediately if no title screen is assigned.
        /// </summary>
        // Footstep SFX IDs for random selection
        private static readonly string[] FootstepIds = { "foot#1", "foot#2", "foot#3", "foot#4" };

        public void BeginQuestionnaire()
        {
            Debug.Log("[Flow] Beginning questionnaire.");

            currentState = State.Questionnaire;
            answeredCount = 0;
            videosStarted = 0;
            isTransitionPlaying = false;

            // Set initial background
            background.SetImmediate(0);

            // Entrance SFX
            SFXManager.Instance.Play("cave_entrance");
            SFXManager.Instance.Play("foot#1");

            // Show Q1
            ShowQuestionUI(0);

            // Start first transition video (plays during Q1)
            TryStartNextVideo();
        }

        private void OnDestroy()
        {
            if (questionUI != null)
                questionUI.OnAnswerClicked -= HandleAnswerClicked;

            if (godReveal != null)
                godReveal.OnRevealComplete -= HandleRevealComplete;

            if (titleScreen != null)
                titleScreen.OnTitleComplete -= HandleTitleComplete;
        }

        // ── Show Question UI ────────────────────────────────────

        private void ShowQuestionUI(int questionIndex)
        {
            QuestionData q = questionData.Questions[questionIndex];
            List<AnswerData> a = q.Answers;

            questionUI.ShowQuestion(
                q.Text,
                a[0].Text, // A
                a[1].Text, // B
                a[2].Text, // C
                a[3].Text  // D
            );

            Debug.Log($"[Flow] Showing question {questionIndex + 1}/6: {q.Id}");
        }

        // ── Handle Answer ──────────────────────────────────────

        private void HandleAnswerClicked(string answerId)
        {
            if (currentState != State.Questionnaire || answeredCount >= 6)
            {
                Debug.Log($"[Flow] Answer click ignored — state: {currentState}, answered: {answeredCount}");
                return;
            }

            int questionIndex = answeredCount;
            QuestionData q = questionData.Questions[questionIndex];
            AnswerData chosenAnswer = q.Answers.Find(a => a.Id == answerId);

            if (chosenAnswer == null)
            {
                Debug.Log($"[Flow] Answer ID '{answerId}' not found in question {q.Id}.");
                return;
            }

            // Score
            var (actionDelta, empathyDelta) = ScoringRule.GetDeltas(answerId);

            // Record
            result.Questions.Add(new AnswerRecord
            {
                QuestionId = q.Id,
                QuestionText = q.Text,
                AnswerId = chosenAnswer.Id,
                AnswerText = chosenAnswer.Text,
                ActionDelta = actionDelta,
                EmpathyDelta = empathyDelta
            });

            result.FinalScores.Action += actionDelta;
            result.FinalScores.Empathy += empathyDelta;

            Debug.Log($"[Flow] {q.Id} → {answerId} | Action: {result.FinalScores.Action}, Empathy: {result.FinalScores.Empathy}");

            answeredCount++;

            if (answeredCount < 6)
            {
                // Show next question immediately (video keeps playing in background)
                ShowQuestionUI(answeredCount);
            }
            else
            {
                // All 6 answered — hide question UI
                questionUI.Hide();

                // Scores are final — compute gods & fire LLM NOW
                // (videos may still be catching up; the LLM request runs in parallel)
                (primaryGod, secondaryGod) = GodAssignment.Evaluate(
                    result.FinalScores.Action,
                    result.FinalScores.Empathy,
                    godData.Gods
                );
                FireNarratorRequest();
                FireDialogueGeneration();
            }

            // Try to advance the video pipeline
            TryStartNextVideo();
        }

        // ── Video Pipeline (decoupled from questions) ───────────

        /// <summary>
        /// Checks if a new transition clip should start.
        /// Called after an answer AND after a video finishes.
        /// </summary>
        private void TryStartNextVideo()
        {
            if (isTransitionPlaying) return;

            // How many transition clips SHOULD have started by now:
            // Clip N starts when question N+1 is shown (or at start for clip 0).
            // Questions shown = answeredCount + 1 (Q1 is shown before any answer).
            // Only 5 transition clips exist (0–4).
            int desiredClips = Math.Min(answeredCount + 1, 5);

            if (videosStarted < desiredClips)
            {
                // Start the next transition clip
                StartTransitionVideo(videosStarted);
            }
            else if (answeredCount >= 6 && videosStarted >= 5)
            {
                // All questions answered + all 5 transition clips done → outro
                StartOutro();
            }
            // else: video caught up to the player, wait for next answer
        }

        private void StartTransitionVideo(int clipIndex)
        {
            isTransitionPlaying = true;
            videosStarted = clipIndex + 1;

            int bgIndex = clipIndex + 1;
            background.CrossfadeTo(bgIndex);

            // Random footstep SFX with each transition
            string footstep = FootstepIds[UnityEngine.Random.Range(0, FootstepIds.Length)];
            SFXManager.Instance.Play(footstep);

            characterDisplay.PlayTransition(clipIndex, () =>
            {
                isTransitionPlaying = false;
                TryStartNextVideo();
            });

            Debug.Log($"[Flow] Playing transition clip {clipIndex} (bg → {bgIndex}), sfx: {footstep}");
        }

        // ── Outro (after Q6) ───────────────────────────────────

        private GodData primaryGod;
        private GodData secondaryGod;

        private void StartOutro()
        {
            currentState = State.PlayingOutro;

            Debug.Log("[Flow] All transition clips done. Starting outro...");

            int outroClipIndex = 5; // last clip in the array

            // Crossfade background to black
            background.CrossfadeToBlack();

            // Play outro video
            // (God assignment + LLM request were already fired in HandleAnswerClicked
            //  when answeredCount reached 6, so the LLM has had time to respond.)
            characterDisplay.PlayTransition(outroClipIndex, () =>
            {
                // Outro finished → show reveal
                characterDisplay.Hide();
                StartGodReveal();
            });
        }

        // ── Narrator LLM Request ────────────────────────────────

        private Coroutine statusCoroutine;

        private void FireNarratorRequest()
        {
            if (llmConfig == null)
            {
                Debug.Log("[Flow] No LLMConfigSO assigned. Narrator will be skipped.");
                godReveal.SetNarratorFailed();
                return;
            }

            // Show status
            ShowLLMStatus(messageSending);

            // Build a temporary profile for the request
            var profile = new PlayerProfileData
            {
                PrimaryGod = GodReference.FromGodData(primaryGod),
                SecondaryGod = GodReference.FromGodData(secondaryGod),
                ActionScore = result.FinalScores.Action,
                EmpathyScore = result.FinalScores.Empathy,
                Questionnaire = result
            };

            // Use the narrator prompt template, or fall back to defaults
            PromptTemplateSO template = narratorPromptTemplate;
            if (template == null)
            {
                Debug.Log("[Flow] No narrator PromptTemplateSO assigned. Using built-in defaults.");
                template = ScriptableObject.CreateInstance<PromptTemplateSO>();
                template.systemPrompt = NarratorPromptDefaults.SystemPrompt;
                template.jsonSchema = NarratorPromptDefaults.JsonSchema;
                template.schemaVersion = NarratorPromptDefaults.SchemaVersion;
            }

            string requestBody = NarratorRequestBuilder.Build(profile, template, llmConfig);
            Debug.Log("[Flow] Firing narrator LLM request...");

            StartCoroutine(LLMClient.SendRequest(requestBody, llmConfig,
                onSuccess: OnNarratorSuccess,
                onError: OnNarratorError
            ));
        }

        private void OnNarratorSuccess(string rawResponse)
        {
            if (NarratorResponseParser.TryParse(rawResponse, out string narratorText, out string error))
            {
                Debug.Log($"[Flow] Narrator response: {narratorText}");
                godReveal.SetNarratorText(narratorText);
                ShowLLMStatus(messageSuccess);
            }
            else
            {
                Debug.Log($"[Flow] Narrator parse failed: {error}");
                godReveal.SetNarratorFailed();
                ShowLLMStatus(messageFailure);
            }
        }

        private void OnNarratorError(string error)
        {
            Debug.Log($"[Flow] Narrator LLM request failed: {error}");
            godReveal.SetNarratorFailed();
            ShowLLMStatus(messageFailure);
        }

        // ── Dialogue Generation (LLM) ────────────────────────────

        private void FireDialogueGeneration()
        {
            if (dialogueGenerator == null)
            {
                Debug.Log("[Flow] No DialogueGeneratorService assigned. Dialogue generation skipped.");
                return;
            }

            var profile = new PlayerProfileData
            {
                PrimaryGod = GodReference.FromGodData(primaryGod),
                SecondaryGod = GodReference.FromGodData(secondaryGod),
                ActionScore = result.FinalScores.Action,
                EmpathyScore = result.FinalScores.Empathy,
                Questionnaire = result
            };

            Debug.Log("[Flow] Firing dialogue generation LLM request...");
            dialogueGenerator.Generate(profile);
        }

        // ── LLM Status Text ─────────────────────────────────────

        private void ShowLLMStatus(string message)
        {
            if (llmStatusText == null) return;

            // Cancel any running fade
            if (statusCoroutine != null)
                StopCoroutine(statusCoroutine);

            statusCoroutine = StartCoroutine(StatusSequence(message));
        }

        private IEnumerator StatusSequence(string message)
        {
            llmStatusText.text = message;
            llmStatusText.gameObject.SetActive(true);

            // Fade in
            float elapsed = 0f;
            float fadeIn = 0.3f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.deltaTime;
                SetTextAlpha(llmStatusText, Mathf.Clamp01(elapsed / fadeIn));
                yield return null;
            }
            SetTextAlpha(llmStatusText, 1f);

            // Hold
            yield return new WaitForSeconds(statusDisplayDuration);

            // Fade out
            elapsed = 0f;
            while (elapsed < statusFadeDuration)
            {
                elapsed += Time.deltaTime;
                SetTextAlpha(llmStatusText, 1f - Mathf.Clamp01(elapsed / statusFadeDuration));
                yield return null;
            }

            SetTextAlpha(llmStatusText, 0f);
            llmStatusText.gameObject.SetActive(false);
            statusCoroutine = null;
        }

        private static void SetTextAlpha(TextMeshProUGUI text, float alpha)
        {
            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }

        // ── God Reveal ──────────────────────────────────────────

        private void StartGodReveal()
        {
            currentState = State.RevealingGods;

            // Subscribe to reveal completion
            godReveal.OnRevealComplete += HandleRevealComplete;

            // Start the reveal sequence on the black screen
            godReveal.ShowReveal(primaryGod, secondaryGod);
        }

        private void HandleRevealComplete()
        {
            godReveal.OnRevealComplete -= HandleRevealComplete;

            // If a transition clip and scene name are configured, transition to the level
            if (levelTransitionClip != null && !string.IsNullOrEmpty(targetSceneName))
            {
                StartCoroutine(TransitionToLevelSequence());
            }
            else
            {
                // No transition configured — complete immediately
                CompleteQuestionnaire();
            }
        }

        // ── Transition to Level ─────────────────────────────────

        private IEnumerator TransitionToLevelSequence()
        {
            currentState = State.TransitionToLevel;

            Debug.Log($"[Flow] Starting level transition. Video: {levelTransitionClip.name}, Scene: {targetSceneName}");

            // 1. Begin async scene load in the background (don't activate yet)
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = false;
                Debug.Log("[Flow] Async scene load started in background.");
            }
            else
            {
                Debug.LogError($"[Flow] Failed to start async load for scene '{targetSceneName}'. Is it in Build Settings?");
            }

            // 2. Play the transition video
            bool videoFinished = false;
            characterDisplay.PlayClip(levelTransitionClip, () =>
            {
                videoFinished = true;
            });

            // Wait for the video to finish
            while (!videoFinished)
                yield return null;

            Debug.Log("[Flow] Level transition video finished.");

            // 3. Fade to black
            if (fadeToBlackOverlay != null)
            {
                fadeToBlackOverlay.gameObject.SetActive(true);
                Color c = fadeToBlackOverlay.color;
                c.a = 0f;
                fadeToBlackOverlay.color = c;

                float elapsed = 0f;
                while (elapsed < fadeToBlackDuration)
                {
                    elapsed += Time.deltaTime;
                    c.a = Mathf.Clamp01(elapsed / fadeToBlackDuration);
                    fadeToBlackOverlay.color = c;
                    yield return null;
                }

                c.a = 1f;
                fadeToBlackOverlay.color = c;
                Debug.Log("[Flow] Fade to black complete.");
            }

            // 4. Wait for async load to be ready (progress reaches 0.9)
            if (asyncLoad != null)
            {
                while (asyncLoad.progress < 0.9f)
                {
                    Debug.Log($"[Flow] Scene loading... {asyncLoad.progress * 100:F0}%");
                    yield return null;
                }

                Debug.Log("[Flow] Scene ready. Activating...");

                // Build profile before we leave this scene
                CompleteQuestionnaire();

                // 5. Activate the loaded scene
                asyncLoad.allowSceneActivation = true;
            }
            else
            {
                // Async load failed earlier — just complete
                CompleteQuestionnaire();
            }
        }

        // ── Completion ─────────────────────────────────────────

        private void CompleteQuestionnaire()
        {
            currentState = State.Completed;

            // Build the full player profile
            var profile = new PlayerProfileData
            {
                PrimaryGod = GodReference.FromGodData(primaryGod),
                SecondaryGod = GodReference.FromGodData(secondaryGod),
                ActionScore = result.FinalScores.Action,
                EmpathyScore = result.FinalScores.Empathy,
                Questionnaire = result
            };

            Debug.Log($"[Flow] Questionnaire completed. Profile:\n{profile.ToJson()}");

            OnQuestionnaireCompleted?.Invoke(profile);
        }
    }
}
