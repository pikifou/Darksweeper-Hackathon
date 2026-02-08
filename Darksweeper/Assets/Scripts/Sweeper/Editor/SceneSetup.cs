#if UNITY_EDITOR
using Mines.Flow;
using Mines.Presentation;
using Sweeper.Flow;
using Sweeper.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sweeper.Editor
{
    /// <summary>
    /// Editor utilities for DarkSweeper.
    /// - "Configure URP" is SAFE: only touches render pipeline settings, never the scene.
    /// - "Create DarkSweeper Scene" is DESTRUCTIVE: replaces the current scene entirely.
    /// </summary>
    public static class SceneSetup
    {
        // ================================================================
        // SAFE: Configure URP settings only (no scene changes)
        // ================================================================

        [MenuItem("DarkSweeper/Configure URP (Forward+ & Lights)")]
        public static void ConfigureURP()
        {
            // --- Pipeline Asset ---
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
                urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");

            if (urpAsset != null)
            {
                var so = new SerializedObject(urpAsset);

                var additionalLightsMode = so.FindProperty("m_AdditionalLightsRenderingMode");
                if (additionalLightsMode != null)
                    additionalLightsMode.intValue = 1; // PerPixel

                var maxLights = so.FindProperty("m_AdditionalLightsPerObjectLimit");
                if (maxLights != null)
                    maxLights.intValue = 8;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(urpAsset);
                Debug.Log("[SceneSetup] URP Pipeline configured: per-pixel additional lights.");
            }
            else
            {
                Debug.LogWarning("[SceneSetup] Could not find URP pipeline asset.");
            }

            // --- Renderer Asset: switch to Forward+ ---
            var rendererAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PC_Renderer.asset");
            if (rendererAsset != null)
            {
                var rendererSO = new SerializedObject(rendererAsset);
                var renderingMode = rendererSO.FindProperty("m_RenderingMode");
                if (renderingMode != null)
                {
                    renderingMode.intValue = 2; // Forward+ in URP 17 (Unity 6)
                    rendererSO.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(rendererAsset);
                    Debug.Log("[SceneSetup] URP Renderer set to Forward+.");
                }
            }

            // --- Disable Tonemapping in the pipeline's default Volume Profile ---
            if (urpAsset != null)
            {
                var urpSO2 = new SerializedObject(urpAsset);
                var volumeProfileProp = urpSO2.FindProperty("m_VolumeProfile");
                if (volumeProfileProp != null && volumeProfileProp.objectReferenceValue != null)
                {
                    var profile = volumeProfileProp.objectReferenceValue as VolumeProfile;
                    if (profile != null && profile.TryGet<Tonemapping>(out var tonemapping))
                    {
                        tonemapping.active = false;
                        tonemapping.mode.Override(TonemappingMode.None);
                        EditorUtility.SetDirty(profile);
                        Debug.Log("[SceneSetup] Tonemapping disabled in default Volume Profile.");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] URP configuration complete. No scene was modified.");
        }

        // ================================================================
        // DESTRUCTIVE: Create a brand new DarkSweeper scene from scratch
        // ================================================================

        [MenuItem("DarkSweeper/Create DarkSweeper Scene (DESTRUCTIVE)")]
        public static void CreateScene()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Create DarkSweeper Scene",
                "WARNING: This will CREATE A NEW EMPTY SCENE and REPLACE whatever is currently open.\n\n" +
                "Any unsaved changes in the current scene will be LOST.\n\n" +
                "Are you sure you want to continue?",
                "Yes, create new scene",
                "Cancel"
            );

            if (!confirmed)
            {
                Debug.Log("[SceneSetup] Scene creation cancelled by user.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Lighting: total darkness by default ---
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;

            // --- Camera: orthographic looking straight down ---
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGO.transform.position = new Vector3(0f, 15f, 0f);
            camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();

            var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
            if (camData != null)
                camData.renderShadows = true;

            // ==================================================================
            // Background Plane — a PERSISTENT scene quad that the user can
            // move, scale, and assign a texture to. GridRenderer references it.
            // At runtime, GridRenderer replaces its material with the fog shader.
            // ==================================================================
            var backgroundPlaneGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backgroundPlaneGO.name = "BackgroundPlane";
            backgroundPlaneGO.transform.rotation = Quaternion.Euler(90f, 180f, 0f); // face up (XZ), 180° Y to fix UV orientation from top-down camera
            backgroundPlaneGO.transform.position = new Vector3(0f, -0.01f, 0f);
            backgroundPlaneGO.transform.localScale = new Vector3(50f, 30f, 1f); // default 50x30 grid
            // Remove collider — it interferes with input raycasts
            var bgCol = backgroundPlaneGO.GetComponent<Collider>();
            if (bgCol != null) Object.DestroyImmediate(bgCol);

            // Apply a simple Unlit material so the texture is visible in the editor
            var bgMeshRend = backgroundPlaneGO.GetComponent<MeshRenderer>();
            var editorBgMat = CreateOrLoadEditorBackgroundMaterial();
            if (editorBgMat != null)
                bgMeshRend.material = editorBgMat;

            // --- Grid Renderer ---
            var gridRendGO = new GameObject("GridRenderer");
            var gridRenderer = gridRendGO.AddComponent<GridRenderer>();
            AssignCellOverlayMaterial(gridRenderer);
            AssignFogOfWarMaterial(gridRenderer);
            AssignFont(gridRenderer);

            // Wire the background plane into GridRenderer
            var gridSO = new SerializedObject(gridRenderer);
            var bgProp = gridSO.FindProperty("backgroundPlaneRenderer");
            if (bgProp != null)
            {
                bgProp.objectReferenceValue = bgMeshRend;
                gridSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- FogOfWarManager ---
            var fogGO = new GameObject("FogOfWarManager");
            var fogOfWar = fogGO.AddComponent<FogOfWarManager>();

            // --- SparseLightGrid ---
            var sparseGO = new GameObject("SparseLightGrid");
            var sparseLights = sparseGO.AddComponent<SparseLightGrid>();

            // --- Input Handler ---
            var inputGO = new GameObject("InputHandler");
            var inputHandler = inputGO.AddComponent<InputHandler>();

            // --- HUD Canvas ---
            var canvasGO = new GameObject("HUD_Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // --- HP Group: heart icon + value text ---
            var hpGroupGO = new GameObject("HP_Group");
            hpGroupGO.transform.SetParent(canvasGO.transform, false);
            var hpGroupRect = hpGroupGO.AddComponent<RectTransform>();
            hpGroupRect.anchorMin = new Vector2(0, 1);
            hpGroupRect.anchorMax = new Vector2(0, 1);
            hpGroupRect.pivot = new Vector2(0, 1);
            hpGroupRect.anchoredPosition = new Vector2(20, -20);
            hpGroupRect.sizeDelta = new Vector2(300, 70);
            var hpLayout = hpGroupGO.AddComponent<HorizontalLayoutGroup>();
            hpLayout.spacing = 10;
            hpLayout.childAlignment = TextAnchor.MiddleLeft;
            hpLayout.childControlWidth = false;
            hpLayout.childControlHeight = false;
            hpLayout.childForceExpandWidth = false;
            hpLayout.childForceExpandHeight = false;

            // Heart icon (placeholder white image — user replaces sprite later)
            var heartGO = new GameObject("Heart_Icon");
            heartGO.transform.SetParent(hpGroupGO.transform, false);
            var heartImage = heartGO.AddComponent<Image>();
            heartImage.color = new Color(1f, 0.3f, 0.35f); // red-ish tint as placeholder
            var heartRect = heartGO.GetComponent<RectTransform>();
            heartRect.sizeDelta = new Vector2(54, 54);

            // HP value text (just the number)
            var hpGO = CreateTMPText(hpGroupGO.transform, "HP_Value", "100",
                Vector2.zero, new Vector2(200, 60), TextAlignmentOptions.MidlineLeft);
            hpGO.GetComponent<TextMeshProUGUI>().fontSize = 48;
            hpGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

            var minesGO = CreateTMPText(canvasGO.transform, "Mines_Text", "Mines: 15",
                new Vector2(-20, -20), new Vector2(300, 50), TextAlignmentOptions.TopRight);
            minesGO.GetComponent<RectTransform>().anchorMin = new Vector2(1, 1);
            minesGO.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            minesGO.GetComponent<RectTransform>().pivot = new Vector2(1, 1);

            var statusGO = CreateTMPText(canvasGO.transform, "Status_Text", "",
                Vector2.zero, new Vector2(600, 100), TextAlignmentOptions.Center);
            statusGO.GetComponent<TextMeshProUGUI>().fontSize = 72;
            statusGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            statusGO.SetActive(false);

            // SweeperHUD
            var hudComponent = canvasGO.AddComponent<SweeperHUD>();
            var hudSO = new SerializedObject(hudComponent);
            hudSO.FindProperty("hpGroup").objectReferenceValue = hpGroupRect;
            hudSO.FindProperty("heartIcon").objectReferenceValue = heartImage;
            hudSO.FindProperty("hpValueText").objectReferenceValue = hpGO.GetComponent<TextMeshProUGUI>();
            hudSO.FindProperty("minesText").objectReferenceValue = minesGO.GetComponent<TextMeshProUGUI>();
            hudSO.FindProperty("statusText").objectReferenceValue = statusGO.GetComponent<TextMeshProUGUI>();
            hudSO.ApplyModifiedPropertiesWithoutUndo();

            // --- Game Controller ---
            var controllerGO = new GameObject("SweeperGameController");
            var controller = controllerGO.AddComponent<SweeperGameController>();

            var ctrlSO = new SerializedObject(controller);
            ctrlSO.FindProperty("gridRenderer").objectReferenceValue = gridRenderer;
            ctrlSO.FindProperty("inputHandler").objectReferenceValue = inputHandler;
            ctrlSO.FindProperty("hud").objectReferenceValue = hudComponent;
            ctrlSO.FindProperty("fogOfWar").objectReferenceValue = fogOfWar;
            ctrlSO.FindProperty("sparseLights").objectReferenceValue = sparseLights;

            var configAsset = AssetDatabase.LoadAssetAtPath<SweeperConfig>("Assets/Data/SweeperConfig_Default.asset");
            if (configAsset != null)
                ctrlSO.FindProperty("config").objectReferenceValue = configAsset;

            ctrlSO.ApplyModifiedPropertiesWithoutUndo();

            // --- EventSystem (required by UGUI for raycasts / button clicks) ---
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<InputSystemUIInputModule>();

            // --- Mine Event System ---
            var mineGO = new GameObject("MineEventSystem");
            var mineController = mineGO.AddComponent<MineEventController>();

            // Create the panel (for dialogues) and toast (for combat/chest/shrine)
            var minePanel = Mines.Editor.MineEventPanelCreator.CreateInScene();
            var mineToast = Mines.Editor.MineEventToastCreator.CreateInScene();

            var mineCtrlSO = new SerializedObject(mineController);
            mineCtrlSO.FindProperty("sweeper").objectReferenceValue = controller;
            mineCtrlSO.FindProperty("inputHandler").objectReferenceValue = inputHandler;
            mineCtrlSO.FindProperty("panel").objectReferenceValue = minePanel;
            mineCtrlSO.FindProperty("toast").objectReferenceValue = mineToast;
            mineCtrlSO.FindProperty("gridRenderer").objectReferenceValue = gridRenderer;

            var distAsset = AssetDatabase.LoadAssetAtPath<MineDistributionSO>("Assets/Data/MineDistribution_Default.asset");
            if (distAsset != null) mineCtrlSO.FindProperty("distribution").objectReferenceValue = distAsset;

            var poolAsset = AssetDatabase.LoadAssetAtPath<EncounterPoolSO>("Assets/Data/EncounterPool_Default.asset");
            if (poolAsset != null) mineCtrlSO.FindProperty("fallbackPool").objectReferenceValue = poolAsset;

            mineCtrlSO.ApplyModifiedPropertiesWithoutUndo();

            // --- Configure URP ---
            ConfigureURP();

            // Save scene
            string scenePath = "Assets/Scenes/DarkSweeper.unity";
            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[SceneSetup] DarkSweeper scene created at {scenePath}");
            Debug.Log("[SceneSetup] The BackgroundPlane is a persistent scene object. " +
                      "Assign your background texture to its material, scale/position it, " +
                      "then adjust GridRenderer's gridOffset and cellSize to match.");
        }

        // ---- Material Helpers ----

        /// <summary>
        /// Creates a simple Unlit material for the background plane in editor mode.
        /// This material shows the texture clearly (no fog). At runtime, GridRenderer
        /// swaps it for the fog-of-war shader.
        /// </summary>
        private static Material CreateOrLoadEditorBackgroundMaterial()
        {
            string matPath = "Assets/Art/Sweeper/Materials/BackgroundEditor.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null)
            {
                EnsureFolder("Assets/Art/Sweeper/Materials");
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) return null;
                mat = new Material(shader);
                mat.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(mat, matPath);
                Debug.Log("[SceneSetup] Created editor background material at " + matPath);
            }

            return mat;
        }

        private static void AssignCellOverlayMaterial(GridRenderer renderer)
        {
            string matPath = "Assets/Art/Sweeper/Materials/CellOverlay.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null)
            {
                EnsureFolder("Assets/Art/Sweeper/Materials");
                var shader = Shader.Find("DarkSweeper/CellOverlay");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.25f, 1f));
                mat.SetFloat("_Brightness", 0f);
                mat.SetColor("_EmissionColor", Color.black);
                AssetDatabase.CreateAsset(mat, matPath);
                Debug.Log("[SceneSetup] Created CellOverlay material at " + matPath);
            }

            var so = new SerializedObject(renderer);
            var prop = so.FindProperty("baseMaterial");
            if (prop != null)
            {
                prop.objectReferenceValue = mat;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignFogOfWarMaterial(GridRenderer renderer)
        {
            string matPath = "Assets/Art/Sweeper/Materials/FogOfWar.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null)
            {
                EnsureFolder("Assets/Art/Sweeper/Materials");
                var shader = Shader.Find("DarkSweeper/FogOfWar");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(shader);
                mat.SetColor("_FogColor", Color.black);
                mat.SetColor("_LitTint", Color.white);
                AssetDatabase.CreateAsset(mat, matPath);
                Debug.Log("[SceneSetup] Created FogOfWar material at " + matPath);
            }

            var so = new SerializedObject(renderer);
            var prop = so.FindProperty("fogOfWarMaterial");
            if (prop != null)
            {
                prop.objectReferenceValue = mat;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignFont(GridRenderer renderer)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            if (font != null)
            {
                var so = new SerializedObject(renderer);
                var prop = so.FindProperty("fontAsset");
                if (prop != null)
                {
                    prop.objectReferenceValue = font;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        // ---- Generic Helpers ----

        private static GameObject CreateTMPText(Transform parent, string name, string text,
            Vector2 anchoredPos, Vector2 sizeDelta, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 32;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            return go;
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
