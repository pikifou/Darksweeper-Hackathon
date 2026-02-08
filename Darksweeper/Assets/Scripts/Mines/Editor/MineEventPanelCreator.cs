#if UNITY_EDITOR
using Mines.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Mines.Editor
{
    /// <summary>
    /// Editor utility that creates a fully-wired MineEventPanel prefab.
    /// Run via <b>DarkSweeper &gt; Create Mine Event Panel Prefab</b>.
    ///
    /// The generated prefab can be freely customised in the editor:
    /// rearrange, restyle, add illustrations, swap sprites, etc.
    /// The <see cref="MineEventPanel"/> script only cares about its
    /// serialised references — the layout is entirely yours.
    /// </summary>
    public static class MineEventPanelCreator
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath  = PrefabFolder + "/MineEventPanel.prefab";

        // ================================================================
        // Menu Entry
        // ================================================================

        [MenuItem("DarkSweeper/Create Mine Event Panel Prefab")]
        public static void CreatePrefab()
        {
            EnsureFolder(PrefabFolder);

            // Build the hierarchy under a temporary root
            var root = BuildHierarchy();

            // Save as prefab
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Overwrite Prefab?",
                    $"A prefab already exists at:\n{PrefabPath}\n\nOverwrite it?",
                    "Overwrite", "Cancel");
                if (!overwrite)
                {
                    Object.DestroyImmediate(root);
                    return;
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // Select it in the Project window
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;

            Debug.Log($"[MineEventPanelCreator] Prefab created at {PrefabPath}.\n" +
                      "Open it, customise freely, then drag into your scene.\n" +
                      "All SerializeField references are already wired.");
        }

        // ================================================================
        // Instantiate in scene (prefers existing prefab)
        // ================================================================

        /// <summary>
        /// Add a MineEventPanel to the active scene.
        /// If the prefab exists at <see cref="PrefabPath"/>, it is instantiated
        /// as a prefab instance (edits to the prefab propagate).
        /// Otherwise, builds the hierarchy from scratch as a fallback.
        /// </summary>
        public static MineEventPanel CreateInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject root;

            if (prefab != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Debug.Log($"[MineEventPanelCreator] Instantiated prefab from {PrefabPath}.");
            }
            else
            {
                root = BuildHierarchy();
                Debug.Log("[MineEventPanelCreator] No prefab found — created hierarchy from scratch. " +
                          "Run DarkSweeper > Create Mine Event Panel Prefab to generate the prefab.");
            }

            Undo.RegisterCreatedObjectUndo(root, "Create MineEventPanel");
            return root.GetComponent<MineEventPanel>();
        }

        // ================================================================
        // Hierarchy Builder
        // ================================================================

        private static GameObject BuildHierarchy()
        {
            // ---- Root: MineEventPanel (Canvas) ----
            var root = new GameObject("MineEventPanel");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            root.AddComponent<GraphicRaycaster>();
            var canvasGroup = root.AddComponent<CanvasGroup>();

            // ---- Dark Overlay (full-screen dim) ----
            var overlayGO = CreateRect("DarkOverlay", root.transform);
            Stretch(overlayGO);
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.75f);
            overlayImg.raycastTarget = true; // blocks clicks behind the panel

            // ---- Event Frame (central card) ----
            var frameGO = CreateRect("EventFrame", root.transform);
            var frameRT = frameGO.GetComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0.2f, 0.1f);
            frameRT.anchorMax = new Vector2(0.8f, 0.9f);
            frameRT.offsetMin = Vector2.zero;
            frameRT.offsetMax = Vector2.zero;

            var frameBg = frameGO.AddComponent<Image>();
            frameBg.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
            frameBg.raycastTarget = true;

            var frameLayout = frameGO.AddComponent<VerticalLayoutGroup>();
            frameLayout.padding = new RectOffset(40, 40, 30, 30);
            frameLayout.spacing = 20f;
            frameLayout.childAlignment = TextAnchor.UpperCenter;
            frameLayout.childControlWidth = true;
            frameLayout.childControlHeight = false;
            frameLayout.childForceExpandWidth = true;
            frameLayout.childForceExpandHeight = false;

            // ---- Title ----
            var titleGO = CreateRect("Title", frameGO.transform);
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "Titre de l'evenement";
            titleTMP.fontSize = 32;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = new Color(1f, 0.85f, 0.4f);
            titleTMP.textWrappingMode = TextWrappingModes.Normal;
            AddLayout(titleGO, preferredHeight: 50f);

            // ---- Separator ----
            var sep1 = CreateRect("Separator_Top", frameGO.transform);
            var sep1Img = sep1.AddComponent<Image>();
            sep1Img.color = new Color(1f, 0.85f, 0.4f, 0.25f);
            sep1Img.raycastTarget = false;
            AddLayout(sep1, preferredHeight: 2f);

            // ---- Description ----
            var descGO = CreateRect("Description", frameGO.transform);
            var descTMP = descGO.AddComponent<TextMeshProUGUI>();
            descTMP.text = "Description de l'evenement...";
            descTMP.fontSize = 22;
            descTMP.alignment = TextAlignmentOptions.Center;
            descTMP.color = new Color(0.85f, 0.85f, 0.85f);
            descTMP.textWrappingMode = TextWrappingModes.Normal;
            descTMP.overflowMode = TextOverflowModes.Overflow;
            AddLayout(descGO, preferredHeight: 100f, flexibleHeight: 1f);

            // ---- Spacer ----
            var spacer1 = CreateRect("Spacer", frameGO.transform);
            AddLayout(spacer1, preferredHeight: 10f);

            // ---- Choices Container ----
            var choicesGO = CreateRect("ChoicesContainer", frameGO.transform);
            var choicesLayout = choicesGO.AddComponent<VerticalLayoutGroup>();
            choicesLayout.spacing = 10f;
            choicesLayout.childAlignment = TextAnchor.UpperCenter;
            choicesLayout.childControlWidth = true;
            choicesLayout.childControlHeight = false;
            choicesLayout.childForceExpandWidth = true;
            choicesLayout.childForceExpandHeight = false;
            var choicesFitter = choicesGO.AddComponent<ContentSizeFitter>();
            choicesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddLayout(choicesGO, flexibleHeight: 0f);

            // Build 3 choice buttons
            var buttons = new Button[3];
            var labels = new TextMeshProUGUI[3];

            for (int i = 0; i < 3; i++)
            {
                var btnGO = CreateRect($"ChoiceButton_{i}", choicesGO.transform);

                var btnBg = btnGO.AddComponent<Image>();
                btnBg.color = new Color(0.12f, 0.12f, 0.16f, 1f);
                btnBg.raycastTarget = true;
                AddLayout(btnGO, preferredHeight: 55f);

                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = btnBg;
                var cols = btn.colors;
                cols.normalColor = new Color(0.12f, 0.12f, 0.16f, 1f);
                cols.highlightedColor = new Color(0.22f, 0.22f, 0.30f, 1f);
                cols.pressedColor = new Color(0.08f, 0.08f, 0.12f, 1f);
                cols.selectedColor = new Color(0.18f, 0.18f, 0.25f, 1f);
                btn.colors = cols;

                // Label (child of button)
                var lblGO = CreateRect("Label", btnGO.transform);
                Stretch(lblGO);
                var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
                lblTMP.text = $"Choix {i + 1}";
                lblTMP.fontSize = 22;
                lblTMP.alignment = TextAlignmentOptions.Center;
                lblTMP.color = Color.white;
                lblTMP.raycastTarget = false;

                buttons[i] = btn;
                labels[i] = lblTMP;
            }

            // ---- Result Area ----
            var resultAreaGO = CreateRect("ResultArea", frameGO.transform);
            var resultLayout = resultAreaGO.AddComponent<VerticalLayoutGroup>();
            resultLayout.spacing = 12f;
            resultLayout.childAlignment = TextAnchor.UpperCenter;
            resultLayout.childControlWidth = true;
            resultLayout.childControlHeight = false;
            resultLayout.childForceExpandWidth = true;
            resultLayout.childForceExpandHeight = false;
            var resultFitter = resultAreaGO.AddComponent<ContentSizeFitter>();
            resultFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddLayout(resultAreaGO, flexibleHeight: 1f);

            // Result text
            var resultTextGO = CreateRect("ResultText", resultAreaGO.transform);
            var resultTMP = resultTextGO.AddComponent<TextMeshProUGUI>();
            resultTMP.text = "Resultat de l'action...";
            resultTMP.fontSize = 20;
            resultTMP.alignment = TextAlignmentOptions.Center;
            resultTMP.color = new Color(0.85f, 0.85f, 0.85f);
            resultTMP.textWrappingMode = TextWrappingModes.Normal;
            resultTMP.overflowMode = TextOverflowModes.Overflow;
            AddLayout(resultTextGO, preferredHeight: 80f, flexibleHeight: 1f);

            // HP delta text
            var hpGO = CreateRect("HpDeltaText", resultAreaGO.transform);
            var hpTMP = hpGO.AddComponent<TextMeshProUGUI>();
            hpTMP.text = "<color=#44FF44>+5 PV</color>";
            hpTMP.fontSize = 24;
            hpTMP.fontStyle = FontStyles.Bold;
            hpTMP.alignment = TextAlignmentOptions.Center;
            AddLayout(hpGO, preferredHeight: 40f);

            // Reward text
            var rewardGO = CreateRect("RewardText", resultAreaGO.transform);
            var rewardTMP = rewardGO.AddComponent<TextMeshProUGUI>();
            rewardTMP.text = "<color=#FFDD44>Vision +1</color>";
            rewardTMP.fontSize = 22;
            rewardTMP.alignment = TextAlignmentOptions.Center;
            AddLayout(rewardGO, preferredHeight: 35f);

            // Separator before continue
            var sep2 = CreateRect("Separator_Bottom", resultAreaGO.transform);
            var sep2Img = sep2.AddComponent<Image>();
            sep2Img.color = new Color(1f, 0.85f, 0.4f, 0.15f);
            sep2Img.raycastTarget = false;
            AddLayout(sep2, preferredHeight: 2f);

            // Continue button
            var contGO = CreateRect("ContinueButton", resultAreaGO.transform);
            var contBg = contGO.AddComponent<Image>();
            contBg.color = new Color(0.18f, 0.30f, 0.18f, 1f);
            contBg.raycastTarget = true;
            AddLayout(contGO, preferredHeight: 50f);

            var contBtn = contGO.AddComponent<Button>();
            contBtn.targetGraphic = contBg;
            var contCols = contBtn.colors;
            contCols.normalColor = new Color(0.18f, 0.30f, 0.18f, 1f);
            contCols.highlightedColor = new Color(0.28f, 0.45f, 0.28f, 1f);
            contCols.pressedColor = new Color(0.12f, 0.20f, 0.12f, 1f);
            contCols.selectedColor = new Color(0.22f, 0.38f, 0.22f, 1f);
            contBtn.colors = contCols;

            var contLblGO = CreateRect("Label", contGO.transform);
            Stretch(contLblGO);
            var contLblTMP = contLblGO.AddComponent<TextMeshProUGUI>();
            contLblTMP.text = "Continuer";
            contLblTMP.fontSize = 22;
            contLblTMP.fontStyle = FontStyles.Bold;
            contLblTMP.alignment = TextAlignmentOptions.Center;
            contLblTMP.color = Color.white;
            contLblTMP.raycastTarget = false;

            // Start with result area hidden (choices shown first)
            resultAreaGO.SetActive(false);

            // ---- Wire MineEventPanel component ----
            var panel = root.AddComponent<MineEventPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("canvas").objectReferenceValue         = canvas;
            so.FindProperty("canvasGroup").objectReferenceValue    = canvasGroup;
            so.FindProperty("darkOverlay").objectReferenceValue    = overlayImg;
            so.FindProperty("eventFrame").objectReferenceValue     = frameRT;
            so.FindProperty("titleText").objectReferenceValue      = titleTMP;
            so.FindProperty("descriptionText").objectReferenceValue = descTMP;

            // Choice arrays
            var btnProp = so.FindProperty("choiceButtons");
            btnProp.arraySize = 3;
            var lblProp = so.FindProperty("choiceLabels");
            lblProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                btnProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                lblProp.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            }

            so.FindProperty("resultArea").objectReferenceValue     = resultAreaGO.GetComponent<RectTransform>();
            so.FindProperty("resultText").objectReferenceValue     = resultTMP;
            so.FindProperty("hpDeltaText").objectReferenceValue    = hpTMP;
            so.FindProperty("rewardText").objectReferenceValue     = rewardTMP;
            so.FindProperty("continueButton").objectReferenceValue = contBtn;

            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AddLayout(GameObject go, float preferredHeight = -1f, float flexibleHeight = -1f)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
            le.flexibleWidth = 1f;
            if (flexibleHeight >= 0f) le.flexibleHeight = flexibleHeight;
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
