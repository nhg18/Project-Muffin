using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// [개발용] 로비 개편 2단계 — LobbyScene 의 Canvas 아래를 docs/systems/14-lobby-ui.md 7-1 배치대로 생성하고 PNG 로 캡처한다.
    /// 디자인 없음: 기본 UI 스프라이트 · 기본 글자만 쓴다. 색 · 스프라이트는 디자인이 나오면 바꾼다 (14-lobby-ui 8절).
    /// Build 는 Canvas 아래를 통째로 다시 만든다 — 에디터에서 손으로 고친 내용은 사라지므로 메뉴로 노출하지 않는다.
    /// 에디터가 열려 있지 않은 프로젝트에서 batchmode 로만 실행한다:
    ///   unity run &lt;프로젝트&gt; -- -executeMethod Chapchu.EditorTools.LobbySceneBuilder.BuildAndCapture -captureDir &lt;폴더&gt;
    ///   unity run &lt;프로젝트&gt; -- -executeMethod Chapchu.EditorTools.LobbySceneBuilder.CaptureOnly -captureDir &lt;폴더&gt;
    /// </summary>
    public static class LobbySceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string FontDir = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
        private const string FontName = "Hakgyoansim Dunggeunmiso OTF R SDF";

        // 14-lobby-ui 4-2 · 7-1 수치 (1080 높이 기준 캔버스 좌표)
        private const float NicknameFontSize = 48f;
        private const float NicknameMaxWidth = 800f;
        private const float NicknameMargin = 48f;
        private const float ButtonWidth = 480f;
        private const float ButtonHeight = 200f;
        private const float ButtonFontSize = 56f;
        private const float ButtonSpacing = 60f;

        // 임시 색 (디자인 미정). 카메라 배경은 1단계에서 #2E2440 으로 맞춰 둠.
        private static readonly Color Night = new Color32(0x2E, 0x24, 0x40, 0xFF);
        private static readonly Color ButtonFill = new Color32(0xD0, 0xD0, 0xD0, 0xFF);
        private static readonly Color ButtonLabel = new Color32(0x2E, 0x24, 0x40, 0xFF);

        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[LobbySceneBuilder] LobbyScene 에 Canvas 가 없다. 1단계(캔버스 초기화)가 먼저다.");
                return;
            }

            Transform canvasRoot = canvas.transform;
            for (int i = canvasRoot.childCount - 1; i >= 0; i--)
            {
                Debug.Log($"[LobbySceneBuilder] 삭제: {canvasRoot.GetChild(i).name}");
                Object.DestroyImmediate(canvasRoot.GetChild(i).gameObject);
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + FontName + ".asset");
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // 화면 전체. 3단계에서 LobbyPanel 컴포넌트를 붙일 자리.
            RectTransform panel = NewRect("LobbyPanel", canvasRoot);
            Stretch(panel);

            // 닉네임 — 좌상단 앵커, 왼쪽 48 · 위 48, 최대 폭 800, 한 줄 말줄임
            TextMeshProUGUI nickname = NewText("NicknameText", panel, font, NicknameFontSize, Color.white, "Nickname", TextAlignmentOptions.Left);
            nickname.enableWordWrapping = false;
            nickname.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform nicknameRect = nickname.rectTransform;
            nicknameRect.anchorMin = nicknameRect.anchorMax = nicknameRect.pivot = new Vector2(0f, 1f);
            nicknameRect.anchoredPosition = new Vector2(NicknameMargin, -NicknameMargin);
            nicknameRect.sizeDelta = new Vector2(NicknameMaxWidth, NormalLineHeight(font, NicknameFontSize));

            // 메인 버튼 줄 — 정중앙 앵커, 3 × (480 × 200) + 간격 60 = 1560 × 200
            RectTransform row = NewRect("MainButtons", panel);
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

            // 이름은 LobbyPanel 의 필드명과 맞춘다 (3단계 연결용)
            NewButton("RandomMatchButton", row, uiSprite, font, "랜덤 매칭");
            NewButton("CreateRoomButton", row, uiSprite, font, "방 만들기");
            NewButton("JoinRoomButton", row, uiSprite, font, "방 참가");

            // 버튼의 RectTransform 값은 HorizontalLayoutGroup 이 구동한다. batchmode 에서는 캔버스 크기가 없어
            // 0 으로 저장되지만, 에디터 · 런타임에서 열면 즉시 480 × 200 으로 다시 계산된다.
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

        private static Button NewButton(string name, Transform parent, Sprite sprite, TMP_FontAsset font, string label)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = ButtonFill;

            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = ButtonWidth;
            element.preferredHeight = ButtonHeight;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            TextMeshProUGUI text = NewText("Label", rect, font, ButtonFontSize, ButtonLabel, label, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
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
        // Scale With Screen Size(높이 기준)는 scaleFactor = 높이 / 1080 과 같으므로 그 값으로 고정해 해상도별로 찍는다.
        private static void Capture()
        {
            string directory = GetArg("-captureDir") ?? Path.Combine(Application.dataPath, "../Temp/LobbyCapture");
            Directory.CreateDirectory(directory);

            var shots = new List<(string name, int width, int height)>
            {
                ("1920x1080", 1920, 1080),
                ("2340x1080_phone", 2340, 1080),
                ("2400x1080_phone", 2400, 1080),
                ("1280x720", 1280, 720),
                ("2048x1536_ipad", 2048, 1536),
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

                // batchmode 의 화면은 640×480 이라 TMP 셰이더가 RenderTexture 크기가 아닌 이 값으로 픽셀 크기를 계산한다.
                // 캡처 때만 머티리얼 인스턴스의 _ScaleX/Y 로 보정한다. 실제 게임과는 무관.
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

            // 캡처용으로 바꾼 Canvas 설정을 저장하지 않도록 씬을 다시 연다.
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
