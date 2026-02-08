using Mines.Flow;
using UnityEngine;

/// <summary>
/// Injects LLM-generated dialogues into a level's <see cref="EncounterPoolSO"/>.
/// Attach to a GameObject in the level scene (e.g. on MineEventSystem).
///
/// On Start, it finds the <see cref="DialogueGeneratorService"/> (via FindAnyObjectByType),
/// reads the generated dialogues for the current level, and overwrites the dialogue pool.
///
/// If no generator is found or no dialogues are available, the pool keeps its fallback content.
/// </summary>
public class DialoguePoolInjector : MonoBehaviour
{
    [Header("Target Pool")]
    [Tooltip("The encounter pool to inject dialogues into. Typically the same pool assigned in LevelDataSO.")]
    [SerializeField] private EncounterPoolSO targetPool;

    [Header("Level")]
    [Tooltip("Which level's dialogues to use. 1 = first 4, 2 = last 8.")]
    [SerializeField] private int levelNumber = 1;

    [Header("Behavior")]
    [Tooltip("If true, replaces the existing dialogue pool entirely. If false, appends to it.")]
    [SerializeField] private bool replaceExisting = true;

    private void Start()
    {
        if (targetPool == null)
        {
            Debug.LogWarning("[DialogueInjector] No target pool assigned. Skipping injection.");
            return;
        }

        var generator = FindAnyObjectByType<DialogueGeneratorService>();
        if (generator == null)
        {
            Debug.Log("[DialogueInjector] No DialogueGeneratorService found in scene. Using pool as-is.");
            return;
        }

        var dialogues = generator.Dialogues;
        if (dialogues == null || dialogues.Length == 0)
        {
            Debug.Log("[DialogueInjector] No dialogues available yet. Using pool as-is.");
            return;
        }

        // Pick the right slice for this level
        DialogueEncounterSO[] levelDialogues = levelNumber == 1
            ? generator.GetLevel1Dialogues()
            : generator.GetLevel2Dialogues();

        if (levelDialogues.Length == 0)
        {
            Debug.Log($"[DialogueInjector] No dialogues for level {levelNumber}. Using pool as-is.");
            return;
        }

        // Inject
        if (replaceExisting)
        {
            targetPool.dialoguePool = levelDialogues;
            Debug.Log($"[DialogueInjector] Replaced dialogue pool with {levelDialogues.Length} LLM-generated dialogues (level {levelNumber}).");
        }
        else
        {
            int existing = targetPool.dialoguePool != null ? targetPool.dialoguePool.Length : 0;
            var merged = new DialogueEncounterSO[existing + levelDialogues.Length];
            if (targetPool.dialoguePool != null)
                targetPool.dialoguePool.CopyTo(merged, 0);
            levelDialogues.CopyTo(merged, existing);
            targetPool.dialoguePool = merged;
            Debug.Log($"[DialogueInjector] Appended {levelDialogues.Length} LLM-generated dialogues to pool (total: {merged.Length}, level {levelNumber}).");
        }
    }
}
