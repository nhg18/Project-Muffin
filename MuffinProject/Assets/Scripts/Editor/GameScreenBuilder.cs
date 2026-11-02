using System.IO;
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
    /// 수치 · 색은 docs/systems/10-ui.md 2-1 (HTML 시안 × 2). 손패 · 상대 손패 · 히스토리 항목은 영역만 두고 만들지 않는다 (2026-11-02).
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
        private static readonly Color TrapEmptyBorder = Hex(0x999999);
        private static readonly Color TrapEmptyFill = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color[] SeatColors = { Blue, Orange, Green };

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
            DiscardPileView discardPile = BuildDiscardPile(canvasRect);

            RectTransform safeArea = NewRect("SafeArea", canvasRect);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeArea>();

            RoomCodeView roomCode = BuildRoomCodeTag(safeArea);
            HistoryView history = BuildHistory(safeArea);
            OpponentSeatView[] seats = BuildOpponentSeats(safeArea);
            MyPanelView myPanel = BuildMyPanel(safeArea);
            RectTransform handArea = BuildHandArea(safeArea);

            VictoryOverlayView victory = BuildVictoryOverlay(canvasRect);

            EnsureEventSystem();

            Wire(screen,
                ("roomCode", roomCode), ("history", history), ("myPanel", myPanel), ("deck", deck),
                ("discardPile", discardPile), ("handArea", handArea), ("victoryOverlay", victory));
            WireArray(screen, "opponentSeats", seats);

            EditorSceneManager.MarkSceneDirty(canvasRect.gameObject.scene);
            Selection.activeGameObject = canvasRect.gameObject;
            Debug.Log($"[{nameof(GameScreenBuilder)}] {CanvasName} 조립 완료 — 씬을 저장할 것.");
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
            return view;
        }

        private static HistoryView BuildHistory(RectTransform parent)
        {
            RectTransform root = NewRect("History", parent);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.offsetMin = new Vector2(36f, 444f);
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

        private static OpponentSeatView[] BuildOpponentSeats(RectTransform parent)
        {
            RectTransform area = NewRect("OpponentSeats", parent);
            area.anchorMin = new Vector2(0f, 1f);
            area.anchorMax = new Vector2(1f, 1f);
            area.pivot = new Vector2(0.5f, 1f);
            area.offsetMin = new Vector2(336f, -124f - 346f);
            area.offsetMax = new Vector2(-36f, -124f);

            var seats = new OpponentSeatView[3];
            for (int i = 0; i < 3; i++)
                seats[i] = BuildOpponentSeat(area, i);
            return seats;
        }

        // 좌석 인덱스 1 · 2 · 3 (02-player.md 3절) = 슬롯 0 · 1 · 2. 영역을 3등분한 가운데에 둔다.
        private static OpponentSeatView BuildOpponentSeat(RectTransform area, int slot)
        {
            Color seatColor = SeatColors[slot];
            int seatIndex = slot + 1;

            Image root = NewImage($"Seat{seatIndex}", area, Color.clear, null, true);
            float centerX = (slot + 0.5f) / 3f;
            Anchor(root.rectTransform, new Vector2(centerX, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(460f, 346f));

            RectTransform turnOutline = BuildFrame("TurnOutline", root.transform, Green, 8f);
            turnOutline.gameObject.SetActive(false);

            Image targetHighlight = NewImage("TargetHighlight", root.transform, new Color(1f, 1f, 1f, 0.4f));
            Stretch(targetHighlight.rectTransform, -8f, -8f, -8f, -8f);
            targetHighlight.gameObject.SetActive(false);

            // 이름 줄
            RectTransform nameRow = NewRect("NameRow", root.transform);
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

            // HP 줄
            RectTransform hpBar = NewRect("HpBar", root.transform);
            hpBar.anchorMin = new Vector2(0f, 1f);
            hpBar.anchorMax = new Vector2(1f, 1f);
            hpBar.pivot = new Vector2(0f, 1f);
            hpBar.offsetMin = new Vector2(0f, -58f - 24f);
            hpBar.offsetMax = new Vector2(-72f, -58f);
            Image hpTrack = Box("Track", hpBar, Ink, 6f, White);
            Image hpFill = NewImage("Fill", hpTrack.transform, seatColor);
            Stretch(hpFill.rectTransform);
            hpFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            TMP_Text hpText = NewText("HpText", root.transform, "100", 22f, Ink, true, TextAlignmentOptions.Left);
            Anchor(hpText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -58f), new Vector2(60f, 24f));

            // 캐릭터 — 그림은 아직 없다 (10-ui.md 2-1)
            Image character = NewImage("CharacterImage", root.transform, White);
            Anchor(character.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(141f, 188f));
            character.enabled = false;
            RectTransform fanAnchor = NewRect("HandFanAnchor", character.rectTransform);
            Anchor(fanAnchor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(42f, 45f));

            // 함정 슬롯
            RectTransform traps = NewRect("TrapSlots", root.transform);
            Anchor(traps, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -302f), new Vector2(105f, 36f));
            var trapBorders = new Image[3];
            var trapFills = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                trapFills[i] = Box($"Slot{i}", traps, TrapEmptyBorder, 6f, TrapEmptyFill);
                trapBorders[i] = trapFills[i].transform.parent.GetComponent<Image>();
                Anchor(trapBorders[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 39f, 0f), new Vector2(27f, 36f));
            }

            var view = root.gameObject.AddComponent<OpponentSeatView>();
            Wire(view,
                ("nicknameText", nicknameText), ("handCountText", handCountText), ("chapchuBadge", badge.gameObject),
                ("hpFill", hpFill.rectTransform), ("hpFillImage", hpFill), ("hpText", hpText),
                ("characterImage", character), ("handFanAnchor", fanAnchor),
                ("turnOutline", turnOutline.gameObject), ("targetHighlight", targetHighlight.gameObject));
            WireArray(view, "trapSlotBorders", trapBorders);
            WireArray(view, "trapSlotFills", trapFills);
            SetColor(view, "seatColor", seatColor);
            return view;
        }

        private static Image BuildChapchuBadge(Transform parent)
        {
            Image fill = LayoutBox("ChapchuBadge", parent, Ink, 4, Orange, new RectOffset(14, 14, 4, 4));
            Image border = fill.transform.parent.GetComponent<Image>();
            HardShadow(border.rectTransform, Ink, 4f, 4f);
            NewText("Text", fill.transform, "찹츄!", 22f, White, true);
            border.gameObject.SetActive(false);
            return border;
        }

        private static MyPanelView BuildMyPanel(RectTransform parent)
        {
            RectTransform root = NewRect("MyPanel", parent);
            Anchor(root, Vector2.zero, Vector2.zero, new Vector2(36f, 36f), new Vector2(460f, 380f));

            HardShadow(root, Shadow, 10f, 10f);
            Image fill = Box("Box", root, Green, 8f, White);

            // 헤더
            Image header = NewImage("Header", fill.transform, Green);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = Vector2.zero;
            header.rectTransform.sizeDelta = new Vector2(0f, 52f);
            HorizontalLayout(header.rectTransform, new RectOffset(20, 20, 0, 0), 12f, TextAnchor.MiddleLeft, true, true);

            TMP_Text nicknameText = NewText("NicknameText", header.transform, "|Player#1", 28f, White, true, TextAlignmentOptions.Left);

            Image meTag = NewImage("MeTag", header.transform, White);
            HorizontalLayout(meTag.rectTransform, new RectOffset(10, 10, 4, 4), 0f, TextAnchor.MiddleCenter, true, true);
            NewText("Text", meTag.transform, "나", 20f, Green, false);

            Image badge = BuildChapchuBadge(header.transform);

            RectTransform spacer = NewRect("Spacer", header.transform);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            TMP_Text hpText = NewText("HpText", header.transform, "HP 100", 22f, White, false, TextAlignmentOptions.Right);

            // HP 바
            RectTransform hpBar = NewRect("HpBar", fill.transform);
            hpBar.anchorMin = new Vector2(0f, 1f);
            hpBar.anchorMax = new Vector2(1f, 1f);
            hpBar.pivot = new Vector2(0f, 1f);
            hpBar.offsetMin = new Vector2(24f, -72f - 20f);
            hpBar.offsetMax = new Vector2(-24f, -72f);
            Image hpTrack = Box("Track", hpBar, Ink, 4f, White);
            Image hpFill = NewImage("Fill", hpTrack.transform, Green);
            Stretch(hpFill.rectTransform);

            // 턴 줄
            RectTransform turnRow = NewRect("TurnRow", fill.transform);
            turnRow.anchorMin = new Vector2(0f, 1f);
            turnRow.anchorMax = new Vector2(1f, 1f);
            turnRow.pivot = new Vector2(0f, 1f);
            turnRow.offsetMin = new Vector2(24f, -108f - 52f);
            turnRow.offsetMax = new Vector2(-24f, -108f);

            Image turnTag = NewImage("TurnTag", turnRow, Orange);
            Anchor(turnTag.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(120f, 44f));
            HorizontalLayout(turnTag.rectTransform, new RectOffset(16, 16, 8, 8), 0f, TextAnchor.MiddleCenter, true, true);
            FitBoth(turnTag.gameObject);
            NewText("Text", turnTag.transform, "나의 턴!", 24f, White, true);

            RectTransform timer = NewRect("Timer", turnRow);
            Anchor(timer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 52f));
            HorizontalLayout(timer, new RectOffset(), 6f, TextAnchor.LowerCenter, true, true);
            FitBoth(timer.gameObject);
            TMP_Text secondsText = NewText("SecondsText", timer, "20", 52f, Ink, true);
            NewText("UnitText", timer, "초", 22f, Ink, false);

            TMP_Text handCountText = NewText("HandCountText", turnRow, "5/10장", 22f, Ink, false, TextAlignmentOptions.Right);
            Anchor(handCountText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(120f, 52f));

            // 찹츄 버튼 (기본: 비활성 모양)
            RectTransform button = NewRect("ChapchuButton", fill.transform);
            button.anchorMin = new Vector2(0f, 0f);
            button.anchorMax = new Vector2(1f, 0f);
            button.pivot = new Vector2(0f, 0f);
            button.offsetMin = new Vector2(24f, 20f);
            button.offsetMax = new Vector2(-24f, 20f + 104f);

            RectTransform ring = NewRect("Ring", button);
            Stretch(ring);
            Image ringOuter = NewImage("Outer", ring, Orange);
            Stretch(ringOuter.rectTransform, -16f, -16f, -16f, -16f);
            Image ringInner = NewImage("Inner", ring, White);
            Stretch(ringInner.rectTransform, -8f, -8f, -8f, -8f);
            ring.gameObject.SetActive(false);

            Image shadow = HardShadow(button, DisabledInk, 8f, 8f);
            shadow.transform.SetAsLastSibling();
            Image buttonFill = Box("Box", button, Ink, 8f, White, true);

            RectTransform content = NewRect("Content", buttonFill.transform);
            Stretch(content);
            HorizontalLayout(content, new RectOffset(), 20f, TextAnchor.MiddleCenter, true, true);
            TMP_Text label = NewText("Label", content, "찹츄!", 48f, DisabledInk, true);
            Image tag = NewImage("Tag", content, DisabledTagFill);
            HorizontalLayout(tag.rectTransform, new RectOffset(12, 12, 6, 6), 0f, TextAnchor.MiddleCenter, true, true);
            TMP_Text tagText = NewText("Text", tag.transform, "5 / 10장", 22f, DisabledTagInk, true);

            Button chapchuButton = button.gameObject.AddComponent<Button>();
            chapchuButton.targetGraphic = buttonFill;
            chapchuButton.transition = Selectable.Transition.None;
            chapchuButton.interactable = false;

            var view = root.gameObject.AddComponent<MyPanelView>();
            Wire(view,
                ("nicknameText", nicknameText), ("hpText", hpText), ("chapchuBadge", badge.gameObject),
                ("hpFill", hpFill.rectTransform), ("turnTag", turnTag.gameObject), ("secondsText", secondsText), ("handCountText", handCountText),
                ("chapchuButton", chapchuButton), ("chapchuFill", buttonFill), ("chapchuLabel", label),
                ("chapchuTag", tag), ("chapchuTagText", tagText), ("chapchuRing", ring.gameObject), ("chapchuShadow", shadow));
            return view;
        }

        private static DeckView BuildDeck(RectTransform parent)
        {
            RectTransform root = NewRect("Deck", parent);
            Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(12f, -81f), new Vector2(179f, 259f));

            HardShadow(root, Shadow, 10f, 10f, 6f);
            Image ringImage = NewImage("Ring", root, Ink);
            Stretch(ringImage.rectTransform, -6f, -6f, -6f, -6f);
            Image fill = Box("Box", root, White, 8f, Blue, true);

            TMP_Text plus = NewText("PlusText", fill.transform, "+", 56f, White, true);
            Anchor(plus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(160f, 64f));
            TMP_Text count = NewText("CountText", fill.transform, "DRAW 18", 20f, White, true);
            count.characterSpacing = 10f;
            Anchor(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(160f, 30f));

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;

            var view = root.gameObject.AddComponent<DeckView>();
            Wire(view, ("button", button), ("countText", count));
            return view;
        }

        private static DiscardPileView BuildDiscardPile(RectTransform parent)
        {
            RectTransform root = NewRect("DiscardPile", parent);
            Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(288f, -81f), new Vector2(179f, 259f));

            BuildBackCard(root, "BackCard2", new Vector2(-24f, -8f), -6f);
            BuildBackCard(root, "BackCard1", new Vector2(-12f, -4f), -3f);

            RectTransform top = NewRect("TopCard", root);
            Stretch(top);
            Image topRing = NewImage("Ring", top, Ink);
            Stretch(topRing.rectTransform, -6f, -6f, -6f, -6f);
            Image face = NewImage("Face", top, White);
            Stretch(face.rectTransform);

            var view = root.gameObject.AddComponent<DiscardPileView>();
            Wire(view, ("topCard", top.gameObject), ("topCardImage", face));
            return view;
        }

        private static void BuildBackCard(RectTransform parent, string name, Vector2 offset, float rotation)
        {
            Image fill = Box(name, parent, Ink, 6f, White);
            RectTransform border = fill.transform.parent.GetComponent<RectTransform>();
            Stretch(border);
            border.anchoredPosition = offset;
            border.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static RectTransform BuildHandArea(RectTransform parent)
        {
            RectTransform area = NewRect("HandArea", parent);
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 0f);
            area.pivot = new Vector2(0f, 0f);
            area.offsetMin = new Vector2(556f, 28f);
            area.offsetMax = new Vector2(-36f, 28f + 280f);
            return area;
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
