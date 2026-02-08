#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Mines.Editor
{
    /// <summary>
    /// Creates a PromptTemplateSO asset pre-filled with the dialogue generation
    /// system prompt and JSON schema. 
    /// Run via DarkSweeper > Create Dialogue Prompt Template.
    /// </summary>
    public static class DialoguePromptTemplateCreator
    {
        private const string AssetPath = "Assets/Data/PromptTemplate_Dialogues.asset";

        [MenuItem("DarkSweeper/Create Dialogue Prompt Template")]
        public static void Create()
        {
            // Don't overwrite if it already exists
            var existing = AssetDatabase.LoadAssetAtPath<PromptTemplateSO>(AssetPath);
            if (existing != null)
            {
                Debug.Log($"[DialoguePromptTemplate] Already exists: {AssetPath}. Select it in Project.");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var so = ScriptableObject.CreateInstance<PromptTemplateSO>();
            so.systemPrompt = DialoguePromptDefaults.SystemPrompt;
            so.jsonSchema = DialoguePromptDefaults.JsonSchema;
            so.schemaVersion = DialoguePromptDefaults.SchemaVersion;

            AssetDatabase.CreateAsset(so, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);

            Debug.Log($"[DialoguePromptTemplate] Created: {AssetPath}");
        }
    }
}
#endif
