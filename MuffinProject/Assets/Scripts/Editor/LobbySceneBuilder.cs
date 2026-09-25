using System.Collections.Generic;
using System.IO;
using Chapchu.UI.Lobby;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// [개발용 — 씬 생성이 끝나면 삭제] LobbyScene 을 TitleScene 과 같은 구조로 다시 만들고 PNG 로 캡처한다.
    /// 수치 기준: docs/systems/14-lobby-ui.md. 구조 기준: TitleScene (LobbyCanvas 루트에 LobbyView + LobbyPresenter, PillButton 프리팹, 같은 배경).
    /// Build 는 씬의 UI 를 통째로 다시 만든다 — 손으로 고친 내용은 사라지므로 메뉴로 노출하지 않는다.
    /// 에디터가 열려 있지 않은 프로젝트에서 batchmode 로만 실행한다:
    ///   unity run &lt;프로젝트&gt; -- -executeMethod Chapchu.EditorTools.LobbySceneBuilder.BuildAndCapture -captureDir &lt;폴더&gt;
    /// </summary>
    public static class LobbySceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string PillButtonPath = "Assets/Prefab/UI/PillButton.prefab";
        private const string BackgroundPath = "Assets/Sprites/TitleBackground.png";
        private const string OverlayPath = "Assets/Sprites/UI/title_overlay_vertical.png";
        private const string FontDir = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
        private const string BoldFontName = "Hakgyoansim Dunggeunmiso OTF B SDF";

        // 14-lobby-ui 4-2 · 7-1 (1080 높이 기준 캔버스 좌표)
        private const float NicknameFontSize = 48f;
        private const float NicknameMaxWidth = 800f;
        private const float NicknameMargin = 48f;
        private const float ButtonWidth = 480f;
        private const float ButtonHeight = 200f;
        private const float ButtonFontSize = 56f;
        private const float ButtonSpacing = 60f;

        private static readonly Color Night = new Color32(0x2E, 0x24, 0x40, 0xFF);
        private static readonly Color Cream = new Color32(0xFF, 0xFD, 0xF8, 0xFF);

        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 카메라 · EventSystem 만 남기고 기존 UI 를 전부 지운다 (TitleSceneBuilder 와 동일).
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.GetComponent<Camera>() != null || rootObject.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                    continue;
                Debug.Log($"[LobbySceneBuilder] 삭제: {rootObject.name}");
                Object.DestroyImmediate(rootObject);
            }

            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Night;
            }

            // 캔버스 — TitleCanvas 와 같은 설정 (14-lobby-ui 4-1)
            GameObject canvasObject = new("LobbyCanvas", typeof(RectTransform));
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

            // 배경 — 타이틀과 같은 일러스트(cover) + 세로 오버레이 (14-lobby-ui 8절)
            Image illust = NewImage("BG_Illust", canvasRoot, AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath), Color.white);
            AspectRatioFitter fitter = illust.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = illust.sprite != null ? illust.sprite.rect.width / illust.sprite.rect.height : 16f / 9f;

            Image overlay = NewImage("BG_Overlay", canvasRoot, AssetDatabase.LoadAssetAtPath<Sprite>(OverlayPath), Color.white);
            Stretch(overlay.rectTransform);

            // 닉네임 — 좌상단 앵커, 왼쪽 48 · 위 48, 최대 폭 800, 한 줄 말줄임
            TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + BoldFontName + ".asset");
            TextMeshProUGUI nickname = NewText("NicknameText", canvasRoot, bold, NicknameFontSize, Cream, "Nickname", TextAlignmentOptions.Left);
            nickname.enableWordWrapping = false;
            nickname.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform nicknameRect = nickname.rectTransform;
            nicknameRect.anchorMin = nicknameRect.anchorMax = nicknameRect.pivot = new Vector2(0f, 1f);
            nicknameRect.anchoredPosition = new Vector2(NicknameMargin, -NicknameMargin);
            nicknameRect.sizeDelta = new Vector2(NicknameMaxWidth, NormalLineHeight(bold, NicknameFontSize));

            // 메인 버튼 줄 — 정중앙, 3 × (480 × 200) + 간격 60 = 1560 × 200. 버튼은 타이틀의 PillButton 프리팹
            RectTransform row = NewRect("MainButtons", canvasRoot);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(ButtonWidth * 3f + ButtonSpacing * 2f, ButtonHeight);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = ButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            GameObject pillButton = AssetDatabase.LoadAssetAtPath<GameObject>(PillButtonPath);
            Button randomMatch = NewPillButton("RandomMatchButton", row, pillButton, "랜덤 매칭");
            Button createRoom = NewPillButton("CreateRoomButton", row, pillButton, "방 만들기");
            Button joinRoom = NewPillButton("JoinRoomButton", row, pillButton, "방 참가");

            // 뷰 · 프레젠터 — TitleCanvas 와 같이 캔버스 루트에 부착
            LobbyView view = canvasObject.AddComponent<LobbyView>();
            SetRefs(view,
                ("nicknameText", nickname),
                ("randomMatchButton", randomMatch),
                ("createRoomButton", createRoom),
                ("joinRoomButton", joinRoom));
            canvasObject.AddComponent<LobbyPresenter>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[LobbySceneBuilder] Build 완료");
        }

        public static void BuildAndCapture()
        {
            Build();
            Capture();
        }

        public static void CaptureOnly() => Capture();

        #region Helpers

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, string text, TextAlignmentOptions alignment)
        {
            RectTransform rect = NewRect(name, parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        // PillButton 프리팹 인스턴스(프리팹 연결 유지)를 480 × 200 · 글자 56 으로 키운다.
        private static Button NewPillButton(string name, Transform parent, GameObject prefab, string label)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            RectTransform rect = (RectTransform)instance.transform;
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            LayoutElement element = instance.AddComponent<LayoutElement>();
            element.preferredWidth = ButtonWidth;
            element.preferredHeight = ButtonHeight;

            TMP_Text text = instance.transform.Find("Body/Label").GetComponent<TMP_Text>();
            text.text = label;
            text.fontSize = ButtonFontSize;
            return instance.GetComponent<Button>();
        }

        // [SerializeField] private 필드에 참조를 넣는다. 필드명이 틀리면 로그로 알린다.
        private static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            SerializedObject serialized = new(target);
            foreach ((string field, Object value) in refs)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[LobbySceneBuilder] {target.GetType().Name} 에 필드 {field} 가 없다");
                    continue;
                }
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 폰트 상승 + 하강 (TMP 의 한 줄 높이)
        private static float NormalLineHeight(TMP_FontAsset font, float size)
        {
            UnityEngine.TextCore.FaceInfo face = font.faceInfo;
            return (face.ascentLine - face.descentLine) * size / face.pointSize;
        }

        #endregion

        #region Capture

        // 결과 확인용. 저장하지 않은 채 Canvas 를 카메라 모드로 바꿔 RenderTexture 에 그린다.
        private static void Capture()
        {
            string directory = GetArg("-captureDir") ?? Path.Combine(Application.dataPath, "../Temp/LobbyCapture");
            Directory.CreateDirectory(directory);

            var shots = new List<(string name, int width, int height)>
            {
                ("1920x1080", 1920, 1080),
                ("2400x1080_phone", 2400, 1080),
                ("1280x720", 1280, 720),
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

                for (int i = 0; i < 3; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
                    foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
                        text.ForceMeshUpdate();
                }

                // batchmode 의 화면은 640×480 이라 TMP 셰이더가 픽셀 크기를 잘못 계산한다. 캡처 때만 보정.
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
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[LobbySceneBuilder] 캡처 완료: {directory}");
        }

        private static string GetArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name)
                    return args[i + 1];
            return null;
        }

        #endregion
    }
}
