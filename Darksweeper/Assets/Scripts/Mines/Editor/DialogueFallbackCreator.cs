#if UNITY_EDITOR
using Mines.Data;
using Mines.Flow;
using UnityEditor;
using UnityEngine;

namespace Mines.Editor
{
    /// <summary>
    /// Batch-creates 12 fallback DialogueEncounterSO assets in Assets/Data/Encounters/Fallback/.
    /// These are used when the LLM is unavailable (offline, timeout, parse error).
    ///
    /// Run via DarkSweeper > Create Fallback Dialogues.
    /// Requires the 8 DialogueCharacterSO assets to already exist in Assets/Data/Characters/.
    /// Existing assets with the same name are skipped (not overwritten).
    /// </summary>
    public static class DialogueFallbackCreator
    {
        private const string FallbackFolder = "Assets/Data/Encounters/Fallback";
        private const string CharFolder = "Assets/Data/Characters";

        [MenuItem("DarkSweeper/Create Fallback Dialogues")]
        public static void CreateAll()
        {
            EnsureFolder(FallbackFolder);

            // Load character references
            var stoneChild = LoadChar("Char_stone_child");
            var lostTraveler = LoadChar("Char_lost_traveler");
            var woundedSoldier = LoadChar("Char_wounded_soldier");
            var mourningMother = LoadChar("Char_mourning_mother");
            var boneMerchant = LoadChar("Char_bone_merchant");
            var chainedBeast = LoadChar("Char_chained_beast");
            var facelessProphet = LoadChar("Char_faceless_prophet");
            var ashPilgrim = LoadChar("Char_ash_pilgrim");

            // === LEVEL 1 (indices 0-3): Simpler dilemmas ===

            Create("Fallback_01_StoneChild", stoneChild,
                "The stone child holds out a crumbling hand. Dust falls from where fingers should be. It does not speak. It only reaches.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Take its hand", "The stone cracks further. Warmth seeps through. You feel weaker.", -3, RewardType.None, 0),
                    Choice(PlayerChoice.Ignore, "Walk past", "It watches you leave. The dust settles where you stood.", 0, RewardType.None, 0),
                });

            Create("Fallback_02_LostTraveler", lostTraveler,
                "Please... the path behind me is gone. Something follows. I need light, or I need a blade. I have neither.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Share your torch", "The darkness recedes. But yours grows.", -5, RewardType.VisionGain, 2),
                    Choice(PlayerChoice.Refuse, "You are not my burden", "He nods, unsurprised. The dark takes him quietly.", 0, RewardType.None, 0),
                });

            Create("Fallback_03_BoneMerchant", boneMerchant,
                "A finger bone for a secret. A rib for safe passage. Everything has a price. What do you offer?",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Sacrifice, "Offer your blood", "The merchant grins. A thin cut, a fat reward.", -4, RewardType.Buff, 3),
                    Choice(PlayerChoice.Refuse, "I trade with no one", "His smile does not falter. Perhaps next time.", 0, RewardType.None, 0),
                });

            Create("Fallback_04_MourningMother", mourningMother,
                "She kneels in ash. Her arms cradle nothing. She does not look up. She whispers a name that is not yours.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Kneel beside her", "You share her silence. Something shifts in you.", -2, RewardType.HpGain, 1),
                    Choice(PlayerChoice.Ignore, "Leave her to grieve", "Some wounds are not yours to mend. You know this.", 0, RewardType.None, 0),
                });

            // === LEVEL 2 (indices 4-11): Harder, more layered ===

            Create("Fallback_05_WoundedSoldier", woundedSoldier,
                "Finish it. The beast went north. My leg is shattered. I will slow you down. End me, or end what I started.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Carry him", "He screams. You bleed. The beast hears.", -8, RewardType.Buff, 2),
                    Choice(PlayerChoice.Harm, "Grant his wish", "His eyes close. Grateful. The blade is heavier now.", -2, RewardType.None, 0),
                    Choice(PlayerChoice.Ignore, "Leave him", "His curses follow you further than you expected.", 0, RewardType.None, 0),
                });

            Create("Fallback_06_ChainedBeast", chainedBeast,
                "The chains bite. The stone holds. It growls — not in anger, but in exhaustion. Its eyes track you. Waiting.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Break the chains", "It leaps free. For a heartbeat, it looks at you. Then it is gone. Something in the dark shifts.", -6, RewardType.VisionGain, 3),
                    Choice(PlayerChoice.Harm, "Put it down", "It does not resist. The chain rattles once, then silence.", -1, RewardType.HpGain, 2),
                    Choice(PlayerChoice.Ignore, "Not your cage", "The growl resumes. Quieter now.", 0, RewardType.None, 0),
                });

            Create("Fallback_07_FacelessProphet", facelessProphet,
                "You chose this path believing you knew its shape. You were wrong. Would you like to know why?",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Tell me", "The truth is a cold thing. You feel it settle in your chest.", -4, RewardType.VisionGain, 4),
                    Choice(PlayerChoice.Refuse, "I walk my own truth", "Then walk. But the shape of the path does not change because you close your eyes.", 0, RewardType.None, 0),
                });

            Create("Fallback_08_AshPilgrim", ashPilgrim,
                "I have walked since before the first fire. My feet are dust. Is there a reason to continue? Tell me yours.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Walk with me", "Two sets of footprints. The ash falls lighter.", -3, RewardType.Buff, 2),
                    Choice(PlayerChoice.Sacrifice, "Give him your purpose", "He straightens. You feel hollow. But he walks again.", -7, RewardType.HpGain, 4),
                    Choice(PlayerChoice.Ignore, "I have no answer", "He nods. Sits. Perhaps that is answer enough.", 0, RewardType.None, 0),
                });

            Create("Fallback_09_StoneChild_Lv2", stoneChild,
                "The child again. This time it holds something — a shard of mirror. In it, you see yourself, but wrong. It offers the shard.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Take the mirror shard", "The reflection blinks before you do. Knowledge at a cost.", -5, RewardType.VisionGain, 3),
                    Choice(PlayerChoice.Harm, "Shatter it", "The child does not flinch. The shards dissolve into dust. What you saw cannot be unseen.", -2, RewardType.None, 0),
                    Choice(PlayerChoice.Refuse, "I know what I am", "Do you? The child tilts its head. The mirror darkens.", 0, RewardType.None, 0),
                });

            Create("Fallback_10_BoneMerchant_Lv2", boneMerchant,
                "You again. Prices have risen. I have something rare — a name. The name of the one who watches you. Cost: something you love.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Sacrifice, "Pay the price", "He whispers. The name burns. You stagger.", -10, RewardType.Buff, 5),
                    Choice(PlayerChoice.Harm, "Take it by force", "He laughs. His bones rattle. You leave with bruises and nothing.", -6, RewardType.None, 0),
                    Choice(PlayerChoice.Refuse, "Keep your secrets", "He bows. The offer stands. It always stands.", 0, RewardType.None, 0),
                });

            Create("Fallback_11_LostTraveler_Lv2", lostTraveler,
                "I found the path. It leads to a door. But behind it — screaming. Not mine. I cannot open it alone. Will you?",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Open together", "The screaming stops. What stands behind the door is worse than sound.", -8, RewardType.VisionGain, 4),
                    Choice(PlayerChoice.Ignore, "Some doors stay shut", "He stares. Then walks toward it alone. You hear the latch.", 0, RewardType.None, 0),
                });

            Create("Fallback_12_MourningMother_Lv2", mourningMother,
                "She stands now. Eyes open. She holds a bundle of ash. She says: Take this. Carry what I cannot. Let it weigh on someone else.",
                new DialogueChoiceEntry[]
                {
                    Choice(PlayerChoice.Help, "Take the burden", "It is heavier than it looks. Far heavier. You feel something break inside.", -10, RewardType.Buff, 4),
                    Choice(PlayerChoice.Sacrifice, "Burn it for her", "The ash falls. She watches it go. Something in her face mends. Something in you doesn't.", -6, RewardType.HpGain, 3),
                    Choice(PlayerChoice.Refuse, "Carry your own grief", "She closes her eyes. The bundle crumbles. Neither of you speak.", 0, RewardType.None, 0),
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DialogueFallbackCreator] 12 fallback dialogue assets created/verified in " + FallbackFolder);
        }

        // ── Helpers ──────────────────────────────────────────

        private static void Create(string fileName, DialogueCharacterSO character, string promptText, DialogueChoiceEntry[] choices)
        {
            string path = $"{FallbackFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueEncounterSO>(path);
            if (existing != null)
            {
                Debug.Log($"[DialogueFallbackCreator] Skipped (already exists): {path}");
                return;
            }

            var so = ScriptableObject.CreateInstance<DialogueEncounterSO>();
            so.character = character;
            so.promptText = promptText;
            so.choices = choices;

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[DialogueFallbackCreator] Created: {path}");
        }

        private static DialogueChoiceEntry Choice(PlayerChoice type, string label, string result, int hp, RewardType reward, int rewardVal)
        {
            return new DialogueChoiceEntry
            {
                choiceType = type,
                label = label,
                resultText = result,
                hpDelta = hp,
                reward = reward,
                rewardValue = rewardVal
            };
        }

        private static DialogueCharacterSO LoadChar(string assetName)
        {
            string path = $"{CharFolder}/{assetName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<DialogueCharacterSO>(path);
            if (so == null)
                Debug.LogWarning($"[DialogueFallbackCreator] Character asset not found: {path}. Run 'Create Dialogue Characters' first.");
            return so;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
