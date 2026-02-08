#if UNITY_EDITOR
using Mines.Flow;
using UnityEditor;
using UnityEngine;

namespace Mines.Editor
{
    /// <summary>
    /// Batch-creates the 8 dialogue character assets in Assets/Data/Characters/.
    /// Run via DarkSweeper > Create Dialogue Characters.
    /// Existing assets with the same name are skipped (not overwritten).
    /// </summary>
    public static class DialogueCharacterCreator
    {
        private const string Folder = "Assets/Data/Characters";

        [MenuItem("DarkSweeper/Create Dialogue Characters")]
        public static void CreateAll()
        {
            EnsureFolder(Folder);

            Create("stone_child", "The Stone Child", "empathy",
                "A child whose body is made of cracked, crumbling rock — like an unfinished statue slowly falling apart. " +
                "Luminous eyes, small, vulnerable. It does not understand danger. " +
                "It reaches out without knowing what it asks for.");

            Create("lost_traveler", "The Lost Traveler", "action_empathy",
                "A trembling silhouette wrapped in dark rags. Gaunt face, pleading eyes. " +
                "Carries an empty sack and claw marks on both arms. " +
                "Clearly wounded, clearly unable to survive alone.");

            Create("wounded_soldier", "The Wounded Soldier", "action",
                "A massive warrior in shattered armor, one knee on the ground, a blade planted in the earth before him. " +
                "Scars everywhere. He does not ask for compassion — he demands that you finish what he started. " +
                "His gaze is hard, impatient.");

            Create("mourning_mother", "The Mourning Mother", "inaction_empathy",
                "A woman kneeling, wrapped in a dark veil. She cradles something invisible in her arms. " +
                "Her face is calm but her eyes are hollow. Nothing can be repaired here. " +
                "She does not ask for help — she asks that you watch.");

            Create("bone_merchant", "The Merchant of Bones", "detachment",
                "A hunched figure wrapped in a coat made of assembled bones. " +
                "Face hidden under a hood, except for a thin smile. " +
                "He sells, he buys, he trades. No emotion, no morals — only exchanges. " +
                "His hands are thin, precise, inhuman.");

            Create("chained_beast", "The Chained Beast", "action_detachment",
                "A massive creature chained to a stone pillar. Half-human, half-animal. " +
                "Tensed muscles, grinding chains. It growls but does not beg. " +
                "Freeing it is an act of force, not compassion. Killing it is pragmatic. Leaving it is cowardice.");

            Create("faceless_prophet", "The Faceless Prophet", "inaction_detachment",
                "A being sitting cross-legged, wrapped in bandages. No visible face — just a smooth, dull mirror-like surface. " +
                "It speaks in uncomfortable truths. It asks nothing, proposes nothing. It observes. " +
                "Its words hurt but never lie.");

            Create("ash_pilgrim", "The Ash Pilgrim", "action_inaction_tension",
                "An exhausted walker covered in ash. Tattered clothes, bare feet, curved back. " +
                "Still walking but no longer knowing why. " +
                "The embodied question: should you continue when no reason remains? " +
                "Asks neither help nor pity — asks if it is worth it.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DialogueCharacterCreator] 8 dialogue characters created/verified in " + Folder);
        }

        private static void Create(string id, string displayName, string axis, string description)
        {
            string path = $"{Folder}/Char_{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueCharacterSO>(path);
            if (existing != null)
            {
                Debug.Log($"[DialogueCharacterCreator] Skipped (already exists): {path}");
                return;
            }

            var so = ScriptableObject.CreateInstance<DialogueCharacterSO>();
            so.characterId = id;
            so.characterName = displayName;
            so.axisTag = axis;
            so.descriptionForLLM = description;
            // introClip left null — designer assigns the video in Inspector

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[DialogueCharacterCreator] Created: {path}");
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
