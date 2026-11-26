using Chapchu.UI;
using Chapchu.UI.Fx;
using Chapchu.UI.GameScreen;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 인게임 HUD — 통합 화면 UI v3 (게시본 1795370661-f10f, 2026-11-23). 값은 시안 CSS px × 2 = 캔버스 px, 테두리 2.5px 는 브라우저처럼 2px 로 (× 2 = 4).
    /// 왼쪽 위 「낸 카드 기록」 · 오른쪽 「턴 방향」 세로 막대 · 상대 머리 위 이름표 · 왼쪽 아래 내 HUD · 가운데 「뽑고 턴 끝」 · 덱 / 버림 이름 · 내 함정 칸.
    /// v2 로 지은 씬은 메뉴 Upgrade HUD v3 가 v2 부품을 걷어내고 바꾼다 — 캐릭터 · 링 · 부채꼴 · 말풍선은 그대로 두고 자리만 옮긴다.
    /// Build UI(Build) 는 아직 v2 를 짓는다 — 새 씬은 Build UI 뒤에 Upgrade HUD v3 를 돌린다.
    /// </summary>
    public static partial class GameScreenBuilder
    {
        private const string OutlineWhiteMaterialPath = "Assets/Fonts/Taebaek/Taebaek SDF - OutlineWhite.mat"; // 흰 테두리 글자 (시안 -webkit-text-stroke #fff)
        private const string HistoryItemPrefabPath = HandCardPrefabFolder + "/HistoryItem.prefab";
        private const string TurnOrderRowPrefabPath = HandCardPrefabFolder + "/TurnOrderRow.prefab";
        private const float Line4 = 4f; // 시안 테 2 · 2.5px — 브라우저가 2.5 를 2 로 그린다 (× 2)

        private static readonly Color HpGreen = Hex(0x3cbf72);
        private static readonly Color SoftInk = Hex(0x5d544c);
        private static readonly Color Warn = Hex(0xff9f1a);
        private static readonly Color DeadTag = Hex(0xddd6cf);
        private static readonly Color GoalBlue = Hex(0x2f7cf6);
        private static readonly Color GoalFill = new Color(1f, 1f, 1f, 0.88f);
        private static readonly Color FaceBg = Hex(0xf6f0e7);
        private static readonly Color TrapDash = Hex(0xe3957d);
        private static readonly Color TrapBadge = Hex(0xa5674a);
        private static readonly Color MyTrapFill = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color TrackFill = new Color(1f, 1f, 1f, 0.5f);

        // 상대 좌석 (시안 3D 판) — 캐릭터 발 가운데 x · 발 y · 키. 이름표는 머리 위(아래 끝 144), 함정 칸은 발밑 한 줄.
        private static readonly float[] SeatFootX = { 512.8f, 960f, 1407.1f };
        private const float SeatFootY = 348.7f;
        private const float SeatHeight = 212.9f;
        private const float SeatRingY = 336.5f;          // 발밑 링 가운데
        private static readonly Vector2 SeatHandOffset = new Vector2(29.6f, -15.3f); // 부채꼴 축 — 발 가운데에서 (오른 앞발)
        private const float SeatHandScale = 0.7097f;     // 뒷면 카드(36 × 54)를 시안 키에 맞춘 배율 — 부채꼴 루트를 줄인다
        private static readonly Vector2 SeatWatchCenter = new Vector2(-127f, 199.7f); // 스톱워치 가운데 (x 는 발 가운데 기준)
        private static readonly Vector2 SeatShoutPoint = new Vector2(45f, 195.8f);    // 말풍선 아래 기준점 (x 는 발 가운데 기준)
        private const float SeatLifeTagTop = 241.8f;     // 「사망 대기 n초」 · 「사망」 팻말 위끝 (가슴)
        private static readonly Vector2 SeatSweat = new Vector2(36f, 155.8f);         // 땀방울 왼쪽 위 (x 는 발 가운데 기준)
        private static readonly Rect SeatTapArea = new Rect(-112f, -12f, 224f, 377f); // 탭 영역 (x 는 발 가운데 기준)
        private const float PlateWidth = 192f, PlateHeight = 140f, PlateBottom = 144f;

        // 상대 함정 칸 (3D) — 칸 가운데가 화면에 보이는 자리 · 배율. 테이블 면처럼 40° 눕힌다.
        private static readonly float[][] SeatTrapX = { new[] { 438.8f, 503.5f, 568.5f }, new[] { 894.5f, 960f, 1025.5f }, new[] { 1351f, 1416.5f, 1480f } };
        private const float SeatTrapY = 391.4f, SeatTrapScale = 0.235f;

        // 내 함정 칸 (시안 .mtrap 56 × 78) — 첫 칸 왼쪽 위 · 칸 사이. 3D 카드는 칸 아래 가운데에 세운다.
        private const int MyTrapSlotCount = 3; // 방장 규칙의 함정 칸 수 (GameServer.TrapSlotCount) — 3D 칸 줄과 같다
        private static readonly Vector2 MyTrapFirst = new Vector2(1245.3f, 480.6f);
        private static readonly Vector2 MyTrapSize = new Vector2(112f, 156f);
        private const float MyTrapStep = 128f, MyTrapCardScale = 0.464f, MyTrapLabelTop = 652.6f;

        // 덱 · 버림 더미 (3D) — 가운데 · 배율, 이름표 「덱 n」 · 「버림 n」 위 가운데. 시안 판과 원근이 달라 화면에 보이는 위 · 아래 끝을 시안에 맞춘 값 (덱 468 ~ 733, 버림 473 ~ 730)
        private static readonly Vector2 DeckCenterV3 = new Vector2(820f, 584f);
        private const float DeckScaleV3 = 1.5f;
        private static readonly Vector2 PileCenterV3 = new Vector2(1097f, 599f);
        private const float PileScaleV3 = 1.4f;
        private static readonly Vector2 DeckLabelTop = new Vector2(812.45f, 749.4f);
        private static readonly Vector2 PileLabelTop = new Vector2(1107.5f, 749.4f);
        private static readonly Vector2 DrawButtonTop = new Vector2(827.85f, 416.6f);

        // 내 캐릭터 · HUD (시안 .mehud 192 · .mgoal · .swatch.me)
        private const float MyCharacterCenterX = 133f, MyFootY = 928f, MyCharacterHeight = 264f, MyRingY = 916f;
        private static readonly Vector2 MyWatchCenter = new Vector2(266f, 732f);
        private const float MyWatchSize = 80f;
        private static readonly Vector2 GoalTopLeft = new Vector2(230f, 852f);
        private const float MyHudWidth = 384f, MyHudHeight = 164f, MyHudBottom = 4f;

        // 낸 카드 기록 (시안 .hist) — 위 32 · 아래 끝은 화면 아래에서 428 (줄 4개), 줄 334 × 128
        private const float HistoryTop = 32f, HistoryBottom = 428f;
        private static readonly Vector2 HistoryItemSize = new Vector2(334f, 128f);

        // 턴 순서 (시안 .tlist) — 오른쪽 32 · 위 124
        private const float TurnOrderTop = 124f;

        // 턴 순서 얼굴 — 시안 캐릭터 그림의 폭 / 높이 (우리 그림은 CharacterAspect)
        private const float MockCharacterAspect = 0.7045f;

        // 설정 버튼 — 동그라미 (시안 #b-set border-radius 50%, 76 × 76 · 테 5)
        private const float GearRadius = 38f;

        // 흑백 머티리얼 (Chapchu/UI Gray) — 최종 사망한 사람의 하트 · HP 막대 · 캐릭터 · 턴 순서 얼굴 (시안 filter:grayscale(1))
        private const string GrayMaterialFolder = "Assets/Materials/Hud";
        private const string GrayMaterialPath = GrayMaterialFolder + "/UIGray.mat";
        private const string GrayShaderName = "Chapchu/UI Gray";

        private static Material _outlineWhite;
        private static Material OutlineWhite => _outlineWhite != null ? _outlineWhite : _outlineWhite = LoadMaterial(OutlineWhiteMaterialPath);

        private static Material _gray;
        private static Material GrayMaterial => _gray != null ? _gray : _gray = EnsureGrayMaterial();

        // 없으면 만든다 — 이미 있으면 그대로 (사람이 고친 값 보존)
        private static Material EnsureGrayMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GrayMaterialPath);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find(GrayShaderName);
            if (shader == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 셰이더가 없다: {GrayShaderName} (Assets/Shaders/UIGray.shader)");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(GrayMaterialFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "Hud");
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, GrayMaterialPath);
            return material;
        }

        // v2 BuildButton 은 그림자를 위로 그렸다 (Stretch 부호) — 버튼의 그림자(위아래 오프셋이 같은 값만큼 위)를 아래로 뒤집는다 (시안 .btn box-shadow 0 3px 0)
        private static void FixButtonShadows(RectTransform canvasRect)
        {
            foreach (Button button in canvasRect.GetComponentsInChildren<Button>(true))
            {
                var shadow = button.transform.Find("Shadow") as RectTransform;
                if (shadow == null || shadow.anchorMin != Vector2.zero || shadow.anchorMax != Vector2.one)
                    continue;

                float lift = shadow.offsetMin.y;
                if (lift > 0f && Mathf.Approximately(shadow.offsetMax.y, lift))
                {
                    shadow.offsetMin = new Vector2(shadow.offsetMin.x, -lift);
                    shadow.offsetMax = new Vector2(shadow.offsetMax.x, -lift);
                }
            }
        }

        // 대상 선택 안내 (시안 .ask) — 카드 이름을 제목 글꼴 36 · 행동 카드 색으로 떼어 낸다 (.ask .q em 18 × 2). 물음은 본문 30 그대로.
        // 안여백은 테(3 × 2) 안쪽에서 위아래 12 · 왼 32 · 오른 14 (시안 padding 6 7 6 16) — 높이 112
        private static void SplitTargetAskName(RectTransform center)
        {
            var ask = center.GetComponentInChildren<TargetAskView>(true);
            Transform question = ask != null ? ask.transform.Find("QuestionText") : null;
            if (question == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 열린 씬에 TargetAsk/QuestionText 가 없다.");
                return;
            }

            var layout = ask.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
                layout.padding = new RectOffset(38, 20, 18, 18);

            Transform existing = ask.transform.Find("CardNameText");
            TMP_Text cardName = existing != null ? existing.GetComponent<TMP_Text>() : NewText("CardNameText", ask.transform, "카드", 36f, Act, true);
            cardName.transform.SetSiblingIndex(question.GetSiblingIndex());
            question.GetComponent<TMP_Text>().text = "· 누구에게 쓸까요?";
            Wire(ask, ("cardNameText", cardName));
        }

        // 설정 버튼을 동그라미로 — 그림자 · 테 · 채움(테 5 안쪽)의 둥근 모서리 반지름 (RoundImage: 64 / 반지름)
        private static void RoundGearButton(RectTransform safeArea)
        {
            Transform gear = safeArea.Find("Menu/GearButton");
            if (gear == null)
                return;

            foreach ((string part, float radius) in new[] { ("Shadow", GearRadius), ("Border", GearRadius), ("Fill", GearRadius - 5f) })
            {
                Transform child = gear.Find(part);
                Image image = child != null ? child.GetComponent<Image>() : null;
                if (image != null)
                    image.pixelsPerUnitMultiplier = 64f / radius;
            }
        }

        /// <summary>
        /// 열린 씬(v2 HUD)을 통합 화면 UI v3 로 바꾼다 — 다시 짓지 않는다. 걷어내는 것: 방 코드 · 턴 띠 · 알림 칩 · 기록 서랍 · 찹츄 버튼 · 내 패널 · 좌석 이름판 · 방석.
        /// 캐릭터 · 링 · 부채꼴 · 말풍선 · 메뉴 · 서랍은 그대로 두고 자리만 옮긴다. 이미 v3 면(SafeArea/MyHud) 아무것도 하지 않는다.
        /// </summary>
        [MenuItem("Chapchu/Game Screen/Upgrade HUD v3")]
        public static void UpgradeHudV3()
        {
            GameObject canvasObject = GameObject.Find(CanvasName);
            var canvasRect = canvasObject != null ? (RectTransform)canvasObject.transform : null;
            var safeArea = canvasRect != null ? (RectTransform)canvasRect.Find("SafeArea") : null;
            var center = canvasRect != null ? (RectTransform)canvasRect.Find("Center") : null;
            var seatArea = center != null ? (RectTransform)center.Find("OpponentSeats") : null;
            GameObject tableObject = GameObject.Find("Table");
            if (safeArea == null || center == null || seatArea == null || tableObject == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 열린 씬에 {CanvasName}/SafeArea · Center/OpponentSeats · Table 중 없는 것이 있다.");
                return;
            }

            if (safeArea.Find("MyHud") != null)
            {
                Debug.Log($"[{nameof(GameScreenBuilder)}] 이미 v3 HUD 다 (SafeArea/MyHud).");
                return;
            }

            Transform table = tableObject.transform;
            var screen = canvasObject.GetComponent<GameScreenView>();
            var presenter = canvasObject.GetComponent<GameScreenPresenter>();
            var history = canvasObject.GetComponent<HistoryPresenter>();
            var turnTimer = canvasObject.GetComponent<TurnTimerPresenter>();
            var seats = seatArea.GetComponent<OpponentSeatsPresenter>();
            var deck = table.GetComponentInChildren<DeckView>(true);
            var pile = table.GetComponentInChildren<DiscardPileView>(true);
            var hand = table.GetComponentInChildren<HandPresenter>(true);
            var myTraps = table.GetComponentInChildren<MyTrapsPresenter>(true);
            if (screen == null || presenter == null || history == null || turnTimer == null || seats == null || deck == null || pile == null || hand == null || myTraps == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 열린 씬에 GameScreenView · GameScreenPresenter · HistoryPresenter · TurnTimerPresenter · OpponentSeatsPresenter · DeckView · DiscardPileView · HandPresenter · MyTrapsPresenter 중 없는 것이 있다.");
                return;
            }

            _regularFont = EnsureFontAsset(RegularFontPath, RegularFontAssetPath);
            _boldFont = EnsureFontAsset(BoldFontPath, BoldFontAssetPath);
            _backCardPrefab = null;

            // v2 부품 — 방 코드 · 턴 띠 · 알림 칩 · 찹츄 버튼 · 발밑 턴 타원 · 스톱워치 · 내 패널 · 기록 서랍 · 방석
            foreach (string name in new[] { "RoomCodeTag", "TurnStrip", "Feed", "ChapchuButton", "MyTurnSpot", "TurnTimer", "MyPanel" })
                DestroyChild(safeArea, name);
            DestroyChild(canvasRect.Find("Sheets"), "LogSheet");
            GameObject background = GameObject.Find(BackgroundCanvasName);
            if (background != null)
                DestroyChild(background.transform, "CushionBoard");

            BuildHudV3(canvasRect, safeArea, center, seatArea, deck, pile, myTraps, out HistoryListView historyList, out TurnOrderView turnOrder, out MyPanelView myPanel, out OpponentSeatView[] seatViews);

            // 잇기 — 바뀐 부품만
            Wire(screen, ("turnOrder", turnOrder), ("history", historyList), ("myPanel", myPanel));
            WireArray(screen, "opponentSeats", seatViews);
            Wire(presenter, ("turnOrder", turnOrder.GetComponent<TurnOrderPresenter>()), ("myPanel", myPanel.GetComponent<MyPanelPresenter>()));
            GameMenuPresenter menu = FindIn<GameMenuPresenter>(safeArea, "Menu");
            if (menu != null)
                Wire(presenter, ("menu", menu)); // 빠져 있던 연결 — 실행 때 찾아 쓰던 것
            Wire(history, ("list", historyList), ("deck", FindGameDeck()), ("cardBack", AssetDatabase.LoadAssetAtPath<Sprite>(CardBackSpritePath)));
            WireArray(turnTimer, "timers", canvasRect.GetComponentsInChildren<TurnTimerView>(true));
            WireArray(hand, "coveredHud", new Object[] { myPanel.GetComponent<CanvasGroup>() });
            Wire(seats, ("myPanel", myPanel));
            WireArray(seats, "seats", seatViews);

            EditorSceneManager.MarkSceneDirty(canvasObject.scene);
            Debug.Log($"[{nameof(GameScreenBuilder)}] HUD 를 통합 화면 UI v3 로 바꿈 — 씬을 저장할 것.");
        }

        /// <summary>
        /// v3 HUD 부품을 짓고 자리 · 순서를 맞춘다 (UpgradeHudV3). 이미 있는 캐릭터 · 링 · 부채꼴 · 말풍선은 다시 만들지 않는다.
        /// 덱 · 버림 · 함정 칸(3D)도 v3 자리로 옮기고 「덱 n」 · 「버림 n」 · 「내 함정 n/3」 · 빈 칸을 2D 로 바꿔 잇는다. 화면 · 연출 큐 · 손패 잇기는 부르는 쪽이 한다.
        /// </summary>
        private static void BuildHudV3(RectTransform canvasRect, RectTransform safeArea, RectTransform center, RectTransform seatArea, DeckView deck, DiscardPileView pile, MyTrapsPresenter myTraps,
            out HistoryListView historyList, out TurnOrderView turnOrder, out MyPanelView myPanel, out OpponentSeatView[] seatViews)
        {
            historyList = BuildHistoryV3(safeArea);
            Image myCharacter = PlaceMyCharacterV3(safeArea, out RectTransform myRing);
            myPanel = BuildMyHudV3(safeArea, myCharacter, myRing);

            seatViews = new OpponentSeatView[SeatFootX.Length];
            Transform referenceShout = seatArea.Find("Seat1/Shout");
            for (int i = 0; i < seatViews.Length; i++)
            {
                var seat = (RectTransform)seatArea.Find($"Seat{i + 1}");
                if (seat == null)
                {
                    seat = NewRect($"Seat{i + 1}", seatArea);
                    Stretch(seat);
                }
                seatViews[i] = BuildSeatV3(seat, i, deck, referenceShout);
            }

            var characters = new Image[seatViews.Length + 1];
            characters[0] = myCharacter;
            for (int i = 0; i < seatViews.Length; i++)
                characters[i + 1] = seatViews[i].transform.Find("Character").GetComponent<Image>();
            turnOrder = BuildTurnOrderV3(safeArea, characters);

            TableHudParts tableHud = BuildTableHudV3(center);
            ApplyTableLayoutV3(deck, pile, myTraps, tableHud);
            FixButtonShadows(canvasRect);
            RoundGearButton(safeArea);
            SplitTargetAskName(center);
            BuildReactionSkipV3(canvasRect, safeArea);

            // 그리는 순서 — 기록 · 턴 순서 · 메뉴 < 내 캐릭터 · 링 · 스톱워치 · 「!」 · 「찹츄까지」 · 내 HUD · 스킵. 가운데는 좌석 · 판 위 HUD < 가림판 < 대상 안내
            OrderChildren(safeArea, "History", "TurnOrder", "Menu", "MyCharacter", "MyPickRing", "MyTurnTimer", "MyBang", "Goal", "MyHud", "SkipButton");
            OrderChildren(center, "OpponentSeats", "TableHud", "Dimmer", "TargetAsk");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
        }

        // 반응 시간 스킵 — 낼 카운터가 있을 때만 (ReactionTimerView). 알약 대신 오른쪽 아래 버튼 + 내 머리 옆 「!」.
        // 버튼: 시안 .btn.skip 128 × 56 · 테 3 · 반지름 15 · 그림자 5 · 글자 22 (× 2), 오른쪽 32 · 아래 144. 「!」: .bang.me 30 · 테 2.5 · 노랑 · 21px (× 2), 가운데 (262, 705) + 아래 꼬리
        private static void BuildReactionSkipV3(RectTransform canvasRect, RectTransform safeArea)
        {
            var timer = canvasRect.GetComponentInChildren<ReactionTimerView>(true);
            if (timer == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 열린 씬에 ReactionTimerView 가 없다 — 스킵 버튼을 잇지 못했다.");
                return;
            }

            RectTransform skip = NewRect("SkipButton", safeArea);
            BottomRight(skip, EdgeX, 144f, 256f, 112f);
            Image shadow = RoundImage("Shadow", skip, Ink, 30f);
            Stretch(shadow.rectTransform, 0f, 10f, 0f, -10f);
            Image fill = RoundBox(skip, Ink, 6f, 30f, White, true);
            TMP_Text label = NewText("Label", skip, "스킵", 44f, Ink, true);
            Stretch(label.rectTransform);
            var button = skip.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            skip.gameObject.SetActive(false);

            RectTransform bang = NewRect("MyBang", safeArea);
            Anchor(bang, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(262f, FromBottom(705f)), new Vector2(60f, 60f));
            // 꼬리 — 잉크 마름모를 세로로 늘려(× 1.75) 아래 꼭짓점만 원 밖으로 7 나오게 (시안 ::after 8 × 7)
            RectTransform tail = NewRect("Tail", bang);
            Anchor(tail, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -7f + 14f), new Vector2(16f, 16f));
            tail.localScale = new Vector3(1f, 1.75f, 1f);
            Image tailInk = NewImage("Ink", tail, Ink);
            CenterIn(tailInk.rectTransform, 11.3f, 11.3f);
            tailInk.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UIShape circle = NewShape("Circle", bang, Turn, 0f, Line4, Ink, UIShape.ShapeKind.Ellipse);
            Stretch(circle.rectTransform);
            TMP_Text mark = NewText("Text", bang, "!", 42f, Ink, true);
            Stretch(mark.rectTransform);
            bang.gameObject.SetActive(false);

            Wire(timer, ("skipCornerButton", button), ("bang", bang));
        }

        #region 낸 카드 기록 (왼쪽 위)

        // 줄 334 × 128 · 간격 148 (시안 .hist gap 10 · .hitem 64) — 위끝부터 쌓고, 자리(높이)만큼만 보인다 (HistoryListView)
        private static HistoryListView BuildHistoryV3(RectTransform safeArea)
        {
            RectTransform root = NewRect("History", safeArea);
            root.anchorMin = Vector2.zero;
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.offsetMin = new Vector2(EdgeX, HistoryBottom);
            root.offsetMax = new Vector2(EdgeX + HistoryItemSize.x, -HistoryTop);

            var view = root.gameObject.AddComponent<HistoryListView>();
            Wire(view, ("itemPrefab", EnsureHistoryItemPrefab()), ("content", root));
            return view;
        }

        // 기록 한 줄 프리팹 (시안 .hitem) — 카드 이름(13 → 26, 흰 테 글자) · 작은 카드 92(종류 색 테 4 + 잉크 선 3 · 그림 72) · 상자 224 × 92(누가 → 누구 / 결과).
        // 이미 있으면 덮어쓰지 않는다 (사람이 고친 것 보존) — 다시 만들려면 프리팹을 지우고 실행
        private static HistoryItemView EnsureHistoryItemPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HistoryItemView>(HistoryItemPrefabPath);
            if (existing != null)
                return existing;

            EnsureHandCardPrefabFolder();
            RectTransform root = NewRect("HistoryItem", null);
            Anchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, HistoryItemSize);

            // 튀어나오는 연출은 줄 가운데를 축으로 — 내용 전체(Body)를 키운다
            RectTransform body = NewRect("Body", root);
            Stretch(body);
            var bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
            bodyGroup.blocksRaycasts = false;

            TMP_Text label = StrokeText("Label", body, "카드 이름", 26f, SoftInk);
            Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(48f, -6f), new Vector2(124f, 26f));
            label.overflowMode = TextOverflowModes.Ellipsis;

            // 작은 카드 — 잉크 선(바깥 3) → 흰 바탕 + 종류 색 테 4 → 그림 72 (모서리 8)
            UIShape thumbLine = NewShape("ThumbLine", body, Ink, 19f, 0f, Color.clear);
            TopLeft(thumbLine.rectTransform, -1f, 33f, 98f, 98f);
            UIShape thumb = NewShape("Thumb", body, White, 16f, Line4, Act);
            TopLeft(thumb.rectTransform, 2f, 36f, 92f, 92f);
            Image artMask = RoundImage("ArtMask", body, White, 8f);
            TopLeft(artMask.rectTransform, 12f, 46f, 72f, 72f);
            artMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var art = NewRect("Art", artMask.transform).gameObject.AddComponent<RawImage>();
            Stretch(art.rectTransform);
            art.raycastTarget = false;

            // 상자 — 두 줄을 세로 가운데에 (HistoryItemView.LayoutLines 가 결과 줄 높이에 맞춰 옮긴다)
            UIShape box = NewShape("Box", body, White, 18f, Line4, Ink);
            TopLeft(box.rectTransform, 110f, 36f, 224f, 92f);
            TMP_Text who = NewText("WhoText", box.transform, "누가 → 누구", 26f, Ink, true, TextAlignmentOptions.Left);
            Anchor(who.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(176f, 26f));
            who.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text fx = NewText("FxText", box.transform, "−HP 20", 32f, Ink, true, TextAlignmentOptions.Left);
            Anchor(fx.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -46f), new Vector2(176f, 38f));
            fx.overflowMode = TextOverflowModes.Ellipsis;

            var view = root.gameObject.AddComponent<HistoryItemView>();
            Wire(view, ("body", body), ("bodyGroup", bodyGroup), ("labelText", label), ("thumb", thumb), ("art", art), ("whoText", who), ("fxText", fx));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, HistoryItemPrefabPath);
            Object.DestroyImmediate(root.gameObject);
            return saved.GetComponent<HistoryItemView>();
        }

        #endregion

        #region 턴 순서 (오른쪽)

        // 「턴 방향 ↓」 알약 + 세로 막대 (폭 128 · 반지름 64 · 흰 50%) + 줄(얼굴 원 · 이름 · 꺾쇠). 지금 턴 = 큰 원 + 왼쪽 꼬리표(이름 형광펜 · 남은 초)
        private static TurnOrderView BuildTurnOrderV3(RectTransform safeArea, Image[] characters)
        {
            RectTransform root = NewRect("TurnOrder", safeArea);
            TopRight(root, EdgeX, TurnOrderTop, 300f, 700f);

            // 머리 알약 — 높이 52 · 반지름 26 · 「턴 방향」 28 + 화살표 · 멈춤 상자 (시안 .thd)
            RectTransform header = NewRect("Header", root);
            Anchor(header, Vector2.one, Vector2.one, Vector2.zero, new Vector2(200f, 52f));
            HorizontalLayout(header, new RectOffset(20, 20, 0, 0), 8f, TextAnchor.MiddleCenter, true, false);
            header.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIShape headerFill = NewShape("Fill", header, White, 26f, Line4, Ink);
            Stretch(headerFill.rectTransform);
            headerFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            NewText("Text", header, "턴 방향", 28f, Ink, true).rectTransform.sizeDelta = new Vector2(100f, 52f);
            Image arrow = FixedIcon("Arrow", header, Hud("icon_arrow_down"), 20f, 26f, Ink);
            UIShape halt = NewShape("Halt", header, WarnFill, 8f, 3f, Ink);
            SetLayoutSize(halt.rectTransform, 32f, 32f);
            Icon("Icon", halt.transform, Hud("icon_pause"), 18f, Ink);
            halt.gameObject.SetActive(false);

            // 세로 막대 — 높이는 줄 수에 맞춰 뷰가 정한다
            UIShape track = NewShape("Track", root, TrackFill, 64f, Line4, Ink);
            Anchor(track.rectTransform, Vector2.one, Vector2.one, new Vector2(0f, -68f), new Vector2(128f, 0f));
            track.gameObject.SetActive(false);

            // 줄 자리 — 폭 0 · 막대 가운데. 줄은 프리팹을 복제해 152 간격으로 쌓는다
            RectTransform rows = NewRect("Rows", root);
            Anchor(rows, Vector2.one, new Vector2(0.5f, 1f), new Vector2(-64f, -84f), Vector2.zero);

            // 지금 턴 꼬리표 — 큰 원 왼쪽. 흰 판(오른쪽 테는 원 밑으로 숨긴다) · 이름 형광펜 · 남은 초 원 48 (시안 .trow.cur)
            RectTransform tab = NewRect("CurrentTab", rows);
            Anchor(tab, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-60f, -62f), new Vector2(200f, 84f));
            HorizontalLayout(tab, new RectOffset(28, 22, 0, 0), 12f, TextAnchor.MiddleLeft, true, false);
            tab.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            RectTransform tabBg = NewRect("Bg", tab);
            Stretch(tabBg);
            tabBg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tabBg.gameObject.AddComponent<RectMask2D>();
            UIShape tabFill = NewShape("Fill", tabBg, White, 24f, Line4, Ink);
            Stretch(tabFill.rectTransform, 0f, 0f, -30f, 0f);

            RectTransform nameGroup = NewRect("Name", tab);
            nameGroup.sizeDelta = new Vector2(100f, 36f);
            HorizontalLayout(nameGroup, new RectOffset(8, 8, 0, 0), 4f, TextAnchor.MiddleCenter, true, false);
            Image band = NewImage("Band", nameGroup, Turn);
            band.rectTransform.anchorMin = new Vector2(0f, 0.04f); // 형광펜 — 글자 높이의 58 ~ 96% (시안 .tpn)
            band.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            band.rectTransform.offsetMin = Vector2.zero;
            band.rectTransform.offsetMax = Vector2.zero;
            band.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image crown = FixedIcon("Crown", nameGroup, Hud("icon_crown"), 26f, 22f, White);
            crown.gameObject.SetActive(false);
            TMP_Text currentName = NewText("NameText", nameGroup, "나", 36f, Ink, true);
            currentName.rectTransform.sizeDelta = new Vector2(40f, 36f);
            currentName.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement currentNameLayout = currentName.gameObject.AddComponent<LayoutElement>();
            BuildSecondsBadgeV3(tab);
            tab.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<TurnOrderView>();
            Wire(view,
                ("headerFill", headerFill), ("directionArrow", arrow.rectTransform), ("haltBox", halt.gameObject), ("track", track.rectTransform), ("rows", rows),
                ("rowPrefab", EnsureTurnOrderRowPrefab()), ("currentTab", tab), ("currentNameText", currentName), ("currentNameLayout", currentNameLayout),
                ("currentBand", band), ("currentCrown", crown.gameObject));
            var presenter = root.gameObject.AddComponent<TurnOrderPresenter>();
            WireArray(presenter, "characters", characters);
            return view;
        }

        // 꼬리표 안 남은 초 — 흰 원 48 · 잉크 테 4 · 26. 5초 이하면 빨간 원 + 흰 글자로 1.15 배 맥동 (시안 .tsec.hot)
        private static TurnTimerView BuildSecondsBadgeV3(Transform parent)
        {
            RectTransform root = NewRect("Seconds", parent);
            SetLayoutSize(root, 48f, 48f);
            UIShape face = NewShape("Face", root, White, 0f, Line4, Ink, UIShape.ShapeKind.Ellipse);
            Stretch(face.rectTransform);
            TMP_Text seconds = NewText("Text", root, "15", 26f, Ink, true);
            Stretch(seconds.rectTransform);

            var view = root.gameObject.AddComponent<TurnTimerView>();
            Wire(view, ("secondsText", seconds), ("face", face));
            SetFloat(view, "urgentPulse", 1.15f);
            return view;
        }

        // 턴 순서 한 줄 프리팹 (시안 .trow) — 92 × 124: 이름 26(흰 테 글자) · 얼굴 원 92(HP 고리 · 얼굴 76 · 잉크 선) · 꺾쇠(줄 아래 28).
        // 이미 있으면 덮어쓰지 않는다 (사람이 고친 것 보존) — 다시 만들려면 프리팹을 지우고 실행
        private static TurnOrderRowView EnsureTurnOrderRowPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TurnOrderRowView>(TurnOrderRowPrefabPath);
            if (existing != null)
                return existing;

            EnsureHandCardPrefabFolder();
            RectTransform root = NewRect("TurnOrderRow", null);
            Anchor(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(92f, 124f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            TMP_Text name = StrokeText("NameText", root, "이름", 26f, Ink);
            Anchor(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(116f, 26f));
            name.overflowMode = TextOverflowModes.Ellipsis;

            // 얼굴 원 — 흰 바탕(잉크 선 3 바깥) → HP 고리(위에서 시계 방향) → 얼굴 바탕 76 · 그림 → 얼굴 잉크 선
            RectTransform ring = NewRect("Ring", root);
            Anchor(ring, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(92f, 92f));
            UIShape line = NewShape("Line", ring, White, 0f, 3f, Ink, UIShape.ShapeKind.Ellipse);
            CenterIn(line.rectTransform, 98f, 98f);
            Image hp = NewImage("Hp", ring, HpGreen, Hud("timer_ring"));
            Stretch(hp.rectTransform);
            hp.type = Image.Type.Filled;
            hp.fillMethod = Image.FillMethod.Radial360;
            hp.fillOrigin = (int)Image.Origin360.Top;
            hp.fillClockwise = true;
            hp.fillAmount = 1f;
            Image face = RoundImage("Face", ring, FaceBg, 38f);
            CenterIn(face.rectTransform, 76f, 76f);
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            // 머리 — 시안 .tf img left −25% · top −24% · 폭 150% (114). 시안 그림(폭/높이 .7045)과 우리 그림의 비율이 달라 높이를 시안 그림 높이(161.8)에 맞추고 가운데(38)를 지킨다
            Image head = NewImage("Head", face.transform, White, CharacterSprite);
            float headHeight = 114f / MockCharacterAspect, headWidth = headHeight * CharacterAspect;
            Anchor(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f - headWidth / 2f, 18.24f), new Vector2(headWidth, headHeight));
            UIShape faceLine = NewShape("FaceLine", ring, Color.clear, 0f, 3f, Ink, UIShape.ShapeKind.Ellipse);
            CenterIn(faceLine.rectTransform, 82f, 82f);

            RectTransform chevron = NewRect("Chevron", root);
            Anchor(chevron, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -138f), new Vector2(92f, 28f));
            Icon("Icon", chevron, Hud("icon_chevron"), 24f, Ink2);

            var view = root.gameObject.AddComponent<TurnOrderRowView>();
            Wire(view, ("nameText", name), ("ring", ring), ("hpFill", hp), ("head", head), ("chevron", chevron), ("group", group), ("grayMaterial", GrayMaterial));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, TurnOrderRowPrefabPath);
            Object.DestroyImmediate(root.gameObject);
            return saved.GetComponent<TurnOrderRowView>();
        }

        #endregion

        #region 내 캐릭터 · HUD (왼쪽 아래)

        // 큰 캐릭터 — 키 264, 발 가운데 (133, 928). 대상 링은 발 위. 이미 있으면 자리만 옮긴다 (가림판 위로 올리는 하위 캔버스는 BuildMyHudV3 가 단다)
        private static Image PlaceMyCharacterV3(RectTransform safeArea, out RectTransform ring)
        {
            Image character = FindIn<Image>(safeArea, "MyCharacter");
            if (character == null)
            {
                character = NewImage("MyCharacter", safeArea, White, CharacterSprite);
                character.preserveAspect = true;
            }
            Anchor(character.rectTransform, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(MyCharacterCenterX, FromBottom(MyFootY)),
                new Vector2(MyCharacterHeight * CharacterAspect, MyCharacterHeight));

            ring = (RectTransform)safeArea.Find("MyPickRing");
            if (ring == null)
                ring = BuildPickRing("MyPickRing", safeArea);
            Anchor(ring, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(MyCharacterCenterX, FromBottom(MyRingY)), PickRingSize);
            return character;
        }

        // 내 HUD (시안 .mehud 192 × 2 = 384) — 이름 줄 44(닉네임 36 · 「나의 턴」) · HP 줄 76(하트 · HP 68 · 손패 카드) · 막대 24. 머리 옆 스톱워치 80 · 「찹츄까지 n장」 알약도 여기서.
        private static MyPanelView BuildMyHudV3(RectTransform safeArea, Image character, RectTransform ring)
        {
            TurnTimerView watch = BuildStopwatchV3("MyTurnTimer", safeArea, MyWatchSize);
            Anchor((RectTransform)watch.transform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(MyWatchCenter.x, FromBottom(MyWatchCenter.y)), new Vector2(MyWatchSize, MyWatchSize));
            watch.gameObject.SetActive(false);

            // 「찹츄까지 n장」 — 흰 88% · 높이 48 · 반지름 16. 카드 아이콘(파란 띠) · 왕관(10장) · 글 28(숫자 36 파랑) (시안 .mgoal)
            RectTransform goal = NewRect("Goal", safeArea);
            Anchor(goal, Vector2.zero, Vector2.zero, new Vector2(GoalTopLeft.x, FromBottom(GoalTopLeft.y + 48f)), new Vector2(240f, 48f));
            HorizontalLayout(goal, new RectOffset(14, 18, 0, 0), 8f, TextAnchor.MiddleLeft, true, false);
            goal.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIShape goalFill = NewShape("Fill", goal, GoalFill, 16f, 0f, Color.clear);
            Stretch(goalFill.rectTransform);
            goalFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UIShape cardIcon = NewShape("CardIcon", goal, White, 5f, Line4, Ink);
            SetLayoutSize(cardIcon.rectTransform, 20f, 28f);
            Image cardBand = NewImage("Band", cardIcon.transform, GoalBlue);
            cardBand.rectTransform.anchorMin = new Vector2(0f, 1f);
            cardBand.rectTransform.anchorMax = Vector2.one;
            cardBand.rectTransform.pivot = new Vector2(0.5f, 1f);
            cardBand.rectTransform.anchoredPosition = new Vector2(0f, -Line4);
            cardBand.rectTransform.sizeDelta = new Vector2(-Line4 * 2f, 6f);
            Image goalCrown = FixedIcon("Crown", goal, Hud("icon_crown"), 32f, 28f, White);
            goalCrown.gameObject.SetActive(false);
            TMP_Text goalText = NewText("Text", goal, "찹츄까지 <size=36><color=#2F7CF6>10</color></size> 장", 28f, Ink, true);
            goalText.rectTransform.sizeDelta = new Vector2(160f, 48f);

            RectTransform root = NewRect("MyHud", safeArea);
            BottomLeft(root, EdgeX, MyHudBottom, MyHudWidth, MyHudHeight);

            // 이름 줄 — 닉네임(흰 테 글자 36, 최대 폭은 뷰가) + 「나의 턴」 · 「찹츄!」 꼬리표 (높이 42 · 테 4 · 26)
            RectTransform nameRow = NewRect("NameRow", root);
            TopLeft(nameRow, 0f, 0f, MyHudWidth, 44f);
            HorizontalLayout(nameRow, new RectOffset(), 12f, TextAnchor.MiddleLeft, true, false);
            TMP_Text nickname = StrokeText("NicknameText", nameRow, "두부", 36f, Ink, TextAlignmentOptions.Left);
            nickname.rectTransform.sizeDelta = new Vector2(80f, 44f);
            nickname.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement nicknameLayout = nickname.gameObject.AddComponent<LayoutElement>();
            RectTransform turnTag = NewRect("TurnTag", nameRow);
            turnTag.sizeDelta = new Vector2(120f, 42f);
            HorizontalLayout(turnTag, new RectOffset(16, 16, 0, 0), 6f, TextAnchor.MiddleCenter, true, false);
            UIShape turnTagFill = NewShape("Fill", turnTag, Turn, 21f, Line4, Ink);
            Stretch(turnTagFill.rectTransform);
            turnTagFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image turnTagCrown = FixedIcon("Crown", turnTag, Hud("icon_crown"), 28f, 24f, White);
            turnTagCrown.gameObject.SetActive(false);
            TMP_Text turnTagText = NewText("Text", turnTag, "나의 턴", 26f, Ink, true);
            turnTagText.rectTransform.sizeDelta = new Vector2(80f, 42f);
            turnTag.gameObject.SetActive(false);

            // HP 줄 — 하트 48 × 44 · HP 68 (흰 테 글자) · 손패 카드 80
            RectTransform stats = NewRect("Stats", root);
            TopLeft(stats, 0f, 54f, MyHudWidth, 76f);
            HorizontalLayout(stats, new RectOffset(), 28f, TextAnchor.MiddleLeft, true, false);
            BuildHpGroupV3(stats, 48f, 44f, 68f, out TMP_Text hpText);
            HandCountBadgeView badge = BuildHandBadgeV3(stats, true);

            // HP 막대 — 흰 바탕 · 잉크 테 4 · 반지름 12, 채움 안쪽 376 × 16
            UIShape bar = NewShape("Bar", root, White, 12f, Line4, Ink);
            TopLeft(bar.rectTransform, 0f, 140f, MyHudWidth, 24f);
            UIShape hpFill = NewShape("Fill", bar.transform, HpGreen, 8f, 0f, Color.clear);
            Anchor(hpFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Line4, 0f), new Vector2(MyHudWidth - Line4 * 2f, 16f));

            // 나를 고르는 동안 — HUD 둘레 빨간 점선 + 캐릭터까지 덮는 탭 영역 (맨 앞)
            UIShape pickOutline = NewShape("PickOutline", root, Color.clear, 20f, Line4, Tgt, UIShape.ShapeKind.RoundRect, 12f, 8f);
            Stretch(pickOutline.rectTransform, -10f, -10f, -10f, -10f);
            pickOutline.gameObject.SetActive(false);
            Image tapArea = NewImage("TapArea", root, Color.clear);
            float characterTop = MyFootY - MyCharacterHeight; // 캐릭터 머리 (위에서)
            float hudTop = HandView.ReferenceHeight - MyHudBottom - MyHudHeight;
            Stretch(tapArea.rectTransform, -16f, -(hudTop - characterTop + 16f), -44f, -16f);

            root.gameObject.AddComponent<CanvasGroup>(); // 올라온 손패가 덮는 동안 눌리지 않게 (HandPresenter.coveredHud)
            Canvas[] layers = { AddPickLayer(character.gameObject), AddPickLayer(ring.gameObject), AddPickLayer(root.gameObject) };
            root.gameObject.AddComponent<GraphicRaycaster>(); // 하위 캔버스의 탭은 그 캔버스의 레이캐스터가 받는다

            var view = root.gameObject.AddComponent<MyPanelView>();
            Wire(view,
                ("nicknameText", nickname), ("nicknameLayout", nicknameLayout), ("turnTag", turnTag.gameObject), ("turnTagFill", turnTagFill), ("turnTagText", turnTagText),
                ("turnTagCrown", turnTagCrown.gameObject), ("turnTimer", watch.gameObject), ("hpText", hpText), ("hpFill", hpFill.rectTransform), ("hpFillGraphic", hpFill),
                ("handBadge", badge), ("character", character), ("goalFill", goalFill), ("goalText", goalText), ("goalCrown", goalCrown.gameObject),
                ("pickRing", ring.gameObject), ("pickOutline", pickOutline.gameObject), ("tapArea", tapArea));
            WireArray(view, "pickLayers", layers);
            view.SetNickname("두부"); // 처음 모습도 실행 때와 같은 폭 규칙
            root.gameObject.AddComponent<MyPanelPresenter>();
            return view;
        }

        #endregion

        #region 상대 좌석

        // 좌석 하나 (시안 상대 머리 위 .tftp · .char · .ring · .dtag · .sweat · .shout · .swatch) — 캐릭터 · 링 · 부채꼴 · 이름표 · 팻말 · 땀 · 말풍선 · 스톱워치 · 탭 영역 순.
        // 고를 수 있는 동안 캐릭터 · 링 · 부채꼴 · 이름표 · 탭 영역을 가림판 위로 올린다 (OpponentSeatView.pickLayers).
        private static OpponentSeatView BuildSeatV3(RectTransform seat, int slot, DeckView deck, Transform referenceShout)
        {
            float cx = SeatFootX[slot];
            foreach (string name in new[] { "TurnSpot", "Plate", "TurnTimer", "TapArea" }) // v2 — 발밑 턴 타원 · 이름판 · 스톱워치 · 탭 영역
                DestroyChild(seat, name);

            Image character = FindIn<Image>(seat, "Character");
            if (character == null)
            {
                character = NewImage("Character", seat, White, CharacterSprite);
                character.preserveAspect = true;
            }
            Anchor(character.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0f), new Vector2(cx, -SeatFootY), new Vector2(SeatHeight * CharacterAspect, SeatHeight));

            var ring = (RectTransform)seat.Find("PickRing");
            if (ring == null)
                ring = BuildPickRing("PickRing", seat);
            CenterAt(ring, cx, SeatRingY, PickRingSize.x, PickRingSize.y);

            OpponentHandView hand = FindIn<OpponentHandView>(seat, "HandFan");
            if (hand == null)
                hand = BuildOpponentHand(seat, Vector2.zero, deck);
            var fan = (RectTransform)hand.transform;
            CenterAt(fan, cx + SeatHandOffset.x, SeatFootY + SeatHandOffset.y, 0f, 0f);
            fan.localScale = Vector3.one * SeatHandScale;

            RectTransform plate = BuildPlateV3(seat, cx, slot, out PlateParts parts);

            // 「사망 대기 n초」 · 「사망」 팻말 — 가슴, 높이 52 · 반지름 14 · 테 4 · 30 (시안 .dtag)
            RectTransform lifeTag = NewRect("LifeTag", seat);
            Anchor(lifeTag, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(cx, -SeatLifeTagTop), new Vector2(200f, 52f));
            HorizontalLayout(lifeTag, new RectOffset(20, 20, 0, 0), 8f, TextAnchor.MiddleCenter, true, false);
            lifeTag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIShape lifeTagFill = NewShape("Fill", lifeTag, Warn, 14f, Line4, Ink);
            Stretch(lifeTagFill.rectTransform);
            lifeTagFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            TMP_Text lifeTagText = NewText("Text", lifeTag, "사망 대기 5초", 30f, Ink, true);
            lifeTagText.rectTransform.sizeDelta = new Vector2(160f, 52f);
            lifeTag.gameObject.SetActive(false);

            Image sweat = NewImage("Sweat", seat, White, Hud("icon_sweat"));
            TopLeft(sweat.rectTransform, cx + SeatSweat.x, SeatSweat.y, 32f, 44f);
            sweat.preserveAspect = true;
            sweat.gameObject.SetActive(false);

            var shout = (RectTransform)seat.Find("Shout");
            if (shout == null)
                shout = BuildShoutV3(seat);
            else if (shout.pivot.x > 0.5f && referenceShout != null && referenceShout != shout)
                CopyShoutTail(referenceShout, shout); // v2 위 좌석은 왼쪽을 보는 말풍선 — v3 는 모두 오른쪽 (시안 .shout 18%)
            Anchor(shout, new Vector2(0f, 1f), new Vector2(0.18f, 0f), new Vector2(cx + SeatShoutPoint.x, -SeatShoutPoint.y), shout.sizeDelta);
            TMP_Text shoutText = shout.GetComponentInChildren<TMP_Text>(true);
            Material shoutInk = LoadMaterial(StickerInkMaterialPath); // 잉크 테 글자 (시안 -webkit-text-stroke 4px ink)
            if (shoutText != null && shoutInk != null)
                shoutText.fontSharedMaterial = shoutInk;
            shout.gameObject.SetActive(false);

            TurnTimerView watch = BuildStopwatchV3("TurnTimer", seat, 92f);
            CenterAt((RectTransform)watch.transform, cx + SeatWatchCenter.x, SeatWatchCenter.y, 92f, 92f);
            watch.gameObject.SetActive(false);

            Image tapArea = NewImage("TapArea", seat, Color.clear);
            TopLeft(tapArea.rectTransform, cx + SeatTapArea.x, SeatTapArea.y, SeatTapArea.width, SeatTapArea.height);

            int order = 0;
            foreach (Transform part in new Transform[] { character.transform, ring, fan, plate, lifeTag, sweat.transform, shout, watch.transform, tapArea.transform })
                part.SetSiblingIndex(order++);

            Canvas[] layers = { AddPickLayer(character.gameObject), AddPickLayer(ring.gameObject), AddPickLayer(fan.gameObject), AddPickLayer(plate.gameObject), AddPickLayer(tapArea.gameObject) };
            tapArea.gameObject.AddComponent<GraphicRaycaster>(); // 하위 캔버스의 탭은 그 캔버스의 레이캐스터가 받는다

            var view = seat.GetComponent<OpponentSeatView>();
            if (view == null)
                view = seat.gameObject.AddComponent<OpponentSeatView>();
            Wire(view,
                ("plate", plate), ("plateGroup", parts.Group), ("nameTag", parts.NameTag), ("nicknameText", parts.Nickname),
                ("nameOutlineMaterial", OutlineWhite), ("namePlainMaterial", BoldFont != null ? BoldFont.material : null), ("crown", parts.Crown), ("pickOutline", parts.PickOutline),
                ("handBadge", parts.Badge), ("heart", parts.Heart), ("hpText", parts.HpText), ("hpBar", parts.HpBar), ("hpFill", parts.HpFill.rectTransform), ("hpFillGraphic", parts.HpFill),
                ("grayMaterial", GrayMaterial), ("character", character), ("turnTimer", watch.gameObject), ("pickRing", ring.gameObject), ("shout", shout.gameObject), ("hand", hand),
                ("lifeTag", lifeTag.gameObject), ("lifeTagFill", lifeTagFill), ("lifeTagText", lifeTagText), ("sweat", sweat.gameObject), ("tapArea", tapArea));
            WireArray(view, "pickLayers", layers);
            return view;
        }

        private readonly struct PlateParts
        {
            public readonly CanvasGroup Group;
            public readonly UIShape NameTag;
            public readonly TMP_Text Nickname;
            public readonly GameObject Crown;
            public readonly GameObject PickOutline;
            public readonly HandCountBadgeView Badge;
            public readonly Image Heart;
            public readonly TMP_Text HpText;
            public readonly UIShape HpBar;
            public readonly UIShape HpFill;

            public PlateParts(CanvasGroup group, UIShape nameTag, TMP_Text nickname, GameObject crown, GameObject pickOutline, HandCountBadgeView badge, Image heart, TMP_Text hpText, UIShape hpBar, UIShape hpFill)
            {
                Group = group; NameTag = nameTag; Nickname = nickname; Crown = crown; PickOutline = pickOutline; Badge = badge; Heart = heart; HpText = hpText; HpBar = hpBar; HpFill = hpFill;
            }
        }

        // 머리 위 이름표 (시안 .tftp) — 192 × 140, 아래 끝 144 에 붙여 위로 쌓는다: 이름 칸 44 · 틈 6 · 손패 카드 60 + 하트 HP 44 · 틈 6 · HP 막대 24.
        // 이름 칸은 보통 투명(흰 테 글자), 턴 · 찹츄 · 사망 대기 · 겨눔이면 칠하고 바깥 잉크 선 4 (뷰). 겨눌 때 가운데를 축으로 1.1 배.
        private static RectTransform BuildPlateV3(RectTransform seat, float cx, int slot, out PlateParts parts)
        {
            RectTransform plate = NewRect("Plate", seat);
            Anchor(plate, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -(PlateBottom - PlateHeight / 2f)), new Vector2(PlateWidth, PlateHeight));
            VerticalLayoutGroup layout = plate.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var group = plate.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            // 이름 칸 — 높이 44 · 안여백 16 · 반지름 14 (+ 바깥 잉크 선 4). 고를 수 있으면 바깥 4 에 빨간 점선
            RectTransform nameRow = NewRect("NameRow", plate);
            nameRow.sizeDelta = new Vector2(PlateWidth, 44f);
            nameRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            RectTransform nameTag = NewRect("NameTag", nameRow);
            Anchor(nameTag, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 44f));
            HorizontalLayout(nameTag, new RectOffset(16, 16, 0, 0), 6f, TextAnchor.MiddleCenter, true, false);
            nameTag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIShape tagFill = NewShape("Fill", nameTag, Color.clear, 18f, Line4, Color.clear);
            Stretch(tagFill.rectTransform, -Line4, -Line4, -Line4, -Line4);
            tagFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UIShape pickOutline = NewShape("PickOutline", nameTag, Color.clear, 22f, Line4, Tgt, UIShape.ShapeKind.RoundRect, 12f, 8f);
            Stretch(pickOutline.rectTransform, -8f, -8f, -8f, -8f);
            pickOutline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            pickOutline.gameObject.SetActive(false);
            Image crown = FixedIcon("Crown", nameTag, Hud("icon_crown"), 32f, 28f, White);
            crown.gameObject.SetActive(false);
            TMP_Text nickname = StrokeText("NicknameText", nameTag, $"Player{slot + 2}", 36f, Ink);
            nickname.rectTransform.sizeDelta = new Vector2(100f, 44f);

            // 손패 카드 · 하트 HP — 가운데 정렬, 사이 16
            RectTransform stats = NewRect("Stats", plate);
            stats.sizeDelta = new Vector2(PlateWidth, 60f);
            HorizontalLayout(stats, new RectOffset(4, 4, 0, 0), 16f, TextAnchor.MiddleCenter, true, false);
            HandCountBadgeView badge = BuildHandBadgeV3(stats, false);
            Image heart = BuildHpGroupV3(stats, 40f, 36f, 44f, out TMP_Text hpText);

            // HP 막대 — 연분홍 · 잉크 테 4 · 반지름 6. 채움은 왼쪽을 안쪽 칸 밖으로 8 빼서 왼끝은 각지고 오른끝만 둥글게 (시안 .tg i)
            UIShape bar = NewShape("Bar", plate, HpBg, 6f, Line4, Ink);
            bar.rectTransform.sizeDelta = new Vector2(PlateWidth, 24f);
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            RectTransform barMask = NewRect("Mask", bar.transform);
            Stretch(barMask, Line4, Line4, Line4, Line4);
            barMask.gameObject.AddComponent<RectMask2D>();
            UIShape hpFill = NewShape("Fill", barMask, HpGreen, 8f, 0f, Color.clear);
            Anchor(hpFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-8f, 0f), new Vector2(PlateWidth, 16f));

            parts = new PlateParts(group, tagFill, nickname, crown.gameObject, pickOutline.gameObject, badge, heart, hpText, bar, hpFill);
            return plate;
        }

        // 찹츄 말풍선 (시안 .shout) — 캐릭터 오른쪽 위, 왼쪽 아래 꼬리. 새 씬에서만 만든다 (이미 있으면 사람이 맞춘 꼬리를 쓴다)
        private static RectTransform BuildShoutV3(Transform parent)
        {
            RectTransform root = NewRect("Shout", parent);
            root.sizeDelta = new Vector2(190f, 64f);
            Image fill = RoundBox(root, Ink, 5f, 28f, Chu);
            // 꼬리 (BubbleTail) — 바탕색 마름모 자리 · 크기는 사용자가 씬에서 맞춘 값 (2026-11-19, Seat1)
            BubbleTail(root, 0, Chu, new Vector2(40f, 0f), 24f, new Vector2(40.54f, 5.4f), 21f);
            TMP_Text text = NewText("Text", fill.transform, "찹츄!", 38f, White, true);
            Stretch(text.rectTransform);
            return root;
        }

        // 기준 말풍선(Seat1)의 꼬리 자리 · 크기를 그대로 옮긴다
        private static void CopyShoutTail(Transform from, Transform to)
        {
            foreach (string name in new[] { "TailInk", "TailFill" })
            {
                var source = (RectTransform)from.Find(name);
                var target = (RectTransform)to.Find(name);
                if (source == null || target == null)
                    continue;
                target.anchorMin = source.anchorMin;
                target.anchorMax = source.anchorMax;
                target.pivot = source.pivot;
                target.anchoredPosition = source.anchoredPosition;
                target.sizeDelta = source.sizeDelta;
                target.localRotation = source.localRotation;
            }
        }

        #endregion

        #region 공용 부품 (스톱워치 · 손패 카드 · 하트 HP)

        // 스톱워치 (시안 .swatch 46 · .swatch.me 40, × 2) — 흰 원 · 잉크 테 6 · 진행 링(바깥에서 6 안쪽, 반지름 28 까지) · 흰 얼굴(가운데 56 · 잉크 테 4) · 남은 초 36.
        // 2시 방향 네모 버튼 26 × 20(테 4 · 반지름 6 · 42° 기울임)은 원 뒤에. 위치 · 켜고 끄기는 부르는 쪽이.
        private static TurnTimerView BuildStopwatchV3(string name, Transform parent, float size)
        {
            RectTransform root = NewRect(name, parent);
            root.sizeDelta = new Vector2(size, size);

            UIShape button = NewShape("Button", root, White, 6f, Line4, Ink);
            CenterAt(button.rectTransform, size - 5f, 10f, 26f, 20f);
            button.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -42f);

            UIShape body = NewShape("Body", root, White, 0f, 6f, Ink, UIShape.ShapeKind.Ellipse);
            Stretch(body.rectTransform);
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
            UIShape hole = NewShape("Hole", root, White, 0f, 0f, Color.clear, UIShape.ShapeKind.Ellipse); // 링 안쪽 반지름 28 — 작은 스톱워치도 링 두께가 시안과 같게
            CenterIn(hole.rectTransform, 56f, 56f);
            UIShape face = NewShape("Face", root, White, 0f, Line4, Ink, UIShape.ShapeKind.Ellipse);
            Stretch(face.rectTransform, 18f, 18f, 18f, 18f);

            TMP_Text seconds = NewText("Text", root, "15", 36f, Ink, true);
            Stretch(seconds.rectTransform);

            var view = root.gameObject.AddComponent<TurnTimerView>();
            Wire(view, ("ring", ring), ("secondsText", seconds));
            return view;
        }

        // 손패 카드 두 장 (시안 .hcI 30 · .big 40, × 2) — 뒤 카드(−14° · 시계 반대) 위에 앞 카드(4°) + 장수
        private static HandCountBadgeView BuildHandBadgeV3(Transform parent, bool big)
        {
            float box = big ? 80f : 60f, radius = big ? 10f : 8f, font = big ? 40f : 30f;
            Rect back = big ? new Rect(2f, 8f, 50f, 66f) : new Rect(2f, 6f, 38f, 50f);
            Rect front = big ? new Rect(24f, 2f, 54f, 72f) : new Rect(18f, 2f, 40f, 54f);

            RectTransform root = NewRect("HandBadge", parent);
            SetLayoutSize(root, box, box);

            UIShape backCard = NewShape("Back", root, White, radius, Line4, Ink);
            CenterAt(backCard.rectTransform, back.x + back.width / 2f, back.y + back.height / 2f, back.width, back.height);
            backCard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            UIShape frontCard = NewShape("Front", root, White, radius, Line4, Ink);
            CenterAt(frontCard.rectTransform, front.x + front.width / 2f, front.y + front.height / 2f, front.width, front.height);
            frontCard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            TMP_Text count = NewText("CountText", frontCard.transform, "5", font, Ink, true);
            Stretch(count.rectTransform);

            var view = root.gameObject.AddComponent<HandCountBadgeView>();
            Wire(view, ("countText", count), ("face", frontCard));
            return view;
        }

        // 하트 + HP (시안 .hpI) — 하트 · 틈 6 · 숫자(흰 테 글자). 높이는 글자 크기(줄 높이 1)에 맞춰 늘고 준다 — 사망이면 이름표가 줄어든다
        private static Image BuildHpGroupV3(Transform parent, float heartWidth, float heartHeight, float fontSize, out TMP_Text hpText)
        {
            RectTransform group = NewRect("Hp", parent);
            group.sizeDelta = new Vector2(120f, fontSize);
            HorizontalLayout(group, new RectOffset(), 6f, TextAnchor.MiddleLeft, true, false);
            group.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image heart = FixedIcon("Heart", group, Hud("icon_heart"), heartWidth, heartHeight, White);
            hpText = StrokeText("HpText", group, "100", fontSize, Ink, TextAlignmentOptions.Left);
            hpText.rectTransform.sizeDelta = new Vector2(80f, fontSize);
            return heart;
        }

        #endregion

        #region 판 위 HUD (가운데)

        private readonly struct TableHudParts
        {
            public readonly Button DrawButton;
            public readonly TMP_Text DeckCount, PileCount, TrapCount;
            public readonly GameObject[] TrapSlots;

            public TableHudParts(Button drawButton, TMP_Text deckCount, TMP_Text pileCount, TMP_Text trapCount, GameObject[] trapSlots)
            {
                DrawButton = drawButton; DeckCount = deckCount; PileCount = pileCount; TrapCount = trapCount; TrapSlots = trapSlots;
            }
        }

        // 가운데 묶음 안, 가림판 아래 — 「뽑고 턴 끝」 · 「덱 n」 · 「버림 n」 · 내 함정 빈 칸 3 · 「내 함정 n/3」. 눌리는 것은 「뽑고 턴 끝」 뿐 (함정 칸 탭은 3D 콜라이더)
        private static TableHudParts BuildTableHudV3(RectTransform center)
        {
            RectTransform root = NewRect("TableHud", center);
            Stretch(root);

            // 「뽑고 턴 끝」 — 덱 바로 위 가운데. 노랑 · 테 4 · 반지름 16 · 아래 그림자 4 · 아이콘 26 + 26 (시안 .drawb v3.19)
            RectTransform draw = NewRect("DrawButton", root);
            Anchor(draw, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(DrawButtonTop.x, -DrawButtonTop.y), new Vector2(160f, 48f));
            HorizontalLayout(draw, new RectOffset(12, 16, 0, 0), 6f, TextAnchor.MiddleCenter, true, false);
            draw.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIShape shadow = NewShape("Shadow", draw, Ink, 16f, 0f, Color.clear);
            Stretch(shadow.rectTransform, 0f, 4f, 0f, -4f);
            shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UIShape fill = NewShape("Fill", draw, Turn, 16f, Line4, Ink);
            Stretch(fill.rectTransform);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            fill.raycastTarget = true;
            fill.raycastPadding = new Vector4(-12f, -20f, -12f, -20f); // 손가락 판정은 넓게 (시안 #game.touch .drawb::before inset −10 −6)
            FixedIcon("Icon", draw, Hud("icon_draw"), 26f, 26f, Ink);
            NewText("Text", draw, "뽑고 턴 끝", 26f, Ink, true).rectTransform.sizeDelta = new Vector2(120f, 48f);
            Button button = draw.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            draw.gameObject.SetActive(false);

            TMP_Text deckCount = BuildPileLabelV3(root, "DeckLabel", DeckLabelTop, "덱 0");
            TMP_Text pileCount = BuildPileLabelV3(root, "PileLabel", PileLabelTop, "버림 0");

            // 내 함정 빈 칸 — 점선 테 4(#e3957d) · 흰 55% · 발바닥 40. 칸이 차면 3D 카드가 보이고 빈 칸은 꺼진다 (TrapSlotView)
            RectTransform traps = NewRect("MyTraps", root);
            Stretch(traps);
            var slots = new GameObject[MyTrapSlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                UIShape slot = NewShape($"Slot{i}", traps, MyTrapFill, 18f, Line4, TrapDash, UIShape.ShapeKind.RoundRect, 12f, 8f);
                TopLeft(slot.rectTransform, MyTrapFirst.x + MyTrapStep * i, MyTrapFirst.y, MyTrapSize.x, MyTrapSize.y);
                Icon("Paw", slot.transform, Hud("icon_paw"), 40f, TrapDash);
                slots[i] = slot.gameObject;
            }

            // 「내 함정 n/3」 — 갈색 원 40(흰 발바닥 24) + 흰 테 글자 30 (시안 .mtlab)
            RectTransform label = NewRect("MyTrapLabel", root);
            Anchor(label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(MyTrapFirst.x, -MyTrapLabelTop), new Vector2(220f, 40f));
            HorizontalLayout(label, new RectOffset(), 10f, TextAnchor.MiddleLeft, true, false);
            label.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIShape badge = NewShape("Badge", label, TrapBadge, 0f, 0f, Color.clear, UIShape.ShapeKind.Ellipse);
            SetLayoutSize(badge.rectTransform, 40f, 40f);
            Icon("Paw", badge.transform, Hud("icon_paw"), 24f, White);
            TMP_Text trapCount = StrokeText("Text", label, "내 함정 0/3", 30f, Ink, TextAlignmentOptions.Left);
            trapCount.rectTransform.sizeDelta = new Vector2(160f, 40f);

            return new TableHudParts(button, deckCount, pileCount, trapCount, slots);
        }

        // 더미 이름표 「덱 n」 · 「버림 n」 — 흰 · 반지름 18 · 높이 52 · 안여백 22 · 32 (시안 .lab), 위 가운데 기준
        private static TMP_Text BuildPileLabelV3(RectTransform parent, string name, Vector2 top, string text)
        {
            RectTransform root = NewRect(name, parent);
            Anchor(root, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(top.x, -top.y), new Vector2(120f, 52f));
            HorizontalLayout(root, new RectOffset(22, 22, 0, 0), 0f, TextAnchor.MiddleCenter, true, false);
            root.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIShape fill = NewShape("Fill", root, White, 18f, 0f, Color.clear);
            Stretch(fill.rectTransform);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            TMP_Text label = NewText("Text", root, text, 32f, Ink, true);
            label.rectTransform.sizeDelta = new Vector2(80f, 52f);
            return label;
        }

        // 3D 층을 v3 자리로 — 덱 · 버림 더미(가운데 · 1.5 배), 상대 함정 칸(발밑 한 줄 · 40° 눕힘), 내 함정 칸(세운 카드). 3D 「덱 n」 · 「버림 n」 글자와 내 빈 칸 판은 2D 로 바꾼다.
        private static void ApplyTableLayoutV3(DeckView deck, DiscardPileView pile, MyTrapsPresenter myTraps, TableHudParts hud)
        {
            deck.transform.position = TableBuilder.CanvasToWorld(DeckCenterV3.x, DeckCenterV3.y);
            deck.transform.localScale = Vector3.one * DeckScaleV3;
            pile.transform.position = TableBuilder.CanvasToWorld(PileCenterV3.x, PileCenterV3.y);
            pile.transform.localScale = Vector3.one * PileScaleV3;
            DestroyChild(deck.transform, "CountText");
            DestroyChild(pile.transform, "CountText");
            Wire(deck, ("countText", hud.DeckCount), ("drawButton", hud.DrawButton));
            Wire(pile, ("countText", hud.PileCount));

            Transform trapRows = myTraps.transform.parent;
            Quaternion tilt = Quaternion.Euler(TableBuilder.TableTilt, 0f, 0f);
            for (int row = 0; row < SeatTrapX.Length; row++)
            {
                Transform seatRow = trapRows.Find($"Seat{row + 1}");
                for (int i = 0; seatRow != null && i < SeatTrapX[row].Length && i < seatRow.childCount; i++)
                {
                    Transform slot = seatRow.Find($"Slot{i}");
                    if (slot == null)
                        continue;
                    float cardHeight = SlotCardHeight(slot);
                    slot.rotation = tilt;
                    slot.localScale = Vector3.one * SeatTrapScale;
                    slot.position = TableBuilder.CanvasToWorld(SeatTrapX[row][i], SeatTrapY) - tilt * new Vector3(0f, cardHeight * SeatTrapScale / 2f, 0f); // 칸 루트 = 카드 아래 가운데
                }
            }

            for (int i = 0; i < MyTrapSlotCount; i++)
            {
                Transform slot = myTraps.transform.Find($"Slot{i}");
                if (slot == null)
                {
                    Debug.LogError($"[{nameof(GameScreenBuilder)}] 내 함정 칸 Slot{i} 이 없다 — 2D 빈 칸 수({MyTrapSlotCount})와 3D 칸 수가 다르다.");
                    continue;
                }
                slot.rotation = Quaternion.identity;
                slot.localScale = Vector3.one * MyTrapCardScale;
                slot.position = TableBuilder.CanvasToWorld(MyTrapFirst.x + MyTrapStep * i + MyTrapSize.x / 2f, MyTrapFirst.y + MyTrapSize.y);
                DestroyChild(slot, "Empty");
                Wire(slot.GetComponent<TrapSlotView>(), ("empty", hud.TrapSlots[i]));
            }
            Wire(myTraps, ("countLabel", hud.TrapCount));
        }

        // 칸에 들어갈 카드 높이 (칸 로컬) — 빈 칸 판(Empty)이 카드 가운데에 있다 (TableBuilder)
        private static float SlotCardHeight(Transform slot)
        {
            Transform empty = slot.Find("Empty");
            Transform filled = slot.Find("Filled");
            return (empty != null ? empty.localPosition.y : filled != null ? filled.localPosition.y : 1.68f) * 2f;
        }

        #endregion

        #region 도우미

        private static float FromBottom(float y) => HandView.ReferenceHeight - y;

        private static Material LoadMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 글자 머티리얼이 없다: {path}");
            return material;
        }

        // 흰 테 글자 (태백체) — 시안 -webkit-text-stroke #fff · paint-order stroke fill
        private static TMP_Text StrokeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            TMP_Text label = NewText(name, parent, text, size, color, true, alignment);
            if (OutlineWhite != null)
                label.fontSharedMaterial = OutlineWhite;
            return label;
        }

        /// <summary>UIShape 한 장 — 바탕 = fill, 테 = 안쪽 border. 점선은 dash · gap (px). 눌리지 않는다.</summary>
        private static UIShape NewShape(string name, Transform parent, Color fill, float radius, float border, Color borderColor,
            UIShape.ShapeKind kind = UIShape.ShapeKind.RoundRect, float dash = 0f, float gap = 0f)
        {
            UIShape shape = NewRect(name, parent).gameObject.AddComponent<UIShape>();
            shape.color = fill;
            shape.raycastTarget = false;
            shape.Set(kind, radius, border, borderColor, 1f, dash, gap);
            return shape;
        }

        // 고정 크기 아이콘 — 레이아웃 안에서도 이 크기
        private static Image FixedIcon(string name, Transform parent, Sprite sprite, float width, float height, Color color)
        {
            Image image = NewImage(name, parent, color, sprite);
            image.preserveAspect = true;
            SetLayoutSize(image.rectTransform, width, height);
            return image;
        }

        private static void SetLayoutSize(RectTransform rect, float width, float height)
        {
            rect.sizeDelta = new Vector2(width, height);
            LayoutElement layout = rect.GetComponent<LayoutElement>();
            if (layout == null)
                layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
        }

        // 부모 가운데에 이 크기로
        private static void CenterIn(RectTransform rect, float width, float height)
            => Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));

        private static T FindIn<T>(Transform parent, string name) where T : Component
        {
            Transform child = parent != null ? parent.Find(name) : null;
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }

        // 이름 순서대로 맨 앞부터 — 없는 이름은 건너뛰고, 목록에 없는 자식은 그 뒤에 원래 순서로
        private static void OrderChildren(Transform parent, params string[] names)
        {
            int index = 0;
            foreach (string name in names)
            {
                Transform child = parent.Find(name);
                if (child != null)
                    child.SetSiblingIndex(index++);
            }
        }

        #endregion
    }
}
