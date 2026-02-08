using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sweeper.Presentation
{
    /// <summary>
    /// Converts mouse input to grid coordinates in the XZ plane and fires click/hover events.
    /// Uses the new Input System package.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        public event Action<int, int> OnLeftClick;
        public event Action<int, int> OnRightClick;
        public event Action<int, int> OnHoverChanged;

        private GridRenderer gridRenderer;
        private int prevHoverX = -1;
        private int prevHoverY = -1;
        private bool inputEnabled = true;

        /// <summary>
        /// When true, all click events are suppressed (modal panel is open).
        /// Set by MineEventController when an interaction panel is shown.
        /// Hover feedback still works so the player sees where they are.
        /// </summary>
        public bool inputBlocked;

        public void Initialize(GridRenderer renderer)
        {
            gridRenderer = renderer;
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }

        private void Update()
        {
            if (gridRenderer == null || !inputEnabled) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Convert mouse screen position to world position on the XZ plane (y=0)
            Vector2 screenPos = mouse.position.ReadValue();
            Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane));

            // For orthographic camera looking down Y, worldPos.x = world X, worldPos.z = world Z
            // The camera's forward is -Y, so ScreenToWorldPoint gives us XZ directly
            WorldToGrid(worldPos, out int gx, out int gy);

            bool inBounds = gx >= 0 && gx < gridRenderer.GridWidth &&
                            gy >= 0 && gy < gridRenderer.GridHeight;

            // --- Hover ---
            if (inBounds)
            {
                if (gx != prevHoverX || gy != prevHoverY)
                {
                    CellView prev = gridRenderer.GetCellView(prevHoverX, prevHoverY);
                    if (prev != null) prev.SetHovered(false);

                    CellView curr = gridRenderer.GetCellView(gx, gy);
                    if (curr != null) curr.SetHovered(true);

                    prevHoverX = gx;
                    prevHoverY = gy;
                    OnHoverChanged?.Invoke(gx, gy);
                }
            }
            else
            {
                if (prevHoverX != -1 || prevHoverY != -1)
                {
                    CellView prev = gridRenderer.GetCellView(prevHoverX, prevHoverY);
                    if (prev != null) prev.SetHovered(false);
                    prevHoverX = -1;
                    prevHoverY = -1;
                }
            }

            // --- Clicks (blocked when modal panel is open) ---
            if (inBounds && !inputBlocked)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    OnLeftClick?.Invoke(gx, gy);
                }

                if (mouse.rightButton.wasPressedThisFrame)
                {
                    OnRightClick?.Invoke(gx, gy);
                }
            }
        }

        /// <summary>
        /// Convert world position (XZ plane) to grid coordinates.
        /// </summary>
        private void WorldToGrid(Vector3 worldPos, out int gx, out int gy)
        {
            Vector3 origin = gridRenderer.GridOrigin;
            float cs = gridRenderer.CellSize;

            // Grid X maps to world X, Grid Y maps to world Z
            gx = Mathf.FloorToInt((worldPos.x - origin.x + cs / 2f) / cs);
            gy = Mathf.FloorToInt((worldPos.z - origin.z + cs / 2f) / cs);
        }
    }
}
