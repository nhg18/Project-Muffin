using System.IO;
using Chapchu.Cards;
using Chapchu.Network;
using Chapchu.UI;
using Chapchu.UI.GameScreen;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 인게임 화면 UI 를 열려 있는 씬에 조립한다 (메뉴 Chapchu/Game Screen/Build UI).
    /// 10-ui.md 2-1 의 층 표대로 — 3D 층(테이블 방석 · 카드 · 덱 · 손패)은 TableBuilder 가 만들고, HUD 는 Overlay 캔버스, 배경(그라데이션 + 벽지)은 3D 층 뒤의 카메라 캔버스에 둔다.
    /// 수치 · 색은 docs/systems/10-ui.md 2-1 「HUD v2」 (HTML 시안 × 2). 가장자리 UI 는 SafeArea 아래, 가운데 묶음(상대 좌석 · 대상 안내)은 캔버스 가운데 1920 × 1080.
    /// 프레젠터도 같이 붙인다 — 영역마다 하나, 캔버스 루트에 연출 큐(GameScreenPresenter) 하나.
    /// 이미 GameCanvas 가 있으면 사람이 고친 것을 덮어쓰지 않도록 멈춘다 — 지우고 다시 실행한다.
    /// Galmuri11 TMP 폰트 에셋이 없으면 먼저 만든다. 시안 글꼴(Jua · 고운돋움)은 아직 안 쓴다 — 굵은 자리 Bold · 본문 Regular (2026-11-07 결정).
    /// </summary>
    public static class GameScreenBuilder
    {
        private const string CanvasName = "GameCanvas";
        private const string BackgroundCanvasName = "BackgroundCanvas";
        private const float BackgroundPlaneDistance = 50f; // 테이블 면(카메라 거리 약 14.85)보다 멀리
        private const string CushionTexturePath = "Assets/Sprites/UI/Hud/table_cushion.png";
        private static readonly Vector2 CushionCenter = new Vector2(960f, 620f); // 시안 TABLE 가운데 — 3D 층의 카드 내는 자리와 같은 점
        private static readonly Vector2 CushionSize = new Vector2(1120f, 560f);
        private const string RegularFontPath = "Assets/Fonts/GlaMuri/Galmuri11.ttf";
        private const string BoldFontPath = "Assets/Fonts/GlaMuri/Galmuri11-Bold.ttf";
        private const string RegularFontAssetPath = "Assets/Fonts/GlaMuri/Galmuri11 SDF.asset";
        private const string BoldFontAssetPath = "Assets/Fonts/GlaMuri/Galmuri11-Bold SDF.asset";
        private const string RoundSpritePath = "Assets/Sprites/UI/Common/ui_round64.png";
        private const string HudSpriteFolder = "Assets/Sprites/UI/Hud";
        private const string CardBackSpritePath = "Assets/Sprites/Cards/CardBack.jpg";
        private const string HandCardPrefabFolder = "Assets/Prefab/UI/GameScreen";

        // TMP 동적 폰트 생성값 — Galmuri9 SDF 와 같은 방식(SDFAA · 여백 5), 아틀라스는 2048 로 시작해 필요하면 늘어난다.
        private const int FontSamplingPointSize = 44;
        private const int FontAtlasPadding = 5;
        private const int FontAtlasSize = 2048;

        // 10-ui.md 2-1 「색」 (HUD v2)
        private static readonly Color Ink = Hex(0x2a2420);
        private static readonly Color Ink2 = Hex(0x6e645c);
        private static readonly Color White = Color.white;
        private static readonly Color Band = Hex(0xcfeedd);
        private static readonly Color Turn = Hex(0xffc12e);
        private static readonly Color Chu = Hex(0xff4f8b);
        private static readonly Color ChuInk = Hex(0xc8195a);
        private static readonly Color Hp = Hex(0xec3b46);
        private static readonly Color Tgt = Hex(0xec3b46);
        private static readonly Color Act = Hex(0x009dff);
        private static readonly Color Fur2 = Hex(0xd6c4a6);
        private static readonly Color Fur3 = Hex(0xbea983);
        private static readonly Color HpBg = Hex(0xffe6e3);
        private static readonly Color Line = Hex(0xe7ded2);
        private static readonly Color Dim = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.42f);
        private static readonly Color ChapchuOffLabel = Hex(0xc4bab0);
        private static readonly Color ChapchuOffShadow = Hex(0xddd3c8);
        private static readonly Color ChapchuOverInk = Hex(0xc81e2b);
        private static readonly Color WarnFill = Hex(0xfff1e0);

        // 가장자리 여백 (시안 gL · gR 16 · gT · gB 10 × 2) — SafeArea 안쪽에서
        private const float EdgeX = 32f;
        private const float EdgeTop = 20f;
        private const float EdgeBottom = 20f;

        private static TMP_FontAsset _regularFont;
        private static TMP_FontAsset _boldFont;
        private static Sprite _round;
        private static RectTransform _backCardPrefab; // 한 번 만든 뒷면 프리팹 — 좌석 3개가 같이 쓴다

        internal static TMP_FontAsset RegularFont => _regularFont != null ? _regularFont : _regularFont = EnsureFontAsset(RegularFontPath, RegularFontAssetPath);
        internal static TMP_FontAsset BoldFont => _boldFont != null ? _boldFont : _boldFont = EnsureFontAsset(BoldFontPath, BoldFontAssetPath);
        private static Sprite Round => _round != null ? _round : _round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSpritePath);

        [MenuItem("Chapchu/Game Screen/Build UI")]
        public static void Build()
        {
            _backCardPrefab = null;
            if (GameObject.Find(CanvasName) != null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 씬에 {CanvasName} 가 이미 있다. 덮어쓰지 않는다 — 지우고 다시 실행할 것.");
                return;
            }

            _regularFont = EnsureFontAsset(RegularFontPath, RegularFontAssetPath);
            _boldFont = EnsureFontAsset(BoldFontPath, BoldFontAssetPath);
            if (Round == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 둥근 모서리 스프라이트가 없다: {RoundSpritePath}");

            Camera camera = TableBuilder.SetupCamera();
            Transform table = TableBuilder.BuildTable();
            if (camera == null || table == null)
                return;
            DeckView deck = TableBuilder.BuildDeck(table);
            DeckData gameDeck = FindGameDeck();
            DiscardPileView discardPile = TableBuilder.BuildDiscardPile(table, gameDeck);

            GameScreenView screen = BuildCanvas(out RectTransform canvasRect);
            BuildBackgroundCanvas(camera);

            RectTransform safeArea = NewRect("SafeArea", canvasRect);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeArea>();

            // 가장자리 HUD
            RoomCodeView roomCode = BuildRoomCodeTag(safeArea);
            TurnStripView turnStrip = BuildTurnStrip(safeArea);
            GameMenuView menu = BuildGearButton(safeArea);
            FeedView feed = BuildFeed(safeArea);
            MyPanelView myPanel = BuildMyCharacterAndPanel(safeArea);
            ChapchuButtonView chapchu = BuildChapchuButton(safeArea);
            Wire(myPanel.GetComponent<MyPanelPresenter>(), ("chapchuButton", chapchu));
            RectTransform dropZone = BuildDropZone(safeArea);

            // 가운데 묶음 — 어둡게 가리는 판 · 상대 좌석 · 대상 선택 안내
            RectTransform center = NewRect("Center", canvasRect);
            Anchor(center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
            Image dimmer = BuildDimmer(center);
            OpponentSeatsPresenter seats = BuildOpponentSeats(center, deck);
            TargetAskView targetAsk = BuildTargetAsk(center, dimmer);
            Wire(seats, ("targetAsk", targetAsk));

            // 서랍 · 팝업 — 가장 위
            RectTransform sheets = NewRect("Sheets", canvasRect);
            Stretch(sheets);
            sheets.gameObject.AddComponent<SafeArea>();
            LogSheetView log = BuildLogSheet(sheets);
            BuildMenuSheet(sheets, menu);

            HandPresenter hand = TableBuilder.BuildHand(table, deck, discardPile, dropZone, seats, gameDeck);

            EnsureEventSystem();

            Wire(screen,
                ("roomCode", roomCode), ("turnStrip", turnStrip), ("feed", feed), ("log", log), ("myPanel", myPanel), ("chapchuButton", chapchu),
                ("deck", deck), ("dropZone", dropZone), ("targetAsk", targetAsk), ("menu", menu));
            WireArray(screen, "opponentSeats", seats.GetComponentsInChildren<OpponentSeatView>(true));
            screen.gameObject.AddComponent<RejectToastPresenter>();

            var history = screen.gameObject.AddComponent<HistoryPresenter>();
            Wire(history, ("feed", feed), ("log", log), ("deck", gameDeck));

            // 연출 큐 — GameEvents 를 받아 영역 프레젠터에 순서대로 넘긴다 (CODE_CONVENTION 9절)
            // 턴 타이머 — 스톱워치 전부(내 것 + 좌석 3)에 값을 넣는다
            var turnTimer = screen.gameObject.AddComponent<TurnTimerPresenter>();
            WireArray(turnTimer, "timers", canvasRect.GetComponentsInChildren<TurnTimerView>(true));

            var presenter = screen.gameObject.AddComponent<GameScreenPresenter>();
            Wire(presenter,
                ("deck", deck.GetComponent<DeckPresenter>()), ("discardPile", discardPile.GetComponent<DiscardPilePresenter>()), ("hand", hand), ("myPanel", myPanel.GetComponent<MyPanelPresenter>()), ("seats", seats),
                ("turnStrip", turnStrip.GetComponent<TurnStripPresenter>()), ("turnTimer", turnTimer), ("history", history));

            EditorSceneManager.MarkSceneDirty(canvasRect.gameObject.scene);
            Selection.activeGameObject = canvasRect.gameObject;
            Debug.Log($"[{nameof(GameScreenBuilder)}] {CanvasName} 조립 완료 — 씬을 저장할 것.");
        }

        /// <summary>uGUI 손패 카드 프리팹 3개를 같은 경로에 다시 만든다 (GUID 유지). CardFxTestBench 가 쓴다 — 인게임 손패는 3D 카드(TableBuilder). 사람이 프리팹을 손봤다면 지워진다.</summary>
        [MenuItem("Chapchu/Game Screen/Rebuild Hand Card Prefabs")]
        public static void RebuildHandCardPrefabs()
        {
            _regularFont = EnsureFontAsset(RegularFontPath, RegularFontAssetPath);
            _boldFont = EnsureFontAsset(BoldFontPath, BoldFontAssetPath);

            EnsureHandCardPrefab("HandCard_Action", ActionCardFramePath, true);
            EnsureHandCardPrefab("HandCard_Counter", CounterCardFramePath, true);
            EnsureHandCardPrefab("HandCard_Trap", TrapCardFramePath, true);
            Debug.Log($"[{nameof(GameScreenBuilder)}] 손패 카드 프리팹 3개 다시 만듦.");
        }

        #region 캔버스 · 배경

        private static GameScreenView BuildCanvas(out RectTransform canvasRect)
        {
            var go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            canvasRect = (RectTransform)go.transform;

            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; // HUD — 3D 층 위에 (10-ui.md 2-1 2D 층)
            SetupScaler(go.GetComponent<CanvasScaler>());

            return go.AddComponent<GameScreenView>();
        }

        // 15-screen.md 3절 #3
        private static void SetupScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        // 배경은 2D 층이지만 3D 층 뒤에 있어야 하므로 Overlay 가 아니라 카메라에 붙은 캔버스(테이블보다 먼 거리)에 둔다.
        // 시안 #game: 하늘 → 분홍 세로 그라데이션 위에 벽지 무늬 180px 타일 (× 2 = 360).
        private static void BuildBackgroundCanvas(Camera camera)
        {
            var go = new GameObject(BackgroundCanvasName, typeof(Canvas), typeof(CanvasScaler));
            go.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = BackgroundPlaneDistance;
            SetupScaler(go.GetComponent<CanvasScaler>());

            var parent = (RectTransform)go.transform;
            Image gradient = NewImage("Gradient", parent, White, Hud("bg_gradient"));
            Stretch(gradient.rectTransform);

            Image wall = NewImage("Wall", parent, White, Hud("bg_wall"));
            wall.type = Image.Type.Tiled;
            Stretch(wall.rectTransform);

            // 테이블 방석 — 3D 층이 아니라 배경(2D) 에 둔다. 3D Quad 로 두면 기울인 카드(뽑기 · 드래그 · 내기 40°)의 윗부분이 방석 면 뒤로 들어가 가려진다
            var cushion = new GameObject("Cushion", typeof(RawImage)).GetComponent<RawImage>();
            cushion.transform.SetParent(parent, false);
            cushion.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CushionTexturePath);
            cushion.raycastTarget = false;
            if (cushion.texture == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 방석 그림이 없다: {CushionTexturePath}");
            CenterAt(cushion.rectTransform, CushionCenter.x, CushionCenter.y, CushionSize.x, CushionSize.y);
        }

        #endregion

        #region 가장자리 HUD

        // 방 코드 꼬리표 — 좌상 (32, 34) · 높이 52 · −2° (시안 .room)
        private static RoomCodeView BuildRoomCodeTag(RectTransform parent)
        {
            RectTransform root = NewRect("RoomCodeTag", parent);
            TopLeft(root, EdgeX, EdgeTop + 14f, 200f, 52f);
            root.localRotation = Quaternion.Euler(0f, 0f, 2f); // CSS rotate −2° = 시계 방향
            Image fill = RoundBox(root, Ink, 4f, 14f, White);
            HorizontalLayout(root, new RectOffset(20, 20, 0, 0), 12f, TextAnchor.MiddleLeft, true, true);
            root.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text label = NewText("Label", root, "ROOM", 22f, Ink2, true);
            label.characterSpacing = 10f;
            TMP_Text code = NewText("CodeText", root, "----", 30f, Ink, true);
            code.characterSpacing = 8f;

            var view = root.gameObject.AddComponent<RoomCodeView>();
            Wire(view, ("codeText", code));
            root.gameObject.AddComponent<RoomCodePresenter>();
            return view;
        }

        // 턴 순서 띠 — 위 가운데 (시안 .strip2): 레일(화살표 · 칩 · 발자국) + 「나의 턴」 칩. 남은 초는 캐릭터 옆 스톱워치(BuildStopwatch, 2026-11-08)
        private static TurnStripView BuildTurnStrip(RectTransform parent)
        {
            RectTransform root = NewRect("TurnStrip", parent);
            Anchor(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -EdgeTop), new Vector2(400f, 76f));
            HorizontalLayout(root, new RectOffset(), 14f, TextAnchor.MiddleCenter, true, true);
            FitBoth(root.gameObject);

            // 레일
            RectTransform rail = NewRect("Rail", root);
            rail.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
            RoundBox(rail, Ink, 5f, 24f, White);
            HorizontalLayout(rail, new RectOffset(14, 16, 0, 0), 10f, TextAnchor.MiddleLeft, true, true);

            Image dir = Icon("Direction", rail, Hud("icon_turn_dir"), 36f, Ink2);
            dir.gameObject.AddComponent<LayoutElement>().preferredWidth = 36f;
            dir.GetComponent<LayoutElement>().preferredHeight = 36f;

            RectTransform chipTemplate = BuildTurnChipTemplate(rail);
            RectTransform separatorTemplate = Icon("Separator", rail, Hud("icon_paw"), 24f, Fur3).rectTransform;
            separatorTemplate.localRotation = Quaternion.Euler(0f, 0f, -90f);
            LayoutElement separatorSize = separatorTemplate.gameObject.AddComponent<LayoutElement>();
            separatorSize.preferredWidth = 24f;
            separatorSize.preferredHeight = 24f;
            separatorTemplate.gameObject.SetActive(false);

            // 지금 차례 칩
            RectTransform now = NewRect("Now", root);
            now.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            Image nowFill = RoundBox(now, Ink, 5f, 20f, White);
            HorizontalLayout(now, new RectOffset(24, 24, 0, 0), 0f, TextAnchor.MiddleCenter, true, true);
            TMP_Text nowText = NewText("Text", now, "나의 턴", 32f, Ink, true);

            var view = root.gameObject.AddComponent<TurnStripView>();
            Wire(view,
                ("rail", rail), ("chipTemplate", chipTemplate), ("separatorTemplate", separatorTemplate),
                ("nowChip", nowFill), ("nowText", nowText));
            root.gameObject.AddComponent<TurnStripPresenter>();
            return view;
        }

        // 플레이어 칩 템플릿 (시안 .pn2) — 루트 → Chapchu(분홍 테, 바깥 5) · Border(잉크 테) · Fill(노랑) · Dot(잉크 고리 → Fill 목걸이 색) · Name. 비활성.
        private static RectTransform BuildTurnChipTemplate(RectTransform rail)
        {
            RectTransform chip = NewRect("ChipTemplate", rail);
            chip.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            HorizontalLayout(chip, new RectOffset(14, 14, 0, 0), 8f, TextAnchor.MiddleLeft, true, false);

            Image chapchu = RoundImage("Chapchu", chip, Chu, 19f);
            Stretch(chapchu.rectTransform, -5f, -5f, -5f, -5f);
            chapchu.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            chapchu.gameObject.SetActive(false);

            Image border = RoundImage("Border", chip, Ink, 14f);
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            border.enabled = false;

            Image fill = RoundImage("Fill", chip, Turn, 10f);
            Stretch(fill.rectTransform, 4f, 4f, 4f, 4f);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            fill.gameObject.SetActive(false);

            Dot(chip, 16f, 3f, Act);
            NewText("Name", chip, "이름", 28f, Ink, true);

            chip.gameObject.SetActive(false);
            return chip;
        }

        // 설정(톱니) 버튼 — 우상 (32, 22) · 76 × 76. 서랍 · 팝업은 BuildMenuSheet 가 Sheets 층에 만든다.
        private static GameMenuView BuildGearButton(RectTransform parent)
        {
            RectTransform root = NewRect("Menu", parent);
            TopRight(root, EdgeX, EdgeTop + 2f, 76f, 76f);
            Button gear = BuildButton("GearButton", root, 76f, 76f, White, 22f, null, 0f, Ink, Hud("icon_gear"), 34f);
            Stretch((RectTransform)gear.transform);

            var view = root.gameObject.AddComponent<GameMenuView>();
            Wire(view, ("gearButton", gear), ("gearFill", gear.targetGraphic));
            root.gameObject.AddComponent<GameMenuPresenter>();
            return view;
        }

        // 방금 알림 칩 — 우상, 설정 버튼 왼쪽 (오른쪽 128 · 위 22). 칩은 오른쑥 정렬 세로 쌓임, 최대 2개 (시안 #feed · .fchip)
        private static FeedView BuildFeed(RectTransform parent)
        {
            RectTransform root = NewRect("Feed", parent);
            Anchor(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-(EdgeX + 96f), -(EdgeTop + 2f)), new Vector2(432f, 48f));
            VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // 전체가 탭 영역 — 칩이 없을 때도 눌리지 않게 칩 자체에만 레이캐스트

            RectTransform chip = NewRect("ChipTemplate", root);
            chip.gameObject.AddComponent<CanvasGroup>();
            Image chipFill = RoundBox(chip, Ink, 4f, 16f, White, true);
            LayoutElement chipSize = chip.gameObject.AddComponent<LayoutElement>();
            chipSize.preferredHeight = 48f;
            HorizontalLayout(chip, new RectOffset(12, 18, 0, 0), 10f, TextAnchor.MiddleLeft, true, false);

            Image icon = Icon("Icon", chip, Hud("icon_card"), 28f, Ink);
            LayoutElement iconSize = icon.gameObject.AddComponent<LayoutElement>();
            iconSize.preferredWidth = 24f;
            iconSize.preferredHeight = 28f;

            RectTransform miniCard = BuildMiniCard(chip, Act);
            miniCard.gameObject.SetActive(false);

            Image crown = Icon("Crown", chip, Hud("icon_crown"), 32f, White);
            LayoutElement crownSize = crown.gameObject.AddComponent<LayoutElement>();
            crownSize.preferredWidth = 32f;
            crownSize.preferredHeight = 26f;
            crown.gameObject.SetActive(false);

            NewText("Name", chip, "이름", 26f, Ink, true).gameObject.AddComponent<LayoutElement>(); // minWidth 는 FeedView 가 글에 맞춰 넣는다
            TMP_Text text = NewText("Text", chip, "카드 1장 뽑기", 26f, Ink, true);
            text.overflowMode = TextOverflowModes.Ellipsis;
            NewText("Value", chip, "−20", 26f, Hp, true);
            chip.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<FeedView>();
            Wire(view, ("chipTemplate", chip));
            return view;
        }

        // 종류 색 띠가 있는 작은 카드 (시안 .mc 14 × 19 → 28 × 38)
        private static RectTransform BuildMiniCard(Transform parent, Color kind)
        {
            RectTransform root = NewRect("MiniCard", parent);
            LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 28f;
            size.preferredHeight = 38f;
            Image border = RoundImage("Border", root, kind, 6f);
            Stretch(border.rectTransform);
            Image face = RoundImage("Face", root, White, 3f);
            Stretch(face.rectTransform, 3f, 3f, 3f, 3f);
            Image band = NewImage("Band", root, kind);
            band.rectTransform.anchorMin = new Vector2(0f, 1f);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            band.rectTransform.pivot = new Vector2(0.5f, 1f);
            band.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            band.rectTransform.sizeDelta = new Vector2(-6f, 10f);
            return root;
        }

        // 내 캐릭터 자리(160 × 280, 좌 72 · 아래 218) + 발밑 턴 타원 + 내 패널(472 × 178, 좌하 32 · 20) (시안 .mine2)
        private static MyPanelView BuildMyCharacterAndPanel(RectTransform parent)
        {
            RectTransform spot = BuildSpot("MyTurnSpot", parent);
            Anchor(spot, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(EdgeX + 120f, EdgeBottom + 178f + 28f), spot.sizeDelta);

            Image character = RoundImage("MyCharacter", parent, new Color(Fur2.r, Fur2.g, Fur2.b, 0.5f), 40f);
            BottomLeft(character.rectTransform, EdgeX + 40f, EdgeBottom + 178f + 20f, 160f, 280f);
            character.preserveAspect = true;

            // 스톱워치 — 내 캐릭터 오른쪽 (시안: left = 캐릭터 왼쪽 + 폭 × 0.98 + 6 + 23, top = 머리 + 키 × 0.3). 캐릭터처럼 아래 기준
            TurnTimerView timer = BuildStopwatch("TurnTimer", parent);
            Anchor((RectTransform)timer.transform, Vector2.zero, new Vector2(0.5f, 0.5f),
                new Vector2(EdgeX + 40f + 160f * 0.98f + StopwatchGap + StopwatchSize / 2f, EdgeBottom + 178f + 20f + 280f * (1f - StopwatchHeadRatio)), new Vector2(StopwatchSize, StopwatchSize));
            timer.gameObject.SetActive(false);

            Frame frame = BuildFrame("MyPanel", parent, 6f, 28f, Band, 68f, 5f);
            BottomLeft(frame.Root, EdgeX, EdgeBottom, 472f, 178f);

            HorizontalLayout(frame.Head, new RectOffset(20, 68, 0, 0), 10f, TextAnchor.MiddleLeft, true, false); // 오른쪽 48 은 손패 배지 자리
            Image dot = Dot(frame.Head, 20f, 3f, PlayerLook.MeColor);
            RectTransform nick = NewRect("Nick", frame.Head);
            HorizontalLayout(nick, new RectOffset(), 2f, TextAnchor.LowerLeft, true, true);
            // 머리 띠 레이아웃은 높이를 맡기지 않으므로 글 높이에 맞춘다 — 안 그러면 기본 높이 100 의 아래쪽(LowerLeft)에 글이 붙어 띠 아래로 내려간다.
            nick.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text nickname = NewText("NicknameText", nick, "두부", 38f, Ink, true);
            nickname.overflowMode = TextOverflowModes.Ellipsis;
            NewText("MeText", nick, "(나)", 28f, Ink, false);
            Image crown = Icon("Crown", frame.Head, Hud("icon_crown"), 40f, White);
            LayoutElement crownSize = crown.gameObject.AddComponent<LayoutElement>();
            crownSize.preferredWidth = 40f;
            crownSize.preferredHeight = 34f;
            crown.gameObject.SetActive(false);
            RectTransform spacer = NewRect("Spacer", frame.Head);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text status = NewText("StatusText", frame.Head, "기다리는 중", 26f, Ink2, false, TextAlignmentOptions.Right);
            status.textWrappingMode = TextWrappingModes.NoWrap; // 글 너비만큼만 — 비어 있으면 닉네임이 자리를 다 쓴다

            HorizontalLayout(frame.Body, new RectOffset(20, 22, 0, 0), 16f, TextAnchor.MiddleLeft, true, false);
            BoneGaugeView bone = BuildBone("Bone", frame.Body, 310f);
            TMP_Text hp = NewText("HpText", frame.Body, "100", 56f, Ink, true, TextAlignmentOptions.Left);
            hp.gameObject.AddComponent<LayoutElement>().minWidth = 92f;

            HandCountBadgeView badge = BuildHandBadge(frame.Root, true);

            var view = frame.Root.gameObject.AddComponent<MyPanelView>();
            Wire(view,
                ("band", frame.BandImage), ("dot", dot), ("nicknameText", nickname), ("crown", crown.gameObject), ("statusText", status),
                ("bone", bone), ("hpText", hp), ("handBadge", badge), ("turnSpot", spot.gameObject), ("turnTimer", timer.gameObject));
            frame.Root.gameObject.AddComponent<MyPanelPresenter>();

            // 발밑 타원은 내 턴에만 (MyPanelView.SetMyTurn 이 켜고 끈다). 기본 숨김
            spot.gameObject.SetActive(false);
            return view;
        }

        // 찹츄 버튼 = 외치는 말풍선 392 × 168, 우하 (32, 44) (시안 .chub)
        private static ChapchuButtonView BuildChapchuButton(RectTransform parent)
        {
            RectTransform root = NewRect("ChapchuButton", parent);
            BottomRight(root, EdgeX, EdgeBottom + 24f, 392f, 168f);

            Image shadow = RoundImage("Shadow", root, ChapchuOffShadow, 48f);
            Stretch(shadow.rectTransform, 0f, -10f, 0f, 10f);
            Image fill = RoundBox(root, Ink, 6f, 48f, White, true);

            // 꼬리 — 왼쪽 아래 (60, −20) 32 × 32 를 45° 돌린 마름모. 잉크 마름모 위에 바탕색 마름모를 6 위로 겹쳐 아래 두 변만 잉크가 보인다
            Image tailInk = NewImage("TailInk", root, Ink);
            Anchor(tailInk.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(76f, -4f), new Vector2(32f, 32f));
            tailInk.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image tailFill = NewImage("TailFill", root, White);
            Anchor(tailFill.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(76f, 2f), new Vector2(32f, 32f));
            tailFill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            RectTransform content = NewRect("Content", root);
            Stretch(content);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TMP_Text label = NewText("Label", content, "찹츄!", 68f, ChapchuOffLabel, true);

            RectTransform pipsRow = NewRect("Pips", content);
            HorizontalLayout(pipsRow, new RectOffset(), 8f, TextAnchor.MiddleCenter, true, true);
            var pips = new Image[10];
            for (int i = 0; i < pips.Length; i++)
            {
                RectTransform pip = NewRect($"Pip{i}", pipsRow);
                LayoutElement size = pip.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = 24f;
                size.preferredHeight = 24f;
                Image ring = RoundImage("Ring", pip, Ink, 12f);
                Stretch(ring.rectTransform);
                pips[i] = RoundImage("Fill", pip, White, 8f);
                Stretch(pips[i].rectTransform, 4f, 4f, 4f, 4f);
            }
            TMP_Text over = NewText("OverText", pipsRow, "+1", 30f, ChapchuOverInk, true);
            over.gameObject.SetActive(false);

            RectTransform readyTag = BuildChapchuTag(content, "ReadyTag", "지금 외치기!", false);
            RectTransform doneTag = BuildChapchuTag(content, "DoneTag", "내 턴까지 10장 지키기", true);
            readyTag.gameObject.SetActive(false);
            doneTag.gameObject.SetActive(false);

            Image em = Icon("EmIcon", root, Hud("icon_em"), 56f, Ink);
            Anchor(em.rectTransform, Vector2.one, new Vector2(1f, 1f), new Vector2(34f, 38f), new Vector2(56f, 56f));
            em.gameObject.SetActive(false);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.interactable = false;

            var view = root.gameObject.AddComponent<ChapchuButtonView>();
            Wire(view,
                ("button", button), ("fill", fill), ("tailFill", tailFill), ("shadow", shadow), ("label", label), ("pipsRow", pipsRow.gameObject),
                ("overText", over), ("readyTag", readyTag.gameObject), ("doneTag", doneTag.gameObject), ("emIcon", em.gameObject));
            WireArray(view, "pips", pips);
            return view;
        }

        // 찹츄 버튼 안 상태 태그 (시안 .chub .s) — 흰 바탕 테두리 4 · 높이 44 · 분홍 잉크 글자 28
        private static RectTransform BuildChapchuTag(Transform parent, string name, string text, bool crown)
        {
            RectTransform root = NewRect(name, parent);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            RoundBox(root, Ink, 4f, 14f, White);
            HorizontalLayout(root, new RectOffset(18, 18, 0, 0), 8f, TextAnchor.MiddleCenter, true, false);
            if (crown)
            {
                Image icon = Icon("Crown", root, Hud("icon_crown"), 34f, White);
                LayoutElement size = icon.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = 34f;
                size.preferredHeight = 28f;
            }
            NewText("Text", root, text, 28f, ChuInk, true);
            return root;
        }

        // 드롭 영역 — 손패 영역 위쪽 화면 전체 (10-ui.md 2-1 「드롭 영역」, 사용자 결정 2026-11-05). 투명 이미지는 디버그 때 색을 켜 보라고 둔다.
        // 레이캐스트는 끈다 — 판정은 HandPresenter 가 Rect 포함으로 하므로 덱 · 좌석 탭을 가리지 않는다. 위치 · 크기는 씬에서 바꾼다.
        private static RectTransform BuildDropZone(RectTransform parent)
        {
            Image image = NewImage("DropZone", parent, Color.clear);
            Stretch(image.rectTransform, 0f, 0f, 0f, HandAreaBottom + HandCardSize.y);
            return image.rectTransform;
        }

        #endregion

        #region 가운데 묶음

        // 대상 선택 중 화면을 어둡게 — 좌석보다 뒤. 캔버스가 1920 보다 넓어도 덮이게 크게 (시안 .dimmer)
        private static Image BuildDimmer(RectTransform center)
        {
            Image dimmer = NewImage("Dimmer", center, Dim);
            Anchor(dimmer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6000f, 6000f));
            dimmer.gameObject.SetActive(false);
            return dimmer;
        }

        // 좌석 자리 (시안 GEO × 2): 캐릭터 가운데 x · 머리 y · 키 · 폭(키 × 0.569) · 이름표 좌상 · 손 위치(그림 안 비율) · 말풍선 방향
        private readonly struct SeatGeo
        {
            public readonly float Cx, Top, Height, Width, PlateX, PlateY;
            public readonly Vector2 HandRatio;
            public readonly bool SayLeft;

            public SeatGeo(float cx, float top, float height, float plateX, float plateY, Vector2 handRatio, bool sayLeft)
            {
                Cx = cx; Top = top; Height = height; Width = Mathf.Round(height * 0.569f / 2f) * 2f;
                PlateX = plateX; PlateY = plateY; HandRatio = handRatio; SayLeft = sayLeft;
            }
        }

        private static readonly Vector2 HandRight = new Vector2(0.83f, 0.75f); // 시안 CHAR_HAND R
        private static readonly Vector2 HandLeft = new Vector2(0.26f, 0.74f);  // 시안 CHAR_HAND L
        private static readonly SeatGeo[] Seats =
        {
            new SeatGeo(260f, 300f, 300f, 104f, 168f, HandRight, false),
            new SeatGeo(960f, 120f, 252f, 1060f, 136f, HandRight, true),
            new SeatGeo(1660f, 300f, 300f, 1504f, 168f, HandLeft, false),
        };

        private static OpponentSeatsPresenter BuildOpponentSeats(RectTransform center, DeckView deck)
        {
            RectTransform area = NewRect("OpponentSeats", center);
            Stretch(area);

            var seats = new OpponentSeatView[Seats.Length];
            for (int i = 0; i < seats.Length; i++)
                seats[i] = BuildOpponentSeat(area, i, deck);

            var presenter = area.gameObject.AddComponent<OpponentSeatsPresenter>();
            WireArray(presenter, "seats", seats);
            return presenter;
        }

        // 좌석 하나 — 발밑(턴 타원 · 대상 고리) → 캐릭터 자리 → 뒷면 부채꼴 → 이름표(판 328) → 말풍선. 좌석 전체를 덮는 탭 영역은 맨 앞.
        private static OpponentSeatView BuildOpponentSeat(RectTransform area, int slot, DeckView deck)
        {
            SeatGeo g = Seats[slot];
            int seatIndex = slot + 1;
            Color seatColor = PlayerLook.SeatColors[slot];

            RectTransform root = NewRect($"Seat{seatIndex}", area);
            Stretch(root);

            float feetY = g.Top + g.Height - 12f;
            RectTransform spot = BuildSpot("TurnSpot", root);
            CenterAt(spot, g.Cx, feetY, spot.sizeDelta.x, spot.sizeDelta.y);
            spot.gameObject.SetActive(false);

            RectTransform ring = NewRect("PickRing", root);
            CenterAt(ring, g.Cx, feetY, 200f, 52f);
            Image ringOuter = RoundImage("Outer", ring, Tgt, 26f);
            Stretch(ringOuter.rectTransform);
            Image ringInner = RoundImage("Inner", ring, new Color(1f, 1f, 1f, 0.4f), 20f);
            Stretch(ringInner.rectTransform, 6f, 6f, 6f, 6f);
            ring.gameObject.SetActive(false);

            Image character = RoundImage("Character", root, new Color(Fur2.r, Fur2.g, Fur2.b, 0.5f), 40f);
            TopLeft(character.rectTransform, g.Cx - g.Width / 2f, g.Top, g.Width, g.Height);
            character.preserveAspect = true;

            // 뒷면 부채꼴 축 — 손 위치(그림 안 비율) + 6 아래, 카드 피벗(아래 바깥 0.6h)까지 16.2 더 아래 (시안 .fback transform-origin 50% 160%)
            float handX = g.Cx - g.Width / 2f + g.Width * g.HandRatio.x;
            float handY = g.Top + g.Height * g.HandRatio.y + 12f + BackCardSize.y * 0.6f;
            OpponentHandView hand = BuildOpponentHand(root, new Vector2(handX, handY), deck);

            Frame plate = BuildFrame("Plate", root, 5f, 24f, Band, 56f, 4f);
            TopLeft(plate.Root, g.PlateX, g.PlateY, 328f, 136f);

            HorizontalLayout(plate.Head, new RectOffset(16, 60, 0, 0), 10f, TextAnchor.MiddleLeft, true, false);
            Image dot = Dot(plate.Head, 18f, 3f, seatColor);
            TMP_Text nickname = NewText("NicknameText", plate.Head, $"Player{seatIndex + 1}", 32f, Ink, true, TextAlignmentOptions.Left);
            nickname.overflowMode = TextOverflowModes.Ellipsis;
            nickname.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Image crown = Icon("Crown", plate.Head, Hud("icon_crown"), 36f, White);
            LayoutElement crownSize = crown.gameObject.AddComponent<LayoutElement>();
            crownSize.preferredWidth = 36f;
            crownSize.preferredHeight = 30f;
            crown.gameObject.SetActive(false);

            HorizontalLayout(plate.Body, new RectOffset(14, 16, 0, 0), 12f, TextAnchor.MiddleLeft, true, false);
            BoneGaugeView bone = BuildBone("Bone", plate.Body, 210f);
            TMP_Text hp = NewText("HpText", plate.Body, "100", 40f, Ink, true, TextAlignmentOptions.Left);
            hp.gameObject.AddComponent<LayoutElement>().minWidth = 66f;

            HandCountBadgeView badge = BuildHandBadge(plate.Root, false);

            // 대상 선택 테 — 판 바깥 8 에 두께 5 빨강 (시안 .plate.pick outline). 기본 숨김
            RectTransform pickOutline = NewRect("PickOutline", plate.Root);
            Stretch(pickOutline, -8f, -8f, -8f, -8f);
            BuildEdgeFrame("Frame", pickOutline, Tgt, 5f);
            pickOutline.gameObject.SetActive(false);

            RectTransform shout = BuildShout(root, g);
            shout.gameObject.SetActive(false);

            // 스톱워치 — 캐릭터 옆 머리 높이(키 × 0.3). 위 좌석(2)만 왼쪽, 나머지는 오른쪽 (시안 .swatch: 좌석 가장자리에서 6 띄움)
            float watchX = g.SayLeft
                ? g.Cx - g.Width / 2f + g.Width * 0.04f - StopwatchGap - StopwatchSize / 2f
                : g.Cx - g.Width / 2f + g.Width * 0.98f + StopwatchGap + StopwatchSize / 2f;
            TurnTimerView timer = BuildStopwatch("TurnTimer", root);
            CenterAt((RectTransform)timer.transform, watchX, g.Top + g.Height * StopwatchHeadRatio, StopwatchSize, StopwatchSize);
            timer.gameObject.SetActive(false);

            // 탭 영역 — 캐릭터 + 이름표를 덮는 투명 이미지. 레이캐스트는 대상 선택 중에만 켠다 (덱과 겹치므로 평소엔 덱 탭을 막지 않게).
            Image tapArea = NewImage("TapArea", root, Color.clear);
            float left = Mathf.Min(g.Cx - g.Width / 2f, g.PlateX) - 16f;
            float right = Mathf.Max(g.Cx + g.Width / 2f, g.PlateX + 328f) + 16f;
            float top = g.Top - 16f;
            float bottom = Mathf.Max(g.Top + g.Height, g.PlateY + 136f) + 16f;
            TopLeft(tapArea.rectTransform, left, top, right - left, bottom - top);

            var view = root.gameObject.AddComponent<OpponentSeatView>();
            Wire(view,
                ("band", plate.BandImage), ("dot", dot), ("nicknameText", nickname), ("crown", crown.gameObject), ("bone", bone), ("hpText", hp), ("handBadge", badge),
                ("character", character), ("turnSpot", spot.gameObject), ("turnTimer", timer.gameObject), ("pickRing", ring.gameObject), ("pickOutline", pickOutline.gameObject),
                ("shout", shout.gameObject), ("hand", hand), ("tapArea", tapArea));
            return view;
        }

        // 스톱워치 (시안 .swatch × 2): 92 원 · 잉크 테 6 · 그 안 진행 링(r 28~40, 바탕 #efe8de 위에 턴 색이 위에서 시계 방향) · 안쪽 흰 원(테 안쪽에서 12 · 테 4) · 남은 초 30 B.
        // 2시 방향 네모 버튼 26 × 20(테 5 · 반지름 6 · 42° 기울임)은 원 뒤에. 위치 · 켜고 끄기는 부르는 쪽이.
        private const float StopwatchSize = 92f;
        private const float StopwatchGap = 12f;          // 캐릭터 가장자리에서 띄우는 간격 (시안 6 × 2)
        private const float StopwatchHeadRatio = 0.3f;   // 캐릭터 머리 위에서 키의 이 비율 높이에 가운데를 맞춘다
        private static readonly Color RingTrack = Hex(0xefe8de);

        private static TurnTimerView BuildStopwatch(string name, Transform parent)
        {
            RectTransform root = NewRect(name, parent);
            root.sizeDelta = new Vector2(StopwatchSize, StopwatchSize);

            RectTransform button = NewRect("Button", root);
            CenterAt(button, 87f, 10f, 26f, 20f); // 시안 right −7 · top −3 (안쪽 상자 기준, × 2) → 가운데 (87, 10)
            button.localRotation = Quaternion.Euler(0f, 0f, -42f);
            RoundBox(button, Ink, 5f, 6f, White);

            RoundBox(root, Ink, 6f, StopwatchSize / 2f, White);

            Sprite ringSprite = Hud("timer_ring");
            Image track = NewImage("Track", root, RingTrack, ringSprite);
            Stretch(track.rectTransform, 6f, 6f, 6f, 6f);
            Image ring = NewImage("Ring", root, Turn, ringSprite);
            Stretch(ring.rectTransform, 6f, 6f, 6f, 6f);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 1f;

            RectTransform face = NewRect("Face", root);
            Stretch(face, 18f, 18f, 18f, 18f); // 시안 inset 6 은 테 안쪽 기준 → 테 6 + 12
            RoundBox(face, Ink, 4f, StopwatchSize / 2f - 18f, White);

            TMP_Text seconds = NewText("Text", root, "20", 30f, Ink, true);
            Stretch(seconds.rectTransform);

            var view = root.gameObject.AddComponent<TurnTimerView>();
            Wire(view, ("ring", ring), ("secondsText", seconds));
            return view;
        }

        // 발밑 노란 타원 184 × 44 + 흰 테 6 (시안 .spot)
        private static RectTransform BuildSpot(string name, Transform parent)
        {
            RectTransform root = NewRect(name, parent);
            root.sizeDelta = new Vector2(196f, 56f);
            Image white = RoundImage("White", root, White, 28f);
            Stretch(white.rectTransform);
            Image fill = RoundImage("Fill", root, Turn, 22f);
            Stretch(fill.rectTransform, 6f, 6f, 6f, 6f);
            return root;
        }

        // 찹츄 말풍선 (시안 .shout) — 캐릭터 옆, 아래 끝이 (cx ± 0.3w, top + 60). 오른쪽 방향은 왼쪽 18% 지점이 기준, 왼쪽 방향은 82%.
        private static RectTransform BuildShout(Transform parent, SeatGeo g)
        {
            RectTransform root = NewRect("Shout", parent);
            float x = g.SayLeft ? g.Cx - g.Width * 0.3f : g.Cx + g.Width * 0.3f;
            Anchor(root, new Vector2(0f, 1f), new Vector2(g.SayLeft ? 0.82f : 0.18f, 0f), new Vector2(x, -(g.Top + 60f)), new Vector2(190f, 64f));

            Image fill = RoundBox(root, Ink, 5f, 28f, Chu);
            float tailX = g.SayLeft ? 190f - 28f - 12f : 28f + 12f;
            Image tailInk = NewImage("TailInk", root, Ink);
            Anchor(tailInk.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(tailX, -6f), new Vector2(24f, 24f));
            tailInk.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image tailFill = NewImage("TailFill", root, Chu);
            Anchor(tailFill.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(tailX, -1f), new Vector2(24f, 24f));
            tailFill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            TMP_Text text = NewText("Text", fill.transform, "찹츄!", 38f, White, true);
            Stretch(text.rectTransform);
            return root;
        }

        // 대상 선택 안내 바 (시안 .ask) — 가운데 묶음 위에서 784, 가운데 정렬. 「카드 · 누구에게 쓸까요?」 + 남은 초 + 취소
        private static TargetAskView BuildTargetAsk(RectTransform center, Image dimmer)
        {
            RectTransform bar = NewRect("TargetAsk", center);
            Anchor(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -784f), new Vector2(600f, 112f));
            RoundBox(bar, Ink, 6f, 32f, White);
            HorizontalLayout(bar, new RectOffset(32, 14, 12, 12), 20f, TextAnchor.MiddleLeft, true, true);
            FitBoth(bar.gameObject);

            BuildMiniCard(bar, Act);
            TMP_Text question = NewText("QuestionText", bar, "<color=#009dff>카드</color> · 누구에게 쓸까요?", 30f, Ink, false);
            TMP_Text seconds = NewText("SecondsText", bar, "5초", 34f, Hp, true);
            Button cancel = BuildButton("CancelButton", bar, 0f, 76f, White, 22f, "취소", 30f, Ink);

            var view = bar.gameObject.AddComponent<TargetAskView>();
            Wire(view, ("dimmer", dimmer.gameObject), ("bar", bar.gameObject), ("questionText", question), ("secondsText", seconds), ("cancelButton", cancel));
            bar.gameObject.SetActive(false);
            return view;
        }

        #endregion

        #region 서랍 · 팝업

        // 서랍 틀 (시안 .sheet) — 우상 (32, 136) · 폭 484 · 머리 띠 68 · 닫기 ✕ 는 오른쪽 위 모서리 밖
        private static Frame BuildSheet(RectTransform parent, string name, Sprite icon, string title, Color bandColor, out Button close)
        {
            Frame frame = BuildFrame(name, parent, 6f, 24f, bandColor, 68f, 4f);
            TopRight(frame.Root, EdgeX, EdgeTop + 116f, 484f, 300f);
            frame.Root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalLayout(frame.Head, new RectOffset(24, 80, 0, 0), 12f, TextAnchor.MiddleLeft, true, false);
            Image headIcon = Icon("Icon", frame.Head, icon, 36f, Ink);
            LayoutElement iconSize = headIcon.gameObject.AddComponent<LayoutElement>();
            iconSize.preferredWidth = 36f;
            iconSize.preferredHeight = 36f;
            NewText("Title", frame.Head, title, 34f, Ink, true);

            close = BuildButton("CloseButton", frame.Root, 60f, 60f, White, 30f, null, 0f, Ink, Hud("icon_x"), 20f, 4f);
            Anchor((RectTransform)close.transform, Vector2.one, new Vector2(1f, 1f), new Vector2(20f, 24f), new Vector2(60f, 60f));
            close.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            VerticalLayoutGroup body = frame.Body.gameObject.AddComponent<VerticalLayoutGroup>();
            body.padding = new RectOffset(24, 20, 20, 20);
            body.spacing = 14f;
            body.childAlignment = TextAnchor.UpperLeft;
            body.childControlWidth = true;
            body.childControlHeight = true;
            body.childForceExpandWidth = true;
            body.childForceExpandHeight = false;
            return frame;
        }

        // 전체 기록 서랍 — 턴 머리글 · 항목 줄 템플릿 + 스크롤 (시안 .log, 최대 높이 456)
        private static LogSheetView BuildLogSheet(RectTransform parent)
        {
            Frame frame = BuildSheet(parent, "LogSheet", Hud("icon_list"), "기록", Band, out Button close);

            RectTransform viewport = NewRect("Viewport", frame.Body);
            viewport.gameObject.AddComponent<LayoutElement>().preferredHeight = 456f;
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportHit = viewport.gameObject.AddComponent<Image>(); // 스크롤 드래그를 받는 투명 면
            viewportHit.color = Color.clear;

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 턴 머리글 — 위 점선(4, 선 색) + 글자 28 회색
            RectTransform header = NewRect("HeaderTemplate", content);
            VerticalLayoutGroup headerLayout = header.gameObject.AddComponent<VerticalLayoutGroup>();
            headerLayout.spacing = 10f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;
            Image line = NewImage("Line", header, Line);
            line.gameObject.AddComponent<LayoutElement>().preferredHeight = 4f;
            NewText("Text", header, "나의 턴", 28f, Ink2, true, TextAlignmentOptions.Left);
            header.gameObject.SetActive(false);

            // 항목 — 「이름 · 무엇을」 28 + 빨간 수치 30
            RectTransform entry = NewRect("EntryTemplate", content);
            HorizontalLayout(entry, new RectOffset(), 14f, TextAnchor.MiddleLeft, true, true);
            TMP_Text entryText = NewText("Text", entry, "이름 · 카드 1장 뽑기", 28f, Ink, false, TextAlignmentOptions.Left);
            entryText.textWrappingMode = TextWrappingModes.Normal;
            entryText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            NewText("Value", entry, "−20", 30f, Hp, true, TextAlignmentOptions.Right);
            entry.gameObject.SetActive(false);

            var view = frame.Root.gameObject.AddComponent<LogSheetView>();
            Wire(view, ("closeButton", close), ("content", content), ("headerTemplate", header), ("entryTemplate", entry), ("scroll", scroll));
            frame.Root.gameObject.SetActive(false);
            return view;
        }

        // 설정 서랍 (「게임 나가기」만 — 배경 음악 · 효과음은 다음 작업) + 나가기 확인 팝업 (시안 .pop2.bad)
        private static void BuildMenuSheet(RectTransform parent, GameMenuView menu)
        {
            Frame sheet = BuildSheet(parent, "MenuSheet", Hud("icon_gear"), "설정", Band, out Button close);
            Button exit = BuildButton("ExitButton", sheet.Body, 0f, 72f, WarnFill, 22f, "게임 나가기", 30f, Ink);
            exit.GetComponent<LayoutElement>().flexibleWidth = 1f;
            sheet.Root.gameObject.SetActive(false);

            RectTransform confirm = NewRect("ExitConfirm", parent);
            Stretch(confirm);
            Image popDim = NewImage("PopDim", confirm, Dim, null, true); // 바탕 탭을 먹어 뒤가 눌리지 않게 — 눌러도 닫히지 않는다 (시안)
            Anchor(popDim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6000f, 6000f));

            Frame pop = BuildFrame("Popup", confirm, 6f, 24f, Tgt, 68f, 4f);
            Anchor(pop.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 300f));
            pop.Root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HorizontalLayout(pop.Head, new RectOffset(24, 24, 0, 0), 12f, TextAnchor.MiddleLeft, true, false);
            Image alert = Icon("Icon", pop.Head, Hud("icon_em"), 36f, Ink);
            LayoutElement alertSize = alert.gameObject.AddComponent<LayoutElement>();
            alertSize.preferredWidth = 36f;
            alertSize.preferredHeight = 36f;
            NewText("Title", pop.Head, "게임 나가기", 36f, Ink, true);

            VerticalLayoutGroup body = pop.Body.gameObject.AddComponent<VerticalLayoutGroup>();
            body.padding = new RectOffset(32, 32, 28, 32);
            body.spacing = 20f;
            body.childControlWidth = true;
            body.childControlHeight = true;
            body.childForceExpandWidth = true;
            body.childForceExpandHeight = false;
            NewText("Question", pop.Body, "게임에서 나갈까요?", 40f, Ink, true, TextAlignmentOptions.Left);
            TMP_Text note = NewText("Note", pop.Body, "나가면 이 게임에 돌아올 수 없어요.", 30f, Ink, false, TextAlignmentOptions.Left);
            note.textWrappingMode = TextWrappingModes.Normal;

            RectTransform row = NewRect("Buttons", pop.Body);
            HorizontalLayout(row, new RectOffset(0, 0, 8, 0), 20f, TextAnchor.MiddleCenter, true, true);
            Button cancel = BuildButton("CancelButton", row, 0f, 88f, White, 22f, "취소", 36f, Ink);
            cancel.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Button confirmExit = BuildButton("ExitButton", row, 0f, 88f, Tgt, 22f, "나가기", 36f, White);
            confirmExit.GetComponent<LayoutElement>().flexibleWidth = 1f;
            confirm.gameObject.SetActive(false);

            Wire(menu,
                ("sheet", sheet.Root.gameObject), ("closeButton", close), ("exitButton", exit),
                ("confirm", confirm.gameObject), ("confirmCancelButton", cancel), ("confirmExitButton", confirmExit));
        }

        #endregion

        #region 부품

        // 판 = 카드 한 장 (시안 .frame): 잉크 테두리 → 흰 몸통 → 머리 띠(위 모서리만 둥글게 — RectMask2D 로 아래를 잘라낸다) → 띠 아래 잉크 선 → Head(띠 안 내용) · Body(그 아래).
        // 루트는 VerticalLayoutGroup — 고정 크기면 Body 가 남은 높이를 채우고, ContentSizeFitter 를 붙이면 Body 내용 높이에 맞춰진다.
        private readonly struct Frame
        {
            public readonly RectTransform Root;
            public readonly Image BandImage;
            public readonly RectTransform Head;
            public readonly RectTransform Body;

            public Frame(RectTransform root, Image band, RectTransform head, RectTransform body)
            {
                Root = root; BandImage = band; Head = head; Body = body;
            }
        }

        private static Frame BuildFrame(string name, Transform parent, float border, float radius, Color bandColor, float bandHeight, float bandLine)
        {
            RectTransform root = NewRect(name, parent);
            VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(Mathf.RoundToInt(border), Mathf.RoundToInt(border), Mathf.RoundToInt(border + bandHeight + bandLine), Mathf.RoundToInt(border));
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            Image fill = RoundBox(root, Ink, border, radius, White);

            RectTransform bandMask = NewRect("BandMask", root);
            bandMask.anchorMin = new Vector2(0f, 1f);
            bandMask.anchorMax = new Vector2(1f, 1f);
            bandMask.pivot = new Vector2(0.5f, 1f);
            bandMask.anchoredPosition = new Vector2(0f, -border);
            bandMask.sizeDelta = new Vector2(-border * 2f, bandHeight);
            bandMask.gameObject.AddComponent<RectMask2D>();
            bandMask.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image band = RoundImage("Band", bandMask, bandColor, radius - border);
            Stretch(band.rectTransform, 0f, 0f, 0f, -(radius - border));

            Image line = NewImage("BandLine", root, Ink);
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.anchoredPosition = new Vector2(0f, -(border + bandHeight));
            line.rectTransform.sizeDelta = new Vector2(-border * 2f, bandLine);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform head = NewRect("Head", root);
            head.anchorMin = new Vector2(0f, 1f);
            head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.anchoredPosition = new Vector2(0f, -border);
            head.sizeDelta = new Vector2(-border * 2f, bandHeight);
            head.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform body = NewRect("Body", root);
            return new Frame(root, band, head, body);
        }

        // HP 뼈다귀 (시안 bone2, viewBox −3 −3 126 × 28 → 폭 width). 잉크 외곽선(원 4 + 막대) → 끝 마디 4(색) → 바탕 막대(연분홍) → 채움 막대(앵커 비율).
        private static BoneGaugeView BuildBone(string name, Transform parent, float width)
        {
            float s = width / 126f;
            RectTransform root = NewRect(name, parent);
            root.sizeDelta = new Vector2(width, 28f * s);
            LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = width;
            size.preferredHeight = 28f * s;

            Vector2 P(float x, float y) => new Vector2((x + 3f) * s, (y + 3f) * s); // viewBox → 왼쪽 위 기준 px
            Vector2[] caps = { new Vector2(8f, 7f), new Vector2(8f, 15f), new Vector2(112f, 7f), new Vector2(112f, 15f) };

            foreach (Vector2 c in caps)
                Circle("Ink", root, P(c.x, c.y), 19f * s, Ink);
            Image inkBar = NewImage("InkBar", root, Ink);
            TopLeft(inkBar.rectTransform, P(5.5f, 2.5f).x, P(5.5f, 2.5f).y, 109f * s, 17f * s);

            var capImages = new Image[caps.Length];
            for (int i = 0; i < caps.Length; i++)
                capImages[i] = Circle($"Cap{i}", root, P(caps[i].x, caps[i].y), 14f * s, HpBg);

            Image bg = NewImage("Track", root, HpBg);
            TopLeft(bg.rectTransform, P(8f, 5f).x, P(8f, 5f).y, 104f * s, 12f * s);
            Image fill = NewImage("Fill", bg.transform, Hex(0x3cbf72));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var view = root.gameObject.AddComponent<BoneGaugeView>();
            Wire(view, ("fill", fill.rectTransform), ("fillImage", fill));
            WireArray(view, "leftCaps", new Object[] { capImages[0], capImages[1] });
            WireArray(view, "rightCaps", new Object[] { capImages[2], capImages[3] });
            return view;
        }

        // 손패 배지 (시안 .hcard2) — 판 오른쑥 위 모서리 밖에 카드 2장: 뒤 흰 카드(−12°) + 앞 카드(색 띠 + 장수). big = 내 패널
        private static HandCountBadgeView BuildHandBadge(RectTransform plate, bool big)
        {
            float w = big ? 82f : 64f, h = big ? 94f : 72f, border = big ? 5f : 4f, radius = big ? 11f : 9f;
            float backW = big ? 64f : 50f, backH = big ? 86f : 66f, backTop = big ? 8f : 6f;
            float frontW = big ? 70f : 54f, frontH = big ? 92f : 70f, bandH = big ? 16f : 12f;
            float font = big ? 50f : 38f, wideFont = big ? 42f : 32f;

            RectTransform root = NewRect("HandBadge", plate);
            Anchor(root, Vector2.one, new Vector2(1f, 1f), new Vector2(big ? 26f : 22f, big ? 40f : 34f), new Vector2(w, h));
            root.localRotation = Quaternion.Euler(0f, 0f, big ? -5f : -6f);
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform back = NewRect("Back", root);
            TopLeft(back, 0f, backTop, backW, backH);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = new Vector2(backW / 2f, -(backTop + backH / 2f));
            back.localRotation = Quaternion.Euler(0f, 0f, 12f);
            RoundBox(back, Ink, border, radius, White);

            RectTransform front = NewRect("Front", root);
            Anchor(front, Vector2.one, Vector2.one, Vector2.zero, new Vector2(frontW, frontH));
            Image face = RoundBox(front, Ink, border, radius, White);
            Image band = RoundImage("Band", front, Act, radius - border);
            band.rectTransform.anchorMin = new Vector2(0f, 1f);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            band.rectTransform.pivot = new Vector2(0.5f, 1f);
            band.rectTransform.anchoredPosition = new Vector2(0f, -border);
            band.rectTransform.sizeDelta = new Vector2(-border * 2f, bandH);
            TMP_Text count = NewText("CountText", front, "5", font, Ink, true);
            Stretch(count.rectTransform, border, border + bandH, border, border);

            var view = root.gameObject.AddComponent<HandCountBadgeView>();
            Wire(view, ("countText", count), ("band", band), ("face", face));
            SetFloat(view, "wideFontSize", wideFont);
            return view;
        }

        // 상대 손패 부채꼴 — 뒷면 36 × 54, 피벗 아래 바깥(0.5, −0.6)을 축으로 회전 (시안 .fback). 새 뒷면은 덱 자리에서 날아온다.
        private static readonly Vector2 BackCardSize = new Vector2(36f, 54f);

        private static OpponentHandView BuildOpponentHand(RectTransform seat, Vector2 pivotPoint, DeckView deck)
        {
            RectTransform fan = NewRect("HandFan", seat);
            CenterAt(fan, pivotPoint.x, pivotPoint.y, 0f, 0f);

            var view = fan.gameObject.AddComponent<OpponentHandView>();
            Wire(view, ("backCardPrefab", EnsureBackCardPrefab()), ("drawOrigin", deck.transform));
            return view;
        }

        // 뒷면 카드 프리팹 — 카드 뒷면 그림 + 잉크 테 3, 모서리 6, 피벗 (0.5, −0.6). 빌더 산출물이라 늘 다시 만든다 (GUID 유지).
        private static RectTransform EnsureBackCardPrefab()
        {
            if (_backCardPrefab != null)
                return _backCardPrefab;

            string path = $"{HandCardPrefabFolder}/HandCard_Back.prefab";
            if (!AssetDatabase.IsValidFolder(HandCardPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefab/UI", "GameScreen");

            RectTransform root = NewRect("HandCard_Back", null);
            root.sizeDelta = BackCardSize;
            root.pivot = new Vector2(0.5f, -0.6f);

            Image ring = RoundImage("Ring", root, Ink, 6f);
            Stretch(ring.rectTransform, -3f, -3f, -3f, -3f);
            Image face = RoundImage("Face", root, White, 6f);
            Stretch(face.rectTransform);
            var backSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CardBackSpritePath);
            if (backSprite != null)
            {
                Image art = NewImage("Art", root, White, backSprite);
                Stretch(art.rectTransform, 2f, 2f, 2f, 2f);
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return _backCardPrefab = (RectTransform)saved.transform;
        }

        // 누르는 버튼 (시안 .btn) — 잉크 테 5 · 아래 두께(그림자) 6 · 둥근 모서리. width 0 이면 레이아웃 그룹이 폭을 정한다 (안여백 28).
        private static Button BuildButton(string name, Transform parent, float width, float height, Color fillColor, float radius, string label, float fontSize, Color textColor,
            Sprite icon = null, float iconSize = 0f, float shadow = 6f)
        {
            RectTransform root = NewRect(name, parent);
            LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = height;
            if (width > 0f)
                size.preferredWidth = width;
            root.sizeDelta = new Vector2(width > 0f ? width : 200f, height);
            HorizontalLayout(root, new RectOffset(label != null ? 28 : 0, label != null ? 28 : 0, 0, 0), 12f, TextAnchor.MiddleCenter, true, false);

            Image shadowImage = RoundImage("Shadow", root, Ink, radius);
            Stretch(shadowImage.rectTransform, 0f, -shadow, 0f, shadow);
            shadowImage.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image fill = RoundBox(root, Ink, 5f, radius, fillColor, true);

            if (icon != null)
            {
                Image image = Icon("Icon", root, icon, iconSize, textColor);
                LayoutElement iconLayout = image.gameObject.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = iconSize;
                iconLayout.preferredHeight = iconSize;
            }
            if (label != null)
                NewText("Label", root, label, fontSize, textColor, true);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            return button;
        }

        // 목걸이 색 점 — 잉크 고리(ring) 안에 색 원. 구조: Dot(잉크) → Fill(색). 돌려주는 Image 는 Fill.
        // 높이를 맡기지 않는 가로 레이아웃(머리 띠 · 턴 칩) 안에서도 원이 되게 Rect 크기도 지름으로 둔다 — 안 그러면 새 Rect 기본 높이 100 으로 늘어난 알약이 된다.
        private static Image Dot(Transform parent, float size, float ring, Color color)
        {
            float diameter = size + ring * 2f;
            RectTransform root = NewRect("Dot", parent);
            root.sizeDelta = new Vector2(diameter, diameter);
            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = diameter;
            layout.preferredHeight = diameter;
            Image ink = RoundImage("Ink", root, Ink, diameter / 2f);
            Stretch(ink.rectTransform);
            Image fill = RoundImage("Fill", root, color, size / 2f);
            Stretch(fill.rectTransform, ring, ring, ring, ring);
            return fill;
        }

        private static Image Circle(string name, Transform parent, Vector2 centerTopLeft, float diameter, Color color)
        {
            Image image = RoundImage(name, parent, color, diameter / 2f);
            CenterAt(image.rectTransform, centerTopLeft.x, centerTopLeft.y, diameter, diameter);
            return image;
        }

        private static Image Icon(string name, Transform parent, Sprite sprite, float size, Color color)
        {
            Image image = NewImage(name, parent, color, sprite);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.preserveAspect = true;
            return image;
        }

        /// <summary>둥근 모서리 사각형 — ui_round64(160px · 테두리 64) 를 Sliced 로, 모서리 반지름은 pixelsPerUnitMultiplier(64 / r). 크기의 절반 이상이면 원 · 알약.</summary>
        private static Image RoundImage(string name, Transform parent, Color color, float radius, bool raycastTarget = false)
        {
            Image image = NewImage(name, parent, color, Round, raycastTarget);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 64f / Mathf.Max(radius, 1f);
            return image;
        }

        /// <summary>잉크 테두리 + 안쪽 채움, 둥근 모서리. 부모를 꽉 채우며 안쪽 채움 Image 를 돌려준다. 레이아웃 그룹 안에서는 둘 다 ignoreLayout.</summary>
        private static Image RoundBox(Transform parent, Color borderColor, float borderWidth, float radius, Color fillColor, bool raycastTarget = false)
        {
            Image border = RoundImage("Border", parent, borderColor, radius);
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image fill = RoundImage("Fill", parent, fillColor, Mathf.Max(radius - borderWidth, 1f), raycastTarget);
            Stretch(fill.rectTransform, borderWidth, borderWidth, borderWidth, borderWidth);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return fill;
        }

        private static Sprite Hud(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{HudSpriteFolder}/{name}.png");
            if (sprite == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] HUD 스프라이트가 없다: {HudSpriteFolder}/{name}.png");
            return sprite;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            // 프로젝트는 옛 Input Manager 를 쓴다 (다른 씬과 같은 StandaloneInputModule)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        #endregion

        #region 손패 (uGUI 카드 — 카드 이펙트 씬용)

        // 손패 카드 226 × 336 — 카드 틀 이미지(820 × 1222) 비율, 높이는 손패 영역. 드롭 영역은 그 위(28 + 336)부터 (10-ui.md 2-1 「손패」).
        private static readonly Vector2 HandCardSize = new Vector2(226f, 336f);
        private const float HandAreaBottom = 28f;
        internal const string ActionCardFramePath = "Assets/Sprites/Cards/ActionCard.png";
        internal const string CounterCardFramePath = "Assets/Sprites/Cards/CounterCard.png";
        internal const string TrapCardFramePath = "Assets/Sprites/Cards/TrapCard.png";

        // 손패 카드 프리팹 — Assets/Prefab/PlayerActionCard Dummy 와 같은 구성(틀 · 그림 · 이름 · 설명)을 UGUI 로.
        // 종류마다 틀 이미지만 다르다. 위치는 더미의 월드 좌표를 틀 크기 비율로 옮긴 값. 이미 있으면 덮어쓰지 않는다 (사람이 고친 것 보존) — overwrite 는 RebuildHandCardPrefabs 만.
        // 구조는 루트(이동 · z 회전 · 호버 확대) → Face(앞 · 뒤 — 뒤집기 연출용 Y 회전 자리, 아직 안 씀) → 틀 · 그림 · 글자.
        // 포인터는 틀 이미지만 받는다 — 그림 · 글자는 raycastTarget 을 끈다 (HandCardView 가 루트에서 이벤트를 받는다).
        private static HandCardView EnsureHandCardPrefab(string name, string framePath, bool overwrite = false)
        {
            string path = $"{HandCardPrefabFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<HandCardView>(path);
            if (existing != null && !overwrite)
                return existing;

            if (!AssetDatabase.IsValidFolder(HandCardPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefab/UI", "GameScreen");

            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
            if (frameSprite == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 카드 틀 이미지가 없다: {framePath}");

            RectTransform root = NewRect(name, null);
            root.sizeDelta = HandCardSize;
            root.pivot = new Vector2(0.5f, 0f);
            root.gameObject.AddComponent<CanvasGroup>(); // 잠금 중 레이캐스트 끄기 (HandCardView.SetRaycastBlocking)

            RectTransform face = NewRect("Face", root);
            Stretch(face);

            // 그리는 순서는 더미와 같다 — 틀 → 그림 → 이름 · 설명.
            Image frame = NewImage("CardImage", face, White, frameSprite, true);
            Stretch(frame.rectTransform);

            Image art = NewImage("Image", face, White);
            AnchorBox(art.rectTransform, 0.1925f, 0.389f, 0.8075f, 0.801f); // 그림 칸 (점선 위 · 이름 띠 아래)
            art.preserveAspect = true;
            art.enabled = false;

            TMP_Text nameText = NewText("Name", face, "Name", 18f, White, true, TextAlignmentOptions.Left);
            AnchorBox(nameText.rectTransform, 0.3256f, 0.8482f, 0.8896f, 0.9502f); // 위쪽 띠
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 12f;
            nameText.fontSizeMax = 18f;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            // 더미는 흰 글자인데 흰 바탕 위라 보이지 않아 잉크로 쓴다.
            TMP_Text description = NewText("Description", face, "Description", 15f, Ink, false, TextAlignmentOptions.Top);
            AnchorBox(description.rectTransform, 0.11f, 0.074f, 0.89f, 0.336f); // 점선 아래
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = 10f;
            description.fontSizeMax = 15f;
            description.overflowMode = TextOverflowModes.Ellipsis;

            var view = root.gameObject.AddComponent<HandCardView>();
            Wire(view, ("nameText", nameText), ("artImage", art), ("descriptionText", description));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return saved.GetComponent<HandCardView>();
        }

        // 손패가 카드 정보를 찾는 덱은 서버가 쓰는 덱과 같아야 한다.
        private static DeckData FindGameDeck()
        {
            var server = Object.FindFirstObjectByType<PunGameServer>();
            DeckData deck = server != null ? new SerializedObject(server).FindProperty("deck").objectReferenceValue as DeckData : null;
            if (deck == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] {nameof(PunGameServer)}.deck 을 찾지 못했다 — {nameof(HandPresenter)}.deck · {nameof(HistoryPresenter)}.deck 을 직접 연결할 것.");
            return deck;
        }

        // 부모 크기 비율(0~1)로 붙는 상자 — 카드 틀 이미지 위의 칸을 비율로 잡는다.
        private static void AnchorBox(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion

        #region 폰트

        private static TMP_FontAsset EnsureFontAsset(string fontPath, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 글꼴 파일이 없다: {fontPath}. TMP 기본 글꼴로 만든다.");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, FontSamplingPointSize, FontAtlasPadding, GlyphRenderMode.SDFAA,
                FontAtlasSize, FontAtlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);

            AssetDatabase.CreateAsset(asset, assetPath);
            asset.material.name = asset.name + " Material";
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[{nameof(GameScreenBuilder)}] TMP 글꼴 에셋 생성: {assetPath}");
            return asset;
        }

        #endregion

        #region 만들기 도우미

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

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

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, bool bold,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI tmp = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;

            TMP_FontAsset font = bold ? _boldFont : _regularFont;
            if (font != null)
                tmp.font = font;
            else if (bold)
                tmp.fontStyle = FontStyles.Bold;

            return tmp;
        }

        /// <summary>속이 빈 테두리(외곽선) — 스프라이트 없이 변 4개로 만든다. 부모 바깥쪽으로 width 만큼 두르며, 안쪽은 비어 있어 뒤가 보인다.</summary>
        private static RectTransform BuildEdgeFrame(string name, Transform parent, Color color, float width)
        {
            RectTransform frame = NewRect(name, parent);
            Stretch(frame, -width, -width, -width, -width);

            Image top = NewImage("Top", frame, color);
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.offsetMin = new Vector2(0f, -width);
            top.rectTransform.offsetMax = Vector2.zero;

            Image bottom = NewImage("Bottom", frame, color);
            bottom.rectTransform.anchorMin = new Vector2(0f, 0f);
            bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            bottom.rectTransform.offsetMin = Vector2.zero;
            bottom.rectTransform.offsetMax = new Vector2(0f, width);

            Image left = NewImage("Left", frame, color);
            left.rectTransform.anchorMin = new Vector2(0f, 0f);
            left.rectTransform.anchorMax = new Vector2(0f, 1f);
            left.rectTransform.offsetMin = Vector2.zero;
            left.rectTransform.offsetMax = new Vector2(width, 0f);

            Image right = NewImage("Right", frame, color);
            right.rectTransform.anchorMin = new Vector2(1f, 0f);
            right.rectTransform.anchorMax = new Vector2(1f, 1f);
            right.rectTransform.offsetMin = new Vector2(-width, 0f);
            right.rectTransform.offsetMax = Vector2.zero;

            return frame;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
            => Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));

        private static void TopRight(RectTransform rect, float right, float top, float width, float height)
            => Anchor(rect, Vector2.one, Vector2.one, new Vector2(-right, -top), new Vector2(width, height));

        private static void BottomLeft(RectTransform rect, float left, float bottom, float width, float height)
            => Anchor(rect, Vector2.zero, Vector2.zero, new Vector2(left, bottom), new Vector2(width, height));

        private static void BottomRight(RectTransform rect, float right, float bottom, float width, float height)
            => Anchor(rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-right, bottom), new Vector2(width, height));

        /// <summary>부모 왼쪽 위 기준 좌표(cx, cy — 아래로 +)에 가운데를 맞춘다.</summary>
        private static void CenterAt(RectTransform rect, float cx, float cy, float width, float height)
            => Anchor(rect, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -cy), new Vector2(width, height));

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static HorizontalLayoutGroup HorizontalLayout(RectTransform rect, RectOffset padding, float spacing, TextAnchor alignment,
            bool controlWidth, bool controlHeight)
        {
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = controlWidth;
            layout.childControlHeight = controlHeight;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static void FitBoth(GameObject go)
        {
            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        #endregion

        #region 참조 연결 ([SerializeField] private 를 SerializedObject 로)

        internal static void Wire(Component target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, Object value) in references)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[{nameof(GameScreenBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void WireArray(Component target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Component target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
