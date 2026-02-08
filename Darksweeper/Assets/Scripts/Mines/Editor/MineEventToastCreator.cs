#if UNITY_EDITOR
using Mines.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Mines.Editor
{
    /// <summary>
    /// Editor utility that creates a fully-wired MineEventToast prefab.
    /// Run via <b>DarkSweeper &gt; Create Mine Event Toast Prefab</b>.
    ///
    /// The toast slides up from the bottom of the screen to show quick
    /// event results (combat, chest, shrine). Customise the generated
    /// prefab freely in the editor.
    /// </summary>
    public static class MineEventToastCreator
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath   = PrefabFolder + "/MineEventToast.prefab";

        // ================================================================
        // Menu Entry
        // ================================================================

        [MenuItem("DarkSweeper/Create Mine Event Toast Prefab")]
        public static void CreatePrefab()
        {
            EnsureFolder(PrefabFolder);

            var root = BuildHierarchy();

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

            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;

            Debug.Log($"[MineEventToastCreator] Prefab created at {PrefabPath}.\n" +
                      "Open it, customise freely, then drag into your scene.");
        }

        // ================================================================
        // Scene instance (used by other editor scripts)
        // ================================================================

        public static MineEventToast CreateInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject root;

            if (prefab != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Debug.Log($"[MineEventToastCreator] Instantiated prefab from {PrefabPath}.");
            }
            else
            {
                root = BuildHierarchy();
                Debug.Log("[MineEventToastCreator] No prefab found — created hierarchy from scratch.");
            }

            Undo.RegisterCreatedObjectUndo(root, "Create MineEventToast");
            return root.GetComponent<MineEventToast>();
        }

        // ================================================================
        // Hierarchy Builder
        // ================================================================

        private static GameObject BuildHierarchy()
        {
            // ---- Root: Canvas ----
            var root = new GameObject("MineEventToast");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // below modal panel (100)

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            root.AddComponent<GraphicRaycaster>();
            var canvasGroup = root.AddComponent<CanvasGroup>();

            // ---- Toast Container (anchored bottom-centre) ----
            var containerGO = CreateRect("ToastContainer", root.transform);
            var containerRT = containerGO.GetComponent<RectTransform>();
            // Anchored at bottom centre, 50% width, ~200px tall
            containerRT.anchorMin = new Vector2(0.25f, 0f);
            containerRT.anchorMax = new Vector2(0.75f, 0f);
            containerRT.pivot = new Vector2(0.5f, 0f);
            containerRT.anchoredPosition = new Vector2(0f, 20f);
            containerRT.sizeDelta = new Vector2(0f, 200f);

            var containerBg = containerGO.AddComponent<Image>();
            containerBg.color = new Color(0.04f, 0.04f, 0.06f, 0.9f);
            containerBg.raycastTarget = false;

            // Horizontal layout: video area left, cartouche right
            var containerLayout = containerGO.AddComponent<HorizontalLayoutGroup>();
            containerLayout.padding = new RectOffset(10, 10, 10, 10);
            containerLayout.spacing = 15f;
            containerLayout.childAlignment = TextAnchor.MiddleLeft;
            containerLayout.childControlWidth = false;
            containerLayout.childControlHeight = true;
            containerLayout.childForceExpandWidth = false;
            containerLayout.childForceExpandHeight = true;

            // ---- Video Area (left side) ----
            var videoAreaGO = CreateRect("VideoArea", containerGO.transform);
            var videoAreaLE = videoAreaGO.AddComponent<LayoutElement>();
            videoAreaLE.preferredWidth = 240f;
            videoAreaLE.flexibleWidth = 0f;

            // RawImage for VideoPlayer render texture
            var rawImageGO = CreateRect("VideoImage", videoAreaGO.transform);
            Stretch(rawImageGO);
            var rawImage = rawImageGO.AddComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;

            // Placeholder icon (shown when no video)
            var placeholderGO = CreateRect("PlaceholderIcon", videoAreaGO.transform);
            Stretch(placeholderGO);
            var placeholderImg = placeholderGO.AddComponent<Image>();
            placeholderImg.color = new Color(0.1f, 0.1f, 0.13f, 0.8f);
            placeholderImg.raycastTarget = false;

            var placeholderTextGO = CreateRect("PlaceholderText", placeholderGO.transform);
            Stretch(placeholderTextGO);
            var placeholderTMP = placeholderTextGO.AddComponent<TextMeshProUGUI>();
            placeholderTMP.text = "\u2694"; // default icon
            placeholderTMP.fontSize = 64;
            placeholderTMP.alignment = TextAlignmentOptions.Center;
            placeholderTMP.color = new Color(0.9f, 0.35f, 0.3f, 0.8f);
            placeholderTMP.raycastTarget = false;

            // VideoPlayer component (on the raw image object)
            var vp = rawImageGO.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = true;
            vp.renderMode = VideoRenderMode.APIOnly;
            // At runtime, MineEventToast will create a RenderTexture and assign it

            // ---- Cartouche (right side, text info) ----
            var cartoucheGO = CreateRect("Cartouche", containerGO.transform);
            var cartoucheLE = cartoucheGO.AddComponent<LayoutElement>();
            cartoucheLE.flexibleWidth = 1f;

            var cartoucheLayout = cartoucheGO.AddComponent<VerticalLayoutGroup>();
            cartoucheLayout.padding = new RectOffset(10, 10, 5, 5);
            cartoucheLayout.spacing = 4f;
            cartoucheLayout.childAlignment = TextAnchor.MiddleLeft;
            cartoucheLayout.childControlWidth = true;
            cartoucheLayout.childControlHeight = false;
            cartoucheLayout.childForceExpandWidth = true;
            cartoucheLayout.childForceExpandHeight = false;

            // Icon + Title row
            var titleRowGO = CreateRect("TitleRow", cartoucheGO.transform);
            var titleRowLayout = titleRowGO.AddComponent<HorizontalLayoutGroup>();
            titleRowLayout.spacing = 8f;
            titleRowLayout.childAlignment = TextAnchor.MiddleLeft;
            titleRowLayout.childControlWidth = false;
            titleRowLayout.childControlHeight = true;
            titleRowLayout.childForceExpandWidth = false;
            titleRowLayout.childForceExpandHeight = false;
            AddLayout(titleRowGO, preferredHeight: 36f);

            // Icon text
            var iconGO = CreateRect("IconText", titleRowGO.transform);
            var iconTMP = iconGO.AddComponent<TextMeshProUGUI>();
            iconTMP.text = "\u2694";
            iconTMP.fontSize = 28;
            iconTMP.alignment = TextAlignmentOptions.Center;
            iconTMP.color = new Color(0.9f, 0.35f, 0.3f, 1f);
            iconTMP.raycastTarget = false;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 40f;

            // Title text
            var titleGO = CreateRect("TitleText", titleRowGO.transform);
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "Nom de l'evenement";
            titleTMP.fontSize = 24;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Left;
            titleTMP.color = new Color(1f, 0.85f, 0.4f, 1f);
            titleTMP.raycastTarget = false;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1f;

            // Result text
            var resultGO = CreateRect("ResultText", cartoucheGO.transform);
            var resultTMP = resultGO.AddComponent<TextMeshProUGUI>();
            resultTMP.text = "Resultat de l'action...";
            resultTMP.fontSize = 18;
            resultTMP.alignment = TextAlignmentOptions.Left;
            resultTMP.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            resultTMP.textWrappingMode = TextWrappingModes.Normal;
            resultTMP.overflowMode = TextOverflowModes.Ellipsis;
            resultTMP.raycastTarget = false;
            AddLayout(resultGO, preferredHeight: 50f, flexibleHeight: 1f);

            // HP delta
            var hpGO = CreateRect("HpDeltaText", cartoucheGO.transform);
            var hpTMP = hpGO.AddComponent<TextMeshProUGUI>();
            hpTMP.text = "<color=#44FF44>+5 PV</color>";
            hpTMP.fontSize = 20;
            hpTMP.fontStyle = FontStyles.Bold;
            hpTMP.alignment = TextAlignmentOptions.Left;
            hpTMP.raycastTarget = false;
            AddLayout(hpGO, preferredHeight: 28f);

            // Reward
            var rewardGO = CreateRect("RewardText", cartoucheGO.transform);
            var rewardTMP = rewardGO.AddComponent<TextMeshProUGUI>();
            rewardTMP.text = "<color=#FFDD44>Vision +1</color>";
            rewardTMP.fontSize = 18;
            rewardTMP.alignment = TextAlignmentOptions.Left;
            rewardTMP.raycastTarget = false;
            AddLayout(rewardGO, preferredHeight: 25f);

            // ---- Wire MineEventToast component ----
            var toast = root.AddComponent<MineEventToast>();
            var so = new SerializedObject(toast);
            so.FindProperty("canvas").objectReferenceValue          = canvas;
            so.FindProperty("canvasGroup").objectReferenceValue     = canvasGroup;
            so.FindProperty("toastContainer").objectReferenceValue  = containerRT;
            so.FindProperty("videoImage").objectReferenceValue      = rawImage;
            so.FindProperty("videoPlayer").objectReferenceValue     = vp;
            so.FindProperty("placeholderIcon").objectReferenceValue = placeholderImg;
            so.FindProperty("placeholderIconText").objectReferenceValue = placeholderTMP;
            so.FindProperty("iconText").objectReferenceValue        = iconTMP;
            so.FindProperty("titleText").objectReferenceValue       = titleTMP;
            so.FindProperty("resultText").objectReferenceValue      = resultTMP;
            so.FindProperty("hpDeltaText").objectReferenceValue     = hpTMP;
            so.FindProperty("rewardText").objectReferenceValue      = rewardTMP;
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
