using UnityEngine;

namespace Mines.Presentation
{
    /// <summary>
    /// Maps each mine event type to a sprite displayed on the cell
    /// after the event is resolved. Assign via GridRenderer Inspector.
    /// Create via Assets > Create > DarkSweeper/UI/Mine Icons.
    /// </summary>
    [CreateAssetMenu(menuName = "DarkSweeper/UI/Mine Icons")]
    public class MineIconsSO : ScriptableObject
    {
        [Header("Resolved Event Sprites")]
        public Sprite combat;
        public Sprite chest;
        public Sprite dialogue;
        public Sprite shrine;

        [Header("Display")]
        [Tooltip("Scale of the icon relative to the cell quad (0.5 = half the cell).")]
        [Range(0.1f, 1f)]
        public float iconScale = 0.6f;

        [Tooltip("Tint color multiplier applied to all icons.")]
        public Color tint = Color.white;
    }
}
