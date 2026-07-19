using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TUFReplay.Editor
{
    internal static class MicrophoneRecordingToastBuilder
    {
        // Keep the prefab free of TUFReplay runtime scripts so it can be loaded by ADOFAI safely.
        private const string AssetRoot = "Assets/TUFReplay";
        private const string ArtRoot = AssetRoot + "/Art";
        private const string FontRoot = AssetRoot + "/Font";
        private const string PrefabRoot = AssetRoot + "/Prefabs";
        private const string PrefabPath = PrefabRoot + "/MicrophoneRecordingToast.prefab";
        private const string PanelSpritePath = ArtRoot + "/rounded-panel.png";
        private const string FontSourcePath = FontRoot + "/MAPLESTORY_OTF_BOLD.OTF";
        private const string FontAssetPath = FontRoot + "/MAPLESTORY_OTF_BOLD Dynamic SDF.asset";
        private const string BundleName = "tufreplay_ui.bundle";

        [InitializeOnLoadMethod]
        private static void CreateInitialPrefab()
        {
            if (!File.Exists(Path.Combine(ProjectRoot, PrefabPath)))
            {
                EditorApplication.delayCall += CreateMicrophoneRecordingToast;
            }
        }

        [MenuItem("TUFReplay/UI/Create Microphone Recording Toast")]
        public static void CreateMicrophoneRecordingToast()
        {
            EnsureAssetFolders();
            EnsureFontSource();
            CreateRoundedPanelTexture();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureSprite(PanelSpritePath, new Vector4(24f, 24f, 24f, 24f));

            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
            if (panelSprite == null)
            {
                throw new InvalidOperationException("Failed to create the rounded panel sprite.");
            }

            TMP_FontAsset font = EnsureFontAsset();
            GameObject root = CreateRoot();
            CreateToastCard(root.transform, panelSprite, font);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null)
            {
                throw new InvalidOperationException("Failed to save the microphone recording toast prefab.");
            }

            AssetImporter importer = AssetImporter.GetAtPath(PrefabPath);
            importer.assetBundleName = BundleName;
            importer.SaveAndReimport();
            AssetDatabase.RemoveUnusedAssetBundleNames();
            ShowPreview();
            Debug.Log($"TUFReplay microphone recording toast created at {PrefabPath}");
        }

        [MenuItem("TUFReplay/UI/Preview Microphone Recording Toast")]
        public static void ShowPreview()
        {
            GameObject existing = GameObject.Find("MicrophoneRecordingToast Preview");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                CreateMicrophoneRecordingToast();
                return;
            }

            GameObject preview = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (preview == null)
            {
                throw new InvalidOperationException("Failed to instantiate the microphone recording toast preview.");
            }

            preview.name = "MicrophoneRecordingToast Preview";
            Selection.activeGameObject = preview;
        }

        [MenuItem("TUFReplay/Build/Build macOS UI Bundle")]
        public static void BuildMacBundle()
        {
            BuildBundle(BuildTarget.StandaloneOSX, "mac");
        }

        [MenuItem("TUFReplay/Build/Build All UI Bundles", priority = 0)]
        public static void BuildAllBundles()
        {
            BuildBundle(BuildTarget.StandaloneOSX, "mac");
            BuildBundle(BuildTarget.StandaloneWindows64, "win");
            BuildBundle(BuildTarget.StandaloneLinux64, "linux");
        }

        [MenuItem("TUFReplay/Build/Build Windows UI Bundle")]
        public static void BuildWindowsBundle()
        {
            BuildBundle(BuildTarget.StandaloneWindows64, "win");
        }

        [MenuItem("TUFReplay/Build/Build Linux UI Bundle")]
        public static void BuildLinuxBundle()
        {
            BuildBundle(BuildTarget.StandaloneLinux64, "linux");
        }

        private static GameObject CreateRoot()
        {
            GameObject root = new GameObject(
                "MicrophoneRecordingToast",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return root;
        }

        private static void CreateToastCard(Transform parent, Sprite panelSprite, TMP_FontAsset font)
        {
            Image card = CreateImage("ToastCard", parent, panelSprite, new Color32(7, 7, 10, 245));
            SetRect(
                card.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-24f, 24f),
                new Vector2(420f, 168f));
            card.type = Image.Type.Sliced;
            card.raycastTarget = true;

            CanvasGroup canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.13f);
            outline.effectDistance = new Vector2(1f, -1f);

            Image timerTrack = CreateImage("TimerTrack", card.transform, null, new Color32(35, 54, 64, 235));
            SetRect(
                timerTrack.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -3f),
                new Vector2(-24f, 6f));

            Image timerFill = CreateImage("TimerFill", timerTrack.transform, null, new Color32(68, 191, 255, 255));
            SetRect(
                timerFill.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            timerFill.type = Image.Type.Simple;
            timerFill.raycastTarget = false;

            Image accentDot = CreateImage("AccentDot", card.transform, panelSprite, new Color32(68, 191, 255, 255));
            SetRect(
                accentDot.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -23f),
                new Vector2(7f, 7f));
            accentDot.type = Image.Type.Sliced;

            CreateText(
                "EyebrowText",
                card.transform,
                font,
                "MICROPHONE RECORDING",
                11f,
                FontStyles.Bold,
                new Color32(119, 197, 235, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(38f, -17f),
                new Vector2(354f, 18f));

            CreateText(
                "TitleText",
                card.transform,
                font,
                "Save microphone recording?",
                21f,
                FontStyles.Normal,
                new Color32(246, 246, 248, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -39f),
                new Vector2(372f, 28f));

            TextMeshProUGUI bodyText = CreateText(
                "BodyText",
                card.transform,
                font,
                "This recording will be discarded automatically in 10 seconds.",
                13f,
                FontStyles.Normal,
                new Color32(174, 177, 187, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -70f),
                new Vector2(372f, 34f));
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Overflow;

            Image divider = CreateImage("Divider", card.transform, null, new Color(1f, 1f, 1f, 0.09f));
            SetRect(
                divider.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -112f),
                new Vector2(372f, 1f));

            CreateButton(
                "DiscardButton",
                card.transform,
                panelSprite,
                font,
                "Discard",
                new Color32(37, 40, 48, 245),
                new Color32(224, 226, 232, 255),
                new Vector2(-134f, 14f),
                new Vector2(90f, 38f),
                false);

            CreateButton(
                "SaveButton",
                card.transform,
                panelSprite,
                font,
                "Save",
                new Color32(68, 191, 255, 255),
                new Color32(12, 20, 25, 255),
                new Vector2(-24f, 14f),
                new Vector2(100f, 38f),
                true);
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Sprite panelSprite,
            TMP_FontAsset font,
            string label,
            Color background,
            Color foreground,
            Vector2 position,
            Vector2 dimensions,
            bool primary)
        {
            Image image = CreateImage(name, parent, panelSprite, background);
            SetRect(
                image.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                position,
                dimensions);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            if (!primary)
            {
                Outline outline = image.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 1f, 1f, 0.1f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary
                ? new Color32(150, 224, 255, 255)
                : new Color32(65, 69, 80, 255);
            colors.pressedColor = primary
                ? new Color32(44, 155, 208, 255)
                : new Color32(27, 29, 35, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(100, 100, 105, 180);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            CreateText(
                "Label",
                image.transform,
                font,
                label,
                primary ? 15f : 14f,
                FontStyles.Bold,
                foreground,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private static void BuildBundle(BuildTarget target, string platformFolder)
        {
            if (!File.Exists(Path.Combine(ProjectRoot, PrefabPath)))
            {
                CreateMicrophoneRecordingToast();
            }

            string outputDirectory = Path.Combine(ProjectRoot, "Build", "AssetBundles", platformFolder);
            Directory.CreateDirectory(outputDirectory);
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outputDirectory,
                BuildAssetBundleOptions.ChunkBasedCompression,
                target);

            if (manifest == null)
            {
                throw new InvalidOperationException($"Failed to build the {platformFolder} UI bundle.");
            }

            string builtBundle = Path.Combine(outputDirectory, BundleName);
            string modAssetDirectory = Path.Combine(RepositoryRoot, "TUFReplay", "Assets", platformFolder);
            Directory.CreateDirectory(modAssetDirectory);
            File.Copy(builtBundle, Path.Combine(modAssetDirectory, BundleName), true);
            AssetDatabase.Refresh();
            Debug.Log($"Built {target} UI bundle and copied it to {modAssetDirectory}");
        }

        private static void EnsureAssetFolders()
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot, ArtRoot));
            Directory.CreateDirectory(Path.Combine(ProjectRoot, FontRoot));
            Directory.CreateDirectory(Path.Combine(ProjectRoot, PrefabRoot));
            AssetDatabase.Refresh();
        }

        private static void EnsureFontSource()
        {
            string destination = Path.Combine(ProjectRoot, FontSourcePath);
            if (File.Exists(destination))
            {
                return;
            }

            string source = Path.Combine(
                RepositoryRoot,
                "..",
                "tufhelper2",
                "TUFHelperLite.Unity",
                "Assets",
                "TUFHelperLite",
                "Font",
                "MAPLESTORY_OTF_BOLD.OTF");
            source = Path.GetFullPath(source);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException(
                    "MapleStory Bold font source was not found in TUFHelperLite.",
                    source);
            }

            File.Copy(source, destination, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static TMP_FontAsset EnsureFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            Font source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (source == null)
            {
                throw new FileNotFoundException("MapleStory Bold font source was not imported.", FontSourcePath);
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                72,
                8,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (fontAsset == null)
            {
                throw new InvalidOperationException("Failed to create the dynamic MapleStory TMP font asset.");
            }

            fontAsset.name = "MAPLESTORY_OTF_BOLD Dynamic SDF";
            Texture2D atlas = fontAsset.atlasTextures[0];
            Material material = fontAsset.material;
            atlas.name = "MAPLESTORY_OTF_BOLD Atlas";
            material.name = "MAPLESTORY_OTF_BOLD Material";

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            AssetDatabase.AddObjectToAsset(material, fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void CreateRoundedPanelTexture()
        {
            string absolutePath = Path.Combine(ProjectRoot, PanelSpritePath);
            if (File.Exists(absolutePath))
            {
                return;
            }

            const int size = 64;
            const float radius = 14f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distanceX = Mathf.Max(radius - x, 0f, x - (size - 1f - radius));
                    float distanceY = Mathf.Max(radius - y, 0f, y - (size - 1f - radius));
                    float distance = Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void ConfigureSprite(string path, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float size,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 dimensions)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            SetRect(text.rectTransform, anchorMin, anchorMax, pivot, position, dimensions);
            return text;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
        }

        private static string ProjectRoot => Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the Unity project directory.");

        private static string RepositoryRoot => Directory.GetParent(ProjectRoot)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the TUFReplay repository directory.");
    }
}
