using System.Collections.Generic;
using System.IO;
using Chapchu.UI.Title;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// [개발용 — 타이틀 S6 QA 가 끝나면 삭제] 타이틀 UI 프리팹 3종과 TitleScene 을 코드로 생성하고, 결과를 PNG 로 캡처한다.
    /// 수치 기준: docs/systems/12-title-ui.md · docs/title-ui-plan.md 2절. 절차는 docs/worklog.md.
    /// Build 는 TitleScene 의 UI 를 통째로 다시 만든다 — 에디터에서 손으로 고친 내용은 사라지므로 메뉴로 노출하지 않는다.
    /// 에디터가 열려 있지 않은 프로젝트에서 batchmode 로만 실행한다:
    ///   unity run &lt;프로젝트&gt; -- -executeMethod Chapchu.EditorTools.TitleSceneBuilder.BuildAndCapture -captureDir &lt;폴더&gt;
    ///   unity run &lt;프로젝트&gt; -- -executeMethod Chapchu.EditorTools.TitleSceneBuilder.CaptureOnly -captureDir &lt;폴더&gt;
    /// </summary>
    public static class TitleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/TitleScene.unity";
        private const string PrefabDir = "Assets/Prefab/UI/";
        private const string SpriteDir = "Assets/Sprites/UI/";
        private const string FontDir = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
        private const string PresetDir = "Assets/Fonts/";
        private const string BoldFontName = "Hakgyoansim Dunggeunmiso OTF B SDF";
        private const string RegularFontName = "Hakgyoansim Dunggeunmiso OTF R SDF";

        private const float ShadowSpriteMargin = 40f;

        // 12-title-ui 4-3 모바일 확대 배율. 아티팩트(1920×1080 모니터 기준)대로면 폰에서 글자 · 버튼이 너무 작다.
        private const float TitleGroupScale = 1.3f;
        private const float FormGroupScale = 1.7f;
        private const float ContentSpacing = 56f;

        private static readonly Color Cream = Hex(0xFFFDF8);
        private static readonly Color Purple = Hex(0xB18AE0);
        private static readonly Color PurpleHover = Hex(0xBF9CE9);
        private static readonly Color InputText = Hex(0x5B3E8C);
        private static readonly Color Muted = Hex(0xA08FB5);
        private static readonly Color ErrorColor = Hex(0xFF6E8A);
        private static readonly Color Night = Hex(0x2E2440);

        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);

            GameObject pillButton = BuildPillButton();
            GameObject pillInput = BuildPillInputField();
            GameObject iconButton = BuildIconButton();
            BuildScene(pillButton, pillInput, iconButton);

            AssetDatabase.SaveAssets();
            Debug.Log("[TitleSceneBuilder] Build 완료");
        }

        public static void BuildAndCapture()
        {
            TitleUIAssetSetup.SetupAll();
            Build();
            Capture();
        }

        public static void CaptureOnly() => Capture();

        #region Prefabs

        private static GameObject BuildPillButton()
        {
            // 아티팩트는 content-box 다. 300 × 74 는 테두리 안쪽 크기이고, 겉 크기는 테두리 5 를 더한 310 × 84.
            GameObject root = NewRoot("PillButton", 310f, 84f);
            // 버튼 전체가 눌림 영역. 보이지 않는 Image 로 레이캐스트만 받는다.
            Image hitArea = root.AddComponent<Image>();
            hitArea.color = Color.clear;

            RectTransform body = NewRect("Body", root.transform);
            Stretch(body, 0f, 0f, 0f, 0f);

            // CSS box-shadow 는 버튼과 함께 움직이므로 그림자도 Body 의 자식이다.
            Image soft = NewImage("SoftShadow", body, Sprite("ui_shadow_soft"), Rgba(50, 20, 55, 0.4f), 1f);
            ShadowRect(soft.rectTransform, 14f);
            Image hard = NewImage("HardShadow", body, Sprite("ui_round64"), Rgba(84, 50, 140, 0.55f), 64f / 22f);
            Stretch(hard.rectTransform, 0f, 0f, 0f, 0f);
            hard.rectTransform.anchoredPosition = new Vector2(0f, -7f);

            Image border = NewImage("Border", body, Sprite("ui_round64"), Cream, 64f / 22f);
            Stretch(border.rectTransform, 0f, 0f, 0f, 0f);
            Image fill = NewImage("Fill", body, Sprite("ui_round64"), Purple, 64f / 17f);
            Stretch(fill.rectTransform, 5f, 5f, 5f, 5f);

            TextMeshProUGUI label = NewText("Label", body, Font(BoldFontName), 30f, Cream, "접속", TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 5f, 5f, 5f, 5f);

            Button button = root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitArea;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            PressableButton pressable = root.AddComponent<PressableButton>();
            SetRefs(pressable, ("body", body), ("hardShadow", hard.rectTransform), ("fill", fill));
            SetColor(pressable, "normalColor", Purple);
            SetColor(pressable, "hoverColor", PurpleHover);

            return SavePrefab(root, "PillButton");
        }

        private static GameObject BuildPillInputField()
        {
            // content-box: 안쪽 560 × 66 + 좌우 여백 22 + 테두리 4 → 겉 612 × 74. 입력 묶음(560)보다 넓어 양옆으로 26씩 넘친다.
            GameObject root = NewRoot("PillInputField", 612f, 74f);

            Image soft = NewImage("SoftShadow", root.transform, Sprite("ui_shadow_soft"), Rgba(60, 25, 60, 0.35f), 1f);
            ShadowRect(soft.rectTransform, 10f);
            // CSS 배경은 테두리 아래까지 깔린다(background-clip: border-box) → 배경은 꽉 차게, 테두리는 링으로 위에.
            Image fill = NewImage("Fill", root.transform, Sprite("ui_round64"), Rgba(255, 253, 248, 0.92f), 64f / 20f);
            Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            Image highlight = NewImage("TopHighlight", root.transform, Sprite("ui_round64_top_highlight"), Rgba(255, 255, 255, 0.8f), 4f);
            Stretch(highlight.rectTransform, 4f, 4f, 4f, 4f);
            Image border = NewImage("Border", root.transform, Sprite("ui_ring_r20_w4"), Purple, 64f / 20f);
            Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

            // 테두리 4 + 좌우 안쪽 여백 22
            RectTransform content = NewRect("Content", root.transform);
            Stretch(content, 26f, 4f, 26f, 4f);
            HorizontalLayoutGroup row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            RectTransform inputRect = NewRect("InputField", content);
            // 입력창을 눌러 포커스할 수 있도록 투명 Image 로 레이캐스트를 받는다.
            Image inputHitArea = inputRect.gameObject.AddComponent<Image>();
            inputHitArea.color = Color.clear;
            LayoutElement inputLayout = inputRect.gameObject.AddComponent<LayoutElement>();
            inputLayout.flexibleWidth = 1f;

            RectTransform textArea = NewRect("Text Area", inputRect);
            Stretch(textArea, 0f, 0f, 0f, 0f);
            textArea.gameObject.AddComponent<RectMask2D>();

            TMP_FontAsset regular = Font(RegularFontName);
            TextMeshProUGUI placeholder = NewText("Placeholder", textArea, regular, 22f, Muted, "닉네임을 입력해주세요.", TextAlignmentOptions.Left);
            Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);
            TextMeshProUGUI text = NewText("Text", textArea, regular, 22f, InputText, string.Empty, TextAlignmentOptions.Left);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

            TMP_InputField input = inputRect.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = inputHitArea;
            input.fontAsset = regular;
            input.pointSize = 22f;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 16;
            input.customCaretColor = true;
            input.caretColor = InputText;
            input.selectionColor = Rgba(177, 138, 224, 0.35f);
            input.transition = Selectable.Transition.None;
            input.navigation = new Navigation { mode = Navigation.Mode.None };

            TextMeshProUGUI count = NewText("CountText", content, regular, 20f, Muted, "0 / 16", TextAlignmentOptions.Right);

            NicknameFieldView view = root.AddComponent<NicknameFieldView>();
            SetRefs(view, ("inputField", input), ("countText", count));

            return SavePrefab(root, "PillInputField");
        }

        private static GameObject BuildIconButton()
        {
            // content-box: 46 + 테두리 2 × 2 → 겉 50 × 50
            GameObject root = NewRoot("IconButton", 50f, 50f);

            Image fill = root.AddComponent<Image>();
            ApplySprite(fill, Sprite("ui_round64"), Rgba(46, 36, 64, 0.45f), 64f / 14f);

            Image border = NewImage("Border", root.transform, Sprite("ui_ring_r14_w2"), Rgba(255, 253, 248, 0.5f), 64f / 14f);
            Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

            Image icon = NewImage("Icon", root.transform, Sprite("icon_sound"), Cream, 1f);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = new Vector2(22f, 22f);

            Button button = root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = fill;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            return SavePrefab(root, "IconButton");
        }

        #endregion

        #region Scene

        private static void BuildScene(GameObject pillButtonPrefab, GameObject pillInputPrefab, GameObject iconButtonPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 카메라 · EventSystem 만 남기고 기존 UI 를 전부 지운다.
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.GetComponent<Camera>() != null || rootObject.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                    continue;
                Debug.Log($"[TitleSceneBuilder] 삭제: {rootObject.name}");
                Object.DestroyImmediate(rootObject);
            }

            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Night;
            }

            GameObject canvasObject = new("TitleCanvas", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Transform canvasRoot = canvasObject.transform;

            // 배경 — 일러스트(cover) + 세로 오버레이
            Image illust = NewImage("BG_Illust", canvasRoot, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/TitleBackground.png"), Color.white, 1f);
            illust.type = Image.Type.Simple;
            AspectRatioFitter fitter = illust.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = illust.sprite != null ? illust.sprite.rect.width / illust.sprite.rect.height : 16f / 9f;

            Image overlay = NewImage("BG_Overlay", canvasRoot, Sprite("title_overlay_vertical"), Color.white, 1f);
            overlay.type = Image.Type.Simple;
            Stretch(overlay.rectTransform, 0f, 0f, 0f, 0f);

            // 중앙 묶음
            RectTransform content = NewRect("Content", canvasRoot);
            Stretch(content, 0f, 0f, 0f, 0f);
            VerticalLayoutGroup contentLayout = AddVertical(content.gameObject, ContentSpacing, TextAnchor.MiddleCenter);
            contentLayout.padding = new RectOffset(24, 24, 76, 108);
            // 12-title-ui 4-3: 묶음 안의 수치는 아티팩트 그대로 두고 묶음 배율로만 키운다 → 레이아웃이 배율을 반영해야 한다.
            contentLayout.childScaleWidth = true;
            contentLayout.childScaleHeight = true;

            RectTransform titleGroup = NewRect("TitleGroup", content);
            AddVertical(titleGroup.gameObject, 6f, TextAnchor.UpperCenter);
            titleGroup.localScale = new Vector3(TitleGroupScale, TitleGroupScale, 1f);

            TMP_FontAsset bold = Font(BoldFontName);
            TextMeshProUGUI logo = NewText("Logo", titleGroup, bold, 190f, Cream, "찹츄", TextAlignmentOptions.Center);
            logo.fontSharedMaterial = Preset("Logo");
            logo.characterSpacing = 4f;
            // CSS line-height .95
            FixHeight(logo, 190f * 0.95f);
            TextMeshProUGUI subtitle = NewText("Subtitle", titleGroup, bold, 36f, Cream, "치와와개판오분전언찹츄찹찹츄", TextAlignmentOptions.Center);
            subtitle.fontSharedMaterial = Preset("Subtitle");
            FixHeight(subtitle, NormalLineHeight(bold, 36f));

            RectTransform formGroup = NewRect("FormGroup", content);
            AddVertical(formGroup.gameObject, 16f, TextAnchor.UpperCenter);
            formGroup.localScale = new Vector3(FormGroupScale, FormGroupScale, 1f);
            LayoutElement formLayout = formGroup.gameObject.AddComponent<LayoutElement>();
            formLayout.preferredWidth = 560f;

            GameObject nicknameField = Instantiate(pillInputPrefab, formGroup, "NicknameField");
            GameObject connectButton = Instantiate(pillButtonPrefab, formGroup, "ConnectButton");

            TextMeshProUGUI errorText = NewText("ErrorText", formGroup, bold, 18f, ErrorColor, string.Empty, TextAlignmentOptions.Top);
            errorText.fontSharedMaterial = Preset("Error");
            LayoutElement errorLayout = errorText.gameObject.AddComponent<LayoutElement>();
            errorLayout.minHeight = 26f;
            errorLayout.preferredHeight = 26f;
            errorLayout.preferredWidth = 560f;
            ErrorLabel errorLabel = errorText.gameObject.AddComponent<ErrorLabel>();

            // 좌하단 버전
            TMP_FontAsset mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/IBMPlexMono SDF.asset");
            Color versionColor = Cream;
            versionColor.a = 0.72f;
            TextMeshProUGUI version = NewText("VersionText", canvasRoot, mono, 12f, versionColor,
                $"ver {PlayerSettings.bundleVersion} · Project ChapChu", TextAlignmentOptions.BottomLeft);
            Corner(version.rectTransform, new Vector2(0f, 0f), new Vector2(26f, 22f));
            ContentSizeFitter versionFitter = version.gameObject.AddComponent<ContentSizeFitter>();
            versionFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            versionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            version.gameObject.AddComponent<VersionLabel>();

            // 우하단 시스템 버튼
            RectTransform systemButtons = NewRect("SystemButtons", canvasRoot);
            Corner(systemButtons, new Vector2(1f, 0f), new Vector2(-26f, 22f));
            HorizontalLayoutGroup systemRow = systemButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            systemRow.spacing = 10f;
            systemRow.childAlignment = TextAnchor.LowerRight;
            systemRow.childControlWidth = true;
            systemRow.childControlHeight = true;
            systemRow.childForceExpandWidth = false;
            systemRow.childForceExpandHeight = false;
            ContentSizeFitter systemFitter = systemButtons.gameObject.AddComponent<ContentSizeFitter>();
            systemFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            systemFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Instantiate(iconButtonPrefab, systemButtons, "Btn_Sound");
            GameObject menuButton = Instantiate(iconButtonPrefab, systemButtons, "Btn_Menu");
            menuButton.transform.Find("Icon").GetComponent<Image>().sprite = Sprite("icon_menu");

            TitleView titleView = canvasObject.AddComponent<TitleView>();
            SetRefs(titleView,
                ("nicknameField", nicknameField.GetComponent<NicknameFieldView>()),
                ("connectButton", connectButton.GetComponent<Button>()),
                ("connectLabel", connectButton.transform.Find("Body/Label").GetComponent<TMP_Text>()),
                ("errorLabel", errorLabel));
            // S7 접속 연결. TitleView 를 GetComponent 로 찾는다.
            canvasObject.AddComponent<TitlePresenter>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        #endregion

        #region Capture

        // 결과 확인용. 저장하지 않은 채 Canvas 를 카메라 모드로 바꿔 RenderTexture 에 그린다.
        // Scale With Screen Size(높이 기준)는 scaleFactor = 높이 / 1080 과 같으므로 그 값으로 고정해 해상도별로 찍는다.
        private static void Capture()
        {
            string directory = GetArg("-captureDir") ?? Path.Combine(Application.dataPath, "../Temp/TitleCapture");
            Directory.CreateDirectory(directory);

            var shots = new List<(string name, int width, int height, string error, bool connecting)>
            {
                ("1920x1080", 1920, 1080, null, false),
                ("1920x1080_flat", 1920, 1080, null, false),
                ("1920x1080_hover", 1920, 1080, null, false),
                ("1920x1080_pressed", 1920, 1080, null, false),
                ("1920x1080_error", 1920, 1080, "사용할 수 없는 문자가 포함되어 있습니다.", true),
                ("1600x900", 1600, 900, null, false),
                ("1280x720", 1280, 720, null, false),
                ("2560x1080_21x9", 2560, 1080, null, false),
                ("2400x1080_phone", 2400, 1080, null, false),
            };

            foreach (var shot in shots)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Canvas canvas = Object.FindObjectOfType<Canvas>();
                Camera camera = new GameObject("CaptureCamera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Night;
                camera.orthographic = true;

                RenderTexture target = new(shot.width, shot.height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = shot.height / 1080f;

                if (shot.name.Contains("_flat"))
                {
                    // 색 비교용: 배경을 회색 단색으로 (아티팩트도 같은 조건으로 찍는다)
                    canvas.transform.Find("BG_Illust").gameObject.SetActive(false);
                    canvas.transform.Find("BG_Overlay").gameObject.SetActive(false);
                    camera.backgroundColor = new Color32(0x80, 0x80, 0x80, 0xFF);
                }
                if (shot.error != null)
                    canvas.transform.Find("Content/FormGroup/ErrorText").GetComponent<TMP_Text>().text = shot.error;
                if (shot.connecting)
                    canvas.transform.Find("Content/FormGroup/ConnectButton/Body/Label").GetComponent<TMP_Text>().text = "접속 중…";

                for (int i = 0; i < 3; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
                    foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
                        text.ForceMeshUpdate();
                }

                if (shot.name.EndsWith("_hover") || shot.name.EndsWith("_pressed"))
                    SimulatePointer(canvas.transform.Find("Content/FormGroup/ConnectButton").GetComponent<PressableButton>(), shot.name.EndsWith("_pressed"));

                // batchmode 의 화면은 640×480 이라 TMP 셰이더가 RenderTexture 크기가 아닌 이 값으로 픽셀 크기를 계산한다
                // (외곽선이 흐리고 옅게 나옴). 캡처 때만 머티리얼 인스턴스의 _ScaleX/Y 로 보정한다. 실제 게임과는 무관.
                foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    Material instance = text.fontMaterial;
                    instance.SetFloat(ShaderUtilities.ID_ScaleX, (float)shot.width / Screen.width);
                    instance.SetFloat(ShaderUtilities.ID_ScaleY, (float)shot.height / Screen.height);
                }

                camera.Render();
                RenderTexture.active = target;
                Texture2D image = new(shot.width, shot.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, shot.width, shot.height), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(directory, shot.name + ".png"), image.EncodeToPNG());

                Object.DestroyImmediate(image);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }

            // 캡처용 변경은 저장하지 않는다.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[TitleSceneBuilder] 캡처 완료: {directory}");
        }

        // 에디터 모드에선 Awake 가 돌지 않으므로 직접 호출한 뒤 포인터 이벤트를 보낸다 (S5 연출 확인용).
        private static void SimulatePointer(PressableButton button, bool pressed)
        {
            typeof(PressableButton).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(button, null);
            var pointer = new UnityEngine.EventSystems.PointerEventData(null) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            button.OnPointerEnter(pointer);
            if (pressed)
                button.OnPointerDown(pointer);
        }

        private static string GetArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return null;
        }

        #endregion

        #region Helpers

        private static GameObject NewRoot(string name, float width, float height)
        {
            GameObject root = new(name, typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = height;
            layout.preferredHeight = height;
            return root;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color, float pixelsPerUnitMultiplier)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            ApplySprite(image, sprite, color, pixelsPerUnitMultiplier);
            image.raycastTarget = false;
            return image;
        }

        private static void ApplySprite(Image image, Sprite sprite, Color color, float pixelsPerUnitMultiplier)
        {
            image.sprite = sprite;
            image.color = color;
            bool sliced = sprite != null && sprite.border != Vector4.zero;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, string content, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = content;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // TMP 의 Middle 정렬은 폰트 상승·하강선 기준이라, 박스 높이를 CSS 줄 높이로 맞추면 기준선 위치가 CSS 와 같아진다.
        private static void FixHeight(TMP_Text text, float height)
        {
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        // CSS line-height: normal = 폰트 상승 + 하강 (이 폰트는 줄 간격 0)
        private static float NormalLineHeight(TMP_FontAsset font, float size) =>
            (font.faceInfo.ascentLine - font.faceInfo.descentLine) / font.faceInfo.pointSize * size;

        private static VerticalLayoutGroup AddVertical(GameObject go, float spacing, TextAnchor alignment)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        // left, bottom, right, top 은 부모 가장자리에서 안쪽으로 들어간 거리
        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // 그림자 스프라이트는 박스보다 사방 40 크게 그려야 박스 윤곽과 맞는다. offsetY 는 아래 방향 px.
        private static void ShadowRect(RectTransform rect, float offsetY)
        {
            Stretch(rect, -ShadowSpriteMargin, -ShadowSpriteMargin, -ShadowSpriteMargin, -ShadowSpriteMargin);
            rect.anchoredPosition = new Vector2(0f, -offsetY);
        }

        private static void Corner(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent, string name)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance;
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            SerializedObject serialized = new(target);
            foreach ((string field, Object value) in refs)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[TitleSceneBuilder] 필드 없음: {target.GetType().Name}.{field}");
                    continue;
                }
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(Object target, string field, Color value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(field).colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite Sprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");
            if (sprite == null)
                Debug.LogError($"[TitleSceneBuilder] 스프라이트 없음: {name}");
            return sprite;
        }

        private static TMP_FontAsset Font(string name)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + name + ".asset");
            if (font == null)
                Debug.LogError($"[TitleSceneBuilder] 폰트 없음: {name}");
            return font;
        }

        private static Material Preset(string suffix)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>($"{PresetDir}{BoldFontName} - {suffix}.mat");
            if (material == null)
                Debug.LogError($"[TitleSceneBuilder] 프리셋 없음: {suffix}");
            return material;
        }

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

        private static Color Rgba(int r, int g, int b, float a) => new Color32((byte)r, (byte)g, (byte)b, (byte)Mathf.RoundToInt(a * 255f));

        #endregion
    }
}
