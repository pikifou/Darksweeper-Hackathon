using System;
using System.Collections.Generic;
using Mines.Data;
using Mines.Flow;
using Sweeper.Data;
using UnityEngine;

namespace Mines.Logic
{
    /// <summary>
    /// Pure functions for mine event assignment and resolution.
    /// No state, no Unity lifecycle — static class.
    /// </summary>
    public static class MineEventLogic
    {
        // ================================================================
        // Event Assignment (called once at level start)
        // ================================================================

        /// <summary>
        /// Iterate all mine cells in the grid and assign an event type + params to each.
        /// Returns a dictionary keyed by (x, y) coordinates.
        /// Content is drawn from the EncounterPoolSO.
        /// Falls back to MineDistributionSO defaults if pool is null or empty for a type.
        /// </summary>
        /// <param name="forcedTypes">Optional dict of (x,y) → forced MineEventType from level painter.
        /// Cells not in this dict get a random type from distribution weights.</param>
        public static Dictionary<(int, int), MineEventData> AssignEvents(
            GridModel grid,
            MineDistributionSO distribution,
            EncounterPoolSO pool,
            Dictionary<(int, int), MineEventType> forcedTypes = null)
        {
            var result = new Dictionary<(int, int), MineEventData>();
            int eventCounter = 0;
            int forcedCount = 0;

            // Count mines by type to pre-draw from pools
            var minePositions = new List<(int x, int y, MineEventType type)>();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    CellData cell = grid.GetCell(x, y);
                    if (cell == null || !cell.hasMine) continue;

                    MineEventType eventType;
                    if (forcedTypes != null && forcedTypes.TryGetValue((x, y), out var forced))
                    {
                        eventType = forced;
                        forcedCount++;
                    }
                    else
                    {
                        eventType = PickEventType(distribution);
                    }
                    minePositions.Add((x, y, eventType));
                }
            }

            // Pre-draw encounters from pools
            var drawnEncounters = PreDrawEncounters(minePositions, pool);

            for (int i = 0; i < minePositions.Count; i++)
            {
                var (x, y, eventType) = minePositions[i];
                MineEventData data = new MineEventData
                {
                    x = x,
                    y = y,
                    eventType = eventType,
                    state = MineState.Hidden,
                    eventId = $"evt_{eventCounter++}_{x}_{y}"
                };

                PopulateParamsFromPool(data, drawnEncounters[i], distribution);
                result[(x, y)] = data;
            }

            Debug.Log($"[MineEvents] Assigned {result.Count} mine events ({forcedCount} forced from level data).");
            return result;
        }

        /// <summary>
        /// Assign events to mines using exact target counts per type.
        /// Content is drawn from the EncounterPoolSO.
        /// </summary>
        public static Dictionary<(int, int), MineEventData> AssignEventsWithTargets(
            GridModel grid,
            int targetCombat, int targetChest, int targetDialogue, int targetShrine,
            Dictionary<(int, int), MineEventType> forcedTypes,
            MineDistributionSO distribution,
            EncounterPoolSO pool)
        {
            var result = new Dictionary<(int, int), MineEventData>();

            // Separate forced and non-forced mine positions
            var forcedPositions = new List<(int x, int y)>();
            var freePositions = new List<(int x, int y)>();
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                    if (grid.GetCell(x, y)?.hasMine == true)
                    {
                        if (forcedTypes != null && forcedTypes.ContainsKey((x, y)))
                            forcedPositions.Add((x, y));
                        else
                            freePositions.Add((x, y));
                    }

            // Shuffle free positions for random spatial distribution
            ShuffleList(freePositions);

            // Subtract forced types from targets
            int remainCombat = targetCombat;
            int remainChest = targetChest;
            int remainDialogue = targetDialogue;
            int remainShrine = targetShrine;
            if (forcedTypes != null)
            {
                foreach (var kvp in forcedTypes)
                {
                    switch (kvp.Value)
                    {
                        case MineEventType.Combat: remainCombat = Mathf.Max(0, remainCombat - 1); break;
                        case MineEventType.Chest: remainChest = Mathf.Max(0, remainChest - 1); break;
                        case MineEventType.Dialogue: remainDialogue = Mathf.Max(0, remainDialogue - 1); break;
                        case MineEventType.Shrine: remainShrine = Mathf.Max(0, remainShrine - 1); break;
                    }
                }
            }

            // Build type list for free positions: remaining targets first, then weight-based
            var typeList = new List<MineEventType>();
            for (int i = 0; i < remainCombat; i++) typeList.Add(MineEventType.Combat);
            for (int i = 0; i < remainChest; i++) typeList.Add(MineEventType.Chest);
            for (int i = 0; i < remainDialogue; i++) typeList.Add(MineEventType.Dialogue);
            for (int i = 0; i < remainShrine; i++) typeList.Add(MineEventType.Shrine);

            int specifiedCount = typeList.Count;
            int totalFree = freePositions.Count;

            if (specifiedCount > totalFree)
            {
                Debug.LogWarning($"[MineEvents] Remaining target counts ({specifiedCount}) exceed free mine slots ({totalFree}). " +
                                 "Shuffling type list and truncating to fit.");
                ShuffleList(typeList);
            }

            // Collect all (pos, type) pairs for pre-drawing
            var allMines = new List<(int x, int y, MineEventType type)>();

            // Forced positions
            foreach (var (x, y) in forcedPositions)
                allMines.Add((x, y, forcedTypes[(x, y)]));

            // Free positions
            for (int i = 0; i < totalFree; i++)
            {
                var (x, y) = freePositions[i];
                MineEventType eventType = i < typeList.Count ? typeList[i] : PickEventType(distribution);
                allMines.Add((x, y, eventType));
            }

            // Pre-draw encounters from pools
            var drawnEncounters = PreDrawEncounters(allMines, pool);

            int eventCounter = 0;
            for (int i = 0; i < allMines.Count; i++)
            {
                var (x, y, eventType) = allMines[i];
                var data = new MineEventData
                {
                    x = x,
                    y = y,
                    eventType = eventType,
                    state = MineState.Hidden,
                    eventId = $"evt_{eventCounter++}_{x}_{y}"
                };
                PopulateParamsFromPool(data, drawnEncounters[i], distribution);
                result[(x, y)] = data;
            }

            int fromWeights = Mathf.Max(0, totalFree - specifiedCount);
            Debug.Log($"[MineEvents] Assigned {result.Count} mine events " +
                      $"({forcedPositions.Count} forced, {Mathf.Min(specifiedCount, totalFree)} from targets, {fromWeights} from weights).");
            return result;
        }

        // ================================================================
        // InteractionDescriptor (for UI) — UNCHANGED
        // ================================================================

        /// <summary>
        /// Build the data the UI panel needs to render an interaction.
        /// The UI is dumb — it renders whatever this returns.
        /// </summary>
        public static InteractionDescriptor GetInteraction(MineEventData data)
        {
            var desc = new InteractionDescriptor
            {
                eventType = data.eventType,
                isResolved = data.state == MineState.Resolved,
            };

            switch (data.eventType)
            {
                case MineEventType.Combat:
                    var cp = data.combatParams;
                    desc.title = cp.isElite ? $"Combat Elite : {cp.monsterName}" : $"Combat : {cp.monsterName}";
                    desc.description = $"Une creature de Force {cp.creatureForce} se dresse devant vous.";
                    desc.creatureName = cp.monsterName;
                    desc.creatureForce = cp.creatureForce;
                    desc.isElite = cp.isElite;
                    desc.choices = new[]
                    {
                        new ChoiceOption { choice = PlayerChoice.Engage, label = "Engager le combat", riskHint = $"Force {cp.creatureForce}" }
                    };
                    break;

                case MineEventType.Chest:
                    var ch = data.chestParams;
                    desc.title = "Coffre";
                    desc.description = ch.description;
                    desc.choices = new[]
                    {
                        new ChoiceOption { choice = PlayerChoice.Open, label = "Ouvrir", riskHint = "incertain" },
                        new ChoiceOption { choice = PlayerChoice.Ignore, label = "Ignorer", riskHint = "" }
                    };
                    break;

                case MineEventType.Dialogue:
                    var dp = data.dialogueParams;
                    desc.title = dp.characterName;
                    desc.description = dp.promptText;
                    var choices = new List<ChoiceOption>();
                    foreach (var c in dp.choices)
                    {
                        choices.Add(new ChoiceOption
                        {
                            choice = c.choiceType,
                            label = c.label,
                            riskHint = ""
                        });
                    }
                    desc.choices = choices.ToArray();
                    break;

                case MineEventType.Shrine:
                    var sp = data.shrineParams;
                    desc.title = "Autel";
                    desc.description = sp.description;
                    desc.choices = new[]
                    {
                        new ChoiceOption { choice = PlayerChoice.Sacrifice, label = $"Sacrifier ({sp.sacrificeCost} PV)", riskHint = "sacrifice" },
                        new ChoiceOption { choice = PlayerChoice.Refuse, label = "Refuser", riskHint = "" }
                    };
                    break;
            }

            return desc;
        }

        // ================================================================
        // Resolution Methods — UNCHANGED
        // ================================================================

        public static ResolutionResult ResolveChest(MineEventData data, PlayerChoice choice, int currentHp)
        {
            var cp = data.chestParams;

            if (choice == PlayerChoice.Ignore)
            {
                return new ResolutionResult
                {
                    hpDelta = 0,
                    reward = RewardType.None,
                    rewardValue = 0,
                    resultText = "Vous passez votre chemin. Le coffre reste ferme.",
                    playerDied = false
                };
            }

            // Open
            if (cp.isTrapped)
            {
                int hpAfter = currentHp - cp.trapDamage;
                return new ResolutionResult
                {
                    hpDelta = -cp.trapDamage,
                    reward = cp.reward,
                    rewardValue = cp.rewardValue,
                    resultText = $"Le coffre etait piege ! Vous perdez {cp.trapDamage} PV." +
                                 (cp.reward != RewardType.None ? $"\nMais vous trouvez quelque chose." : ""),
                    playerDied = hpAfter <= 0
                };
            }
            else
            {
                return new ResolutionResult
                {
                    hpDelta = 0,
                    reward = cp.reward,
                    rewardValue = cp.rewardValue,
                    resultText = "Le coffre s'ouvre sans danger. Vous trouvez une recompense.",
                    playerDied = false
                };
            }
        }

        public static ResolutionResult ResolveDialogue(MineEventData data, PlayerChoice choice, int currentHp)
        {
            var dp = data.dialogueParams;

            // Find the matching choice
            foreach (var c in dp.choices)
            {
                if (c.choiceType == choice)
                {
                    int hpAfter = currentHp + c.hpDelta;
                    return new ResolutionResult
                    {
                        hpDelta = c.hpDelta,
                        reward = c.reward,
                        rewardValue = c.rewardValue,
                        resultText = c.resultText,
                        playerDied = hpAfter <= 0
                    };
                }
            }

            // Fallback — should not happen
            return new ResolutionResult
            {
                hpDelta = 0,
                reward = RewardType.None,
                rewardValue = 0,
                resultText = "...",
                playerDied = false
            };
        }

        public static ResolutionResult ResolveShrine(MineEventData data, PlayerChoice choice, int currentHp)
        {
            var sp = data.shrineParams;

            if (choice == PlayerChoice.Refuse)
            {
                return new ResolutionResult
                {
                    hpDelta = 0,
                    reward = RewardType.None,
                    rewardValue = 0,
                    resultText = "Vous refusez l'offrande. L'autel s'eteint.",
                    playerDied = false
                };
            }

            // Sacrifice
            int hpAfter = currentHp - sp.sacrificeCost;
            return new ResolutionResult
            {
                hpDelta = -sp.sacrificeCost,
                reward = sp.reward,
                rewardValue = sp.rewardValue,
                resultText = $"Vous sacrifiez {sp.sacrificeCost} PV. L'autel accepte votre offrande.",
                playerDied = hpAfter <= 0
            };
        }

        // ================================================================
        // Pool Drawing
        // ================================================================

        /// <summary>
        /// Draw N items from a pool of M items.
        /// - If N &lt;= M: pick N unique items (shuffle, take first N)
        /// - If N &gt; M: each item used at least once, extras drawn randomly
        /// - If pool is null/empty: returns N nulls
        /// </summary>
        public static T[] DrawFromPool<T>(T[] pool, int needed) where T : class
        {
            var result = new T[needed];

            if (pool == null || pool.Length == 0)
                return result; // all nulls — caller handles fallback

            if (needed <= pool.Length)
            {
                // Shuffle and take first N (unique picks)
                var shuffled = new T[pool.Length];
                Array.Copy(pool, shuffled, pool.Length);
                ShuffleArray(shuffled);
                Array.Copy(shuffled, result, needed);
            }
            else
            {
                // Use each at least once, then random extras
                var shuffled = new T[pool.Length];
                Array.Copy(pool, shuffled, pool.Length);
                ShuffleArray(shuffled);

                // First pass: one of each
                for (int i = 0; i < pool.Length; i++)
                    result[i] = shuffled[i];

                // Extras: random picks
                for (int i = pool.Length; i < needed; i++)
                    result[i] = pool[UnityEngine.Random.Range(0, pool.Length)];

                // Shuffle final result so guaranteed items aren't always first
                ShuffleArray(result);
            }

            return result;
        }

        // ================================================================
        // Private Helpers
        // ================================================================

        /// <summary>
        /// For each mine in the list, pre-draw an encounter object from the pool.
        /// Returns an object[] of the same length — each entry is the drawn SO (or null for fallback).
        /// </summary>
        private static object[] PreDrawEncounters(
            List<(int x, int y, MineEventType type)> mines,
            EncounterPoolSO pool)
        {
            // Count per type
            int combatCount = 0, chestCount = 0, dialogueCount = 0, shrineCount = 0;
            foreach (var (_, _, type) in mines)
            {
                switch (type)
                {
                    case MineEventType.Combat: combatCount++; break;
                    case MineEventType.Chest: chestCount++; break;
                    case MineEventType.Dialogue: dialogueCount++; break;
                    case MineEventType.Shrine: shrineCount++; break;
                }
            }

            // Draw from pools
            CombatEncounterSO[] drawnCombats = pool != null
                ? DrawFromPool(pool.combatPool, combatCount) : new CombatEncounterSO[combatCount];
            ChestEncounterSO[] drawnChests = pool != null
                ? DrawFromPool(pool.chestPool, chestCount) : new ChestEncounterSO[chestCount];
            DialogueEncounterSO[] drawnDialogues = pool != null
                ? DrawFromPool(pool.dialoguePool, dialogueCount) : new DialogueEncounterSO[dialogueCount];
            ShrineEncounterSO[] drawnShrines = pool != null
                ? DrawFromPool(pool.shrinePool, shrineCount) : new ShrineEncounterSO[shrineCount];

            // Map back to mine order
            var result = new object[mines.Count];
            int ci = 0, chi = 0, di = 0, si = 0;
            for (int i = 0; i < mines.Count; i++)
            {
                switch (mines[i].type)
                {
                    case MineEventType.Combat: result[i] = drawnCombats[ci++]; break;
                    case MineEventType.Chest: result[i] = drawnChests[chi++]; break;
                    case MineEventType.Dialogue: result[i] = drawnDialogues[di++]; break;
                    case MineEventType.Shrine: result[i] = drawnShrines[si++]; break;
                }
            }

            return result;
        }

        /// <summary>
        /// Populate MineEventData params from a drawn encounter SO.
        /// If the SO is null (pool was empty for that type), use MineDistributionSO defaults.
        /// </summary>
        private static void PopulateParamsFromPool(MineEventData data, object drawnSO, MineDistributionSO dist)
        {
            switch (data.eventType)
            {
                case MineEventType.Combat:
                    var combatSO = drawnSO as CombatEncounterSO;
                    if (combatSO != null)
                    {
                        data.combatParams = new CombatParams
                        {
                            monsterName = combatSO.monsterName,
                            creatureForce = combatSO.creatureForce,
                            isElite = combatSO.isElite,
                            reward = combatSO.reward,
                            rewardValue = combatSO.rewardValue
                        };
                        data.videoClip = combatSO.videoClip;
                        data.penaltyVideoClip = combatSO.penaltyVideoClip;
                    }
                    else
                    {
                        // Fallback to distribution defaults
                        bool isElite = UnityEngine.Random.value < dist.eliteChance;
                        data.combatParams = new CombatParams
                        {
                            monsterName = "Creature",
                            creatureForce = isElite ? dist.eliteCreatureForce : dist.normalCreatureForce,
                            isElite = isElite,
                            reward = isElite ? RewardType.HpGain : RewardType.None,
                            rewardValue = isElite ? dist.hpGainAmount : 0
                        };
                    }
                    break;

                case MineEventType.Chest:
                    var chestSO = drawnSO as ChestEncounterSO;
                    if (chestSO != null)
                    {
                        data.chestParams = new ChestParams
                        {
                            description = chestSO.description,
                            isTrapped = chestSO.isTrapped,
                            trapDamage = chestSO.trapDamage,
                            reward = chestSO.reward,
                            rewardValue = chestSO.rewardValue
                        };
                        data.videoClip = chestSO.videoClip;
                        data.penaltyVideoClip = chestSO.penaltyVideoClip;
                    }
                    else
                    {
                        bool isTrapped = UnityEngine.Random.value < dist.trapChance;
                        data.chestParams = new ChestParams
                        {
                            description = "Un coffre mysterieux.",
                            isTrapped = isTrapped,
                            trapDamage = isTrapped ? dist.trapDamage : 0,
                            reward = RewardType.HpGain,
                            rewardValue = dist.hpGainAmount
                        };
                    }
                    break;

                case MineEventType.Dialogue:
                    var dialogueSO = drawnSO as DialogueEncounterSO;
                    if (dialogueSO != null)
                    {
                        var dialogueChoices = new List<DialogueChoice>();
                        if (dialogueSO.choices != null)
                        {
                            foreach (var c in dialogueSO.choices)
                            {
                                dialogueChoices.Add(new DialogueChoice
                                {
                                    choiceType = c.choiceType,
                                    label = c.label,
                                    resultText = c.resultText,
                                    hpDelta = c.hpDelta,
                                    reward = c.reward,
                                    rewardValue = c.rewardValue
                                });
                            }
                        }

                        var character = dialogueSO.character;
                        data.dialogueParams = new DialogueParams
                        {
                            dialogueId = character != null ? character.characterId : dialogueSO.name,
                            characterName = dialogueSO.CharacterName,
                            promptText = dialogueSO.promptText,
                            choices = dialogueChoices.ToArray(),
                            character = character
                        };

                        // Intro video comes from the character
                        data.videoClip = character != null ? character.introClip : null;
                        data.penaltyVideoClip = null; // penalty videos are generic, handled at controller level
                    }
                    else
                    {
                        data.dialogueParams = new DialogueParams
                        {
                            dialogueId = "fallback",
                            characterName = "Unknown",
                            promptText = "A presence watches you in silence.",
                            choices = new[]
                            {
                                new DialogueChoice { choiceType = PlayerChoice.Ignore, label = "Leave", resultText = "You walk away.", hpDelta = 0, reward = RewardType.None, rewardValue = 0 }
                            }
                        };
                    }
                    break;

                case MineEventType.Shrine:
                    var shrineSO = drawnSO as ShrineEncounterSO;
                    if (shrineSO != null)
                    {
                        data.shrineParams = new ShrineParams
                        {
                            shrineId = shrineSO.name,
                            description = $"{shrineSO.shrineDescription}\n{shrineSO.offerText}",
                            sacrificeCost = shrineSO.sacrificeCost,
                            reward = shrineSO.reward,
                            rewardValue = shrineSO.rewardValue
                        };
                        data.videoClip = shrineSO.videoClip;
                        data.penaltyVideoClip = shrineSO.penaltyVideoClip;
                    }
                    else
                    {
                        RewardType shrineReward = UnityEngine.Random.value < 0.5f ? RewardType.VisionGain : RewardType.Buff;
                        int shrineRewardValue = shrineReward == RewardType.VisionGain ? dist.visionGainAmount : dist.buffDuration;
                        data.shrineParams = new ShrineParams
                        {
                            shrineId = "fallback",
                            description = "Un autel silencieux.",
                            sacrificeCost = dist.sacrificeCost,
                            reward = shrineReward,
                            rewardValue = shrineRewardValue
                        };
                    }
                    break;
            }
        }

        private static MineEventType PickEventType(MineDistributionSO dist)
        {
            int total = dist.TotalWeight;
            if (total <= 0) return MineEventType.Combat;

            int roll = UnityEngine.Random.Range(0, total);

            if (roll < dist.combatWeight) return MineEventType.Combat;
            roll -= dist.combatWeight;

            if (roll < dist.chestWeight) return MineEventType.Chest;
            roll -= dist.chestWeight;

            if (roll < dist.dialogueWeight) return MineEventType.Dialogue;

            return MineEventType.Shrine;
        }

        private static void ShuffleArray<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
