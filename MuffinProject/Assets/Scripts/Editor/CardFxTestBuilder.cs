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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 카드 연출 테스트 씬과 카드별 연출 프리팹을 만든다 (메뉴 Chapchu/Card Fx).
    /// 씬 · 프리팹이 이미 있으면 사람이 고친 것을 덮어쓰지 않도록 멈춘다 — 지우고 다시 실행한다.
    /// 지금 열려 있는 씬은 건드리지 않는다 (새 씬을 추가로 열어 만들고 저장한 뒤 닫는다).
    /// </summary>
    public static class CardFxTestBuilder
    {
        private const string ScenePath = "Assets/Scenes/CardFxTestScene.unity";
        private const string FxPrefabFolder = "Assets/Prefab/UI/CardFx";
        private const string HellBladePrefabPath = FxPrefabFolder + "/HellBladeFx.prefab";
        private const string SpriteFolder = "Assets/Sprites/CardFx";
        private const string HellBladeSprites = SpriteFolder + "/HellBlade/";
        private const string CommonSprites = SpriteFolder + "/Common/";
        private const string HandCardPrefabFolder = "Assets/Prefab/UI/GameScreen";
        private const string BoldFontAssetPath = "Assets/Fonts/GlaMuri/Galmuri11-Bold SDF.asset";
        private const string BackgroundPath = "Assets/Sprites/UI/Background/bg_ingame.png";
        private const string RoundSpritePath = "Assets/Sprites/UI/Common/ui_round64.png";
        private const string HellBladeArtPath = "Assets/Sprites/Cards/Images/A03.jpg";

        // 시안 「찹츄 카드 이펙트」 지옥참마도 장면 좌표 (정사각형 장면의 왼쪽 위 기준 %)
        private static readonly Rect DrawFrameBox = new Rect(0f, 0f, 45f, 76f);
        private static readonly Rect EyeBox = new Rect(56f, 24f, 19.5f, 17.5f);
        private static readonly Vector2 EyeOpenPivot = new Vector2(43.387f, 52.932f);
        private static readonly Vector2 EyeCenter = new Vector2(64.46f, 33.263f);
        private static readonly Vector2 SwordPivot = new Vector2(31.921f, 48.627f);
        private const float SwordAngle = 11.9466f;
        private static readonly Vector2 SwordJoint = new Vector2(31.825f, 45.25f);
        private static readonly Rect SwordGlowBox = new Rect(25f, 34.75f, 13.65f, 10.5f);

        private static readonly Color Ink = new Color32(0x2b, 0x28, 0x56, 0xff);
        private static readonly Color Red = new Color32(0xff, 0x2a, 0x3a, 0xff);

        private static TMP_FontAsset _font;

        [MenuItem("Chapchu/Card Fx/Build Test Scene")]
        public static void BuildTestScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.LogError($"[{nameof(CardFxTestBuilder)}] {ScenePath} 가 이미 있다. 덮어쓰지 않는다 — 지우고 다시 실행할 것.");
                return;
            }

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontAssetPath);
            HellBladeFx hellBlade = EnsureHellBladePrefab();

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.tag = "MainCamera";
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            BuildBench(hellBlade);

            EditorSceneManager.SaveScene(scene, ScenePath);
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

        #region 테스트 씬

        private static void BuildBench(HellBladeFx hellBlade)
        {
            var canvasGo = new GameObject("CardFxTestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var canvas = (RectTransform)canvasGo.transform;

            Sprite round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSpritePath);

            RectTransform stage = NewRect("Stage", canvas);
            Stretch(stage);
            Image background = NewImage("Background", stage, Color.white, AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath));
            Stretch(background.rectTransform, -40f, -40f, -40f, -40f);

            FxDummySeat caster = BuildSeat(stage, "Caster", "나", new Vector2(-560f, 120f), round);
            FxDummySeat target = BuildSeat(stage, "Target", "초코", new Vector2(560f, 120f), round);

            // 카드 프리팹은 피벗이 아래 가운데 — 가운데에 오도록 (336 × 1.3) / 2 만큼 내린다
            RectTransform playSpot = NewRect("PlaySpot", stage);
            Anchor(playSpot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -218f), new Vector2(10f, 10f));

            RectTransform hand = NewRect("HandArea", stage);
            Anchor(hand, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1600f, 336f));
            hand.localScale = Vector3.one * 0.7f;
            HorizontalLayoutGroup layout = hand.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TMP_Text caption = NewText("Caption", canvas, "", 34f, Color.white);
            Anchor(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1200f, 60f));
            caption.outlineWidth = 0.25f;
            caption.outlineColor = Ink;

            Button replay = NewButton("ReplayButton", canvas, "다시 (Space)", new Vector2(-300f, -36f), round);
            Button reset = NewButton("ResetButton", canvas, "체력 초기화 (R)", new Vector2(-40f, -36f), round);

            RectTransform fxLayer = NewRect("FxLayer", canvas);
            Stretch(fxLayer);

            CardFxTestBench bench = canvasGo.AddComponent<CardFxTestBench>();
            Wire(bench,
                ("actionCardPrefab", LoadHandCard("HandCard_Action")), ("counterCardPrefab", LoadHandCard("HandCard_Counter")),
                ("trapCardPrefab", LoadHandCard("HandCard_Trap")), ("stage", stage), ("handArea", hand), ("playSpot", playSpot),
                ("fxLayer", fxLayer), ("caster", caster), ("target", target), ("captionText", caption),
                ("replayButton", replay), ("resetButton", reset));

            var serialized = new SerializedObject(bench);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("cardName").stringValue = "지옥참마도";
            entry.FindPropertyRelative("type").enumValueIndex = (int)CardType.Action;
            entry.FindPropertyRelative("art").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HellBladeArtPath);
            entry.FindPropertyRelative("description").stringValue = "대상 40 피해";
            entry.FindPropertyRelative("fxPrefab").objectReferenceValue = hellBlade;
            entry.FindPropertyRelative("damage").intValue = 40;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static FxDummySeat BuildSeat(RectTransform parent, string name, string label, Vector2 position, Sprite round)
        {
            Image panel = NewImage(name, parent, Color.white, round);
            panel.type = Image.Type.Sliced;
            Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(300f, 150f));

            TMP_Text nameText = NewText("Name", panel.rectTransform, label, 34f, Ink);
            Anchor(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(260f, 44f));

            Image hpBack = NewImage("HpBar", panel.rectTransform, new Color32(0xff, 0xe3, 0xec, 0xff), round);
            hpBack.type = Image.Type.Sliced;
            Anchor(hpBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(250f, 24f));
            Image hpFill = NewImage("Fill", hpBack.rectTransform, new Color32(0xff, 0x4f, 0x7b, 0xff), round);
            Stretch(hpFill.rectTransform);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;

            TMP_Text hpText = NewText("Hp", panel.rectTransform, "100", 26f, Ink);
            Anchor(hpText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(200f, 34f));

            TMP_Text pop = NewText("DamagePop", panel.rectTransform, "-0", 96f, new Color32(0xff, 0x4f, 0x7b, 0xff));
            Anchor(pop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(300f, 110f));
            pop.outlineWidth = 0.3f;
            pop.outlineColor = Ink;

            FxDummySeat seat = panel.gameObject.AddComponent<FxDummySeat>();
            Wire(seat, ("hpFill", hpFill), ("hpText", hpText), ("damagePopTemplate", pop));
            return seat;
        }

        private static Button NewButton(string name, RectTransform parent, string label, Vector2 position, Sprite round)
        {
            Image image = NewImage(name, parent, Color.white, round, true);
            image.type = Image.Type.Sliced;
            Anchor(image.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), position, new Vector2(240f, 60f));
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

            RectTransform root = NewRect("HellBladeFx", null);
            Stretch(root);

            // 컷인 — 흔들림은 Shaker 에만, 장면 확대는 Cam 에만
            RectTransform cutInRect = NewRect("CutIn", root);
            Stretch(cutInRect);
            CanvasGroup cutIn = NoInput(cutInRect.gameObject.AddComponent<CanvasGroup>());
            RectTransform shaker = NewRect("Shaker", cutInRect);
            Stretch(shaker);

            Image shotImage = NewImage("Shot", shaker, new Color32(0x12, 0x0a, 0x08, 0xff));
            Stretch(shotImage.rectTransform);
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
            Image swordGlow = NewImage("Glow", sword, Red, Common("fx_glow_soft"));
            Box(swordGlow.rectTransform, Grow(SwordGlowBox, 1.9f));
            Image swordRing = NewImage("Ring", sword, Color.white, HellBlade("hellblade_ring"));
            Box(swordRing.rectTransform, Around(SwordJoint, 18f, 18f));
            Image swordStreak = NewImage("Streak", sword, Color.white, HellBlade("hellblade_streak"));
            Box(swordStreak.rectTransform, Around(SwordJoint, 44f, 3f));
            Image swordSpark = NewImage("Spark", sword, Color.white, Common("fx_spark"));
            Box(swordSpark.rectTransform, Around(SwordJoint, 7f, 7f));

            Image eyeCover = NewImage("EyeCover", cam, Color.white, HellBlade("hellblade_eye_cover"));
            Box(eyeCover.rectTransform, EyeBox);
            Image eyeOpen = NewImage("EyeOpen", cam, Color.white, HellBlade("hellblade_eye_open"));
            Box(eyeOpen.rectTransform, EyeBox);
            eyeOpen.rectTransform.pivot = new Vector2(EyeOpenPivot.x / 100f, 1f - EyeOpenPivot.y / 100f);
            Image tint = NewImage("Tint", cam, Color.white, HellBlade("hellblade_tint"));
            Stretch(tint.rectTransform);
            Image eyeRed = NewImage("EyeRed", cam, Color.white, HellBlade("hellblade_eye_red"));
            Box(eyeRed.rectTransform, EyeBox);

            RectTransform eyeGlowRect = NewRect("EyeGlow", cam);
            Box(eyeGlowRect, Around(EyeCenter, 4f, 4f));
            CanvasGroup eyeGlow = NoInput(eyeGlowRect.gameObject.AddComponent<CanvasGroup>());
            Image halo = NewImage("Halo", eyeGlowRect, new Color32(0xff, 0x28, 0x3c, 0xff), Common("fx_glow_soft"));
            AnchorRange(halo.rectTransform, new Vector2(-1.8f, -1.8f), new Vector2(2.8f, 2.8f));
            Image flare = NewImage("Flare", eyeGlowRect, new Color32(0xff, 0x80, 0x90, 0xff), HellBlade("hellblade_streak"));
            AnchorRange(flare.rectTransform, new Vector2(-12.5f, 0.2f), new Vector2(13.5f, 0.8f));

            Image black = NewImage("Black", shaker, Color.black);
            Stretch(black.rectTransform);
            Image kanji = NewImage("Kanji", shaker, Color.white, HellBlade("hellblade_kanji"));
            Anchor(kanji.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f));

            RectTransform slashRect = NewRect("Slashes", shaker);
            Stretch(slashRect);
            CanvasGroup slashLayer = NoInput(slashRect.gameObject.AddComponent<CanvasGroup>());
            Image slashBackground = NewImage("Background", slashRect, new Color32(0x4a, 0x06, 0x10, 0xff), Common("fx_radial_bg"));
            Stretch(slashBackground.rectTransform);

            Image flash = NewImage("Flash", shaker, Color.white);
            Stretch(flash.rectTransform);

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
                ("drawFrame", drawFrame), ("eyeCover", eyeCover), ("eyeOpen", eyeOpen), ("eyeRed", eyeRed), ("tint", tint),
                ("eyeGlow", eyeGlow), ("swordGlow", swordGlow), ("swordRing", swordRing), ("swordStreak", swordStreak),
                ("swordSpark", swordSpark), ("black", black), ("kanji", kanji), ("slashLayer", slashLayer),
                ("slashBackground", slashBackground), ("flash", flash), ("finish", finish), ("dimmer", dimmer),
                ("finishFlash", finishFlash), ("slashBand", HellBlade("hellblade_slash_band")),
                ("slashThin", HellBlade("hellblade_slash_thin")), ("boom", HellBlade("hellblade_boom")),
                ("ring", HellBlade("hellblade_ring")), ("glow", Common("fx_glow_soft")), ("streak", HellBlade("hellblade_streak")));
            WireArray(fx, "drawSprites", drawSprites);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, HellBladePrefabPath);
            Object.DestroyImmediate(root.gameObject);
            Debug.Log($"[{nameof(CardFxTestBuilder)}] {HellBladePrefabPath} 생성 완료.");
            return saved.GetComponent<HellBladeFx>();
        }

        // 연출 이미지는 전부 한 장짜리 스프라이트로 (밉맵 없음 · 투명 그대로).
        // 프로젝트 기본 가져오기가 Multiple(투명 테두리를 잘라 조각냄)이라 그대로 두면 장면 · 프레임의 위치가 어긋난다.
        private static void ImportAsSprites(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && !importer.mipmapEnabled)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
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

        private static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null, bool raycastTarget = false)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
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
