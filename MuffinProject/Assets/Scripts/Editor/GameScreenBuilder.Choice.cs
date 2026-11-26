using Chapchu.Network;
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
    /// 카드 효과 고르기 판 (1019 목표 조준) · 이름표 옆 상태 알약 · 왼쪽 판 효과 줄 (PM 2026-11-27).
    /// 고르기 판 = 판을 어둡게 하지 않고 판 위에 놓는다 — 왼쪽 쓴 카드 자리(체인의 3D 카드가 온다) + 「쓴 사람 → 고르는 사람」, 오른쪽 고를 카드 두 장, 위 남은 시간 막대 + 한 줄 안내.
    /// 고를 카드 · 상태 알약은 프리팹 (반복 부품). 열린 씬은 메뉴 Upgrade Choice Board 로 바꾼다 — 고르기 판만 새로 짓고, 나머지는 자리 · 부품만 더한다.
    /// </summary>
    public static partial class GameScreenBuilder
    {
        private const string PretendardBoldPath = "Assets/Fonts/Pretendard/Pretendard Bold SDF.asset"; // 문구 Bold (글꼴 조합 ④)
        private const string ChoiceOptionPrefabPath = HandCardPrefabFolder + "/ChoiceOption.prefab";
        private const string StatusChipPrefabPath = HandCardPrefabFolder + "/StatusChip.prefab";

        private static readonly Color StatusTeal = Hex(0x0e8f98); // 상태 효과 키워드 색
        private static readonly Color HandKeyword = Hex(0x2f63d6);
        private static readonly Color HandArt = Hex(0xf3f6fd);
        private static readonly Color DashLine = Hex(0xc9c2bb);

        // 고르기 판 — 캔버스 왼쪽 위 기준 px. 쓴 카드(맨 왼쪽) → 틈 28 → 고를 카드 · 틈 16 · 고를 카드. 판 효과 줄(392 ~ 674) 오른쪽, 턴 순서(1588 ~) 왼쪽
        private static readonly Rect ChoiceBar = new Rect(694f, 190f, 874f, 24f);
        private static readonly Rect ChoiceHint = new Rect(841f, 222f, 580f, 52f);
        private static readonly Rect ChoiceUsedCard = new Rect(694f, 284f, 290f, 415f);
        private static readonly Vector2 ChoiceTagCenter = new Vector2(839f, 714f);
        private static readonly Vector2[] ChoiceOptionTopLeft = { new Vector2(1012f, 318f), new Vector2(1298f, 318f) };
        private static readonly Vector2 ChoiceOptionSize = new Vector2(270f, 388f);
        private static readonly Rect ChoiceCardsArea = new Rect(1012f, 284f, 556f, 415f); // 「손패에서 1장」 카드 칸 — 고를 카드 두 장 자리, 높이는 쓴 카드와 같게
        private const float ChoiceCardBorder = 6f, ChoiceCardRadius = 24f, ChoiceCardShadow = 10f;

        // 상태 알약 — 좌석은 이름표 오른쪽 8 에서 이름표 아래 끝부터 위로 쌓는다, 나는 캐릭터 오른쪽 목표 칩 위에 옆으로
        private const float StatusSeatGap = 8f;
        private static readonly Vector2 MyStatusBottomLeft = new Vector2(230f, 238f);

        // 판 효과 줄 — 낸 카드 기록 오른쪽 세로, 위에서 아래로 개판 자명종 → 기다려! → 피폭
        private static readonly Vector2 BoardColumnTopLeft = new Vector2(392f, 460f);
        private const float BoardPillWidth = 282f;

        // 개판 제목 · 판정 — 상대 이름표(위 4 ~ 144) 아래로 (가운데 y). 도장은 제목 오른쪽, 턴 순서(1588 ~) 왼쪽
        private const float GaepanTitleY = 470f, GaepanStampX = 1400f, GaepanStampY = 500f, GaepanBannerY = 440f, GaepanRollY = 590f;

        private static TMP_FontAsset _pretendardBold;
        private static TMP_FontAsset PretendardBold => _pretendardBold != null ? _pretendardBold : _pretendardBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PretendardBoldPath);

        /// <summary>
        /// 열린 씬의 고르기 판을 새로 짓고, 좌석 · 내 HUD 에 상태 알약을 더하고, 판 효과 · 개판 자명종을 왼쪽 세로 줄로 옮긴다.
        /// 다시 조립하지 않으므로 사람이 고친 씬의 다른 부분은 그대로다. 여러 번 돌려도 같은 결과.
        /// </summary>
        [MenuItem("Chapchu/Game Screen/Upgrade Choice Board")]
        public static void UpgradeChoiceBoard()
        {
            GameObject canvas = GameObject.Find(CanvasName);
            Transform sheets = canvas != null ? canvas.transform.Find("Sheets") : null;
            Transform safeArea = canvas != null ? canvas.transform.Find("SafeArea") : null;
            Transform boardLayer = canvas != null ? canvas.transform.Find("BoardLayer") : null;
            ChoicePopupPresenter oldChoice = sheets != null ? sheets.GetComponentInChildren<ChoicePopupPresenter>(true) : null;
            GameScreenPresenter presenter = canvas != null ? canvas.GetComponentInChildren<GameScreenPresenter>(true) : null;
            ChainCardsPresenter chainCards = Object.FindFirstObjectByType<ChainCardsPresenter>(FindObjectsInactive.Include);
            MyPanelView myPanel = canvas != null ? canvas.GetComponentInChildren<MyPanelView>(true) : null;
            BoardEffectsView boardEffects = boardLayer != null ? boardLayer.GetComponentInChildren<BoardEffectsView>(true) : null;
            GaepanHudView gaepanHud = boardLayer != null ? boardLayer.GetComponentInChildren<GaepanHudView>(true) : null;
            if (sheets == null || safeArea == null || boardLayer == null || oldChoice == null || presenter == null || chainCards == null || myPanel == null || boardEffects == null || gaepanHud == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 열린 씬에 {CanvasName} 의 Sheets · SafeArea · BoardLayer · 고르기 판 · 연출 큐 · 체인 카드 · 내 HUD · 판 효과 · 개판 자명종 중 없는 것이 있다.");
                return;
            }

            _regularFont = EnsureFontAsset(RegularFontPath, RegularFontAssetPath);
            _boldFont = EnsureFontAsset(BoldFontPath, BoldFontAssetPath);

            // 1. 고르기 판 — 같은 자리에 새로 짓는다
            var server = (PunGameServer)new SerializedObject(oldChoice).FindProperty("server").objectReferenceValue;
            int index = oldChoice.transform.GetSiblingIndex();
            Object.DestroyImmediate(oldChoice.gameObject);
            ChoicePopupPresenter choice = BuildChoicePopup((RectTransform)sheets, server);
            choice.transform.SetSiblingIndex(index);
            Wire(choice, ("chainCards", chainCards), ("hand", Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include))); // 「손패에서 1장」 고르기가 내 손패를 읽는다
            Wire(presenter, ("choicePopup", choice));

            // 2. 상태 알약 — 좌석마다 이름표 오른쪽, 나는 목표 칩 위. 땀방울(사망 대기 표시)은 지운다
            foreach (OpponentSeatView seat in canvas.GetComponentsInChildren<OpponentSeatView>(true))
            {
                DestroyChild(seat.transform, "Sweat");
                BuildSeatStatus(seat);
            }

            BuildMyStatus(safeArea, myPanel);

            // 3. 판 효과 줄 — 왼쪽 세로 (자명종이 낸 카드 기록을 덮지 않게, 가운데 위 띠를 비운다)
            BuildBoardColumn((RectTransform)boardLayer, (RectTransform)gaepanHud.transform, boardEffects);

            // 4. 개판 제목 · 판정 — 이름표 아래로
            MoveGaepanTexts(canvas.transform);

            EditorSceneManager.MarkSceneDirty(canvas.scene);
            Debug.Log($"[{nameof(GameScreenBuilder)}] 고르기 판 · 상태 알약 · 판 효과 줄을 바꿈 — 씬을 저장할 것.");
        }

        /// <summary>고르기 판 — 루트(뷰 · 프레젠터)는 켜 두고 판(Panel)은 끈다. chainCards 는 부른 쪽이 잇는다.</summary>
        public static ChoicePopupPresenter BuildChoicePopup(RectTransform parent, PunGameServer server)
        {
            RectTransform root = NewRect("ChoicePopup", parent);
            Stretch(root);
            RectTransform panel = NewRect("Panel", root);
            Stretch(panel);

            // 남은 시간 막대 — 새 카드 보여 주기와 같은 꼴 (흰 테 막대 + 노랑 채움이 왼쪽으로 준다)
            UIShape bar = NewShape("Bar", panel, White, ChoiceBar.height / 2f, Line4, Ink);
            TopLeft(bar.rectTransform, ChoiceBar.x, ChoiceBar.y, ChoiceBar.width, ChoiceBar.height);
            UIShape drain = NewShape("Drain", bar.transform, Turn, ChoiceBar.height / 2f - Line4, 0f, Color.clear);
            Anchor(drain.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Line4, 0f),
                new Vector2(ChoiceBar.width - Line4 * 2f, ChoiceBar.height - Line4 * 2f));

            RectTransform hint = NewRect("Hint", panel);
            TopLeft(hint, ChoiceHint.x, ChoiceHint.y, ChoiceHint.width, ChoiceHint.height);
            UIShape hintBox = NewShape("Box", hint, White, ChoiceHint.height / 2f, Line4, Ink);
            Stretch(hintBox.rectTransform);
            TMP_Text hintText = BoldBodyText("Text", hint, "둘 중 하나를 눌러요 · 5초 지나면 무작위", 26f, Ink);
            Stretch(hintText.rectTransform, 18f, 0f, 18f, 0f);

            // 쓴 카드 자리 — 비어 있다. 체인의 3D 카드가 이 사각형에 맞춰 온다 (ChainCardsPresenter.Focus)
            RectTransform usedCard = NewRect("UsedCardSlot", panel);
            TopLeft(usedCard, ChoiceUsedCard.x, ChoiceUsedCard.y, ChoiceUsedCard.width, ChoiceUsedCard.height);

            // 고를 카드 — 서버의 고르기 번호 순서 (0 손패 모두 버리기 · 1 피해)
            GameObject optionPrefab = ChoiceOptionPrefab();
            var options = new Object[2];
            string[] names = { "OptionHand", "OptionDamage" };
            for (int i = 0; i < options.Length; i++)
            {
                var option = (GameObject)PrefabUtility.InstantiatePrefab(optionPrefab, panel);
                option.name = names[i];
                CenterAt((RectTransform)option.transform, ChoiceOptionTopLeft[i].x + ChoiceOptionSize.x / 2f, ChoiceOptionTopLeft[i].y + ChoiceOptionSize.y / 2f,
                    ChoiceOptionSize.x, ChoiceOptionSize.y);
                options[i] = option.GetComponent<ChoiceOptionView>();
            }

            // 「쓴 사람 → 고르는 사람」 — 쓴 카드 아래에 걸친다. 이름 길이에 맞춰 양옆으로 는다
            RectTransform tag = NewRect("Tag", panel);
            CenterAt(tag, ChoiceTagCenter.x, ChoiceTagCenter.y, 152f, 52f);
            HorizontalLayout(tag, new RectOffset(18, 18, 0, 0), 0f, TextAnchor.MiddleCenter, true, false);
            tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIShape tagBox = NewShape("Box", tag, White, 26f, Line4, Ink);
            Stretch(tagBox.rectTransform);
            tagBox.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            TMP_Text tagText = BoldBodyText("Text", tag, "봇 1 → 나", 26f, Ink);
            tagText.rectTransform.sizeDelta = new Vector2(116f, 52f);
            tag.gameObject.AddComponent<LayoutElement>().minWidth = 152f;

            panel.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<ChoicePopupView>();
            Wire(view, ("panel", panel.gameObject), ("barDrain", drain.rectTransform), ("hintText", hintText), ("tagText", tagText),
                ("usedCardSlot", usedCard), ("handIcon", Hud("icon_hand_minus")), ("damageIcon", Hud("icon_burst")));
            WireArray(view, "options", options);
            BuildChoiceCards(view, panel);
            var presenter = root.gameObject.AddComponent<ChoicePopupPresenter>();
            Wire(presenter, ("server", server));
            return presenter;
        }

        // 고를 카드 한 장 (프리팹) — 잉크 테 6 · 반지름 24 · 아래 그림자 10, 이름 띠 55 + 잉크 선 5, 그림 칸 165 + 점선 3, 결과 숫자 · 한 줄.
        // 처음 한 번 만들고, 있으면 그대로 쓴다 (사람이 고친 값 보존).
        private static GameObject ChoiceOptionPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ChoiceOptionPrefabPath);
            if (existing != null)
                return existing;

            const float inner = 270f - ChoiceCardBorder * 2f;
            RectTransform root = NewRect("ChoiceOption", null);
            root.sizeDelta = ChoiceOptionSize;

            UIShape shadow = NewShape("Shadow", root, Ink, ChoiceCardRadius, 0f, Color.clear);
            Stretch(shadow.rectTransform, 0f, ChoiceCardShadow, 0f, -ChoiceCardShadow);
            UIShape body = NewShape("Body", root, White, ChoiceCardRadius, ChoiceCardBorder, Ink);
            Stretch(body.rectTransform);
            body.raycastTarget = true;

            // 이름 띠 — 위 모서리만 둥글게: 둥근 칸을 아래로 길게 두고 아래를 잘라낸다
            RectTransform ribbonClip = NewRect("Ribbon", root);
            Anchor(ribbonClip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -ChoiceCardBorder), new Vector2(inner, 55f));
            ribbonClip.gameObject.AddComponent<RectMask2D>();
            float ribbonRadius = ChoiceCardRadius - ChoiceCardBorder;
            UIShape ribbon = NewShape("Fill", ribbonClip, HandKeyword, ribbonRadius, 0f, Color.clear);
            Anchor(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(inner, 55f + ribbonRadius));
            TMP_Text nameText = NewText("Name", ribbonClip, "손패 모두 버리기", 28f, White, true);
            Stretch(nameText.rectTransform);
            Image ribbonLine = NewImage("RibbonLine", root, Ink);
            Anchor(ribbonLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -61f), new Vector2(inner, 5f));

            Image art = NewImage("Art", root, HandArt);
            Anchor(art.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(inner, 165f));
            Image icon = NewImage("Icon", art.transform, White, Hud("icon_hand_minus"));
            icon.preserveAspect = true;
            CenterIn(icon.rectTransform, 124f, 124f);
            UIShape dash = NewShape("Dash", root, Color.clear, 0f, 1.5f, DashLine, UIShape.ShapeKind.RoundRect, 8f, 6f);
            Anchor(dash.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -231f), new Vector2(inner, 3f));

            // 결과 — 「지금 → 고르면」 (고르면 쪽은 키워드 색), HP 면 하트를 앞에
            RectTransform result = NewRect("Result", root);
            Anchor(result, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -277f), new Vector2(inner, 56f));
            HorizontalLayout(result, new RectOffset(0, 0, 0, 0), 6f, TextAnchor.MiddleCenter, true, false);
            Image heart = FixedIcon("Heart", result, Hud("icon_heart"), 40f, 37f, White);
            heart.gameObject.SetActive(false);
            TMP_Text fromText = NewText("From", result, "7장", 46f, Ink, true);
            fromText.rectTransform.sizeDelta = new Vector2(80f, 56f);
            TMP_Text arrow = BoldBodyText("Arrow", result, "→", 40f, Ink);
            arrow.rectTransform.sizeDelta = new Vector2(48f, 56f);
            TMP_Text toText = NewText("To", result, "0장", 46f, HandKeyword, true);
            toText.rectTransform.sizeDelta = new Vector2(80f, 56f);

            TMP_Text subText = BoldBodyText("Sub", root, "찹츄에서 멀어짐", 24f, Ink2);
            Anchor(subText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -327f), new Vector2(inner, 32f));

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None; // 보는 사람 판(누를 수 없음)도 같은 색으로

            var view = root.gameObject.AddComponent<ChoiceOptionView>();
            Wire(view, ("button", button), ("ribbon", ribbon), ("nameText", nameText), ("art", art), ("icon", icon), ("heart", heart.gameObject),
                ("fromText", fromText), ("toText", toText), ("subText", subText));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, ChoiceOptionPrefabPath);
            Object.DestroyImmediate(root.gameObject);
            return saved;
        }

        // 좌석 상태 알약 — 이름표(Plate) 오른쪽 8 에서 이름표 아래 끝부터 위로 쌓는다. 있던 것은 새로 짓는다 (UpgradeChoiceBoard · BuildSeatV3)
        private static void BuildSeatStatus(OpponentSeatView seat)
        {
            DestroyChild(seat.transform, "StatusChips");
            var plate = (RectTransform)seat.transform.Find("Plate");
            StatusChipsView chips = BuildStatusChips("StatusChips", seat.transform, true);
            float right = plate.anchoredPosition.x + plate.sizeDelta.x * (1f - plate.pivot.x);
            float bottom = -plate.anchoredPosition.y + plate.sizeDelta.y * plate.pivot.y;
            Anchor((RectTransform)chips.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(right + StatusSeatGap, -bottom), Vector2.zero);
            chips.transform.SetSiblingIndex(plate.GetSiblingIndex() + 1);
            Wire(seat, ("status", chips));
        }

        // 내 상태 알약 — 「찹츄까지」 칩(Goal) 위에 옆으로. 있던 것은 새로 짓는다 (UpgradeChoiceBoard · BuildMyHudV3)
        private static void BuildMyStatus(Transform safeArea, MyPanelView myPanel)
        {
            DestroyChild(safeArea, "MyStatus");
            StatusChipsView myChips = BuildStatusChips("MyStatus", safeArea, false);
            Anchor((RectTransform)myChips.transform, Vector2.zero, Vector2.zero, MyStatusBottomLeft, Vector2.zero);
            Transform goal = safeArea.Find("Goal");
            if (goal != null)
                myChips.transform.SetSiblingIndex(goal.GetSiblingIndex() + 1);
            Wire(myPanel, ("status", myChips));
        }

        // 상태 알약 묶음 — 「행동 금지」 · 「스킵」 (꺼 둔다). stackUp 이면 아래 끝에서 위로 쌓고, 아니면 옆으로.
        private static StatusChipsView BuildStatusChips(string name, Transform parent, bool stackUp)
        {
            RectTransform root = NewRect(name, parent);
            if (stackUp)
            {
                VerticalLayoutGroup column = root.gameObject.AddComponent<VerticalLayoutGroup>();
                column.spacing = 6f;
                column.childAlignment = TextAnchor.LowerLeft;
                column.childControlWidth = true;
                column.childControlHeight = true;
                column.childForceExpandWidth = false;
                column.childForceExpandHeight = false;
            }
            else
            {
                HorizontalLayout(root, new RectOffset(0, 0, 0, 0), 8f, TextAnchor.LowerLeft, true, true);
            }

            FitBoth(root.gameObject);
            GameObject prefab = StatusChipPrefab();
            GameObject blocked = StatusChip(prefab, root, "Blocked", "icon_lock", "행동 금지");
            GameObject skip = StatusChip(prefab, root, "Skip", "icon_skip", "스킵");
            var view = root.gameObject.AddComponent<StatusChipsView>();
            Wire(view, ("blockChip", blocked), ("skipChip", skip));
            return view;
        }

        private static GameObject StatusChip(GameObject prefab, Transform parent, string name, string icon, string text)
        {
            var chip = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            chip.name = name;
            chip.transform.Find("Icon").GetComponent<Image>().sprite = Hud(icon);
            chip.transform.Find("Text").GetComponent<TMP_Text>().text = text;
            chip.SetActive(false);
            return chip;
        }

        // 상태 알약 한 개 (프리팹) — 흰 알약 · 잉크 테 4 · 높이 52, 아이콘 34 + 청록 글자 (Pretendard Bold 26)
        private static GameObject StatusChipPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatusChipPrefabPath);
            if (existing != null)
                return existing;

            RectTransform root = NewRect("StatusChip", null);
            root.sizeDelta = new Vector2(160f, 52f);
            HorizontalLayout(root, new RectOffset(8, 18, 0, 0), 8f, TextAnchor.MiddleLeft, true, true);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            UIShape fill = NewShape("Fill", root, White, 26f, Line4, Ink);
            Stretch(fill.rectTransform);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            FixedIcon("Icon", root, Hud("icon_lock"), 34f, 34f, White);
            BoldBodyText("Text", root, "행동 금지", 26f, StatusTeal);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, StatusChipPrefabPath);
            Object.DestroyImmediate(root.gameObject);
            return saved;
        }

        // 판 효과 줄 — 왼쪽 세로 묶음 (자명종 → 판 효과 알약들). 알약은 두 줄 (주황 이름표 / 설명), 폭 282.
        private static void BuildBoardColumn(RectTransform boardLayer, RectTransform gaepanHud, BoardEffectsView boardEffects)
        {
            var effects = (RectTransform)boardEffects.transform;
            var column = (RectTransform)boardLayer.Find("BoardColumn");
            if (column == null)
            {
                column = NewRect("BoardColumn", boardLayer);
                VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                FitBoth(column.gameObject);
            }

            if (effects.parent == boardLayer) // 처음 옮길 때만 — 다시 돌리면 effects 는 이미 묶음 안이다
                column.SetSiblingIndex(effects.GetSiblingIndex());
            Anchor(column, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(BoardColumnTopLeft.x, -BoardColumnTopLeft.y), Vector2.zero);
            gaepanHud.SetParent(column, false);
            effects.SetParent(column, false);
            gaepanHud.SetSiblingIndex(0);
            effects.SetSiblingIndex(1);
            Object.DestroyImmediate(gaepanHud.GetComponent<ContentSizeFitter>()); // 크기는 묶음이 정한다
            Object.DestroyImmediate(effects.GetComponent<ContentSizeFitter>());
            CompactGaepanHud(gaepanHud);

            if (effects.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row)
            {
                Object.DestroyImmediate(row);
                VerticalLayoutGroup list = effects.gameObject.AddComponent<VerticalLayoutGroup>();
                list.spacing = 10f;
                list.childAlignment = TextAnchor.UpperLeft;
                list.childControlWidth = true;
                list.childControlHeight = true;
                list.childForceExpandWidth = false;
                list.childForceExpandHeight = false;
            }

            var pill = (RectTransform)new SerializedObject(boardEffects).FindProperty("pillTemplate").objectReferenceValue;
            if (pill.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup pillRow)
            {
                Object.DestroyImmediate(pillRow);
                VerticalLayoutGroup lines = pill.gameObject.AddComponent<VerticalLayoutGroup>();
                lines.padding = new RectOffset(14, 18, 10, 12);
                lines.spacing = 6f;
                lines.childAlignment = TextAnchor.UpperLeft;
                lines.childControlWidth = true;
                lines.childControlHeight = true;
                lines.childForceExpandWidth = false;
                lines.childForceExpandHeight = false;
            }

            LayoutElement pillSize = pill.GetComponent<LayoutElement>();
            pillSize.preferredWidth = BoardPillWidth;
            pillSize.preferredHeight = -1f;
            TMP_Text text = pill.Find(BoardEffectsView.TextChild).GetComponent<TMP_Text>();
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.fontSize = 24f;
        }

        // 개판 자명종을 줄 폭에 맞게 — 시계 0.8 배 · 글자 작게 · 「n 바퀴」 (시안 「개판 오분전 5바퀴」)
        private static void CompactGaepanHud(RectTransform hud)
        {
            HorizontalLayoutGroup row = hud.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(8, 18, 4, 4);
            row.spacing = 6f;
            var clock = (RectTransform)hud.Find("Clock");
            clock.localScale = Vector3.one * 0.8f;
            LayoutElement clockSize = clock.GetComponent<LayoutElement>();
            clockSize.preferredWidth = 54f;
            clockSize.preferredHeight = 54f;
            hud.Find("Label").GetComponent<TMP_Text>().fontSize = 23f;
            hud.Find("Number").GetComponent<TMP_Text>().fontSize = 44f;
            TMP_Text unit = hud.Find("Unit").GetComponent<TMP_Text>();
            unit.text = "바퀴";
            unit.fontSize = 22f;
        }

        // 개판 시작 제목 · 판정 팻말 — 상대 이름표를 덮지 않게 판 가운데 쪽으로
        private static void MoveGaepanTexts(Transform canvas)
        {
            GaepanTitleView title = canvas.GetComponentInChildren<GaepanTitleView>(true);
            GaepanJudgeView judge = canvas.GetComponentInChildren<GaepanJudgeView>(true);
            SetY(title != null ? title.transform.Find("Lines") : null, GaepanTitleY);
            SetY(title != null ? title.transform.Find("Stamp") : null, GaepanStampY);
            if (title != null && title.transform.Find("Stamp") is RectTransform stamp)
                stamp.anchoredPosition = new Vector2(GaepanStampX, stamp.anchoredPosition.y);
            SetY(judge != null ? judge.transform.Find("Banner") : null, GaepanBannerY);
            SetY(judge != null ? judge.transform.Find("Roll") : null, GaepanRollY);
        }

        private static void SetY(Transform part, float y)
        {
            if (part is RectTransform rect)
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
        }

        // 문구 Bold (Pretendard Bold) — 없으면 본문 글꼴
        private static TMP_Text BoldBodyText(string name, Transform parent, string text, float size, Color color)
        {
            TMP_Text tmp = NewText(name, parent, text, size, color, false);
            if (PretendardBold != null)
                tmp.font = PretendardBold;
            return tmp;
        }
    }
}
