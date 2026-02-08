using UnityEditor;
using UnityEngine;

namespace Audio.Editor
{
    /// <summary>
    /// Custom PropertyDrawer for SFXLibrarySO.SFXEntry.
    /// Auto-fills the "id" field with the clip's file name when a clip is
    /// dragged into the entry and the id is still empty.
    /// Also sets volume to 1 by default on new entries.
    /// </summary>
    [CustomPropertyDrawer(typeof(SFXLibrarySO.SFXEntry))]
    public class SFXEntryDrawer : PropertyDrawer
    {
        private const float LineSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3 + LineSpacing * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty idProp = property.FindPropertyRelative("id");
            SerializedProperty clipProp = property.FindPropertyRelative("clip");
            SerializedProperty volumeProp = property.FindPropertyRelative("volume");

            float lineH = EditorGUIUtility.singleLineHeight;
            Rect idRect = new Rect(position.x, position.y, position.width, lineH);
            Rect clipRect = new Rect(position.x, position.y + lineH + LineSpacing, position.width, lineH);
            Rect volRect = new Rect(position.x, position.y + (lineH + LineSpacing) * 2, position.width, lineH);

            // Draw id and volume with standard PropertyField
            EditorGUI.PropertyField(idRect, idProp);
            EditorGUI.PropertyField(volRect, volumeProp);

            // Draw clip with ObjectField to get the new value immediately on drag-and-drop
            AudioClip oldClip = clipProp.objectReferenceValue as AudioClip;
            AudioClip newClip = (AudioClip)EditorGUI.ObjectField(
                clipRect,
                clipProp.displayName,
                oldClip,
                typeof(AudioClip),
                false
            );

            // Detect clip change
            if (newClip != oldClip)
            {
                clipProp.objectReferenceValue = newClip;

                // Always update id to match the clip name on drag-and-drop
                if (newClip != null)
                {
                    idProp.stringValue = newClip.name;
                }

                // Default volume to 1 if it's still at 0 (new entry)
                if (newClip != null && volumeProp.floatValue <= 0f)
                {
                    volumeProp.floatValue = 1f;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
