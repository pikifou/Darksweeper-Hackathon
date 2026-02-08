using Mines.Data;
using Mines.Presentation;
using Sweeper.Data;
using TMPro;
using UnityEngine;

namespace Sweeper.Presentation
{
    /// <summary>
    /// Visual representation of a single grid cell in 3D world space.
    /// Uses MeshRenderer with a CellOverlay shader for the quad surface,
    /// TextMeshPro (3D) for numbers. Brightness is shader-driven (no Unity Lights).
    ///
    /// VISUAL RULES:
    /// - No permanent borders on any cell, ever.
    /// - Hover feedback on ALL cells:
    ///   - WHITE outline = visible cell (lit, whether revealed or not)
    ///   - GREEN outline = actionable but dark (active + dark) — can only right-click to flag
    ///   - RED fill = disabled (inactive) — no interaction
    /// - Flagged dark cells: subtle emission so the player sees their flag.
    /// </summary>
    public class CellView : MonoBehaviour
    {
        private MeshRenderer meshRenderer;
        private TextMeshPro numberText;
        private Material cellMaterial; // instanced per cell

        // Cached state for hover feedback
        private bool isClickable;  // lit + active + unrevealed + not flagged (left-click)
        private bool isHovered;
        private bool isRevealed;   // cell has been revealed (no hover feedback)
        private bool isLit;        // cell.light > 0 (visible to the player)
        private bool isActive;     // cell is active (not inactive/wall)
        private bool isFlagged;    // cell is flagged by the player
        private bool quadVisibleByState;
        private Color currentBaseColor;

        // Classic Minesweeper number colors
        private static readonly Color[] NumberColors = new Color[]
        {
            new Color(0.2f, 0.4f, 1.0f),   // 1 — blue
            new Color(0.1f, 0.7f, 0.1f),   // 2 — green
            new Color(1.0f, 0.2f, 0.2f),   // 3 — red
            new Color(0.1f, 0.1f, 0.7f),   // 4 — dark blue
            new Color(0.6f, 0.1f, 0.1f),   // 5 — maroon
            new Color(0.1f, 0.6f, 0.6f),   // 6 — teal
            new Color(0.15f, 0.15f, 0.15f), // 7 — dark
            new Color(0.5f, 0.5f, 0.5f),   // 8 — grey
        };

        // --- Base state colors ---
        private static readonly Color ColorUnrevealed = new Color(0.1f, 0.1f, 0.12f, 0.3f);
        private static readonly Color ColorMine = new Color(0.8f, 0.12f, 0.12f, 1f);
        private static readonly Color ColorFlag = new Color(1.0f, 0.55f, 0.1f, 1f);

        // --- Hover colors (set from GridRenderer Inspector via SetHoverColors) ---
        private static Color HoverBorderVisible = new Color(1f, 1f, 1f, 1f);
        private static Color HoverBorderActionable = new Color(0.3f, 1f, 0.3f, 1f);
        private static Color HoverEmissionActionable = new Color(0.1f, 0.4f, 0.1f, 1f);
        private static Color HoverFillDisabled = new Color(0.8f, 0.15f, 0.15f, 0.7f);
        private static Color HoverBorderDisabled = new Color(1f, 0.2f, 0.2f, 1f);
        private static Color HoverEmissionDisabled = new Color(0.4f, 0.05f, 0.05f, 1f);
        private static Color FlagDarkEmission = new Color(0.5f, 0.25f, 0.05f, 1f);
        private static readonly Color BorderNone = new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// Called by GridRenderer to push Inspector-configured hover colors to all cells.
        /// </summary>
        public static void SetHoverColors(
            Color borderVisible, Color borderActionable, Color emissionActionable,
            Color fillDisabled, Color borderDisabled, Color emissionDisabled,
            Color flagDark)
        {
            HoverBorderVisible = borderVisible;
            HoverBorderActionable = borderActionable;
            HoverEmissionActionable = emissionActionable;
            HoverFillDisabled = fillDisabled;
            HoverBorderDisabled = borderDisabled;
            HoverEmissionDisabled = emissionDisabled;
            FlagDarkEmission = flagDark;
        }

        public void Initialize(MeshRenderer renderer, TextMeshPro tmp, Material baseMat)
        {
            meshRenderer = renderer;
            cellMaterial = new Material(baseMat);
            meshRenderer.material = cellMaterial;

            numberText = tmp;
            if (numberText != null)
                numberText.gameObject.SetActive(false);

            isClickable = false;
            isHovered = false;
            isRevealed = false;
            isLit = false;
            isActive = true;
            isFlagged = false;
            quadVisibleByState = true;
            currentBaseColor = ColorUnrevealed;

            ApplyColors();
            SetBrightness(0f);
        }

        public void UpdateVisual(CellData data)
        {
            // A resolved mine keeps its icon permanently — skip the normal visual update.
            if (isMineResolved)
                return;

            isRevealed = data.isRevealed;
            isLit = data.light > 0f;
            isActive = data.isActive;
            isFlagged = data.isFlagged;

            if (!data.isRevealed)
            {
                quadVisibleByState = true;
                currentBaseColor = data.isFlagged ? ColorFlag : ColorUnrevealed;
                isClickable = data.isActive && isLit && !data.isFlagged;
                HideNumber();
            }
            else if (data.hasMine)
            {
                quadVisibleByState = true;
                currentBaseColor = ColorMine;
                isClickable = false;
                HideNumber();
            }
            else
            {
                // Revealed safe cell — hide quad, show number if needed
                quadVisibleByState = false;
                isClickable = false;

                if (data.adjacentMines > 0)
                    ShowNumber(data.adjacentMines);
                else
                    HideNumber();
            }

            ApplyColors();
        }

        public void UpdateBrightness(float brightness)
        {
            SetBrightness(brightness);

            // Tint the resolved icon sprite to match the cell's lighting
            if (iconRenderer != null)
            {
                Color tint = sharedIcons != null ? sharedIcons.tint : Color.white;
                iconRenderer.color = tint * new Color(brightness, brightness, brightness, 1f);
            }
        }

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            ApplyColors();
        }

        /// <summary>
        /// Apply material colors based on state + hover.
        /// Hover feedback on ALL cells (including revealed):
        ///   WHITE outline  = lit cell (visible — revealed or not)
        ///   GREEN outline  = dark + active (can only right-click to flag)
        ///   RED fill       = inactive (no interaction possible)
        /// Flagged dark cells: subtle emission so the player sees their flag.
        /// </summary>
        private void ApplyColors()
        {
            if (cellMaterial == null) return;

            // --- HOVER FEEDBACK (any cell, any state) ---
            if (isHovered)
            {
                if (!isActive)
                {
                    // INACTIVE / DISABLED → red fill + red border + emission
                    ShowQuad(true);
                    cellMaterial.SetColor("_BaseColor", HoverFillDisabled);
                    cellMaterial.SetColor("_BorderColor", HoverBorderDisabled);
                    cellMaterial.SetColor("_EmissionColor", HoverEmissionDisabled);
                }
                else if (isLit)
                {
                    // ACTIVE + LIT → white outline (visible cell, can interact)
                    ShowQuad(true);
                    cellMaterial.SetColor("_BaseColor", isRevealed ? new Color(0f, 0f, 0f, 0f) : currentBaseColor);
                    cellMaterial.SetColor("_BorderColor", HoverBorderVisible);
                    cellMaterial.SetColor("_EmissionColor", Color.black);
                }
                else
                {
                    // ACTIVE + DARK → green outline (can only right-click to flag)
                    // Emission makes it visible even at brightness=0
                    ShowQuad(true);
                    cellMaterial.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
                    cellMaterial.SetColor("_BorderColor", HoverBorderActionable);
                    cellMaterial.SetColor("_EmissionColor", HoverEmissionActionable);
                }
            }
            // --- NORMAL STATE (no hover) ---
            else
            {
                ShowQuad(quadVisibleByState);
                cellMaterial.SetColor("_BaseColor", currentBaseColor);
                cellMaterial.SetColor("_BorderColor", BorderNone);

                // Flagged dark cells get subtle emission so the player sees their flag
                if (isFlagged && !isLit && !isRevealed)
                    cellMaterial.SetColor("_EmissionColor", FlagDarkEmission);
                else
                    cellMaterial.SetColor("_EmissionColor", Color.black);
            }
        }

        private void ShowQuad(bool visible)
        {
            if (meshRenderer != null)
                meshRenderer.enabled = visible;
        }

        private void SetBrightness(float brightness)
        {
            if (cellMaterial != null)
                cellMaterial.SetFloat("_Brightness", Mathf.Clamp01(brightness));
        }

        private void ShowNumber(int adjacentMines)
        {
            if (numberText == null) return;
            int idx = Mathf.Clamp(adjacentMines - 1, 0, NumberColors.Length - 1);
            numberText.text = adjacentMines.ToString();
            numberText.color = NumberColors[idx];
            numberText.gameObject.SetActive(true);
        }

        private void HideNumber()
        {
            if (numberText != null)
                numberText.gameObject.SetActive(false);
        }

        // ================================================================
        // Mine Event Indicators
        // ================================================================

        private bool isMineResolved;
        private SpriteRenderer iconRenderer;

        /// <summary>
        /// Shared icon config — set once by GridRenderer at grid creation time.
        /// </summary>
        private static MineIconsSO sharedIcons;

        /// <summary>
        /// Called by GridRenderer to push the icon config to all cells.
        /// </summary>
        public static void SetMineIcons(MineIconsSO icons)
        {
            sharedIcons = icons;
        }

        /// <summary>
        /// Show that this mine cell has been resolved (interaction completed).
        /// Displays a sprite icon matching the event type.
        /// If no <see cref="MineIconsSO"/> is assigned or the sprite is null,
        /// falls back to a Unicode character via TextMeshPro.
        /// </summary>
        public void ShowMineResolved(MineEventType eventType)
        {
            isMineResolved = true;
            isClickable = false;

            // Hide the cell quad — no background, no color.
            quadVisibleByState = false;
            ShowQuad(false);

            // Show only the icon (sprite or Unicode fallback)
            Sprite sprite = GetSpriteForType(eventType);
            if (sprite != null)
            {
                ShowIconSprite(sprite);
                HideNumber();
            }
            else
            {
                ShowIconText(eventType);
            }
        }

        private void ShowIconSprite(Sprite sprite)
        {
            if (iconRenderer == null)
            {
                var iconGO = new GameObject("ResolvedIcon");
                iconGO.transform.SetParent(transform, false);
                // Position slightly above the quad so it renders on top.
                // Quad faces -Z in local space (camera looks down Y), so the
                // sprite faces up via X rotation.
                iconGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                iconGO.transform.localRotation = Quaternion.identity;

                iconRenderer = iconGO.AddComponent<SpriteRenderer>();
                iconRenderer.sortingOrder = 5;
            }

            iconRenderer.sprite = sprite;
            iconRenderer.color = sharedIcons != null ? sharedIcons.tint : Color.white;

            // Scale the icon relative to the quad
            float scale = sharedIcons != null ? sharedIcons.iconScale : 0.6f;
            // Sprite pixels-per-unit determines world size; normalise to ~1 unit then apply scale
            float ppu = sprite.pixelsPerUnit;
            float spriteWorldSize = Mathf.Max(sprite.rect.width, sprite.rect.height) / ppu;
            float desiredSize = scale; // quad is ~1 unit
            float s = desiredSize / spriteWorldSize;
            iconRenderer.transform.localScale = new Vector3(s, s, 1f);

            iconRenderer.gameObject.SetActive(true);
        }

        private void ShowIconText(MineEventType eventType)
        {
            string icon;
            Color iconColor;
            switch (eventType)
            {
                case MineEventType.Combat:
                    icon = "\u2694"; iconColor = new Color(0.9f, 0.35f, 0.3f, 0.9f); break;
                case MineEventType.Chest:
                    icon = "\u2617"; iconColor = new Color(1f, 0.85f, 0.3f, 0.9f); break;
                case MineEventType.Dialogue:
                    icon = "\u2637"; iconColor = new Color(0.5f, 0.75f, 1f, 0.9f); break;
                case MineEventType.Shrine:
                    icon = "\u2726"; iconColor = new Color(0.7f, 0.5f, 1f, 0.9f); break;
                default:
                    icon = "\u2713"; iconColor = new Color(0.4f, 0.7f, 0.4f, 0.8f); break;
            }

            if (numberText != null)
            {
                numberText.text = icon;
                numberText.color = iconColor;
                numberText.gameObject.SetActive(true);
            }
        }

        private static Sprite GetSpriteForType(MineEventType type)
        {
            if (sharedIcons == null) return null;
            return type switch
            {
                MineEventType.Combat   => sharedIcons.combat,
                MineEventType.Chest    => sharedIcons.chest,
                MineEventType.Dialogue => sharedIcons.dialogue,
                MineEventType.Shrine   => sharedIcons.shrine,
                _                      => null
            };
        }

        private void OnDestroy()
        {
            if (cellMaterial != null)
                Destroy(cellMaterial);

            if (iconRenderer != null)
                Destroy(iconRenderer.gameObject);
        }
    }
}
