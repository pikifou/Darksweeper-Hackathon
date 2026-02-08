using System.Collections;
using System.Collections.Generic;
using Mines.Data;
using Mines.Logic;
using Mines.Presentation;
using Sweeper.Data;
using Sweeper.Flow;
using Sweeper.Presentation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Mines.Flow
{
    /// <summary>
    /// Single orchestrator for all mine interactions.
    ///
    /// Flow per event type:
    /// - <b>Combat / Chest / Shrine</b>: auto-resolved immediately, result shown
    ///   in a non-blocking <see cref="MineEventToast"/>. Input is NOT blocked.
    /// - <b>Dialogue</b>: modal <see cref="MineEventPanel"/> with choices.
    ///   Input IS blocked until the player picks and clicks Continue.
    /// </summary>
    public class MineEventController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SweeperGameController sweeper;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private MineEventPanel panel;
        [SerializeField] private MineEventToast toast;
        [SerializeField] private GridRenderer gridRenderer;

        [Header("Sentence Ending (LV3)")]
        [Tooltip("Canvas (Screen Space - Overlay, high sort order) for fullscreen sentence endings. Starts disabled.")]
        [SerializeField] private Canvas sentenceCanvas;
        [Tooltip("RawImage filling the canvas — receives the VideoPlayer RenderTexture.")]
        [SerializeField] private RawImage sentenceVideoDisplay;
        [Tooltip("VideoPlayer for sentence ending videos.")]
        [SerializeField] private VideoPlayer sentenceVideoPlayer;

        [Header("Configuration")]
        [SerializeField] private MineDistributionSO distribution;
        [Tooltip("Fallback encounter pool used when levelData has no pool assigned.")]
        [SerializeField] private EncounterPoolSO fallbackPool;
        [Tooltip("Optional — if set, uses encounter type targets and encounter pool from the level data.")]
        [SerializeField] private LevelDataSO levelData;

        // Runtime state
        private Dictionary<(int, int), MineEventData> mineEvents;
        private RunLog runLog = new();
        private MineEventData currentEvent;    // event currently in DIALOGUE flow (null otherwise)
        private RenderTexture sentenceRenderTexture; // created at runtime for sentence video

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Awake()
        {
            // Ensure the sentence ending canvas is hidden at start
            if (sentenceCanvas != null)
                sentenceCanvas.enabled = false;
            if (sentenceVideoDisplay != null)
                sentenceVideoDisplay.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Debug.Log($"[MineEvents] OnEnable — sweeper={(sweeper != null ? sweeper.name : "NULL")}, " +
                      $"distribution={(distribution != null ? distribution.name : "NULL")}, " +
                      $"fallbackPool={(fallbackPool != null ? fallbackPool.name : "NULL")}, " +
                      $"panel={(panel != null ? panel.name : "NULL")}, " +
                      $"toast={(toast != null ? toast.name : "NULL")}, " +
                      $"inputHandler={(inputHandler != null ? "OK" : "NULL")}, " +
                      $"gridRenderer={(gridRenderer != null ? "OK" : "NULL")}");
            if (sweeper != null)
            {
                sweeper.OnGridReady += HandleGridReady;
                sweeper.OnLeftClickMine += HandleLeftClickMine;
                sweeper.OnRightClickMine += HandleRightClickMine;
                Debug.Log("[MineEvents] Subscribed to sweeper events.");
            }
            else
            {
                Debug.LogError("[MineEvents] sweeper is NULL — cannot subscribe to events!");
            }
        }

        private void OnDisable()
        {
            if (sweeper != null)
            {
                sweeper.OnGridReady -= HandleGridReady;
                sweeper.OnLeftClickMine -= HandleLeftClickMine;
                sweeper.OnRightClickMine -= HandleRightClickMine;
            }
        }

        // ================================================================
        // Grid Ready — assign events to all mines
        // ================================================================

        private void HandleGridReady(GridModel grid)
        {
            Debug.Log($"[MineEvents] HandleGridReady received — grid {grid.Width}x{grid.Height}, {grid.MineCount} mines.");
            if (distribution == null)
            {
                Debug.LogWarning("[MineEvents] Distribution SO is null — mine events disabled.");
                mineEvents = new Dictionary<(int, int), MineEventData>();
                return;
            }

            EncounterPoolSO pool = levelData != null && levelData.encounterPool != null
                ? levelData.encounterPool
                : fallbackPool;

            if (pool == null)
                Debug.LogWarning("[MineEvents] No encounter pool assigned. Events will use distribution defaults.");

            // Build forced types map from level data
            Dictionary<(int, int), MineEventType> forcedTypes = null;
            if (levelData != null && levelData.cells != null)
            {
                forcedTypes = new Dictionary<(int, int), MineEventType>();
                for (int x = 0; x < levelData.width; x++)
                {
                    for (int y = 0; y < levelData.height; y++)
                    {
                        CellTag tag = levelData.GetCell(x, y);
                        if (!tag.IsSpecificEncounter()) continue;
                        MineEventType eventType = tag switch
                        {
                            CellTag.Combat   => MineEventType.Combat,
                            CellTag.Chest    => MineEventType.Chest,
                            CellTag.Dialogue => MineEventType.Dialogue,
                            CellTag.Shrine   => MineEventType.Shrine,
                            CellTag.Sentence => MineEventType.Sentence,
                            _                => MineEventType.Combat
                        };
                        forcedTypes[(x, y)] = eventType;
                    }
                }
                if (forcedTypes.Count == 0) forcedTypes = null;
            }

            bool hasTargets = levelData != null && levelData.HasEncounterTargets;
            if (hasTargets)
            {
                mineEvents = MineEventLogic.AssignEventsWithTargets(
                    grid, levelData.targetCombat, levelData.targetChest,
                    levelData.targetDialogue, levelData.targetShrine,
                    levelData.targetSentence,
                    forcedTypes, distribution, pool);
            }
            else
            {
                mineEvents = MineEventLogic.AssignEvents(grid, distribution, pool, forcedTypes);
            }

            runLog.Clear();
            Debug.Log($"[MineEvents] {mineEvents.Count} mine events assigned" +
                      (hasTargets ? " (from level targets)." : " (from distribution weights).") +
                      (pool != null ? $" Pool: {pool.name}." : " No pool.") +
                      (forcedTypes != null ? $" ({forcedTypes.Count} forced)" : ""));
        }

        // ================================================================
        // Right-Click on Mine — normal interaction
        // ================================================================

        private void HandleRightClickMine(int x, int y, CellData cell)
        {
            if (mineEvents == null) return;
            if (!mineEvents.TryGetValue((x, y), out MineEventData data)) return;
            if (data.state == MineState.Resolved) return;

            if (data.state == MineState.Hidden)
                data.state = MineState.Revealed;

            Debug.Log($"[MineEvents] RightClick ({x},{y}): type={data.eventType}");

            if (data.eventType == MineEventType.Sentence)
            {
                // --- SENTENCE: fullscreen ending video, no dialogue panel ---
                if (inputHandler != null) inputHandler.inputBlocked = true;
                data.state = MineState.Resolved;
                PlaySentenceEnding(data.videoClip);
                Debug.Log($"[MineEvents] Sentence at ({x},{y}) — launching fullscreen ending video.");
                return;
            }

            if (data.eventType == MineEventType.Dialogue)
            {
                // --- DIALOGUE: modal panel with intro video + choices → result → continue ---
                currentEvent = data;
                if (inputHandler != null) inputHandler.inputBlocked = true;

                var dpLog = data.dialogueParams;
                Debug.Log($"[MineEvents] Dialogue data: character='{dpLog?.characterName ?? "NULL"}', " +
                          $"prompt='{dpLog?.promptText ?? "NULL"}', " +
                          $"choices={dpLog?.choices?.Length ?? 0}, " +
                          $"characterSO={(dpLog?.character != null ? dpLog.character.characterId : "NULL")}");

                InteractionDescriptor descriptor = MineEventLogic.GetInteraction(data);
                VideoClip introClip = data.videoClip; // intro clip comes from the character SO

                if (panel != null)
                {
                    panel.Show(descriptor, OnPanelDialogueChoice, introClip);
                }
                else
                {
                    // No panel — auto-pick first choice
                    if (descriptor.choices != null && descriptor.choices.Length > 0)
                        OnPanelDialogueChoice(descriptor.choices[0].choice);
                }
            }
            else
            {
                // --- COMBAT / CHEST / SHRINE: auto-resolve, toast, no input block ---
                ResolveQuick(data, false);
            }
        }

        // ================================================================
        // Left-Click on Mine — accidental, penalty
        // ================================================================

        private void HandleLeftClickMine(int x, int y, CellData cell)
        {
            if (mineEvents == null) return;
            if (!mineEvents.TryGetValue((x, y), out MineEventData data)) return;
            if (data.state == MineState.Resolved) return;

            if (data.state == MineState.Hidden)
                data.state = MineState.Revealed;

            Debug.Log($"[MineEvents] LeftClick penalty ({x},{y}): type={data.eventType}");
            ResolveQuick(data, true);
        }

        // ================================================================
        // Quick Resolution (Combat / Chest / Shrine + all left-clicks)
        // ================================================================

        /// <summary>
        /// Auto-resolve the event, apply effects, show toast, show icon.
        /// Does NOT block input — the player keeps playing.
        /// </summary>
        private void ResolveQuick(MineEventData data, bool leftClickPenalty)
        {
            int hpBefore = sweeper != null ? sweeper.CurrentHP : 0;
            ResolutionResult result;
            PlayerChoice choiceMade;

            if (leftClickPenalty && data.eventType == MineEventType.Combat)
            {
                // Combat with x2 penalty
                var cp = data.combatParams;
                int playerForce = distribution != null ? distribution.playerForce : 1;
                result = CombatLogic.Resolve(playerForce, cp.creatureForce, true, hpBefore);
                if (cp.reward != RewardType.None)
                {
                    result.reward = cp.reward;
                    result.rewardValue = cp.rewardValue;
                }
                choiceMade = PlayerChoice.Engage;
            }
            else if (leftClickPenalty)
            {
                // Non-combat left-click: interaction destroyed
                result = new ResolutionResult
                {
                    hpDelta = 0,
                    reward = RewardType.None,
                    rewardValue = 0,
                    resultText = "L'interaction a ete detruite par votre approche imprudente.\nVous ne trouvez rien.",
                    playerDied = false
                };
                choiceMade = PlayerChoice.Ignore;
            }
            else
            {
                // Normal right-click auto-resolve
                switch (data.eventType)
                {
                    case MineEventType.Combat:
                        var cp = data.combatParams;
                        int playerForce = distribution != null ? distribution.playerForce : 1;
                        result = CombatLogic.Resolve(playerForce, cp.creatureForce, false, hpBefore);
                        if (cp.reward != RewardType.None)
                        {
                            result.reward = cp.reward;
                            result.rewardValue = cp.rewardValue;
                        }
                        choiceMade = PlayerChoice.Engage;
                        break;

                    case MineEventType.Chest:
                        result = MineEventLogic.ResolveChest(data, PlayerChoice.Open, hpBefore);
                        choiceMade = PlayerChoice.Open;
                        break;

                    case MineEventType.Shrine:
                        result = MineEventLogic.ResolveShrine(data, PlayerChoice.Sacrifice, hpBefore);
                        choiceMade = PlayerChoice.Sacrifice;
                        break;

                    default:
                        result = new ResolutionResult { resultText = "..." };
                        choiceMade = PlayerChoice.Ignore;
                        break;
                }
            }

            // --- Apply effects ---

            // HP
            if (result.hpDelta != 0 && sweeper != null)
                sweeper.ApplyHPDelta(result.hpDelta);

            // Reward
            string rewardDesc = "";
            if (result.reward != RewardType.None)
            {
                rewardDesc = RewardLogic.ApplyReward(
                    result.reward, result.rewardValue,
                    applyHpDelta: delta => sweeper?.ApplyHPDelta(delta),
                    currentRevealRadius: sweeper?.Config?.revealRadius ?? 3,
                    setRevealRadius: r => { if (sweeper?.Config != null) sweeper.Config.revealRadius = r; },
                    setBuff: n => { if (sweeper != null) sweeper.BuffCombatsRemaining = n; }
                );
            }

            // Mark resolved
            data.state = MineState.Resolved;
            data.choiceMade = choiceMade;
            data.hpDelta = result.hpDelta;
            data.rewardGiven = result.reward;

            // Log
            int hpAfter = sweeper != null ? sweeper.CurrentHP : hpBefore + result.hpDelta;
            runLog.Record(new RunEvent
            {
                eventType = data.eventType,
                eventId = data.eventId,
                tileX = data.x,
                tileY = data.y,
                levelId = "",
                hpBefore = hpBefore,
                hpAfter = hpAfter,
                choice = choiceMade,
                reward = result.reward,
                wasLeftClickPenalty = leftClickPenalty
            });

            Debug.Log($"[MineEvents] Quick-resolved {data.eventType} at ({data.x},{data.y}): " +
                      $"choice={choiceMade}, hpDelta={result.hpDelta}, reward={result.reward}" +
                      (leftClickPenalty ? " (LEFT-CLICK PENALTY)" : ""));

            // --- Show toast ---
            string title = GetEventTitle(data);
            var clip = (leftClickPenalty && data.penaltyVideoClip != null)
                ? data.penaltyVideoClip
                : data.videoClip;
            if (toast != null)
                toast.Show(data.eventType, title, result, rewardDesc, leftClickPenalty, clip);

            // --- Show resolved icon on cell and lift fog on that one cell ---
            if (gridRenderer != null)
            {
                CellView cv = gridRenderer.GetCellView(data.x, data.y);
                if (cv != null)
                    cv.ShowMineResolved(data.eventType);
            }
            if (sweeper != null)
                sweeper.RevealCellFog(data.x, data.y);

            // --- Check if all mines are now resolved ---
            CheckAllMinesResolved();
        }

        // ================================================================
        // Dialogue flow (two-phase toast with choices)
        // ================================================================

        /// <summary>
        /// Player picked a choice in the dialogue panel.
        /// Resolves the dialogue, applies effects, then shows the result on the panel.
        /// </summary>
        private void OnPanelDialogueChoice(PlayerChoice choice)
        {
            if (currentEvent == null) return;

            int hpBefore = sweeper != null ? sweeper.CurrentHP : 0;
            ResolutionResult result = MineEventLogic.ResolveDialogue(currentEvent, choice, hpBefore);

            // Apply effects
            if (result.hpDelta != 0 && sweeper != null)
                sweeper.ApplyHPDelta(result.hpDelta);

            string rewardDesc = "";
            if (result.reward != RewardType.None)
            {
                rewardDesc = RewardLogic.ApplyReward(
                    result.reward, result.rewardValue,
                    applyHpDelta: delta => sweeper?.ApplyHPDelta(delta),
                    currentRevealRadius: sweeper?.Config?.revealRadius ?? 3,
                    setRevealRadius: r => { if (sweeper?.Config != null) sweeper.Config.revealRadius = r; },
                    setBuff: n => { if (sweeper != null) sweeper.BuffCombatsRemaining = n; }
                );
            }

            // Mark resolved
            currentEvent.state = MineState.Resolved;
            currentEvent.choiceMade = choice;
            currentEvent.hpDelta = result.hpDelta;
            currentEvent.rewardGiven = result.reward;

            // Log
            int hpAfter = sweeper != null ? sweeper.CurrentHP : hpBefore + result.hpDelta;
            runLog.Record(new RunEvent
            {
                eventType = currentEvent.eventType,
                eventId = currentEvent.eventId,
                tileX = currentEvent.x,
                tileY = currentEvent.y,
                levelId = "",
                hpBefore = hpBefore,
                hpAfter = hpAfter,
                choice = choice,
                reward = result.reward,
                wasLeftClickPenalty = false
            });

            Debug.Log($"[MineEvents] Dialogue resolved at ({currentEvent.x},{currentEvent.y}): " +
                      $"choice={choice}, hpDelta={result.hpDelta}, reward={result.reward}");

            // Look up the generic result video for this action type
            EncounterPoolSO pool = GetActivePool();
            VideoClip resultClip = pool != null ? pool.GetActionResultClip(choice) : null;

            // Show result on the panel, then OnPanelContinue when the player clicks Continue
            if (panel != null)
                panel.ShowResult(result, rewardDesc, false, OnPanelContinue, resultClip);
            else
                OnPanelContinue();
        }

        /// <summary>
        /// Called when the player clicks Continue on the dialogue panel.
        /// Hides the panel, shows resolved icon, lifts fog, unblocks input.
        /// </summary>
        private void OnPanelContinue()
        {
            // Hide the panel
            if (panel != null) panel.Hide();

            // Show resolved icon and lift fog on that one cell
            if (currentEvent != null)
            {
                if (gridRenderer != null)
                {
                    CellView cv = gridRenderer.GetCellView(currentEvent.x, currentEvent.y);
                    if (cv != null)
                        cv.ShowMineResolved(currentEvent.eventType);
                }
                if (sweeper != null)
                    sweeper.RevealCellFog(currentEvent.x, currentEvent.y);
            }

            // Resume input
            if (inputHandler != null) inputHandler.inputBlocked = false;

            currentEvent = null;

            // Check if all mines are now resolved
            CheckAllMinesResolved();
        }

        // ================================================================
        // Victory Check — all mines resolved
        // ================================================================

        /// <summary>
        /// Check if every mine event has been resolved.
        /// Sentence encounters are excluded (they are the ending, not a victory condition).
        /// If all non-Sentence mines are resolved, triggers victory on the sweeper.
        /// </summary>
        private void CheckAllMinesResolved()
        {
            if (mineEvents == null || mineEvents.Count == 0) return;
            if (sweeper == null) return;
            if (sweeper.CurrentState == GameState.Won || sweeper.CurrentState == GameState.Lost) return;

            foreach (var kvp in mineEvents)
            {
                MineEventData data = kvp.Value;
                // Skip sentence encounters — they have their own ending flow
                if (data.eventType == MineEventType.Sentence) continue;
                if (data.state != MineState.Resolved) return; // at least one not resolved
            }

            // All non-Sentence mines are resolved — victory!
            Debug.Log("[MineEvents] All mines resolved — triggering victory.");
            sweeper.TriggerVictory();
        }

        // ================================================================
        // Sentence Ending (fullscreen video)
        // ================================================================

        /// <summary>
        /// Play a fullscreen ending video for a sentence encounter.
        /// Activates the sentence canvas, sets up a RenderTexture, and plays the clip.
        /// The video stays on screen — this is the game ending.
        /// </summary>
        private void PlaySentenceEnding(VideoClip clip)
        {
            if (sentenceVideoPlayer == null || sentenceCanvas == null)
            {
                Debug.LogWarning("[MineEvents] Sentence canvas or video player not assigned. Cannot play ending.");
                return;
            }

            if (clip == null)
            {
                Debug.LogWarning("[MineEvents] Sentence encounter has no video clip assigned.");
                return;
            }

            // Activate fullscreen canvas
            sentenceCanvas.enabled = true;

            // Create RenderTexture at screen resolution
            sentenceRenderTexture = new RenderTexture(Screen.width, Screen.height, 0);
            sentenceRenderTexture.Create();

            // Configure VideoPlayer
            sentenceVideoPlayer.clip = clip;
            sentenceVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            sentenceVideoPlayer.targetTexture = sentenceRenderTexture;
            sentenceVideoPlayer.isLooping = false;
            sentenceVideoPlayer.playOnAwake = false;
            sentenceVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            // Assign to display
            if (sentenceVideoDisplay != null)
            {
                sentenceVideoDisplay.texture = sentenceRenderTexture;
                sentenceVideoDisplay.gameObject.SetActive(true);
            }

            // Pause on last frame when done
            sentenceVideoPlayer.loopPointReached += OnSentenceVideoFinished;
            sentenceVideoPlayer.Play();

            Debug.Log($"[MineEvents] Playing sentence ending video: {clip.name}");
        }

        private void OnSentenceVideoFinished(VideoPlayer vp)
        {
            // Pause so the last frame stays visible (this is the ending screen)
            vp.Pause();
            vp.loopPointReached -= OnSentenceVideoFinished;
            Debug.Log("[MineEvents] Sentence ending video finished — paused on last frame.");
        }

        private void OnDestroy()
        {
            if (sentenceRenderTexture != null)
            {
                sentenceRenderTexture.Release();
                Destroy(sentenceRenderTexture);
                sentenceRenderTexture = null;
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>Returns the active encounter pool (level-specific or fallback).</summary>
        private EncounterPoolSO GetActivePool()
        {
            return levelData != null && levelData.encounterPool != null
                ? levelData.encounterPool
                : fallbackPool;
        }

        private static string GetEventTitle(MineEventData data)
        {
            return data.eventType switch
            {
                MineEventType.Combat => data.combatParams?.monsterName ?? "Combat",
                MineEventType.Chest => "Coffre",
                MineEventType.Shrine => "Sanctuaire",
                MineEventType.Dialogue => data.dialogueParams?.characterName ?? "Dialogue",
                _ => "Evenement"
            };
        }

        // ================================================================
        // Public Accessors
        // ================================================================

        public RunLog GetRunLog() => runLog;

        public MineEventData GetMineEvent(int x, int y)
        {
            if (mineEvents == null) return null;
            mineEvents.TryGetValue((x, y), out MineEventData data);
            return data;
        }
    }
}
