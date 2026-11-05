using System.IO;
using Chapchu.DebugTools;
using Chapchu.Game;
using Chapchu.UI.Fx;
using Chapchu.UI.Fx.Common;
using Chapchu.UI.Fx.HellBlade;
using Chapchu.UI.GameScreen;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 카드 연출 테스트 씬(CardFxTestScene) · 이펙트 시험장(CardFxLabScene) · 공통 이펙트 프리팹(Prefab/UI/CardFx/Common) · 카드별 연출 프리팹을 만들고 고친다 (메뉴 Chapchu/Card Fx).
    /// 씬은 열어 둔 채로 계속 쓴다 — 열려 있으면 그 자리에서 없는 것만 만들고 연결을 맞춘 뒤 저장한다. 있는 오브젝트는 건드리지 않는다.
    /// 프리팹은 이미 있으면 그대로 쓴다 (사람이 고친 값일 수 있다). 카드 연출을 처음부터 다시 만들려면 Rebuild 메뉴.
    /// </summary>
    public static class CardFxTestBuilder
    {
        private const string ScenePath = "Assets/Scenes/CardFxTestScene.unity";
        private const string CanvasName = "CardFxTestCanvas";
        private const string LabScenePath = "Assets/Scenes/CardFxLabScene.unity";
        private const string LabCanvasName = "CardFxLabCanvas";
        private const string FxPrefabFolder = "Assets/Prefab/UI/CardFx";
        private const string CommonFxFolder = FxPrefabFolder + "/Common";
        // 카드별 연출 프리팹은 재생할 때만 불러오므로 Resources 아래 (CardFxLoader)
        private const string CardFxResourceFolder = "Assets/Resources/CardFx";
        private const string HellBladePrefabPath = CardFxResourceFolder + "/" + nameof(HellBladeFx) + ".prefab";
        private const string HellBladeName = "지옥참마도";
        private const string SpriteFolder = "Assets/Sprites/CardFx";
        private const string HellBladeSprites = SpriteFolder + "/HellBlade/";
        private const string CommonSprites = SpriteFolder + "/Common/";
        private const string HandCardPrefabFolder = "Assets/Prefab/UI/GameScreen";
        private const string BoldFontAssetPath = "Assets/Fonts/GlaMuri/Galmuri11-Bold SDF.asset";
        private const string BackgroundPath = "Assets/Sprites/UI/Background/bg_ingame.png";
        private const string RoundSpritePath = "Assets/Sprites/UI/Common/ui_round64.png";
        private const string HellBladeArtPath = "Assets/Sprites/Cards/Images/A03.jpg";
        private const string FxShaderName = "Chapchu/UI Fx Blend";
        private const string MaterialFolder = "Assets/Materials/CardFx";
        private const string AdditivePath = MaterialFolder + "/FxAdditive.mat";
        private const string MultiplyPath = MaterialFolder + "/FxMultiply.mat";
        private const string LabFloorTexturePath = MaterialFolder + "/Bench/fx_bench_checker.png";
        private const string LabFloorMaterialPath = MaterialFolder + "/Bench/FxLabFloor.mat";

        // 시안 「찹츄 카드 이펙트」 지옥참마도 장면 좌표 (정사각형 장면의 왼쪽 위 기준 %)
        private static readonly Rect DrawFrameBox = new Rect(0f, 0f, 45f, 76f);
        private static readonly Rect EyeBox = new Rect(56f, 24f, 19.5f, 17.5f);
        private static readonly Vector2 EyeOpenPivot = new Vector2(43.387f, 52.932f);
        private static readonly Vector2 EyeCenter = new Vector2(64.46f, 33.263f);
        private static readonly Vector2 SwordPivot = new Vector2(31.921f, 48.627f);
        private const float SwordAngle = 11.9466f;
        private static readonly Vector2 SwordJoint = new Vector2(31.825f, 45.25f);
        private static readonly Rect SwordGlowBox = new Rect(25f, 34.75f, 13.65f, 10.5f);

        // 컷인 안에서 흔들리는 판을 화면보다 이만큼씩 더 크게 — 흔들려도(최대 24) 가장자리가 비지 않게
        private const float ShakeOverscan = 120f;
        // 시안 px → 이 화면 단위 (시안 카드 110px · 이름표 170px 기준)
        private const float PixelScale = 1.5f;

        private static readonly Color Ink = new Color32(0x2b, 0x28, 0x56, 0xff);
        private static readonly Color Red = new Color32(0xff, 0x2a, 0x3a, 0xff);
        // 번쩍임 그림은 투명하게 두고 이펙트가 켠다 — 불투명하면 쓰지 않는 프리팹에서 화면을 덮는다
        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        private static TMP_FontAsset _font;

        /// <summary>테스트 씬이 열려 있으면 그 자리에서 맞추고 저장, 없으면 새로 만든다.</summary>
        [MenuItem("Chapchu/Card Fx/Build Or Update Test Scene")]
        public static void BuildOrUpdateTestScene()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontAssetPath);
            HellBladeFx hellBlade = EnsureHellBladePrefab();

            Scene active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
            {
                ApplyBench(active, hellBlade);
                EditorSceneManager.SaveScene(active);
                Debug.Log($"[{nameof(CardFxTestBuilder)}] 열린 {ScenePath} 을 맞추고 저장했다.");
                return;
            }

            if (File.Exists(ScenePath))
            {
                Debug.LogError($"[{nameof(CardFxTestBuilder)}] {ScenePath} 가 이미 있다 — 그 씬을 열고 다시 실행할 것.");
                return;
            }

            Scene previous = active;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            ApplyBench(scene, hellBlade);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                Debug.LogError($"[{nameof(CardFxTestBuilder)}] {ScenePath} 저장 실패.");
            if (previous.IsValid())
                SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[{nameof(CardFxTestBuilder)}] {ScenePath} 생성 완료.");
        }

        [MenuItem("Chapchu/Card Fx/Build Hell Blade Prefab")]
        public static void BuildHellBladePrefab()
        {
            EnsureHellBladePrefab();
        }

        /// <summary>지옥참마도 프리팹 안을 처음부터 다시 만든다 (사람이 고친 값은 사라진다). 루트는 그대로라 씬의 연결은 유지된다.</summary>
        [MenuItem("Chapchu/Card Fx/Rebuild Hell Blade Prefab")]
        public static void RebuildHellBladePrefab()
        {
            EnsureHellBladePrefab(true);
        }

        /// <summary>공통 이펙트 프리팹 중 없는 것만 만든다.</summary>
        [MenuItem("Chapchu/Card Fx/Build Common Fx Prefabs")]
        public static void BuildCommonFxPrefabs()
        {
            EnsureCommonFxPrefabs();
        }

        /// <summary>이펙트 시험장(CardFxLabScene)이 열려 있으면 그 자리에서 맞추고 저장, 없으면 새로 만든다.</summary>
        [MenuItem("Chapchu/Card Fx/Build Or Update Fx Lab Scene")]
        public static void BuildOrUpdateLabScene()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontAssetPath);
            HellBladeFx hellBlade = EnsureHellBladePrefab();

            Scene active = SceneManager.GetActiveScene();
            if (active.path == LabScenePath)
            {
                ApplyLab(active, hellBlade);
                EditorSceneManager.SaveScene(active);
                Debug.Log($"[{nameof(CardFxTestBuilder)}] 열린 {LabScenePath} 을 맞추고 저장했다.");
                return;
            }

            if (File.Exists(LabScenePath))
            {
                Debug.LogError($"[{nameof(CardFxTestBuilder)}] {LabScenePath} 가 이미 있다 — 그 씬을 열고 다시 실행할 것.");
                return;
            }

            // 열린 씬은 건드리지 않는다 — 새 씬을 옆에 만들어 저장하고 닫는다
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            ApplyLab(scene, hellBlade);
            if (!EditorSceneManager.SaveScene(scene, LabScenePath))
                Debug.LogError($"[{nameof(CardFxTestBuilder)}] {LabScenePath} 저장 실패.");
            if (active.IsValid())
                SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[{nameof(CardFxTestBuilder)}] {LabScenePath} 생성 완료.");
        }

        #region 테스트 씬

        // 없는 것만 만들고, 참조는 매번 다시 연결한다
        private static void ApplyBench(Scene scene, HellBladeFx hellBlade)
        {
            if (FindRoot(scene, "Main Camera") == null)
            {
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.tag = "MainCamera";
            }
            if (FindRoot(scene, "EventSystem") == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Sprite round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSpritePath);
            GameObject canvasGo = FindRoot(scene, CanvasName);
            if (canvasGo == null)
            {
                canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.layer = LayerMask.NameToLayer("UI");
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }
            var canvas = (RectTransform)canvasGo.transform;

            RectTransform stage = Child(canvas, "Stage");
            if (stage == null)
                stage = NewStretch("Stage", canvas);
            if (Child(stage, "Background") == null)
            {
                Image background = NewImage("Background", stage, Color.white, AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath));
                Stretch(background.rectTransform, -40f, -40f, -40f, -40f);
                background.transform.SetAsFirstSibling();
            }

            // 시안 vw-plate: 나 = 왼쪽 6% · 아래 8%, 상대 = 오른쪽 6% · 위 8%. 시안 판은 위쪽 막대(58px) 아래에서 시작해서
            // 상대 이름표를 그만큼 내린다 — 안 내리면 이름표 위로 튀는 터짐 숫자가 화면 밖으로 잘린다.
            // 예전 모양(피해 숫자 칸이 있던 것)이면 다시 만든다
            FxDummySeat caster = EnsureSeat(stage, "Caster", "나", new Vector2(0.06f, 0.08f), new Vector2(0f, 0f), round);
            FxDummySeat target = EnsureSeat(stage, "Target", "초코", new Vector2(0.94f, 0.92f), new Vector2(1f, 1f), round);
            if (target.transform.parent == stage && ((RectTransform)target.transform).anchoredPosition == Vector2.zero)
                ((RectTransform)target.transform).anchoredPosition = new Vector2(0f, -58f * PixelScale);

            RectTransform playSpot = Child(stage, "PlaySpot");
            if (playSpot == null)
            {
                playSpot = NewRect("PlaySpot", stage);
                Anchor(playSpot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            }

            // 시안 vw-pile: 오른쪽 7% · 아래 9%, 70 × 104 칸
            RectTransform pile = Child(stage, "DiscardPile");
            if (pile == null)
            {
                Image pileImage = NewImage("DiscardPile", stage, new Color(1f, 1f, 1f, 0.35f), round);
                pileImage.type = Image.Type.Sliced;
                pile = pileImage.rectTransform;
                pile.anchorMin = pile.anchorMax = new Vector2(0.93f, 0.09f);
                pile.pivot = new Vector2(1f, 0f);
                pile.sizeDelta = new Vector2(70f, 104f) * PixelScale;
                TMP_Text pileLabel = NewText("Label", pile, "버림 더미", 11f * PixelScale, new Color(Ink.r, Ink.g, Ink.b, 0.5f));
                Stretch(pileLabel.rectTransform);
            }

            RectTransform hand = Child(stage, "HandArea");
            if (hand == null)
            {
                hand = NewRect("HandArea", stage);
                Anchor(hand, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1600f, 336f));
                hand.localScale = Vector3.one * 0.73f;
                HorizontalLayoutGroup layout = hand.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 28f;
                layout.childAlignment = TextAnchor.LowerCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            RectTransform captionRect = Child(canvas, "Caption");
            TMP_Text caption = captionRect != null ? captionRect.GetComponent<TMP_Text>() : null;
            if (caption == null)
            {
                caption = NewText("Caption", canvas, "", 34f, Color.white);
                Anchor(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1200f, 60f));
                caption.outlineWidth = 0.25f;
                caption.outlineColor = Ink;
            }

            Button replay = FindOrCreateButton(canvas, "ReplayButton", "다시 (Space)", new Vector2(-280f, 20f), round);
            Button reset = FindOrCreateButton(canvas, "ResetButton", "체력 초기화 (R)", new Vector2(-20f, 20f), round);

            // 연출 레이어는 맨 위 (판 밖)
            RectTransform fxLayerRect = Child(canvas, "FxLayer");
            if (fxLayerRect == null)
                fxLayerRect = NewStretch("FxLayer", canvas);
            fxLayerRect.SetAsLastSibling();
            // 따로 Canvas — 연출이 매 프레임 움직여도 손패 · 이름표 쪽 Canvas 는 다시 계산되지 않게
            GetOrAdd<Canvas>(fxLayerRect.gameObject);
            FxLayer fxLayer = GetOrAdd<FxLayer>(fxLayerRect.gameObject);
            Wire(fxLayer,
                ("stage", stage), ("additive", AssetDatabase.LoadAssetAtPath<Material>(AdditivePath)), ("star", Common("fx_star")),
                ("dot", Common("fx_dot")), ("glow", Common("fx_glow_soft")), ("streak", Common("fx_streak")),
                ("starburst", Common("fx_starburst")), ("boomFont", _font));

            CardFxTestBench bench = GetOrAdd<CardFxTestBench>(canvasGo);
            Wire(bench,
                ("actionCardPrefab", LoadHandCard("HandCard_Action")), ("counterCardPrefab", LoadHandCard("HandCard_Counter")),
                ("trapCardPrefab", LoadHandCard("HandCard_Trap")), ("handArea", hand), ("playSpot", playSpot),
                ("discardPile", pile), ("fxLayer", fxLayer), ("caster", caster), ("target", target),
                ("captionText", caption), ("replayButton", replay), ("resetButton", reset), ("playedGlow", Common("fx_card_glow")));
            EnsureHellBladeEntry(bench, hellBlade);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        // 이펙트 시험장 — 원근으로 깔린 어두운 체크 바닥(3D) 위에 대상 자리 하나, 그 위에 UI(판 · 연출 레이어 · 설명).
        // 없는 것만 만들고, 참조는 매번 다시 연결한다
        private static void ApplyLab(Scene scene, HellBladeFx hellBlade)
        {
            Color backdrop = new Color32(0x0b, 0x08, 0x12, 0xff);
            if (FindRoot(scene, "Main Camera") == null)
            {
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = backdrop;
                camera.fieldOfView = 40f;
                camera.farClipPlane = 80f;
                camera.transform.SetPositionAndRotation(new Vector3(0f, 2.4f, -7f), Quaternion.Euler(16f, 0f, 0f));
                camera.tag = "MainCamera";
            }

            // 멀어질수록 어둠에 묻히게
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.07f;
            RenderSettings.fogColor = backdrop;

            if (FindRoot(scene, "Floor") == null)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floor.name = "Floor";
                Object.DestroyImmediate(floor.GetComponent<Collider>());
                floor.transform.SetPositionAndRotation(new Vector3(0f, 0f, 8f), Quaternion.Euler(90f, 0f, 0f));
                floor.transform.localScale = new Vector3(60f, 60f, 1f);
                floor.GetComponent<MeshRenderer>().sharedMaterial = EnsureLabFloorMaterial();
            }

            GameObject canvasGo = FindRoot(scene, LabCanvasName);
            if (canvasGo == null)
            {
                canvasGo = new GameObject(LabCanvasName, typeof(Canvas), typeof(CanvasScaler));
                canvasGo.layer = LayerMask.NameToLayer("UI");
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }
            var canvas = (RectTransform)canvasGo.transform;

            // 판 — 대상 자리 · 바닥 빛. 마무리 이펙트가 이 판을 흔든다
            RectTransform stage = Child(canvas, "Stage");
            if (stage == null)
                stage = NewStretch("Stage", canvas);

            if (Child(stage, "Spot") == null)
            {
                Image spot = NewImage("Spot", stage, new Color(0.55f, 0.3f, 1f, 0.22f), Common("fx_glow_soft"),
                    AssetDatabase.LoadAssetAtPath<Material>(AdditivePath));
                Anchor(spot.rectTransform, new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 380f));
            }

            // 대상 — 가운데가 이펙트가 터지는 점. 발밑 그림자와 이름표
            RectTransform target = Child(stage, "Target");
            if (target == null)
            {
                target = NewRect("Target", stage);
                Anchor(target, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
                Image shadow = NewImage("Shadow", target, new Color(0f, 0f, 0f, 0.6f), Common("fx_glow_soft"));
                Anchor(shadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 70f));
                TMP_Text label = NewText("Label", target, "대상", 26f, new Color(1f, 1f, 1f, 0.35f));
                Anchor(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(200f, 40f));
            }

            // 연출 레이어는 판 위 — 따로 Canvas 라 연출이 매 프레임 움직여도 판 쪽은 다시 계산되지 않는다
            RectTransform fxLayerRect = Child(canvas, "FxLayer");
            if (fxLayerRect == null)
                fxLayerRect = NewStretch("FxLayer", canvas);
            fxLayerRect.SetAsLastSibling();
            GetOrAdd<Canvas>(fxLayerRect.gameObject);
            FxLayer fxLayer = GetOrAdd<FxLayer>(fxLayerRect.gameObject);
            Wire(fxLayer,
                ("stage", stage), ("additive", AssetDatabase.LoadAssetAtPath<Material>(AdditivePath)), ("star", Common("fx_star")),
                ("dot", Common("fx_dot")), ("glow", Common("fx_glow_soft")), ("streak", Common("fx_streak")),
                ("starburst", Common("fx_starburst")), ("boomFont", _font));

            RectTransform captionRect = Child(canvas, "Caption");
            TMP_Text caption = captionRect != null ? captionRect.GetComponent<TMP_Text>() : null;
            if (caption == null)
            {
                caption = NewText("Caption", canvas, "", 30f, Color.white);
                Anchor(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(240f, 30f), new Vector2(1300f, 50f));
            }
            // 설명은 연출(컷인 위아래 검은 띠)에 가리지 않게 맨 위
            caption.transform.SetAsLastSibling();

            CardFxLab lab = GetOrAdd<CardFxLab>(canvasGo);
            Wire(lab, ("fxLayer", fxLayer), ("target", target), ("captionText", caption));
            EnsureLabCard(lab, HellBladeName, hellBlade);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        // 시험장 카드 목록에 그 카드 줄이 없으면 더하고, 연출 프리팹 연결을 맞춘다. 다른 줄은 건드리지 않는다
        private static void EnsureLabCard(CardFxLab lab, string cardName, CardFx fx)
        {
            var serialized = new SerializedObject(lab);
            SerializedProperty cards = serialized.FindProperty("cards");
            SerializedProperty entry = null;
            for (int i = 0; i < cards.arraySize; i++)
            {
                if (cards.GetArrayElementAtIndex(i).FindPropertyRelative("cardName").stringValue == cardName)
                    entry = cards.GetArrayElementAtIndex(i);
            }

            if (entry == null)
            {
                cards.arraySize++;
                entry = cards.GetArrayElementAtIndex(cards.arraySize - 1);
                entry.FindPropertyRelative("cardName").stringValue = cardName;
            }

            entry.FindPropertyRelative("fx").objectReferenceValue = fx;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 바닥 — 체크 텍스처를 반복해 까는 Unlit 머티리얼 (안개를 받는다)
        private static Material EnsureLabFloorMaterial()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(LabFloorTexturePath);
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(LabFloorMaterialPath);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Unlit/Texture"))
            {
                mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LabFloorTexturePath),
                mainTextureScale = new Vector2(30f, 30f),
            };
            AssetDatabase.CreateAsset(material, LabFloorMaterialPath);
            return material;
        }

        // 목록에 지옥참마도 줄이 없으면 더하고, 연출 프리팹 연결을 맞춘다. 다른 줄은 건드리지 않는다
        private static void EnsureHellBladeEntry(CardFxTestBench bench, HellBladeFx hellBlade)
        {
            var serialized = new SerializedObject(bench);
            SerializedProperty entries = serialized.FindProperty("entries");
            SerializedProperty entry = null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("cardName").stringValue == HellBladeName)
                    entry = entries.GetArrayElementAtIndex(i);
            }

            if (entry == null)
            {
                entries.arraySize++;
                entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("cardName").stringValue = HellBladeName;
                entry.FindPropertyRelative("type").enumValueIndex = (int)CardType.Action;
                entry.FindPropertyRelative("art").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HellBladeArtPath);
                entry.FindPropertyRelative("description").stringValue = "대상 40 피해";
                entry.FindPropertyRelative("damage").intValue = 40;
            }

            entry.FindPropertyRelative("fxName").stringValue = hellBlade.GetType().Name;
            entry.FindPropertyRelative("bigHit").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static FxDummySeat EnsureSeat(RectTransform stage, string name, string label, Vector2 anchor, Vector2 pivot, Sprite round)
        {
            RectTransform existing = Child(stage, name);
            if (existing != null && Child(existing, "DamagePop") == null && existing.TryGetComponent(out FxDummySeat current))
                return current;

            int index = existing != null ? existing.GetSiblingIndex() : stage.childCount;
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            FxDummySeat seat = BuildSeat(stage, name, label, anchor, pivot, round);
            seat.transform.SetSiblingIndex(index);
            return seat;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }
            return null;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child != null ? (RectTransform)child : null;
        }

        // 에디터의 GetComponent 는 없을 때 "가짜 null" 을 돌려줘서 ?? 로 고를 수 없다
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
        }

        private static Button FindOrCreateButton(RectTransform parent, string name, string label, Vector2 position, Sprite round)
        {
            RectTransform existing = Child(parent, name);
            if (existing != null && existing.TryGetComponent(out Button button))
                return button;
            return NewButton(name, parent, label, position, round);
        }

        private static RectTransform NewStretch(string name, Transform parent)
        {
            RectTransform rect = NewRect(name, parent);
            Stretch(rect);
            return rect;
        }

        private static FxDummySeat BuildSeat(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pivot, Sprite round)
        {
            Image panel = NewImage(name, parent, Color.white, round);
            panel.type = Image.Type.Sliced;
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = anchor;
            panel.rectTransform.pivot = pivot;
            panel.rectTransform.anchoredPosition = Vector2.zero;
            panel.rectTransform.sizeDelta = new Vector2(170f, 92f) * PixelScale;

            TMP_Text nameText = NewText("Name", panel.rectTransform, label, 20f * PixelScale, Ink);
            Anchor(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f * PixelScale), new Vector2(150f, 26f) * PixelScale);

            TMP_Text hpText = NewText("Hp", panel.rectTransform, "100", 12f * PixelScale, Ink);
            Anchor(hpText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f * PixelScale), new Vector2(150f, 18f) * PixelScale);

            Image hpBack = NewImage("HpBar", panel.rectTransform, new Color32(0xee, 0xee, 0xee, 0xff), round);
            hpBack.type = Image.Type.Sliced;
            Anchor(hpBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f * PixelScale), new Vector2(150f, 14f) * PixelScale);
            Image hpFill = NewImage("Fill", hpBack.rectTransform, new Color32(0x3d, 0xdc, 0x84, 0xff), round);
            Stretch(hpFill.rectTransform);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;

            FxDummySeat seat = panel.gameObject.AddComponent<FxDummySeat>();
            Wire(seat, ("hpFill", hpFill), ("hpText", hpText));
            return seat;
        }

        private static Button NewButton(string name, RectTransform parent, string label, Vector2 position, Sprite round)
        {
            Image image = NewImage(name, parent, Color.white, round, raycastTarget: true);
            image.type = Image.Type.Sliced;
            // 시안 vw-replay: 오른쪽 아래 (버림 더미 밑)
            Anchor(image.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), position, new Vector2(240f, 60f));
            Button button = image.gameObject.AddComponent<Button>();
            TMP_Text text = NewText("Label", image.rectTransform, label, 24f, Ink);
            Stretch(text.rectTransform);
            return button;
        }

        private static HandCardView LoadHandCard(string name)
        {
            var card = AssetDatabase.LoadAssetAtPath<HandCardView>($"{HandCardPrefabFolder}/{name}.prefab");
            if (card == null)
                Debug.LogError($"[{nameof(CardFxTestBuilder)}] 손패 카드 프리팹이 없다: {name} — Chapchu/Game Screen/Build UI 로 먼저 만들 것.");
            return card;
        }

        #endregion

        #region 공통 이펙트 프리팹

        // 공통 이펙트 프리팹 — 카드 연출이 중첩 프리팹으로 가져다 놓고 시각 · 값만 바꿔 쓴다. 이미 있으면 그대로 쓴다 (사람이 고친 값일 수 있다)
        private static readonly string[] CommonFxNames =
        {
            "Flash", "Shake", "Blackout", "FlareBurst", "GlowPop", "GleamSweep", "GlyphSlam", "SlashFlurry", "BigSlash", "TargetSlash", "TargetBoom",
        };

        private static Scene _preview;

        private static string CommonFxPath(string name) => $"{CommonFxFolder}/{name}.prefab";

        private static void EnsureCommonFxPrefabs()
        {
            ImportAsSprites(SpriteFolder);
            if (!AssetDatabase.IsValidFolder(FxPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefab/UI", "CardFx");
            if (!AssetDatabase.IsValidFolder(CommonFxFolder))
                AssetDatabase.CreateFolder(FxPrefabFolder, "Common");
            Material additive = EnsureMaterial(AdditivePath, BlendMode.SrcAlpha, BlendMode.One, false);

            foreach (string name in CommonFxNames)
            {
                if (AssetDatabase.LoadAssetAtPath<CardFxStep>(CommonFxPath(name)) != null)
                    continue;

                RectTransform root = BuildCommonFx(name, additive);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, CommonFxPath(name));
                Object.DestroyImmediate(root.gameObject);
                Debug.Log($"[{nameof(CardFxTestBuilder)}] {CommonFxPath(name)} 생성 완료.");
            }
            ClosePreview();
        }

        // 기본값은 지옥참마도에서 가장 많이 쓰는 값. 한 점 이펙트는 크기를 가진 가운데 앵커, 화면을 덮는 이펙트는 늘어난 앵커
        private static RectTransform BuildCommonFx(string name, Material additive)
        {
            RectTransform root = NewPrefabRoot(name);
            switch (name)
            {
                case "Flash":
                {
                    Overscan(root);
                    AddImage(root, Color.white);
                    Configure(root.gameObject.AddComponent<FlashFx>(), ("alpha", 0.5f), ("durationMs", 160f));
                    break;
                }
                case "Shake":
                    Configure(root.gameObject.AddComponent<ShakeFx>(), ("amount", 6f), ("durationMs", 160f));
                    break;
                case "Blackout":
                {
                    Overscan(root);
                    AddImage(root, Color.black);
                    Configure(root.gameObject.AddComponent<FadeFx>(), ("alpha", 1f), ("inMs", 40f));
                    break;
                }
                case "FlareBurst":
                {
                    // 시안 칼집 이음새: 고리 18 · 줄 빛 44×3 · 별 7 (장면 %) — 고리 칸 기준 비율
                    root.sizeDelta = new Vector2(240f, 240f);
                    Image ring = NewImage("Ring", root, Color.white, Common("fx_flare_ring"), additive);
                    Stretch(ring.rectTransform);
                    Image streak = NewImage("Streak", root, Color.white, Common("fx_flare_streak"), additive);
                    AnchorRange(streak.rectTransform, new Vector2(0.5f - 22f / 18f, 0.5f - 1.5f / 18f), new Vector2(0.5f + 22f / 18f, 0.5f + 1.5f / 18f));
                    Image spark = NewImage("Spark", root, Color.white, Common("fx_spark"), additive);
                    AnchorRange(spark.rectTransform, new Vector2(0.5f - 3.5f / 18f, 0.5f - 3.5f / 18f), new Vector2(0.5f + 3.5f / 18f, 0.5f + 3.5f / 18f));
                    Configure(root.gameObject.AddComponent<FlareBurstFx>(), ("ring", ring), ("streak", streak), ("spark", spark), ("durationMs", 520f));
                    break;
                }
                case "GlowPop":
                {
                    root.sizeDelta = new Vector2(86f, 86f);
                    NoInput(root.gameObject.AddComponent<CanvasGroup>());
                    Image halo = NewImage("Halo", root, new Color32(0xff, 0x28, 0x3c, 0xff), Common("fx_glow_soft"), additive);
                    AnchorRange(halo.rectTransform, new Vector2(-1.8f, -1.8f), new Vector2(2.8f, 2.8f));
                    Image flare = NewImage("Flare", root, new Color32(0xff, 0x80, 0x90, 0xff), Common("fx_flare_streak"), additive);
                    AnchorRange(flare.rectTransform, new Vector2(-12.5f, 0.2f), new Vector2(13.5f, 0.8f));
                    root.gameObject.AddComponent<GlowPopFx>();
                    break;
                }
                case "GleamSweep":
                {
                    root.sizeDelta = new Vector2(560f, 430f);
                    Image glow = AddImage(root, Red, Common("fx_glow_soft"), additive);
                    Image gleam = NewImage("Gleam", root, Color.white, Common("fx_glow_soft"), additive);
                    AnchorRange(gleam.rectTransform, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f));
                    Configure(root.gameObject.AddComponent<GleamSweepFx>(), ("glow", glow), ("gleam", gleam));
                    break;
                }
                case "GlyphSlam":
                {
                    Stretch(root);
                    Image glyph = NewImage("Glyph", root, Color.white, HellBlade("hellblade_kanji"));
                    Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f));
                    Image glow = NewImage("Glow", glyph.rectTransform, Color.white, HellBlade("hellblade_kanji_glow"), additive);
                    Stretch(glow.rectTransform);
                    Image ring = NewImage("Ring", root, Color.white, Common("fx_ring_glow"), additive);
                    Anchor(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(324f, 324f));
                    Configure(root.gameObject.AddComponent<GlyphSlamFx>(), ("glyph", glyph), ("glow", glow), ("ring", ring));
                    break;
                }
                case "SlashFlurry":
                case "BigSlash":
                {
                    bool flurry = name == "SlashFlurry";
                    Overscan(root);
                    NoInput(root.gameObject.AddComponent<CanvasGroup>());
                    Image background = NewImage("Background", root, flurry ? (Color)new Color32(0x4a, 0x06, 0x10, 0xff) : Color.black, Common("fx_radial_bg"));
                    Stretch(background.rectTransform);
                    RectTransform container = NewRect("Slashes", root);
                    Stretch(container);
                    Image flash = NewImage("Flash", root, Clear);
                    Stretch(flash.rectTransform);
                    Configure(root.gameObject.AddComponent<SlashLayer>(), ("background", background), ("container", container), ("additive", additive));
                    if (flurry)
                        Configure(root.gameObject.AddComponent<SlashFlurryFx>(), ("flash", flash), ("bandSprite", Common("fx_slash_band")),
                            ("thinSprite", Common("fx_slash_thin")), ("durationMs", 1460f));
                    else
                        Configure(root.gameObject.AddComponent<BigSlashFx>(), ("flash", flash), ("bandSprite", Common("fx_slash_band")));
                    break;
                }
                case "TargetSlash":
                {
                    Stretch(root);
                    Image flash = NewImage("Flash", root, Clear);
                    Stretch(flash.rectTransform);
                    Configure(root.gameObject.AddComponent<TargetSlashFx>(), ("sprite", Common("fx_target_slash")), ("additive", additive));
                    break;
                }
                case "TargetBoom":
                    Configure(root.gameObject.AddComponent<TargetBoomFx>(), ("glyph", HellBlade("hellblade_boom_glyph")));
                    break;
            }
            return root;
        }

        // 프리팹을 만들 임시 오브젝트는 미리보기 씬에 — 열린 씬을 건드리지 않게
        private static RectTransform NewPrefabRoot(string name)
        {
            if (!_preview.IsValid())
                _preview = EditorSceneManager.NewPreviewScene();
            RectTransform root = NewRect(name, null);
            SceneManager.MoveGameObjectToScene(root.gameObject, _preview);
            return root;
        }

        private static void ClosePreview()
        {
            if (_preview.IsValid())
                EditorSceneManager.ClosePreviewScene(_preview);
            _preview = default;
        }

        private static Image AddImage(RectTransform rect, Color color, Sprite sprite = null, Material material = null)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.material = material;
            image.raycastTarget = false;
            return image;
        }

        #endregion

        #region 지옥참마도 프리팹

        // 프리팹을 지우지 않고 그 자리에서 고친다 — 루트와 HellBladeFx 컴포넌트가 그대로라 씬의 연결(GUID · fileID)이 끊기지 않는다.
        // 장면 조각(CutInSceneStep)이 이미 있으면 사람이 고친 값일 수 있어 건드리지 않는다 (force 면 다시 만든다)
        private static HellBladeFx EnsureHellBladePrefab(bool force = false)
        {
            EnsureCommonFxPrefabs();
            Material multiply = EnsureMaterial(MultiplyPath, BlendMode.DstColor, BlendMode.Zero, true);

            if (!AssetDatabase.IsValidFolder(CardFxResourceFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "CardFx");
            if (AssetDatabase.LoadAssetAtPath<HellBladeFx>(HellBladePrefabPath) == null)
            {
                RectTransform empty = NewPrefabRoot("HellBladeFx");
                Stretch(empty);
                empty.gameObject.AddComponent<HellBladeFx>();
                PrefabUtility.SaveAsPrefabAsset(empty.gameObject, HellBladePrefabPath);
                Object.DestroyImmediate(empty.gameObject);
                ClosePreview();
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(HellBladePrefabPath);
            try
            {
                if (!force && contents.GetComponentInChildren<CutInSceneStep>(true) != null)
                    return AssetDatabase.LoadAssetAtPath<HellBladeFx>(HellBladePrefabPath);

                for (int i = contents.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(contents.transform.GetChild(i).gameObject);
                BuildHellBlade((RectTransform)contents.transform, multiply);
                PrefabUtility.SaveAsPrefabAsset(contents, HellBladePrefabPath);
                Debug.Log($"[{nameof(CardFxTestBuilder)}] {HellBladePrefabPath} 를 장면 + 공통 이펙트로 만들었다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return AssetDatabase.LoadAssetAtPath<HellBladeFx>(HellBladePrefabPath);
        }

        // 그림은 그려질 순서대로 (위가 먼저 그려진다). 공통 이펙트는 그려질 자리에 중첩 프리팹으로 놓고 시각 · 값만 바꾼다.
        // 값은 시안 SPECIAL · FINISH 지옥참마도
        private static void BuildHellBlade(RectTransform root, Material multiply)
        {
            // 컷인 — 흔들림은 Shaker 에만, 장면 확대는 Cam 에만
            RectTransform cutInRect = NewRect("CutIn", root);
            Stretch(cutInRect);
            CanvasGroup cutIn = NoInput(cutInRect.gameObject.AddComponent<CanvasGroup>());
            RectTransform shaker = NewRect("Shaker", cutInRect);
            Stretch(shaker);
            shaker.gameObject.AddComponent<FxShaker>();

            Image shotImage = NewImage("Shot", shaker, new Color32(0x12, 0x0a, 0x08, 0xff));
            Overscan(shotImage.rectTransform);
            CanvasGroup shot = NoInput(shotImage.gameObject.AddComponent<CanvasGroup>());

            RectTransform cam = NewRect("Cam", shotImage.rectTransform);
            Anchor(cam, new Vector2(0.5f, 0.5f), new Vector2(0.46f, 0.6f), Vector2.zero, new Vector2(2150f, 2150f));

            Image sceneBlur = NewImage("SceneBlur", cam, Color.white, HellBlade("hellblade_scene_blur"));
            sceneBlur.rectTransform.anchorMin = new Vector2(-0.1f, -0.1f);
            sceneBlur.rectTransform.anchorMax = new Vector2(1.1f, 1.1f);
            sceneBlur.rectTransform.offsetMin = sceneBlur.rectTransform.offsetMax = Vector2.zero;
            Image scene = NewImage("Scene", cam, Color.white, HellBlade("hellblade_scene"));
            Stretch(scene.rectTransform);

            var drawSprites = new Object[10];
            for (int i = 0; i < drawSprites.Length; i++)
                drawSprites[i] = HellBlade($"hellblade_draw_{i}");
            Image drawFrame = NewImage("DrawFrame", cam, Color.white, (Sprite)drawSprites[0]);
            Box(drawFrame.rectTransform, DrawFrameBox);
            FramePlayer frames = drawFrame.gameObject.AddComponent<FramePlayer>();
            WireArray(frames, "frames", drawSprites);

            // 칼: 장면 기준으로 기울어진 칼집 이음새에 빛
            RectTransform sword = NewRect("Sword", cam);
            Stretch(sword);
            sword.pivot = new Vector2(SwordPivot.x / 100f, 1f - SwordPivot.y / 100f);
            sword.localRotation = Quaternion.Euler(0f, 0f, SwordAngle);
            GleamSweepFx gleam = Instance<GleamSweepFx>("GleamSweep", sword, "칼날 빛", 600f);
            Box((RectTransform)gleam.transform, Grow(SwordGlowBox, 1.9f));
            Box((RectTransform)Instance<FlareBurstFx>("FlareBurst", sword, "칼집 빛 — 살짝", 130f, ("durationMs", 520f)).transform, Around(SwordJoint, 18f, 18f));
            Box((RectTransform)Instance<FlareBurstFx>("FlareBurst", sword, "칼집 빛 — 뽑기", 600f, ("durationMs", 480f)).transform, Around(SwordJoint, 18f, 18f));
            Box((RectTransform)Instance<FlareBurstFx>("FlareBurst", sword, "칼집 빛 — 납도", 3550f, ("durationMs", 560f)).transform, Around(SwordJoint, 18f, 18f));

            Image eyeCover = NewImage("EyeCover", cam, Color.white, HellBlade("hellblade_eye_cover"));
            Box(eyeCover.rectTransform, EyeBox);
            Image eyeOpen = NewImage("EyeOpen", cam, Color.white, HellBlade("hellblade_eye_open"));
            Box(eyeOpen.rectTransform, EyeBox);
            eyeOpen.rectTransform.pivot = new Vector2(EyeOpenPivot.x / 100f, 1f - EyeOpenPivot.y / 100f);
            Image tint = NewImage("Tint", cam, Color.white, HellBlade("hellblade_tint"), multiply);
            Stretch(tint.rectTransform);
            Image eyeRed = NewImage("EyeRed", cam, Color.white, HellBlade("hellblade_eye_red"));
            Box(eyeRed.rectTransform, EyeBox);

            GlowPopFx eyeGlow = Instance<GlowPopFx>("GlowPop", cam, "눈빛", 600f);
            Box((RectTransform)eyeGlow.transform, Around(EyeCenter, 4f, 4f));
            Step<FadeFx>(cam, "남은 눈빛", 3260f, ("target", eyeGlow.gameObject), ("resetAlpha", -1f), ("alpha", 0.85f), ("inMs", 0f));
            Step<FadeFx>(cam, "남은 칼날 빛", 3260f, ("target", gleam.gameObject), ("resetAlpha", -1f), ("alpha", 0.7f), ("inMs", 0f));

            // 장면 — 그림 애니메이션. 앞 장면은 암전이 다 덮은 뒤(870ms) 숨고, 뒤 장면은 일섬이 끝난 3260ms 부터 컷인 끝까지
            var painting = new Object[] { shotImage, sceneBlur, scene, eyeCover };
            (string, object)[] sceneRefs =
            {
                ("shot", shot), ("cam", cam), ("swordFrames", frames), ("eye", eyeOpen), ("eyeOpenSprite", HellBlade("hellblade_eye_open")),
                ("eyeClosedSprite", HellBlade("hellblade_eye_closed")), ("redEye", eyeRed), ("tint", tint),
            };
            WireArray(Step<DrawSceneStep>(cam, "칼 뽑기", 0f, With(sceneRefs, ("durationMs", 870f))), "painting", painting);
            WireArray(Step<SheathSceneStep>(cam, "납도", 3260f, With(sceneRefs, ("durationMs", 890f))), "painting", painting);

            // 장면 위 겹 — 암전 · 글자 · 칼선 판 · 번쩍 · 흔들림
            Instance<FadeFx>("Blackout", shaker, "암전", 830f, ("holdMs", 2390f), ("outMs", 0f));
            Instance<GlyphSlamFx>("GlyphSlam", shaker, "절 글자", 950f);
            SlashFlurryFx flurry = Instance<SlashFlurryFx>("SlashFlurry", shaker, "난도질", 1540f);
            BigSlashFx bigSlash = Instance<BigSlashFx>("BigSlash", shaker, "마지막 일섬", 3000f);
            Instance<FlashFx>("Flash", shaker, "들어올 때 번쩍", 0f);
            Instance<FlashFx>("Flash", shaker, "절 번쩍", 950f, ("alpha", 0.55f), ("durationMs", 140f));
            Instance<ShakeFx>("Shake", shaker, "뽑을 때 흔들림", 600f);
            Instance<ShakeFx>("Shake", shaker, "절 흔들림", 950f, ("amount", 14f), ("durationMs", 300f));
            Instance<ShakeFx>("Shake", shaker, "납도 흔들림", 3550f, ("amount", 5f), ("durationMs", 140f));

            RectTransform bars = NewRect("Bars", cutInRect);
            Stretch(bars);
            AnchorRange(NewImage("Top", bars, Color.black).rectTransform, new Vector2(0f, 0.93f), Vector2.one);
            AnchorRange(NewImage("Bottom", bars, Color.black).rectTransform, Vector2.zero, new Vector2(1f, 0.07f));
            Step<FadeFx>(cutInRect, "컷인 사라짐", 3930f, ("target", cutInRect.gameObject), ("resetAlpha", -1f), ("alpha", 0f), ("inMs", 220f));

            // 마무리 — 대상 자리에 찍는다 (시각은 마무리 시작 기준)
            RectTransform finish = NewRect("Finish", root);
            Stretch(finish);
            FadeFx dim = Instance<FadeFx>("Blackout", finish, "대상 어둡게", 0f, ("alpha", 0.88f), ("inMs", 93f), ("holdMs", 279f), ("outMs", 248f));
            Stretch((RectTransform)dim.transform);
            Instance<TargetSlashFx>("TargetSlash", finish, "짧은 칼질 ×7", 0f);
            TargetSlashFx big = Instance<TargetSlashFx>("TargetSlash", finish, "큰 칼질", 360f, ("count", 1), ("jitter", 0f),
                ("lengthRange", new Vector2(560f, 560f)), ("widthRange", new Vector2(22f, 22f)), ("angleRange", new Vector2(-38f, -38f)),
                ("lifeMs", 380f), ("shake", 22f), ("punch", 0.05f));
            Configure(big, ("flash", big.transform.Find("Flash").GetComponent<Image>()));
            SetColors(big, "colors", new Color32(0xff, 0x2a, 0x3a, 0xff));
            Instance<TargetBoomFx>("TargetBoom", finish, "절 터짐", 420f);

            Configure(root.GetComponent<HellBladeFx>(), ("cutIn", cutIn), ("finish", finish), ("shot", shot), ("cam", cam), ("backdrop", shotImage));
            WireArray(root.GetComponent<HellBladeFx>(), "hideWhenClear", new Object[]
            {
                cutIn, eyeGlow.GetComponent<CanvasGroup>(), flurry.GetComponent<CanvasGroup>(), bigSlash.GetComponent<CanvasGroup>(),
            });
        }

        // 공통 이펙트 프리팹을 중첩 프리팹으로 놓고 시각 · 값을 바꾼다 (프리팹 원본과 다른 값은 Inspector 에서 굵게 보인다)
        private static T Instance<T>(string prefabName, Transform parent, string name, float startMs, params (string, object)[] values) where T : CardFxStep
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommonFxPath(prefabName));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            T step = go.GetComponent<T>();
            Configure(step, With(values, ("startMs", startMs)));
            return step;
        }

        // 장면 · 그림 없는 이펙트처럼 프리팹이 아닌 조각 오브젝트 하나 — 이름은 계층에서 보이는 연출 이름
        private static T Step<T>(RectTransform parent, string name, float startMs, params (string, object)[] values) where T : CardFxStep
        {
            RectTransform rect = NewRect(name, parent);
            Stretch(rect);
            T step = rect.gameObject.AddComponent<T>();
            Configure(step, With(values, ("startMs", startMs)));
            return step;
        }

        private static void Configure(Component target, params (string field, object value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, object value) in values)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[{nameof(CardFxTestBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
                    continue;
                }

                switch (value)
                {
                    case float number: property.floatValue = number; break;
                    case int number: property.intValue = number; break;
                    case bool flag: property.boolValue = flag; break;
                    case Vector2 vector: property.vector2Value = vector; break;
                    case Color color: property.colorValue = color; break;
                    case Object reference: property.objectReferenceValue = reference; break;
                    default: Debug.LogError($"[{nameof(CardFxTestBuilder)}] {target.GetType().Name}.{field} — 넣을 수 없는 값 {value}"); break;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static (string, object)[] With((string, object)[] values, params (string, object)[] more)
        {
            var all = new (string, object)[values.Length + more.Length];
            values.CopyTo(all, 0);
            more.CopyTo(all, values.Length);
            return all;
        }

        private static void SetColors(Component target, string field, params Color[] colors)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++)
                property.GetArrayElementAtIndex(i).colorValue = colors[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 연출 이미지는 전부 한 장짜리 스프라이트로 (투명 그대로). 밉맵은 켠다 — 얇은 칼선 · 고리를 작게 그릴 때 깨지지 않게.
        // 프로젝트 기본 가져오기가 Multiple(투명 테두리를 잘라 조각냄)이라 그대로 두면 장면 · 프레임의 위치가 어긋난다.
        private static void ImportAsSprites(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && importer.mipmapEnabled && importer.filterMode == FilterMode.Trilinear)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static Material EnsureMaterial(string path, BlendMode source, BlendMode destination, bool multiply)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "CardFx");

            var material = new Material(Shader.Find(FxShaderName));
            material.SetFloat("_SrcBlend", (float)source);
            material.SetFloat("_DstBlend", (float)destination);
            material.SetFloat("_Multiply", multiply ? 1f : 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Sprite HellBlade(string name) => LoadSprite(HellBladeSprites + name + ".png");

        private static Sprite Common(string name) => LoadSprite(CommonSprites + name + ".png");

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogError($"[{nameof(CardFxTestBuilder)}] 스프라이트가 없다: {path}");
            return sprite;
        }

        private static CanvasGroup NoInput(CanvasGroup group)
        {
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        // 장면 좌표(왼쪽 위 기준 %) 상자 → 앵커
        private static void Box(RectTransform rect, Rect percent)
        {
            AnchorRange(rect,
                new Vector2(percent.x / 100f, 1f - (percent.y + percent.height) / 100f),
                new Vector2((percent.x + percent.width) / 100f, 1f - percent.y / 100f));
        }

        private static Rect Around(Vector2 centerPercent, float width, float height)
            => new Rect(centerPercent.x - width / 2f, centerPercent.y - height / 2f, width, height);

        private static Rect Grow(Rect rect, float scale)
            => Around(rect.center, rect.width * scale, rect.height * scale);

        #endregion

        #region 공용

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null, Material material = null,
            bool raycastTarget = false)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.material = material;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
        {
            TextMeshProUGUI tmp = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (_font != null)
                tmp.font = _font;
            return tmp;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Overscan(RectTransform rect)
        {
            Stretch(rect, -ShakeOverscan, -ShakeOverscan, -ShakeOverscan, -ShakeOverscan);
        }

        private static void AnchorRange(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Wire(Component target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, Object value) in references)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[{nameof(CardFxTestBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Component target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
