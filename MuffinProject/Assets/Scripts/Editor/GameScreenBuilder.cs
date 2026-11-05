using System.IO;
using Chapchu.Cards;
using Chapchu.Network;
using Chapchu.UI;
using Chapchu.UI.GameScreen;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Splines;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 인게임 화면 UI 를 열려 있는 씬에 조립한다 (메뉴 Chapchu/Game Screen/Build UI).
    /// 수치 · 색은 docs/systems/10-ui.md 2-1 (HTML 시안 × 2). 손패는 부채꼴 · 카드 프리팹(종류별 3개)까지, 상대 손패 · 히스토리 항목은 만들지 않는다.
    /// 프레젠터도 같이 붙인다 — 영역마다 하나, 캔버스 루트에 연출 큐(GameScreenPresenter) 하나.
    /// 이미 GameCanvas 가 있으면 사람이 고친 것을 덮어쓰지 않도록 멈춘다 — 지우고 다시 실행한다.
    /// Galmuri11 TMP 폰트 에셋이 없으면 먼저 만든다.
    /// </summary>
    public static class GameScreenBuilder
    {
        private const string CanvasName = "GameCanvas";
        private const string RegularFontPath = "Assets/Fonts/GlaMuri/Galmuri11.ttf";
        private const string BoldFontPath = "Assets/Fonts/GlaMuri/Galmuri11-Bold.ttf";
        private const string RegularFontAssetPath = "Assets/Fonts/GlaMuri/Galmuri11 SDF.asset";
        private const string BoldFontAssetPath = "Assets/Fonts/GlaMuri/Galmuri11-Bold SDF.asset";
        private const string BackgroundPath = "Assets/Sprites/UI/Background/bg_ingame.png";
        private const string RoundSpritePath = "Assets/Sprites/UI/Common/ui_round64.png";
        private const string HandCardPrefabFolder = "Assets/Prefab/UI/GameScreen";

        // TMP 동적 폰트 생성값 — Galmuri9 SDF 와 같은 방식(SDFAA · 여백 5), 아틀라스는 2048 로 시작해 필요하면 늘어난다.
        private const int FontSamplingPointSize = 44;
        private const int FontAtlasPadding = 5;
        private const int FontAtlasSize = 2048;

        // 10-ui.md 2-1 「색 · 글꼴」
        private static readonly Color Ink = Hex(0x111111);
        private static readonly Color Blue = Hex(0x0A9CFF);
        private static readonly Color Orange = Hex(0xF54A18);
        private static readonly Color Green = Hex(0x6FA33A);
        private static readonly Color White = Color.white;
        private static readonly Color Shadow = new Color(0.067f, 0.067f, 0.067f, 0.35f);
        private static readonly Color Dim = new Color(0.067f, 0.067f, 0.067f, 0.55f);
        private static readonly Color DisabledInk = Hex(0x9AA3AD);
        private static readonly Color DisabledTagFill = Hex(0xE6E9ED);
        private static readonly Color DisabledTagInk = Hex(0x6B7480);
        private static readonly Color[] SeatColors = { Blue, Orange, Green }; // 좌석 1 · 2 · 3 (10-ui.md 2-1 「색 · 글꼴」)
        private const float SeatHeaderHeight = 82f; // 이름 줄 50 + 간격 8 + HP 줄 24 — 턴 외곽선이 감싸는 범위

        private static TMP_FontAsset _regularFont;
        private static TMP_FontAsset _boldFont;

        [MenuItem("Chapchu/Game Screen/Build UI")]
        public static void Build()
        {
            if (GameObject.Find(CanvasName) != null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 씬에 {CanvasName} 가 이미 있다. 덮어쓰지 않는다 — 지우고 다시 실행할 것.");
                return;
            }

            _regularFont = EnsureFontAsset(RegularFontPath, RegularFontAssetPath);
            _boldFont = EnsureFontAsset(BoldFontPath, BoldFontAssetPath);

            GameScreenView screen = BuildCanvas(out RectTransform canvasRect);
            BuildBackground(canvasRect);

            DeckView deck = BuildDeck(canvasRect);

            RectTransform safeArea = NewRect("SafeArea", canvasRect);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeArea>();

            RoomCodeView roomCode = BuildRoomCodeTag(safeArea);
            HistoryView history = BuildHistory(safeArea);
            OpponentSeatsPresenter seats = BuildOpponentSeats(safeArea, deck);
            MyPanelView myPanel = BuildMyPanel(safeArea);
            RectTransform dropZone = BuildDropZone(safeArea);
            RectTransform handArea = BuildHandArea(safeArea);
            HandPresenter hand = BuildHand(handArea, deck, dropZone, seats);

            VictoryOverlayView victory = BuildVictoryOverlay(canvasRect);

            EnsureEventSystem();

            Wire(screen,
                ("roomCode", roomCode), ("history", history), ("myPanel", myPanel), ("deck", deck),
                ("handArea", handArea), ("dropZone", dropZone), ("victoryOverlay", victory));
            WireArray(screen, "opponentSeats", seats.GetComponentsInChildren<OpponentSeatView>(true));
            screen.gameObject.AddComponent<RejectToastPresenter>();

            // 연출 큐 — GameEvents 를 받아 영역 프레젠터에 순서대로 넘긴다 (CODE_CONVENTION 9절)
            var presenter = screen.gameObject.AddComponent<GameScreenPresenter>();
            Wire(presenter,
                ("deck", deck.GetComponent<DeckPresenter>()), ("hand", hand), ("myPanel", myPanel.GetComponent<MyPanelPresenter>()), ("seats", seats));

            EditorSceneManager.MarkSceneDirty(canvasRect.gameObject.scene);
            Selection.activeGameObject = canvasRect.gameObject;
            Debug.Log($"[{nameof(GameScreenBuilder)}] {CanvasName} 조립 완료 — 씬을 저장할 것.");
        }

        /// <summary>손패 카드 프리팹 3개를 같은 경로에 다시 만든다 (GUID 유지 — 씬 · HandView 참조가 그대로). 사람이 프리팹을 손봤다면 지워진다.</summary>
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

        #region 영역

        private static GameScreenView BuildCanvas(out RectTransform canvasRect)
        {
            var go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            canvasRect = (RectTransform)go.transform;

            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            // 15-screen.md 3절 #3
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            return go.AddComponent<GameScreenView>();
        }

        private static void BuildBackground(RectTransform parent)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (sprite == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 배경 스프라이트가 없다: {BackgroundPath}");

            Image image = NewImage("Background", parent, White, sprite);
            Anchor(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));

            // CSS cover — 비율 유지, 넘침 잘림 (15-screen.md 3절 #6)
            AspectRatioFitter fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1920f / 1080f;
        }

        private static RoomCodeView BuildRoomCodeTag(RectTransform parent)
        {
            Image tag = NewImage("RoomCodeTag", parent, Ink);
            TopLeft(tag.rectTransform, 36f, 32f, 320f, 64f);
            HorizontalLayout(tag.rectTransform, new RectOffset(28, 28, 0, 0), 0f, TextAnchor.MiddleLeft, true, true);
            tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text text = NewText("CodeText", tag.transform, "| ROOM ----", 28f, White, true, TextAlignmentOptions.Left);

            var view = tag.gameObject.AddComponent<RoomCodeView>();
            Wire(view, ("codeText", text));
            tag.gameObject.AddComponent<RoomCodePresenter>();
            return view;
        }

        private static HistoryView BuildHistory(RectTransform parent)
        {
            RectTransform root = NewRect("History", parent);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.offsetMin = new Vector2(36f, 36f + MyPanelSize.y + 28f); // 내 패널 위 28
            root.offsetMax = new Vector2(36f + 240f, -148f);

            HardShadow(root, Shadow, 10f, 10f);
            Image fill = Box("Box", root, Ink, 8f, White);

            Image header = NewImage("Header", fill.transform, Ink);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = Vector2.zero;
            header.rectTransform.sizeDelta = new Vector2(0f, 48f);
            TMP_Text headerText = NewText("HeaderText", header.transform, "| HISTORY", 24f, White, true, TextAlignmentOptions.Left);
            Stretch(headerText.rectTransform, 20f, 0f, 0f, 0f);

            RectTransform content = NewRect("Content", fill.transform);
            Stretch(content, 0f, 48f, 0f, 0f);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var view = root.gameObject.AddComponent<HistoryView>();
            Wire(view, ("content", content));
            return view;
        }

        // 상대 좌석 영역 — 좌 336 · 우 36 · 위 124 · 높이 346 (10-ui.md 2-1 「배치」). 슬롯 3개 = 좌석 인덱스 1 · 2 · 3.
        private static OpponentSeatsPresenter BuildOpponentSeats(RectTransform parent, DeckView deck)
        {
            RectTransform area = NewRect("OpponentSeats", parent);
            area.anchorMin = new Vector2(0f, 1f);
            area.anchorMax = new Vector2(1f, 1f);
            area.pivot = new Vector2(0.5f, 1f);
            area.offsetMin = new Vector2(336f, -124f - 346f);
            area.offsetMax = new Vector2(-36f, -124f);

            var seats = new OpponentSeatView[3];
            for (int i = 0; i < seats.Length; i++)
                seats[i] = BuildOpponentSeat(area, i, deck);

            var presenter = area.gameObject.AddComponent<OpponentSeatsPresenter>();
            WireArray(presenter, "seats", seats);
            return presenter;
        }

        // 슬롯 460 × 346 — 영역을 3등분한 가운데(중심 x 594 · 1110 · 1626). 이름 줄 50 · HP 줄 24 (10-ui.md 2-1 「좌석 슬롯」).
        // 캐릭터 그림 · 함정 슬롯은 보류 (2026-11-03) — 캐릭터 자리(141 × 188)는 뒷면 부채꼴 앵커의 기준으로만 둔다.
        // 턴 외곽선(초록, 바깥 8)은 이름 줄 + HP 줄(위 82)만 감싼다 — 캐릭터를 덮지 않는다 (사용자 결정 2026-11-03). 슬롯 뒤, 기본 활성 아님.
        private static OpponentSeatView BuildOpponentSeat(RectTransform area, int slot, DeckView deck)
        {
            Color seatColor = SeatColors[slot];
            int seatIndex = slot + 1;

            RectTransform root = NewRect($"Seat{seatIndex}", area);
            Anchor(root, new Vector2((slot + 0.5f) / 3f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(460f, 346f));

            RectTransform turnOutline = NewRect("TurnOutline", root);
            turnOutline.anchorMin = new Vector2(0f, 1f);
            turnOutline.anchorMax = new Vector2(1f, 1f);
            turnOutline.pivot = new Vector2(0f, 1f);
            turnOutline.anchoredPosition = Vector2.zero;
            turnOutline.sizeDelta = new Vector2(0f, SeatHeaderHeight);
            BuildFrame("Frame", turnOutline, Green, 8f);
            turnOutline.gameObject.SetActive(false);

            // 대상 선택 외곽선(파랑) — 턴 외곽선과 같은 범위. 기본 활성 아님 (10-ui.md 6절, 사용자 결정 2026-11-05).
            RectTransform targetOutline = NewRect("TargetOutline", root);
            targetOutline.anchorMin = new Vector2(0f, 1f);
            targetOutline.anchorMax = new Vector2(1f, 1f);
            targetOutline.pivot = new Vector2(0f, 1f);
            targetOutline.anchoredPosition = Vector2.zero;
            targetOutline.sizeDelta = new Vector2(0f, SeatHeaderHeight);
            BuildFrame("Frame", targetOutline, Blue, 8f);
            targetOutline.gameObject.SetActive(false);

            // 탭 영역 — 슬롯 전체를 덮는 투명 이미지. 레이캐스트는 대상 선택 중에만 켠다 (덱과 60 겹치므로 평소엔 덱 탭을 막지 않게).
            Image tapArea = NewImage("TapArea", root, Color.clear);
            Stretch(tapArea.rectTransform);
            tapArea.transform.SetAsFirstSibling();

            // 이름 줄
            RectTransform nameRow = NewRect("NameRow", root);
            nameRow.anchorMin = new Vector2(0f, 1f);
            nameRow.anchorMax = new Vector2(1f, 1f);
            nameRow.pivot = new Vector2(0f, 1f);
            nameRow.anchoredPosition = Vector2.zero;
            nameRow.sizeDelta = new Vector2(0f, 50f);
            HorizontalLayout(nameRow, new RectOffset(), 12f, TextAnchor.MiddleLeft, true, true);

            Image nameTag = NewImage("NameTag", nameRow, seatColor);
            HorizontalLayout(nameTag.rectTransform, new RectOffset(20, 44, 8, 8), 0f, TextAnchor.MiddleLeft, true, true);
            nameTag.gameObject.AddComponent<LayoutElement>().minHeight = 50f;
            TMP_Text nicknameText = NewText("NicknameText", nameTag.transform, $"|Player#{seatIndex + 1}", 28f, White, true, TextAlignmentOptions.Left);

            Image handCountFill = LayoutBox("HandCountBox", nameRow, Ink, 4, White, new RectOffset(10, 10, 2, 2));
            handCountFill.transform.parent.gameObject.AddComponent<LayoutElement>().minWidth = 44f;
            TMP_Text handCountText = NewText("HandCountText", handCountFill.transform, "5", 24f, Ink, true);

            Image badge = BuildChapchuBadge(nameRow);

            // HP 줄 — 바는 수치 폭(60) + 간격 12 만큼 짧게
            RectTransform hpBar = NewRect("HpBar", root);
            hpBar.anchorMin = new Vector2(0f, 1f);
            hpBar.anchorMax = new Vector2(1f, 1f);
            hpBar.pivot = new Vector2(0f, 1f);
            hpBar.offsetMin = new Vector2(0f, -58f - 24f);
            hpBar.offsetMax = new Vector2(-72f, -58f);
            Image hpTrack = Box("Track", hpBar, Ink, 6f, White);
            Image hpFill = NewImage("Fill", hpTrack.transform, seatColor);
            Stretch(hpFill.rectTransform);
            TMP_Text hpText = NewText("HpText", root, "100", 22f, Ink, true, TextAlignmentOptions.Left);
            Anchor(hpText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -58f), new Vector2(60f, 24f));

            // 캐릭터 자리 — HP 줄 아래 16 (줄 간격 8 · 16 · 16). 그림은 보류라 빈 Rect.
            RectTransform character = NewRect("Character", root);
            Anchor(character, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(SeatHeaderHeight + 16f)), OpponentCharacterSize);

            OpponentHandView hand = BuildOpponentHand(character, deck);

            var view = root.gameObject.AddComponent<OpponentSeatView>();
            Wire(view,
                ("nicknameText", nicknameText), ("handCountText", handCountText), ("hand", hand), ("chapchuBadge", badge.gameObject),
                ("hpFill", hpFill.rectTransform), ("hpFillImage", hpFill), ("hpText", hpText), ("turnOutline", turnOutline.gameObject), ("targetOutline", targetOutline.gameObject), ("tapArea", tapArea));
            SetColor(view, "seatColor", seatColor);
            return view;
        }

        // 뒷면 부채꼴 — 캐릭터 상자의 30% × 24%(42 × 45), 아래에서 20%(38) 위, 아래 가운데 기준 (시안 .fan). 카드는 상자 높이 45 · 9:13.
        // 스플라인은 내 손패와 같은 3점 원호 — 양 끝 (±8, −4) 면 1/10 간격 한 칸에 약 10° (시안 rotate ±20° / 5장).
        private static readonly Vector2 OpponentCharacterSize = new Vector2(141f, 188f);
        private static readonly Vector2 OpponentFanSize = new Vector2(42f, 45f);
        private const float OpponentFanBottom = 38f;
        private const float OpponentFanHalfWidth = 8f;
        private const float OpponentFanDrop = 4f;
        private static readonly Vector2 BackCardSize = new Vector2(31f, 45f); // 9:13, 높이 = 부채꼴 상자

        private static OpponentHandView BuildOpponentHand(RectTransform character, DeckView deck)
        {
            RectTransform fan = NewRect("HandFan", character);
            Anchor(fan, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, OpponentFanBottom), OpponentFanSize);

            SplineContainer container = fan.gameObject.AddComponent<SplineContainer>();
            Spline spline = container.Spline;
            spline.Clear();
            spline.Add(new BezierKnot(new float3(-OpponentFanHalfWidth, -OpponentFanDrop, 0f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(float3.zero), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(OpponentFanHalfWidth, -OpponentFanDrop, 0f)), TangentMode.AutoSmooth);

            var view = fan.gameObject.AddComponent<OpponentHandView>();
            Wire(view, ("backCardPrefab", EnsureBackCardPrefab()), ("drawOrigin", deck.transform));
            return view;
        }

        // 뒷면 카드 프리팹 — 시안 .fan span: 파랑 바탕 · 흰 테두리 2 · 바깥 검정 링 2, 피벗 아래 가운데. 이미 있으면 덮어쓰지 않는다.
        private static RectTransform EnsureBackCardPrefab()
        {
            string path = $"{HandCardPrefabFolder}/HandCard_Back.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return (RectTransform)existing.transform;

            if (!AssetDatabase.IsValidFolder(HandCardPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefab/UI", "GameScreen");

            RectTransform root = NewRect("HandCard_Back", null);
            root.sizeDelta = BackCardSize;
            root.pivot = new Vector2(0.5f, 0f);

            Image ring = NewImage("Ring", root, Ink);
            Stretch(ring.rectTransform);
            Image fill = Box("Box", root, White, 2f, Blue);
            Stretch((RectTransform)fill.transform.parent, 2f, 2f, 2f, 2f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return (RectTransform)saved.transform;
        }

        private static Image BuildChapchuBadge(Transform parent, float scale = 1f)
        {
            int S(float v) => Mathf.RoundToInt(v * scale);
            Image fill = LayoutBox("ChapchuBadge", parent, Ink, S(4), Orange, new RectOffset(S(14), S(14), S(4), S(4)));
            Image border = fill.transform.parent.GetComponent<Image>();
            HardShadow(border.rectTransform, Ink, S(4), S(4));
            NewText("Text", fill.transform, "찹츄!", S(22), White, true);
            border.gameObject.SetActive(false);
            return border;
        }

        // 내 패널은 시안 수치(460 × 380 기준)를 MyPanelScale 배로 줄여 히스토리(폭 240)보다 조금 크게 둔다 (2026-11-03).
        private const float MyPanelScale = 0.65f;
        private static readonly Vector2 MyPanelSize = new Vector2(300f, 248f);

        private static MyPanelView BuildMyPanel(RectTransform parent)
        {
            float S(float v) => Mathf.Round(v * MyPanelScale);
            int I(float v) => Mathf.RoundToInt(v * MyPanelScale);

            RectTransform root = NewRect("MyPanel", parent);
            Anchor(root, Vector2.zero, Vector2.zero, new Vector2(36f, 36f), MyPanelSize);

            HardShadow(root, Shadow, 10f, 10f);
            Image fill = Box("Box", root, Green, S(8), White);

            // 헤더
            Image header = NewImage("Header", fill.transform, Green);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = Vector2.zero;
            header.rectTransform.sizeDelta = new Vector2(0f, S(52));
            HorizontalLayout(header.rectTransform, new RectOffset(I(20), I(20), 0, 0), S(12), TextAnchor.MiddleLeft, true, true);

            TMP_Text nicknameText = NewText("NicknameText", header.transform, "|Player#1", S(28), White, true, TextAlignmentOptions.Left);

            Image meTag = NewImage("MeTag", header.transform, White);
            HorizontalLayout(meTag.rectTransform, new RectOffset(I(10), I(10), I(4), I(4)), 0f, TextAnchor.MiddleCenter, true, true);
            NewText("Text", meTag.transform, "나", S(20), Green, false);

            Image badge = BuildChapchuBadge(header.transform, MyPanelScale);

            RectTransform spacer = NewRect("Spacer", header.transform);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            TMP_Text hpText = NewText("HpText", header.transform, "HP 100", S(22), White, false, TextAlignmentOptions.Right);

            // HP 바
            RectTransform hpBar = NewRect("HpBar", fill.transform);
            hpBar.anchorMin = new Vector2(0f, 1f);
            hpBar.anchorMax = new Vector2(1f, 1f);
            hpBar.pivot = new Vector2(0f, 1f);
            hpBar.offsetMin = new Vector2(S(24), -S(72) - S(20));
            hpBar.offsetMax = new Vector2(-S(24), -S(72));
            Image hpTrack = Box("Track", hpBar, Ink, S(4), White);
            Image hpFill = NewImage("Fill", hpTrack.transform, Green);
            Stretch(hpFill.rectTransform);

            // 턴 줄
            RectTransform turnRow = NewRect("TurnRow", fill.transform);
            turnRow.anchorMin = new Vector2(0f, 1f);
            turnRow.anchorMax = new Vector2(1f, 1f);
            turnRow.pivot = new Vector2(0f, 1f);
            turnRow.offsetMin = new Vector2(S(24), -S(108) - S(52));
            turnRow.offsetMax = new Vector2(-S(24), -S(108));

            Image turnTag = NewImage("TurnTag", turnRow, Orange);
            Anchor(turnTag.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(S(120), S(44)));
            HorizontalLayout(turnTag.rectTransform, new RectOffset(I(16), I(16), I(8), I(8)), 0f, TextAnchor.MiddleCenter, true, true);
            FitBoth(turnTag.gameObject);
            TMP_Text turnTagText = NewText("Text", turnTag.transform, "나의 턴!", S(24), White, true);

            RectTransform timer = NewRect("Timer", turnRow);
            Anchor(timer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(S(100), S(52)));
            HorizontalLayout(timer, new RectOffset(), S(6), TextAnchor.LowerCenter, true, true);
            FitBoth(timer.gameObject);
            TMP_Text secondsText = NewText("SecondsText", timer, "20", S(52), Ink, true);
            NewText("UnitText", timer, "초", S(22), Ink, false);

            TMP_Text handCountText = NewText("HandCountText", turnRow, "5/10장", S(22), Ink, false, TextAlignmentOptions.Right);
            Anchor(handCountText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(S(120), S(52)));

            // 찹츄 버튼 (기본: 비활성 모양)
            RectTransform button = NewRect("ChapchuButton", fill.transform);
            button.anchorMin = new Vector2(0f, 0f);
            button.anchorMax = new Vector2(1f, 0f);
            button.pivot = new Vector2(0f, 0f);
            button.offsetMin = new Vector2(S(24), S(20));
            button.offsetMax = new Vector2(-S(24), S(20) + S(104));

            RectTransform ring = NewRect("Ring", button);
            Stretch(ring);
            Image ringOuter = NewImage("Outer", ring, Orange);
            Stretch(ringOuter.rectTransform, -S(16), -S(16), -S(16), -S(16));
            Image ringInner = NewImage("Inner", ring, White);
            Stretch(ringInner.rectTransform, -S(8), -S(8), -S(8), -S(8));
            ring.gameObject.SetActive(false);

            Image shadow = HardShadow(button, DisabledInk, S(8), S(8));
            shadow.transform.SetAsLastSibling();
            Image buttonFill = Box("Box", button, Ink, S(8), White, true);

            RectTransform content = NewRect("Content", buttonFill.transform);
            Stretch(content);
            HorizontalLayout(content, new RectOffset(), S(20), TextAnchor.MiddleCenter, true, true);
            TMP_Text label = NewText("Label", content, "찹츄!", S(48), DisabledInk, true);
            Image tag = NewImage("Tag", content, DisabledTagFill);
            HorizontalLayout(tag.rectTransform, new RectOffset(I(12), I(12), I(6), I(6)), 0f, TextAnchor.MiddleCenter, true, true);
            TMP_Text tagText = NewText("Text", tag.transform, "5 / 10장", S(22), DisabledTagInk, true);

            Button chapchuButton = button.gameObject.AddComponent<Button>();
            chapchuButton.targetGraphic = buttonFill;
            chapchuButton.transition = Selectable.Transition.None;
            chapchuButton.interactable = false;

            var view = root.gameObject.AddComponent<MyPanelView>();
            Wire(view,
                ("nicknameText", nicknameText), ("hpText", hpText), ("chapchuBadge", badge.gameObject),
                ("hpFill", hpFill.rectTransform), ("turnTag", turnTag), ("turnTagText", turnTagText), ("timer", timer.gameObject), ("secondsText", secondsText), ("handCountText", handCountText),
                ("chapchuButton", chapchuButton), ("chapchuFill", buttonFill), ("chapchuLabel", label),
                ("chapchuTag", tag), ("chapchuTagText", tagText), ("chapchuRing", ring.gameObject), ("chapchuShadow", shadow));
            root.gameObject.AddComponent<MyPanelPresenter>();
            return view;
        }

        // 중앙 묶음 — 시안처럼 덱 오른쪽에 버림 더미 자리를 비워 둔다 (10-ui.md 2-1).
        private static DeckView BuildDeck(RectTransform parent)
        {
            RectTransform root = NewRect("Deck", parent);
            Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(12f, 0f), new Vector2(179f, 259f)); // 손패 위로 올림 (2026-11-03)

            HardShadow(root, Shadow, 10f, 10f, 6f);
            Image ringImage = NewImage("Ring", root, Ink);
            Stretch(ringImage.rectTransform, -6f, -6f, -6f, -6f);
            Image fill = Box("Box", root, White, 8f, Blue, true);

            TMP_Text plus = NewText("PlusText", fill.transform, "+", 56f, White, true);
            Anchor(plus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(160f, 64f));
            TMP_Text count = NewText("CountText", fill.transform, "DRAW -", 20f, White, true);
            count.characterSpacing = 10f;
            Anchor(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(160f, 30f));

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;

            var view = root.gameObject.AddComponent<DeckView>();
            Wire(view, ("button", button), ("countText", count));

            var presenter = root.gameObject.AddComponent<DeckPresenter>();
            var server = Object.FindFirstObjectByType<PunGameServer>();
            if (server == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 씬에 {nameof(PunGameServer)} 가 없다 — {nameof(DeckPresenter)}.server 를 직접 연결할 것.");
            Wire(presenter, ("server", server));
            return view;
        }

        private static RectTransform BuildHandArea(RectTransform parent)
        {
            RectTransform area = NewRect("HandArea", parent);
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 0f);
            area.pivot = new Vector2(0f, 0f);
            // 화면 가운데 정렬 — 좌우를 같게 비워 내 패널(오른쪽 끝 336)과 겹치지 않게 (2026-11-03).
            area.offsetMin = new Vector2(HandAreaSideInset, HandAreaBottom);
            area.offsetMax = new Vector2(-HandAreaSideInset, HandAreaBottom + HandCardSize.y);
            return area;
        }

        // 드롭 영역 — 손패 영역 위쪽 화면 전체 (10-ui.md 2-1 「드롭 영역」, 사용자 결정 2026-11-05). 투명 이미지는 디버그 때 색을 켜 보라고 둔다.
        // 레이캐스트는 끈다 — 판정은 HandPresenter 가 Rect 포함으로 하므로 덱 · 좌석 탭을 가리지 않는다. 위치 · 크기는 씬에서 바꾼다.
        private static RectTransform BuildDropZone(RectTransform parent)
        {
            Image image = NewImage("DropZone", parent, Color.clear);
            Stretch(image.rectTransform, 0f, 0f, 0f, HandAreaBottom + HandCardSize.y);
            return image.rectTransform;
        }

        private static VictoryOverlayView BuildVictoryOverlay(RectTransform parent)
        {
            RectTransform root = NewRect("VictoryOverlay", parent);
            Stretch(root);

            Image dim = NewImage("Dim", root, Dim, null, true);
            Stretch(dim.rectTransform);

            RectTransform box = NewRect("Box", root);
            Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 494f));

            // 본문 (리본보다 먼저 그려 리본 · 배지가 위로)
            Image bodyBorder = NewImage("Body", box, Orange);
            bodyBorder.rectTransform.anchorMin = Vector2.zero;
            bodyBorder.rectTransform.anchorMax = Vector2.one;
            bodyBorder.rectTransform.offsetMin = new Vector2(56f, 0f);
            bodyBorder.rectTransform.offsetMax = new Vector2(-32f, -132f);
            Image bodyFill = NewImage("Fill", bodyBorder.transform, White);
            Stretch(bodyFill.rectTransform, 10f, 0f, 10f, 10f);

            RectTransform content = NewRect("Content", bodyFill.transform);
            Stretch(content);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 72, 48);
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TMP_Text winner = NewText("WinnerText", content, "Player#1 승리!", 36f, Ink, true);
            TMP_Text message = NewText("MessageText", content, "손패 10장을 모아 찹츄를 외쳤습니다.", 26f, Ink, false);

            RectTransform buttons = NewRect("Buttons", content);
            HorizontalLayout(buttons, new RectOffset(), 20f, TextAnchor.MiddleCenter, true, true);
            Button restart = BuildOverlayButton(buttons, "RestartButton", "다시 하기", 260f, Orange, White);
            Button toRoom = BuildOverlayButton(buttons, "ToRoomButton", "룸으로", 200f, White, Ink);

            // 리본
            Image ribbon = NewImage("Ribbon", box, Orange);
            TopLeft(ribbon.rectTransform, 112f, 20f, 920f - 112f, 116f);
            TMP_Text ribbonText = NewText("Text", ribbon.transform, "|찹츄! 승리", 56f, White, true, TextAlignmentOptions.Left);
            Stretch(ribbonText.rectTransform, 80f, 0f, 0f, 0f);

            // 배지
            var round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundSpritePath);
            RectTransform badge = NewRect("Badge", box);
            TopLeft(badge, -8f, -8f, 184f, 184f);
            Image badgeOuter = NewImage("Outer", badge, Orange, round);
            Stretch(badgeOuter.rectTransform);
            Image badgeWhite = NewImage("White", badge, White, round);
            Stretch(badgeWhite.rectTransform, 8f, 8f, 8f, 8f);
            Image badgeCore = NewImage("Core", badge, Orange, round);
            Stretch(badgeCore.rectTransform, 18f, 18f, 18f, 18f);
            TMP_Text badgeText = NewText("Text", badge, "찹", 60f, White, true);
            Stretch(badgeText.rectTransform);

            var view = root.gameObject.AddComponent<VictoryOverlayView>();
            Wire(view, ("winnerText", winner), ("messageText", message), ("restartButton", restart), ("toRoomButton", toRoom));
            root.gameObject.SetActive(false);
            return view;
        }

        private static Button BuildOverlayButton(RectTransform parent, string name, string label, float width, Color fillColor, Color inkColor)
        {
            RectTransform root = NewRect(name, parent);
            LayoutElement element = root.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 100f;

            HardShadow(root, Ink, 8f, 8f);
            Image fill = Box("Box", root, Ink, 8f, fillColor, true);
            TMP_Text text = NewText("Text", fill.transform, label, 32f, inkColor, true);
            Stretch(text.rectTransform);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            // 프로젝트는 옛 Input Manager 를 쓴다 (다른 씬과 같은 StandaloneInputModule)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        #endregion

        #region 손패

        // 손패 카드 226 × 336 — 카드 틀 이미지(820 × 1222) 비율, 높이는 손패 영역. 부채꼴은 시안처럼 한 칸에 약 1.5° (10-ui.md 2-1 「손패」).
        private static readonly Vector2 HandCardSize = new Vector2(226f, 336f);
        private const float HandAreaSideInset = 356f; // 손패 영역 좌우 여백 — 내 패널 오른쪽 끝(336) + 20
        private const float HandAreaBottom = 28f;     // 손패 영역 아래 여백. 드롭 영역은 그 위(28 + 336)부터
        private const string ActionCardFramePath = "Assets/Sprites/Cards/ActionCard.png";
        private const string CounterCardFramePath = "Assets/Sprites/Cards/CounterCard.png";
        private const string TrapCardFramePath = "Assets/Sprites/Cards/TrapCard.png";
        // 10장일 때 맨 왼쪽 카드(원호의 5% 지점, 약 -0.9 × 반폭)의 왼쪽 끝이 내 패널 오른쪽(336)보다 오른쪽에 오게 한다.
        private const float HandFanHalfWidth = 520f;
        private const float HandFanDrop = 34f;       // 양 끝이 가운데보다 내려가는 정도 — 한 칸 1.5° 인 원호

        private static HandPresenter BuildHand(RectTransform handArea, DeckView deck, RectTransform dropZone, OpponentSeatsPresenter seats)
        {
            RectTransform root = NewRect("Hand", handArea);
            Anchor(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);

            SplineContainer container = root.gameObject.AddComponent<SplineContainer>();
            Spline spline = container.Spline;
            spline.Clear();
            spline.Add(new BezierKnot(new float3(-HandFanHalfWidth, -HandFanDrop, 0f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(float3.zero), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(HandFanHalfWidth, -HandFanDrop, 0f)), TangentMode.AutoSmooth);

            var view = root.gameObject.AddComponent<HandView>();
            Wire(view,
                ("actionCardPrefab", EnsureHandCardPrefab("HandCard_Action", ActionCardFramePath)),
                ("counterCardPrefab", EnsureHandCardPrefab("HandCard_Counter", CounterCardFramePath)),
                ("trapCardPrefab", EnsureHandCardPrefab("HandCard_Trap", TrapCardFramePath)));

            var presenter = root.gameObject.AddComponent<HandPresenter>();
            var server = Object.FindFirstObjectByType<PunGameServer>();
            if (server == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 씬에 {nameof(PunGameServer)} 가 없다 — {nameof(HandPresenter)}.server 를 직접 연결할 것.");
            Wire(presenter, ("deck", FindGameDeck()), ("drawOrigin", deck.transform), ("server", server), ("dropZone", dropZone), ("seats", seats));
            return presenter;
        }

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
                Debug.LogError($"[{nameof(GameScreenBuilder)}] {nameof(PunGameServer)}.deck 을 찾지 못했다 — {nameof(HandPresenter)}.deck 을 직접 연결할 것.");
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

        /// <summary>검정 테두리 + 안쪽 채움. 바깥(테두리) 오브젝트를 만들고 안쪽 채움 Image 를 돌려준다.</summary>
        private static Image Box(string name, Transform parent, Color borderColor, float borderWidth, Color fillColor, bool raycastTarget = false)
        {
            Image border = NewImage(name, parent, borderColor);
            Stretch(border.rectTransform);
            Image fill = NewImage("Fill", border.transform, fillColor, null, raycastTarget);
            Stretch(fill.rectTransform, borderWidth, borderWidth, borderWidth, borderWidth);
            return fill;
        }

        /// <summary>
        /// 테두리 + 채움 상자인데 크기를 안의 글자에서 받는다 — 레이아웃 그룹 안에 넣는 작은 상자용.
        /// 바깥(테두리) · 안쪽(채움) 둘 다 HorizontalLayoutGroup 이라 글자 → 채움 → 테두리 순으로 크기가 전해진다.
        /// </summary>
        private static Image LayoutBox(string name, Transform parent, Color borderColor, int borderWidth, Color fillColor, RectOffset innerPadding)
        {
            Image border = NewImage(name, parent, borderColor);
            HorizontalLayout(border.rectTransform, new RectOffset(borderWidth, borderWidth, borderWidth, borderWidth), 0f, TextAnchor.MiddleCenter, true, true);
            Image fill = NewImage("Fill", border.transform, fillColor);
            HorizontalLayout(fill.rectTransform, innerPadding, 0f, TextAnchor.MiddleCenter, true, true);
            return fill;
        }

        /// <summary>속이 빈 테두리(외곽선) — 스프라이트 없이 변 4개로 만든다. 부모 바깥쪽으로 width 만큼 두르며, 안쪽은 비어 있어 뒤가 보인다.</summary>
        private static RectTransform BuildFrame(string name, Transform parent, Color color, float width)
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

        /// <summary>블러 없는 하드 섀도우 — 오른쪽 · 아래로 밀린 같은 크기의 사각형. 형제 중 맨 뒤에 두고, 레이아웃 그룹이 잡지 않게 한다.</summary>
        private static Image HardShadow(Transform parent, Color color, float dx, float dy, float spread = 0f)
        {
            Image shadow = NewImage("Shadow", parent, color);
            RectTransform rect = shadow.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(dx - spread, -dy - spread);
            rect.offsetMax = new Vector2(dx + spread, -dy + spread);
            rect.SetAsFirstSibling();
            shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return shadow;
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

        private static void Wire(Component target, params (string field, Object value)[] references)
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

        private static void WireArray(Component target, string field, Object[] values)
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

        private static void SetColor(Component target, string field, Color color)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
                return;
            }

            property.colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
