#if UNITY_EDITOR
using Mines.Data;
using Mines.Flow;
using Mines.Presentation;
using Sweeper.Flow;
using Sweeper.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Mines.Editor
{
    /// <summary>
    /// Non-destructive editor utility that adds the Mine Event system to the current scene.
    /// Creates example encounter SO assets, an EncounterPoolSO, a MineDistributionSO,
    /// and a MineEventSystem GameObject with all references wired.
    /// </summary>
    public static class MineEventSetup
    {
        private const string EncounterFolder = "Assets/Data/Encounters";
        private const string DataFolder = "Assets/Data";

        [MenuItem("DarkSweeper/Add Mine Event System")]
        public static void AddMineEventSystem()
        {
            // 1. Create SO assets
            EnsureFolder(EncounterFolder);

            var combats = CreateCombatEncounters();
            var chests = CreateChestEncounters();
            var dialogues = CreateDialogueEncounters();
            var shrines = CreateShrineEncounters();

            // 2. Create EncounterPoolSO
            var pool = CreateOrLoad<EncounterPoolSO>($"{DataFolder}/EncounterPool_Default.asset");
            pool.combatPool = combats;
            pool.chestPool = chests;
            pool.dialoguePool = dialogues;
            pool.shrinePool = shrines;
            EditorUtility.SetDirty(pool);

            // 3. Create MineDistributionSO
            var distribution = CreateOrLoad<MineDistributionSO>($"{DataFolder}/MineDistribution_Default.asset");
            EditorUtility.SetDirty(distribution);

            // 3b. Ensure EventSystem exists in the scene
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGO, "Add EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[MineEventSetup] Created EventSystem (was missing from scene).");
            }

            // 4. Create or re-wire scene objects
            var existingController = Object.FindFirstObjectByType<MineEventController>();
            if (existingController != null)
            {
                Debug.Log("[MineEventSetup] MineEventController already exists in the scene — re-wiring references.");
                WireController(existingController, distribution, pool);
                AssetDatabase.SaveAssets();
                return;
            }

            // Create MineEventController on its own GameObject
            var go = new GameObject("MineEventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Add Mine Event System");
            var controller = go.AddComponent<MineEventController>();

            // Create the panel (for dialogues) and toast (for combat/chest/shrine)
            var panel = MineEventPanelCreator.CreateInScene();
            var toastObj = MineEventToastCreator.CreateInScene();

            WireController(controller, distribution, pool, panel, toastObj);

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            AssetDatabase.SaveAssets();

            Debug.Log("[MineEventSetup] Mine Event System added to scene successfully!\n" +
                      $"  - {combats.Length} combat encounters\n" +
                      $"  - {chests.Length} chest encounters\n" +
                      $"  - {dialogues.Length} dialogue encounters\n" +
                      $"  - {shrines.Length} shrine encounters\n" +
                      $"  - Pool: {AssetDatabase.GetAssetPath(pool)}\n" +
                      $"  - Distribution: {AssetDatabase.GetAssetPath(distribution)}");
        }

        // ================================================================
        // Wiring
        // ================================================================

        private static void WireController(MineEventController controller, MineDistributionSO distribution, EncounterPoolSO pool, MineEventPanel panelOverride = null, MineEventToast toastOverride = null)
        {
            var so = new SerializedObject(controller);

            // Scene references
            var sweeper = Object.FindFirstObjectByType<SweeperGameController>();
            var inputHandler = Object.FindFirstObjectByType<InputHandler>();
            var gridRenderer = Object.FindFirstObjectByType<GridRenderer>();
            var panel = panelOverride != null ? panelOverride : Object.FindFirstObjectByType<MineEventPanel>();
            var toastRef = toastOverride != null ? toastOverride : Object.FindFirstObjectByType<MineEventToast>();

            if (sweeper != null) so.FindProperty("sweeper").objectReferenceValue = sweeper;
            else Debug.LogWarning("[MineEventSetup] SweeperGameController not found in scene!");

            if (inputHandler != null) so.FindProperty("inputHandler").objectReferenceValue = inputHandler;
            else Debug.LogWarning("[MineEventSetup] InputHandler not found in scene!");

            if (gridRenderer != null) so.FindProperty("gridRenderer").objectReferenceValue = gridRenderer;
            else Debug.LogWarning("[MineEventSetup] GridRenderer not found in scene!");

            if (panel != null) so.FindProperty("panel").objectReferenceValue = panel;
            else Debug.LogWarning("[MineEventSetup] MineEventPanel not found!");

            if (toastRef != null) so.FindProperty("toast").objectReferenceValue = toastRef;
            else Debug.LogWarning("[MineEventSetup] MineEventToast not found! Use DarkSweeper > Create Mine Event Toast Prefab.");

            // SO references
            so.FindProperty("distribution").objectReferenceValue = distribution;
            so.FindProperty("fallbackPool").objectReferenceValue = pool;

            // Try to get LevelDataSO from SweeperGameController
            if (sweeper != null)
            {
                var sweeperSO = new SerializedObject(sweeper);
                var levelDataProp = sweeperSO.FindProperty("levelData");
                if (levelDataProp != null && levelDataProp.objectReferenceValue != null)
                {
                    var levelData = levelDataProp.objectReferenceValue as LevelDataSO;
                    so.FindProperty("levelData").objectReferenceValue = levelData;

                    // Also assign the pool to the LevelDataSO if it doesn't have one
                    if (levelData != null && levelData.encounterPool == null)
                    {
                        levelData.encounterPool = pool;
                        EditorUtility.SetDirty(levelData);
                        Debug.Log($"[MineEventSetup] Assigned encounter pool to LevelDataSO: {levelData.name}");
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================
        // Encounter Asset Creation
        // ================================================================

        private static CombatEncounterSO[] CreateCombatEncounters()
        {
            var c1 = CreateOrLoad<CombatEncounterSO>($"{EncounterFolder}/Combat_OmbreRampante.asset");
            c1.monsterName = "Ombre Rampante";
            c1.description = "Une forme sombre se dresse devant vous.";
            c1.creatureForce = 3;
            c1.isElite = false;
            c1.reward = RewardType.None;
            c1.rewardValue = 0;
            EditorUtility.SetDirty(c1);

            var c2 = CreateOrLoad<CombatEncounterSO>($"{EncounterFolder}/Combat_VeilleurBrise.asset");
            c2.monsterName = "Veilleur Brise";
            c2.description = "Un gardien corrompu bloque le passage.";
            c2.creatureForce = 5;
            c2.isElite = false;
            c2.reward = RewardType.HpGain;
            c2.rewardValue = 3;
            EditorUtility.SetDirty(c2);

            var c3 = CreateOrLoad<CombatEncounterSO>($"{EncounterFolder}/Combat_EclatDeVide.asset");
            c3.monsterName = "Eclat de Vide";
            c3.description = "Une fissure dans l'espace prend forme.";
            c3.creatureForce = 8;
            c3.isElite = true;
            c3.reward = RewardType.HpGain;
            c3.rewardValue = 5;
            EditorUtility.SetDirty(c3);

            return new[] { c1, c2, c3 };
        }

        private static ChestEncounterSO[] CreateChestEncounters()
        {
            var ch1 = CreateOrLoad<ChestEncounterSO>($"{EncounterFolder}/Chest_CoffreAncien.asset");
            ch1.description = "Un coffre ancien repose dans l'obscurite. Il vibre legerement.";
            ch1.isTrapped = false;
            ch1.trapDamage = 0;
            ch1.reward = RewardType.HpGain;
            ch1.rewardValue = 5;
            EditorUtility.SetDirty(ch1);

            var ch2 = CreateOrLoad<ChestEncounterSO>($"{EncounterFolder}/Chest_ReceptaclePierre.asset");
            ch2.description = "Un receptacle de pierre, scelle par des runes eteintes.";
            ch2.isTrapped = true;
            ch2.trapDamage = 8;
            ch2.reward = RewardType.VisionGain;
            ch2.rewardValue = 1;
            EditorUtility.SetDirty(ch2);

            var ch3 = CreateOrLoad<ChestEncounterSO>($"{EncounterFolder}/Chest_BoiteOs.asset");
            ch3.description = "Une boite d'os, entrouverte. Quelque chose brille a l'interieur.";
            ch3.isTrapped = false;
            ch3.trapDamage = 0;
            ch3.reward = RewardType.Buff;
            ch3.rewardValue = 2;
            EditorUtility.SetDirty(ch3);

            return new[] { ch1, ch2, ch3 };
        }

        private static DialogueEncounterSO[] CreateDialogueEncounters()
        {
            var d1 = CreateOrLoad<DialogueEncounterSO>($"{EncounterFolder}/Dialogue_VoyageurPerdu.asset");
            // Character reference will be wired manually in Inspector (or via Create Dialogue Characters)
            d1.promptText = "Une silhouette tremblante vous tend la main. \"Aide-moi... ou prends ce que j'ai.\"";
            d1.choices = new[]
            {
                new DialogueChoiceEntry { choiceType = PlayerChoice.Help, label = "Aider", resultText = "Vous tendez la main. Il disparait, mais quelque chose reste.", hpDelta = -3, reward = RewardType.HpGain, rewardValue = 0 },
                new DialogueChoiceEntry { choiceType = PlayerChoice.Harm, label = "Depouiller", resultText = "Vous prenez ce qu'il porte. Son regard vous hante.", hpDelta = 0, reward = RewardType.HpGain, rewardValue = 5 },
                new DialogueChoiceEntry { choiceType = PlayerChoice.Ignore, label = "Ignorer", resultText = "Vous passez votre chemin. Le silence revient.", hpDelta = 0, reward = RewardType.None, rewardValue = 0 },
            };
            EditorUtility.SetDirty(d1);

            var d2 = CreateOrLoad<DialogueEncounterSO>($"{EncounterFolder}/Dialogue_EnfantDePierre.asset");
            // Character reference will be wired manually in Inspector (or via Create Dialogue Characters)
            d2.promptText = "Un enfant fait de roche vous observe. \"Tu vois aussi dans le noir, toi ?\"";
            d2.choices = new[]
            {
                new DialogueChoiceEntry { choiceType = PlayerChoice.Help, label = "Proteger", resultText = "Vous restez un moment. Il sourit, puis s'effrite.", hpDelta = -5, reward = RewardType.VisionGain, rewardValue = 1 },
                new DialogueChoiceEntry { choiceType = PlayerChoice.Harm, label = "Briser", resultText = "Les fragments revelent une lumiere captive.", hpDelta = 0, reward = RewardType.VisionGain, rewardValue = 2 },
            };
            EditorUtility.SetDirty(d2);

            return new[] { d1, d2 };
        }

        private static ShrineEncounterSO[] CreateShrineEncounters()
        {
            var s1 = CreateOrLoad<ShrineEncounterSO>($"{EncounterFolder}/Shrine_AutolCendres.asset");
            s1.shrineDescription = "Un autel de cendres pulse faiblement.";
            s1.offerText = "Offrez votre vitalite en echange d'une vision accrue.";
            s1.sacrificeCost = 10;
            s1.reward = RewardType.VisionGain;
            s1.rewardValue = 1;
            EditorUtility.SetDirty(s1);

            var s2 = CreateOrLoad<ShrineEncounterSO>($"{EncounterFolder}/Shrine_CairnSacre.asset");
            s2.shrineDescription = "Des os empiles forment un cairn sacre.";
            s2.offerText = "Le sacrifice apaise le territoire. Le prochain combat sera adouci.";
            s2.sacrificeCost = 8;
            s2.reward = RewardType.Buff;
            s2.rewardValue = 2;
            EditorUtility.SetDirty(s2);

            var s3 = CreateOrLoad<ShrineEncounterSO>($"{EncounterFolder}/Shrine_FlammeNoire.asset");
            s3.shrineDescription = "Une flamme noire brule sans chaleur.";
            s3.offerText = "Votre sang contre sa protection.";
            s3.sacrificeCost = 12;
            s3.reward = RewardType.Buff;
            s3.rewardValue = 3;
            EditorUtility.SetDirty(s3);

            return new[] { s1, s2, s3 };
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>
        /// Load an existing SO asset or create a new one at the given path.
        /// </summary>
        private static T CreateOrLoad<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, assetPath);
            Debug.Log($"[MineEventSetup] Created asset: {assetPath}");
            return instance;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
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
