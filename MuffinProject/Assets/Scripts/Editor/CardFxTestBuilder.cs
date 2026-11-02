using System.IO;
using Chapchu.DebugTools;
using Chapchu.Game;
using Chapchu.UI.Fx;
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
    /// 카드 연출 테스트 씬(CardFxTestScene)과 카드별 연출 프리팹을 만들고 고친다 (메뉴 Chapchu/Card Fx).
    /// 테스트 씬은 열어 둔 채로 계속 쓴다 — 열려 있으면 그 자리에서 없는 것만 만들고 연결을 맞춘 뒤 저장한다. 있는 오브젝트는 건드리지 않는다.
    /// 프리팹은 이미 있으면 그대로 쓴다. 다시 만들려면 Rebuild 메뉴 (사람이 고친 프리팹이면 쓰지 않는다).
    /// </summary>
    public static class CardFxTestBuilder
    {
        private const string ScenePath = "Assets/Scenes/CardFxTestScene.unity";
        private const string CanvasName = "CardFxTestCanvas";
        private const string FxPrefabFolder = "Assets/Prefab/UI/CardFx";
        private const string HellBladePrefabPath = FxPrefabFolder + "/HellBladeFx.prefab";
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

        /// <summary>지옥참마도 프리팹을 지우고 다시 만든다. 테스트 씬의 연결이 끊기므로 이어서 Build Or Update Test Scene 을 실행한다.</summary>
        [MenuItem("Chapchu/Card Fx/Rebuild Hell Blade Prefab")]
        public static void RebuildHellBladePrefab()
        {
            AssetDatabase.DeleteAsset(HellBladePrefabPath);
            EnsureHellBladePrefab();
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

            entry.FindPropertyRelative("fxPrefab").objectReferenceValue = hellBlade;
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

        #region 지옥참마도 프리팹

        private static HellBladeFx EnsureHellBladePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HellBladeFx>(HellBladePrefabPath);
            if (existing != null)
                return existing;

            ImportAsSprites(SpriteFolder);
            if (!AssetDatabase.IsValidFolder(FxPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefab/UI", "CardFx");
            Material additive = EnsureMaterial(AdditivePath, BlendMode.SrcAlpha, BlendMode.One, false);
            Material multiply = EnsureMaterial(MultiplyPath, BlendMode.DstColor, BlendMode.Zero, true);

            RectTransform root = NewRect("HellBladeFx", null);
            Stretch(root);

            // 컷인 — 흔들림은 Shaker 에만, 장면 확대는 Cam 에만
            RectTransform cutInRect = NewRect("CutIn", root);
            Stretch(cutInRect);
            CanvasGroup cutIn = NoInput(cutInRect.gameObject.AddComponent<CanvasGroup>());
            RectTransform shaker = NewRect("Shaker", cutInRect);
            Stretch(shaker);

            Image shotImage = NewImage("Shot", shaker, new Color32(0x12, 0x0a, 0x08, 0xff));
            Overscan(shotImage.rectTransform);
            CanvasGroup shot = NoInput(shotImage.gameObject.AddComponent<CanvasGroup>());

            RectTransform cam = NewRect("Cam", shotImage.rectTransform);
            Anchor(cam, new Vector2(0.5f, 0.5f), new Vector2(0.46f, 0.6f), Vector2.zero, new Vector2(2150f, 2150f));

            Image sceneBlur = NewImage("SceneBlur", cam, Color.white, HellBlade("hellblade_scene_blur"));
            sceneBlur.rectTransform.anchorMin = new Vector2(-0.1f, -0.1f);
            sceneBlur.rectTransform.anchorMax = new Vector2(1.1f, 1.1f);
            sceneBlur.rectTransform.offsetMin = sceneBlur.rectTransform.offsetMax = Vector2.zero;
            Stretch(NewImage("Scene", cam, Color.white, HellBlade("hellblade_scene")).rectTransform);

            var drawSprites = new Sprite[10];
            for (int i = 0; i < drawSprites.Length; i++)
                drawSprites[i] = HellBlade($"hellblade_draw_{i}");
            Image drawFrame = NewImage("DrawFrame", cam, Color.white, drawSprites[0]);
            Box(drawFrame.rectTransform, DrawFrameBox);

            // 칼: 장면 기준으로 기울어진 칼집 이음새에 빛
            RectTransform sword = NewRect("Sword", cam);
            Stretch(sword);
            sword.pivot = new Vector2(SwordPivot.x / 100f, 1f - SwordPivot.y / 100f);
            sword.localRotation = Quaternion.Euler(0f, 0f, SwordAngle);
            Image swordGlow = NewImage("Glow", sword, Red, Common("fx_glow_soft"), additive);
            Box(swordGlow.rectTransform, Grow(SwordGlowBox, 1.9f));
            Image swordGleam = NewImage("Gleam", swordGlow.rectTransform, Color.white, Common("fx_glow_soft"), additive);
            AnchorRange(swordGleam.rectTransform, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f));
            Image swordRing = NewImage("Ring", sword, Color.white, HellBlade("hellblade_ring"), additive);
            Box(swordRing.rectTransform, Around(SwordJoint, 18f, 18f));
            Image swordStreak = NewImage("Streak", sword, Color.white, HellBlade("hellblade_streak"), additive);
            Box(swordStreak.rectTransform, Around(SwordJoint, 44f, 3f));
            Image swordSpark = NewImage("Spark", sword, Color.white, Common("fx_spark"), additive);
            Box(swordSpark.rectTransform, Around(SwordJoint, 7f, 7f));

            Image eyeCover = NewImage("EyeCover", cam, Color.white, HellBlade("hellblade_eye_cover"));
            Box(eyeCover.rectTransform, EyeBox);
            Image eyeOpen = NewImage("EyeOpen", cam, Color.white, HellBlade("hellblade_eye_open"));
            Box(eyeOpen.rectTransform, EyeBox);
            eyeOpen.rectTransform.pivot = new Vector2(EyeOpenPivot.x / 100f, 1f - EyeOpenPivot.y / 100f);
            Image tint = NewImage("Tint", cam, Color.white, HellBlade("hellblade_tint"), multiply);
            Stretch(tint.rectTransform);
            Image eyeRed = NewImage("EyeRed", cam, Color.white, HellBlade("hellblade_eye_red"));
            Box(eyeRed.rectTransform, EyeBox);

            RectTransform eyeGlowRect = NewRect("EyeGlow", cam);
            Box(eyeGlowRect, Around(EyeCenter, 4f, 4f));
            CanvasGroup eyeGlow = NoInput(eyeGlowRect.gameObject.AddComponent<CanvasGroup>());
            Image halo = NewImage("Halo", eyeGlowRect, new Color32(0xff, 0x28, 0x3c, 0xff), Common("fx_glow_soft"), additive);
            AnchorRange(halo.rectTransform, new Vector2(-1.8f, -1.8f), new Vector2(2.8f, 2.8f));
            Image flare = NewImage("Flare", eyeGlowRect, new Color32(0xff, 0x80, 0x90, 0xff), HellBlade("hellblade_streak"), additive);
            AnchorRange(flare.rectTransform, new Vector2(-12.5f, 0.2f), new Vector2(13.5f, 0.8f));

            Image black = NewImage("Black", shaker, Color.black);
            Overscan(black.rectTransform);
            Image kanji = NewImage("Kanji", shaker, Color.white, HellBlade("hellblade_kanji"));
            Anchor(kanji.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f));
            Image kanjiGlow = NewImage("Glow", kanji.rectTransform, Color.white, HellBlade("hellblade_kanji_glow"), additive);
            Stretch(kanjiGlow.rectTransform);
            Image kanjiRing = NewImage("KanjiRing", shaker, Color.white, Common("fx_ring_glow"), additive);
            Anchor(kanjiRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(324f, 324f));

            RectTransform slashRect = NewRect("Slashes", shaker);
            Overscan(slashRect);
            CanvasGroup slashLayer = NoInput(slashRect.gameObject.AddComponent<CanvasGroup>());
            Image slashBackground = NewImage("Background", slashRect, new Color32(0x4a, 0x06, 0x10, 0xff), Common("fx_radial_bg"));
            Stretch(slashBackground.rectTransform);

            Image flash = NewImage("Flash", shaker, Color.white);
            Overscan(flash.rectTransform);

            RectTransform bars = NewRect("Bars", cutInRect);
            Stretch(bars);
            AnchorRange(NewImage("Top", bars, Color.black).rectTransform, new Vector2(0f, 0.93f), Vector2.one);
            AnchorRange(NewImage("Bottom", bars, Color.black).rectTransform, Vector2.zero, new Vector2(1f, 0.07f));

            // 마무리 — 대상 자리에 찍는다
            RectTransform finish = NewRect("Finish", root);
            Stretch(finish);
            Image dimmer = NewImage("Dimmer", finish, Color.black);
            Stretch(dimmer.rectTransform);
            Image finishFlash = NewImage("Flash", finish, Color.white);
            Stretch(finishFlash.rectTransform);

            HellBladeFx fx = root.gameObject.AddComponent<HellBladeFx>();
            Wire(fx,
                ("cutIn", cutIn), ("shaker", shaker), ("shot", shot), ("cam", cam),
                ("swordGleam", swordGleam), ("kanjiGlow", kanjiGlow), ("kanjiRing", kanjiRing), ("additive", additive),
                ("drawFrame", drawFrame), ("eyeCover", eyeCover), ("eyeOpen", eyeOpen), ("eyeRed", eyeRed), ("tint", tint),
                ("eyeGlow", eyeGlow), ("swordGlow", swordGlow), ("swordRing", swordRing), ("swordStreak", swordStreak),
                ("swordSpark", swordSpark), ("black", black), ("kanji", kanji), ("slashLayer", slashLayer),
                ("slashBackground", slashBackground), ("flash", flash), ("finish", finish), ("dimmer", dimmer),
                ("finishFlash", finishFlash), ("slashBand", HellBlade("hellblade_slash_band")),
                ("slashThin", HellBlade("hellblade_slash_thin")), ("finishSlash", HellBlade("hellblade_finish_slash")),
                ("boomGlyph", HellBlade("hellblade_boom_glyph")));
            WireArray(fx, "drawSprites", drawSprites);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, HellBladePrefabPath);
            Object.DestroyImmediate(root.gameObject);
            Debug.Log($"[{nameof(CardFxTestBuilder)}] {HellBladePrefabPath} 생성 완료.");
            return saved.GetComponent<HellBladeFx>();
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
