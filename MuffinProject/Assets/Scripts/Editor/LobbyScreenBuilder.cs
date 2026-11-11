using System.Collections.Generic;
using System.IO;
using Chapchu.UI;
using Chapchu.UI.Lobby;
using Chapchu.UI.Title;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 로비 화면을 「통합 화면 UI v2」 로비(게시본 1794316285-7080, 로그인 이후 허브)대로 조립한다 — 메뉴 Chapchu/Lobby Screen/Build UI.
    /// 열린 LobbyScene 에 LobbyCanvas 를 만든다. 이미 있으면 덮어쓰지 않는다 (사람이 손본 것을 지우지 않게).
    /// 수치는 시안 무대(960 × 540) × 2 = 캔버스 1920 × 1080. 판 · 버튼 · 그림자는 공용 스프라이트에 색만 (CODE_CONVENTION 10절).
    /// 기준 문서: docs/systems/17-lobby-ui-full.md · 14-lobby-ui.md
    /// </summary>
    public static class LobbyScreenBuilder
    {
        private const string CanvasName = "LobbyCanvas";
        private const string FontAssetPath = "Assets/Fonts/Jua/Jua SDF.asset";
        private const string MonoFontAssetPath = "Assets/Fonts/IBMPlexMono SDF.asset";
        private const string OutlineMaterialPath = "Assets/Fonts/Jua/Jua SDF - Outline.mat";
        private const string GainMaterialPath = "Assets/Fonts/Jua/Jua SDF - OutlineWhite.mat";
        private const string RoundSpritePath = "Assets/Sprites/UI/Common/ui_round64.png";
        private const string SoftRectSpritePath = "Assets/Sprites/UI/Common/ui_soft_rect.png";
        private const string RingSpritePath = "Assets/Sprites/UI/Hud/timer_ring.png";
        private const string CardBackPath = "Assets/Sprites/Cards/CardBack.jpg";

        // 링 스프라이트(timer_ring 184px)에서 링 바깥 지름이 차지하는 비율 — 160 / 184
        private const float RingImageRatio = 184f / 160f;

        // 이 화면에서 새로 들여온 그림 — 스프라이트로 쓰게 임포트 설정을 맞춘다
        private static readonly string[] NewSprites =
        {
            "Common/icon_bot", "Common/icon_copy", "Common/icon_friends", "Common/icon_mail", "Common/icon_plus", "Common/icon_shop",
            "Lobby/avatar_default", "Lobby/coin_bone", "Lobby/coin_churu", "Lobby/fx_burst", "Lobby/icon_enter", "Lobby/icon_ghost",
            "Lobby/icon_gift", "Lobby/icon_lock", "Lobby/icon_none", "Lobby/icon_play", "Lobby/icon_quest", "Lobby/icon_shirt",
            "Lobby/icon_spark", "Lobby/icon_unfriend", "Lobby/lobby_character",
        };

        // 「통합 화면 UI v2」 키트 색
        private static readonly Color Ink = Hex(0x2a2420);
        private static readonly Color Ink2 = Hex(0x6e645c);
        private static readonly Color Band = Hex(0xcfeedd);
        private static readonly Color Turn = Hex(0xffc12e);
        private static readonly Color Hp = Hex(0xec3b46);
        private static readonly Color Warn = Hex(0xff9f1a);
        private static readonly Color Off = Hex(0xb8aea4);
        private static readonly Color Soft = Hex(0xf6f0e7);
        private static readonly Color Line = Hex(0xe7ded2);
        private static readonly Color Track = Hex(0xefe8de);
        private static readonly Color Gold = Hex(0xe3a008);
        private static readonly Color BonusFill = Hex(0xfff6dc);
        private static readonly Color White = Color.white;
        private static readonly Color Dim = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.42f);
        private static readonly Color ResultDim = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.6f);
        private static readonly Color FootShadow = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.14f);

        // 조립이 끝난 뒤(레이아웃 계산 뒤) 꺼 둘 것들
        private static readonly List<GameObject> _hidden = new List<GameObject>();

        private static TMP_FontAsset _font;
        private static TMP_FontAsset _mono;
        private static Material _outlineMaterial;
        private static Material _gainMaterial;
        private static Sprite _round;
        private static Sprite _ring;
        private static Sprite _cardBack;
        private static Sprite _character;
        private static Sprite _avatar;

        [MenuItem("Chapchu/Lobby Screen/Build UI")]
        public static void Build()
        {
            if (GameObject.Find(CanvasName) != null)
            {
                Debug.LogError($"[{nameof(LobbyScreenBuilder)}] 씬에 {CanvasName} 가 이미 있다. 덮어쓰지 않는다 — 지우고 다시 실행할 것.");
                return;
            }

            if (!EnsureSprites())
                return;

            _font = Load<TMP_FontAsset>(FontAssetPath);
            _mono = Load<TMP_FontAsset>(MonoFontAssetPath);
            _outlineMaterial = Load<Material>(OutlineMaterialPath);
            _round = Load<Sprite>(RoundSpritePath);
            _ring = Load<Sprite>(RingSpritePath);
            _cardBack = Load<Sprite>(CardBackPath);
            _character = Lobby("lobby_character");
            _avatar = Lobby("avatar_default");
            if (_font == null || _mono == null || _outlineMaterial == null || _round == null || _ring == null || _cardBack == null || _character == null || _avatar == null)
                return;

            _gainMaterial = EnsureGainMaterial();
            _hidden.Clear();

            RectTransform canvas = BuildCanvas();
            BuildBackground(canvas);

            // 가장자리에 붙는 화면 — 노치 안쪽
            RectTransform edge = NewRect("SafeArea", canvas);
            Stretch(edge);
            edge.gameObject.AddComponent<SafeArea>();
            LobbyHeroView hero = BuildHero(edge);
            LobbyHomeView home = BuildHome(edge);
            LobbySectionView section = BuildSection(edge, out LobbyItemPaneView itemPane, out LobbyGachaPaneView gachaPane);
            LobbyTopBarView topBar = BuildTopBar(edge);

            // 창 — 가림판 하나를 같이 쓴다
            Image popupDimmer = NewImage("PopupDimmer", canvas, Dim, null, true); // 바탕 탭을 먹는다 — 눌러도 닫히지 않는다 (22 1절 #4)
            Stretch(popupDimmer.rectTransform);
            popupDimmer.gameObject.SetActive(false);
            LobbyFriendsPopupView friends = BuildFriendsPopup(canvas);
            LobbyProfilePopupView profile = BuildProfilePopup(canvas);
            LobbySettingsPopupView settings = BuildSettingsPopup(canvas);
            LobbyQuestPopupView quests = BuildQuestPopup(canvas);
            LobbyRatesPopupView rates = BuildRatesPopup(canvas);

            // 위층 — 입력 · 경고 · 요청 중 → 초대 배너 → 뽑기 결과 → 알림 · 손맛
            LobbyModalView modal = BuildModal(canvas);
            LobbyInviteBannerView invite = BuildInvite(canvas);
            LobbyGachaResultView gachaResult = BuildGachaResult(canvas);
            LobbyFxView fx = BuildFx(canvas);

            EnsureEventSystem();

            LobbyPresenter presenter = canvas.gameObject.AddComponent<LobbyPresenter>();
            Wire(presenter,
                ("home", home), ("topBar", topBar), ("hero", hero), ("section", section), ("itemPane", itemPane), ("gachaPane", gachaPane),
                ("popupDimmer", popupDimmer.gameObject), ("friends", friends), ("profile", profile), ("settings", settings), ("quests", quests), ("rates", rates),
                ("modal", modal), ("invite", invite), ("gachaResult", gachaResult), ("fx", fx), ("playIcon", Lobby("icon_play")));

            // 레이아웃을 지금 계산해 둔다 — 안 그러면 저장 뒤 첫 갱신에서 앵커 · 크기가 바뀌어 씬이 다시 더러워진다
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvas);
            Canvas.ForceUpdateCanvases();
            foreach (GameObject go in _hidden)
                go.SetActive(false);
            _hidden.Clear();

            // 끈 것이 레이아웃 그룹에서 빠진 상태까지 계산해 둔다 — 안 그러면 저장 뒤 첫 갱신에서 씬이 다시 더러워진다
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvas);
            Canvas.ForceUpdateCanvases();

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = canvas.gameObject;
            Debug.Log($"[{nameof(LobbyScreenBuilder)}] {CanvasName} 조립 완료 — 씬을 저장할 것.");
        }

        #region 캔버스 · 배경

        private static RectTransform BuildCanvas()
        {
            var go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 15-screen.md 3절 #3
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return (RectTransform)go.transform;
        }

        // 하늘 → 분홍 + 벽 무늬 (시안 .bgv2 — 인게임과 같은 그림)
        private static void BuildBackground(RectTransform canvas)
        {
            Image gradient = NewImage("Gradient", canvas, White, Hud("bg_gradient"));
            Stretch(gradient.rectTransform);
            Image wall = NewImage("Wall", canvas, White, Hud("bg_wall"));
            wall.type = Image.Type.Tiled;
            Stretch(wall.rectTransform);
        }

        // 가운데 1920 × 1080 묶음 — 넓거나 높은 화면에서도 창은 가운데
        private static RectTransform BuildCenter(string name, Transform parent)
        {
            RectTransform center = NewRect(name, parent);
            Anchor(center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
            return center;
        }

        #endregion

        #region 캐릭터 · 홈 · 오른쪽 위

        // 내 캐릭터 — 왼쪽 80 · 아래 100 × 2, 누르는 자리 360 × 330 × 2, 그림 높이 318 × 2 (시안 .hero)
        private static LobbyHeroView BuildHero(RectTransform edge)
        {
            Image hit = NewImage("Hero", edge, Color.clear, null, true);
            RectTransform root = hit.rectTransform;
            Anchor(root, Vector2.zero, Vector2.zero, new Vector2(160f, 200f), new Vector2(720f, 660f));
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            // 발밑 그림자 — 타원 380 × 60 (원을 눌러 만든다)
            Image shadow = RoundImage("Shadow", root, FootShadow, 80f);
            Anchor(shadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(160f, 160f));
            shadow.rectTransform.localScale = new Vector3(380f / 160f, 60f / 160f, 1f);

            Image character = NewImage("Character", root, White, _character);
            character.preserveAspect = true;
            float width = 636f * _character.rect.width / _character.rect.height;
            Anchor(character.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(width, 636f));

            // 카드 뒷면 미리보기 — 74 × 104 × 2, 오른쪽 6 · 아래 60 × 2. 「재생」 때만 보인다
            RectTransform card = NewRect("CardPreview", root);
            Anchor(card, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-86f, 224f), new Vector2(148f, 208f));
            CanvasGroup cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            Stretch(RoundImage("Border", card, Ink, 18f).rectTransform);
            Image cardImage = BuildCover("Art", card, _cardBack, 6f);

            LobbyHeroView view = root.gameObject.AddComponent<LobbyHeroView>();
            Wire(view, ("button", button), ("character", character), ("cardPreview", card), ("cardPreviewGroup", cardGroup), ("cardPreviewImage", cardImage));
            return view;
        }

        // 홈 — 프로필 칩(왼쪽 위) · 퀘스트 / 꾸미기 / 상점(왼쪽 아래) · 플레이 묶음(오른쪽 아래) · 버전 (시안 #home)
        private static LobbyHomeView BuildHome(RectTransform edge)
        {
            RectTransform root = NewRect("Home", edge);
            Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();

            // 프로필 칩 — 높이 64 × 2 · 왼쪽 위 24 × 2. 사진 44 · 닉네임 24 · 친구 코드 15 (× 2)
            ButtonParts chip = BuildButton("ProfileChip", root, 0f, 128f, White, 28f, 5f, 6f, null, 0f, null, 0f, 0f, 24f);
            HorizontalLayoutGroup chipLayout = chip.Body.GetComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(20, 32, 0, 0);
            chipLayout.childAlignment = TextAnchor.MiddleLeft;
            Anchor(chip.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -48f), new Vector2(400f, 128f));
            FitWidth(chip);
            BuildAvatar(chip.Body, 88f, 5f);
            RectTransform chipTexts = NewRect("Texts", chip.Body);
            VerticalLayout(chipTexts, new RectOffset(), 6f, TextAnchor.MiddleLeft);
            TMP_Text nickname = NewText("Nickname", chipTexts, "닉네임", 48f, Ink, TextAlignmentOptions.Left);
            nickname.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement nicknameSize = nickname.gameObject.AddComponent<LayoutElement>();
            RectTransform codeRow = NewRect("Code", chipTexts);
            HorizontalLayout(codeRow, new RectOffset(), 10f, TextAnchor.MiddleLeft, true, true);
            TMP_Text code = NewText("Text", codeRow, "ML01-JY22", 30f, Ink2, TextAlignmentOptions.Left, _mono);
            Fixed(Icon("Copy", codeRow, Common("icon_copy"), 26f, Ink2), 26f, 26f);

            // 아래 왼쪽 줄 — 퀘스트 128 · 꾸미기 160 · 상점 128 (× 2), 높이 64 × 2, 아래 28 × 2. 캐릭터 가운데에 맞춘 자리
            ButtonParts quest = BuildButton("QuestButton", root, 256f, 128f, White, 22f, 5f, 6f, "퀘스트", 38f, Lobby("icon_quest"), 48f, 0f, 14f);
            Anchor(quest.Root, Vector2.zero, Vector2.zero, new Vector2(80f, 56f), new Vector2(256f, 128f));
            (GameObject questBadge, TMP_Text questBadgeText) = BuildBadge(quest.Body);
            ButtonParts deco = BuildButton("DecoButton", root, 320f, 128f, White, 22f, 5f, 6f, "꾸미기", 40f, Lobby("icon_shirt"), 48f, 0f, 16f);
            Anchor(deco.Root, Vector2.zero, Vector2.zero, new Vector2(360f, 56f), new Vector2(320f, 128f));
            ButtonParts shop = BuildButton("ShopButton", root, 256f, 128f, White, 22f, 5f, 6f, "상점", 38f, Common("icon_shop"), 48f, 0f, 14f);
            Anchor(shop.Root, Vector2.zero, Vector2.zero, new Vector2(704f, 56f), new Vector2(256f, 128f));

            // 플레이 묶음 — 폭 312 × 2, 오른쪽 24 · 아래 28 × 2. 작은 버튼 셋(높이 64) 위, 「랜덤 매칭」(높이 88) 아래
            RectTransform play = NewRect("Play", root);
            Anchor(play, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 56f), new Vector2(624f, 320f));
            VerticalLayout(play, new RectOffset(), 16f, TextAnchor.LowerCenter);
            play.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RectTransform row = NewRect("Row", play);
            HorizontalLayout(row, new RectOffset(), 12f, TextAnchor.MiddleCenter, true, true).childForceExpandWidth = true;
            ButtonParts solo = BuildButton("SoloButton", row, 0f, 128f, White, 22f, 5f, 6f, "혼자 하기", 30f, Common("icon_bot"), 40f, 8f, 10f, true);
            Flex(solo);
            ButtonParts create = BuildButton("CreateButton", row, 0f, 128f, White, 22f, 5f, 6f, "방 만들기", 30f, Common("icon_plus"), 40f, 8f, 10f, true);
            Flex(create);
            ButtonParts join = BuildButton("CodeButton", row, 0f, 128f, White, 22f, 5f, 6f, "코드로 참가", 30f, Common("icon_key"), 40f, 8f, 10f, true);
            Flex(join);
            ButtonParts random = BuildButton("RandomButton", play, 0f, 176f, Turn, 32f, 6f, 10f, "랜덤 매칭", 56f, Common("icon_dice"), 60f, 28f, 20f);

            // 버전 — 왼쪽 13 · 아래 11 × 2, 고정폭 11 × 2
            TMP_Text version = NewText("Version", root, "ver 0.1.0 · Project ChapChu", 22f, Ink2, TextAlignmentOptions.BottomLeft, _mono);
            Anchor(version.rectTransform, Vector2.zero, Vector2.zero, new Vector2(26f, 22f), new Vector2(800f, 30f));
            version.gameObject.AddComponent<VersionLabel>();

            LobbyHomeView view = root.gameObject.AddComponent<LobbyHomeView>();
            Wire(view,
                ("profileButton", chip.Button), ("nicknameText", nickname), ("nicknameSize", nicknameSize), ("codeText", code),
                ("questButton", quest.Button), ("questBadge", questBadge), ("questBadgeText", questBadgeText), ("decoButton", deco.Button), ("shopButton", shop.Button),
                ("soloButton", solo.Button), ("createButton", create.Button), ("codeButton", join.Button), ("randomButton", random.Button));
            return view;
        }

        // 오른쪽 위 — 재화 둘 · 친구(배지) · 설정. 오른쪽 24 · 위 24 × 2, 사이 12 × 2 (시안 .tr · .wallet)
        private static LobbyTopBarView BuildTopBar(RectTransform edge)
        {
            RectTransform root = NewRect("TopBar", edge);
            Anchor(root, Vector2.one, Vector2.one, new Vector2(-48f, -48f), new Vector2(800f, 128f));
            HorizontalLayout(root, new RectOffset(), 24f, TextAnchor.MiddleRight, true, true);
            FitBoth(root.gameObject);

            RectTransform wallet = NewRect("Wallet", root);
            HorizontalLayout(wallet, new RectOffset(), 12f, TextAnchor.MiddleCenter, true, true);
            (Button bone, TMP_Text boneText) = BuildCoin("BoneCoin", wallet, Lobby("coin_bone"));
            (Button churu, TMP_Text churuText) = BuildCoin("ChuruCoin", wallet, Lobby("coin_churu"));

            ButtonParts friends = BuildButton("FriendsButton", root, 128f, 128f, White, 28f, 5f, 6f, null, 0f, Common("icon_friends"), 56f, 0f);
            (GameObject badge, TMP_Text badgeText) = BuildBadge(friends.Body);
            ButtonParts settings = BuildButton("SettingsButton", root, 128f, 128f, White, 28f, 5f, 6f, null, 0f, Hud("icon_gear"), 56f, 0f);

            LobbyTopBarView view = root.gameObject.AddComponent<LobbyTopBarView>();
            Wire(view,
                ("boneButton", bone), ("boneText", boneText), ("churuButton", churu), ("churuText", churuText),
                ("friendsButton", friends.Button), ("friendsBadge", badge), ("friendsBadgeText", badgeText), ("settingsButton", settings.Button));
            return view;
        }

        // 재화 칸 — 높이 44 × 2 · 그림 24 · 숫자 18 · 노란 「+」 22 (× 2). 누르면 충전 탭 (시안 .coin)
        private static (Button, TMP_Text) BuildCoin(string name, Transform parent, Sprite coin)
        {
            ButtonParts parts = BuildButton(name, parent, 0f, 88f, White, 24f, 5f, 6f, null, 0f, null, 0f, 0f, 12f);
            parts.Body.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(18, 12, 0, 0);
            Fixed(Icon("Coin", parts.Body, coin, 48f, White), 48f, 48f);
            TMP_Text amount = NewText("Amount", parts.Body, "0", 36f, Ink);
            RectTransform plus = NewRect("Plus", parts.Body);
            Fixed(plus, 44f, 44f);
            RoundBox(plus, Ink, 4f, 14f, Turn);
            Image cross = Icon("Icon", plus, Common("icon_plus"), 24f, Ink);
            Anchor(cross.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            return (parts.Button, amount);
        }

        // 빨간 숫자 배지 — 버튼 오른쪽 위 모서리 밖 10 × 2, 높이 26 × 2 (시안 .cnt)
        private static (GameObject, TMP_Text) BuildBadge(RectTransform buttonBody)
        {
            RectTransform root = NewRect("Badge", buttonBody);
            Anchor(root, Vector2.one, Vector2.one, new Vector2(20f, 20f), new Vector2(52f, 52f));
            LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
            size.ignoreLayout = true;
            size.minWidth = 52f;
            HorizontalLayout(root, new RectOffset(12, 12, 0, 0), 0f, TextAnchor.MiddleCenter, true, true);
            root.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            RoundBox(root, Ink, 5f, 26f, Hp);
            TMP_Text text = NewText("Count", root, "1", 28f, White);
            root.gameObject.SetActive(false);
            return (root.gameObject, text);
        }

        #endregion

        #region 섹션 — 머리줄 · 정보 줄 · 오른쪽 판

        private static LobbySectionView BuildSection(RectTransform edge, out LobbyItemPaneView itemPane, out LobbyGachaPaneView gachaPane)
        {
            RectTransform root = NewRect("Section", edge);
            Stretch(root);

            // 머리줄 — 뒤로 64 · 이름 칸 · 상점 탭 셋(칸 폭 84). 왼쪽 위 24 × 2, 사이 12 × 2 (시안 .secbar)
            RectTransform head = NewRect("Head", root);
            Anchor(head, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -48f), new Vector2(800f, 128f));
            HorizontalLayout(head, new RectOffset(), 24f, TextAnchor.MiddleLeft, true, true);
            FitBoth(head.gameObject);
            CanvasGroup headGroup = head.gameObject.AddComponent<CanvasGroup>();
            ButtonParts back = BuildButton("BackButton", head, 128f, 128f, White, 28f, 5f, 6f, null, 0f, Common("icon_back"), 56f, 0f);
            RectTransform nameBox = NewRect("Name", head);
            HorizontalLayout(nameBox, new RectOffset(32, 32, 0, 0), 0f, TextAnchor.MiddleCenter, true, true);
            Fixed(nameBox, 0f, 128f);
            RoundBox(nameBox, Ink, 5f, 28f, White);
            TMP_Text nameText = NewText("Label", nameBox, "꾸미기", 48f, Ink);
            RectTransform shopTabRow = NewRect("ShopTabs", head);
            HorizontalLayout(shopTabRow, new RectOffset(11, 11, 0, 0), 6f, TextAnchor.MiddleCenter, true, true);
            Fixed(shopTabRow, 0f, 128f);
            RoundBox(shopTabRow, Ink, 5f, 28f, White);
            string[] shopTabNames = { "뽑기", "아이템", "충전" };
            var shopTabs = new LobbyTabView[shopTabNames.Length];
            for (int i = 0; i < shopTabs.Length; i++)
                shopTabs[i] = BuildTab($"Tab{i}", shopTabRow, shopTabNames[i], 168f, 106f, 20f, 5f, 40f, 0f);

            // 정보 줄 — 캐릭터 아래. 왼쪽 80 · 아래 28 × 2, 360 × 64 × 2 (시안 .info)
            RectTransform info = NewRect("Info", root);
            Anchor(info, Vector2.zero, Vector2.zero, new Vector2(160f, 56f), new Vector2(720f, 128f));
            HorizontalLayout(info, new RectOffset(28, 18, 0, 0), 20f, TextAnchor.MiddleLeft, true, true);
            CanvasGroup infoGroup = info.gameObject.AddComponent<CanvasGroup>();
            RoundBox(info, Ink, 5f, 28f, White);
            RectTransform texts = NewRect("Texts", info);
            FlexWidth(texts.gameObject);
            VerticalLayout(texts, new RectOffset(), 0f, TextAnchor.MiddleLeft);
            RectTransform item = NewRect("Item", texts);
            VerticalLayout(item, new RectOffset(), 8f, TextAnchor.MiddleLeft);
            TMP_Text infoName = NewText("Name", item, "이름 미정", 40f, Ink, TextAlignmentOptions.Left);
            infoName.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform sub = NewRect("Sub", item);
            HorizontalLayout(sub, new RectOffset(), 12f, TextAnchor.MiddleLeft, true, true);
            (GameObject gradeTag, Image gradeTagBorder, TMP_Text gradeTagText) = BuildGradeTag(sub, 40f, 14f, 12f, 24f);
            Image leadIcon = Fixed(Icon("LeadIcon", sub, Lobby("coin_churu"), 36f, White), 36f, 36f);
            TMP_Text subText = NewText("Text", sub, "캐릭터 · 보유", 26f, Ink2, TextAlignmentOptions.Left);
            Image priceIcon = Fixed(Icon("PriceIcon", sub, Lobby("coin_bone"), 30f, White), 30f, 30f);
            TMP_Text priceText = NewText("Price", sub, "300", 26f, Ink, TextAlignmentOptions.Left);
            TMP_Text hint = NewText("Hint", texts, "아직 고른 상품이 없어요", 30f, Ink2, TextAlignmentOptions.Left);
            ButtonParts infoButton = BuildButton("InfoButton", info, 0f, 88f, White, 22f, 5f, 6f, "확률 보기", 30f, Lobby("icon_play"), 34f);

            // 오른쪽 판 — 464 × 396 × 2, 오른쪽 24 · 아래 28 × 2, 테 3 × 2 (시안 .pane)
            RectTransform pane = NewRect("Pane", root);
            Anchor(pane, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 56f), new Vector2(928f, 792f));
            RoundBox(pane, Ink, 6f, 28f, White, true);
            RectTransform paneContent = NewRect("Content", pane);
            Stretch(paneContent, 6f, 6f, 6f, 6f);
            CanvasGroup paneGroup = paneContent.gameObject.AddComponent<CanvasGroup>();
            itemPane = BuildItemPane(paneContent);
            gachaPane = BuildGachaPane(paneContent);

            LobbySectionView view = root.gameObject.AddComponent<LobbySectionView>();
            Wire(view,
                ("head", headGroup), ("pane", pane), ("paneContent", paneGroup), ("info", infoGroup),
                ("backButton", back.Button), ("nameText", nameText), ("shopTabRow", shopTabRow.gameObject),
                ("infoItem", item.gameObject), ("infoName", infoName), ("gradeTag", gradeTag), ("gradeTagBorder", gradeTagBorder), ("gradeTagText", gradeTagText),
                ("infoLeadIcon", leadIcon), ("infoSubText", subText), ("infoPriceIcon", priceIcon), ("infoPriceText", priceText), ("infoHint", hint),
                ("infoButton", infoButton.Button), ("infoButtonIcon", infoButton.Icon), ("infoButtonLabel", infoButton.Label));
            WireArray(view, "shopTabs", shopTabs);
            return view;
        }

        // 판 안 아이템 격자 — 머리 띠 탭(높이 44) · 격자(4칸 × 98, 사이 8 · 10) · 아래 줄(높이 66) (× 2, 시안 .ptabs · .grid · .pfoot)
        private static LobbyItemPaneView BuildItemPane(RectTransform paneContent)
        {
            RectTransform root = NewRect("ItemPane", paneContent);
            Stretch(root);
            RectTransform content = NewRect("Content", root);
            Stretch(content);

            // 머리 띠 — 민트, 위 모서리만 둥글게
            RectTransform bandArea = NewRect("Band", content);
            TopStrip(bandArea, 0f, 88f);
            BuildRoundedEdge(bandArea, Band, 22f, false);
            Image bandLine = NewImage("BandLine", content, Ink);
            TopStrip(bandLine.rectTransform, 88f, 4f);

            RectTransform tabRow = NewRect("Tabs", content);
            TopStrip(tabRow, 0f, 88f);
            HorizontalLayout(tabRow, new RectOffset(10, 10, 10, 10), 6f, TextAnchor.MiddleCenter, true, true).childForceExpandWidth = true;
            var tabs = new LobbyTabView[5];
            for (int i = 0; i < tabs.Length; i++)
                tabs[i] = BuildTab($"Tab{i}", tabRow, LobbyHubData.SlotName((HubSlot)i), -1f, 68f, 16f, 4f, 32f, 0f);

            RectTransform chargeLabel = NewRect("ChargeLabel", content);
            TopStrip(chargeLabel, 0f, 88f);
            HorizontalLayout(chargeLabel, new RectOffset(), 12f, TextAnchor.MiddleCenter, true, true);
            Fixed(Icon("Icon", chargeLabel, Lobby("coin_churu"), 40f, White), 40f, 40f);
            NewText("Label", chargeLabel, "츄르 충전 · 뽑기에 써요", 32f, Ink);
            chargeLabel.gameObject.SetActive(false);

            // 격자 — 4칸. 칸마다 아래 두께 · 고른 칸 테가 있어 사이를 넉넉히 둔다
            Image scrollHit = NewImage("Grid", content, Soft, null, true);
            Stretch(scrollHit.rectTransform, 0f, 92f, 0f, 136f);
            RectTransform viewport = NewRect("Viewport", scrollHit.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform gridRect = NewRect("Content", viewport);
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(1f, 1f);
            gridRect.pivot = new Vector2(0.5f, 1f);
            gridRect.anchoredPosition = Vector2.zero;
            gridRect.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(20, 20, 24, 32);
            grid.cellSize = new Vector2(196f, 196f);
            grid.spacing = new Vector2(16f, 20f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            gridRect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = scrollHit.gameObject.AddComponent<ScrollRect>();
            SetupScroll(scroll, viewport, gridRect);
            var cells = new LobbyItemCellView[10];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = BuildCell(gridRect, i);

            // 아래 줄 — 왼쪽에 「어디서 얻는지」, 오른쪽에 버튼 (폭 150 × 2 부터, 높이 46 × 2)
            Image footLine = NewImage("FootLine", content, Ink);
            BottomStrip(footLine.rectTransform, 132f, 4f);
            RectTransform foot = NewRect("Foot", content);
            BottomStrip(foot, 0f, 132f);
            HorizontalLayout(foot, new RectOffset(24, 24, 0, 0), 16f, TextAnchor.MiddleRight, true, true);
            RectTransform note = NewRect("Note", foot);
            FlexWidth(note.gameObject);
            HorizontalLayout(note, new RectOffset(), 8f, TextAnchor.MiddleLeft, true, true);
            TMP_Text noteText = NewText("Text", note, "뽑기에서만 나와요", 26f, Ink2, TextAlignmentOptions.Left);
            Image noteIcon = Fixed(Icon("Icon", note, Lobby("coin_bone"), 30f, White), 30f, 30f);
            TMP_Text noteAmount = NewText("Amount", note, "300", 26f, Ink2, TextAlignmentOptions.Left);
            ButtonParts footButton = BuildButton("FootButton", foot, 0f, 92f, Turn, 22f, 5f, 6f, "장착", 38f, Lobby("icon_shirt"), 36f, 24f, 10f);
            footButton.Button.GetComponent<LayoutElement>().minWidth = 300f;
            Image footPriceIcon = Fixed(Icon("PriceIcon", footButton.Body, Lobby("coin_bone"), 36f, White), 36f, 36f);
            TMP_Text footPriceText = NewText("Price", footButton.Body, "300", 38f, Ink);
            Dimmable(footButton, footPriceText);

            LobbyItemPaneView view = root.gameObject.AddComponent<LobbyItemPaneView>();
            Wire(view,
                ("content", content.gameObject), ("tabRow", tabRow.gameObject), ("chargeLabel", chargeLabel.gameObject), ("grid", grid), ("scroll", scroll),
                ("footNote", note.gameObject), ("footNoteText", noteText), ("footNoteIcon", noteIcon), ("footNoteAmount", noteAmount),
                ("footButton", footButton.Button), ("footIcon", footButton.Icon), ("footLabel", footButton.Label), ("footPriceIcon", footPriceIcon), ("footPriceText", footPriceText),
                ("boneIcon", Lobby("coin_bone")), ("churuIcon", Lobby("coin_churu")), ("shopIcon", Common("icon_shop")), ("giftIcon", Lobby("icon_gift")),
                ("shirtIcon", Lobby("icon_shirt")), ("enterIcon", Lobby("icon_enter")), ("ghostIcon", Lobby("icon_ghost")), ("cardIcon", Hud("icon_card")),
                ("sparkIcon", Lobby("icon_spark")), ("noneIcon", Lobby("icon_none")));
            WireArray(view, "tabs", tabs);
            WireArray(view, "cells", cells);
            HideAfterBuild(content.gameObject);
            return view;
        }

        // 격자 칸 — 98 × 98 (상점 칸 98 × 124) × 2 · 테 3 × 2 = 등급 색 · 아래 두께 3 × 2 (시안 .cell)
        private static LobbyItemCellView BuildCell(Transform parent, int index)
        {
            RectTransform root = NewRect($"Cell{index}", parent);
            root.sizeDelta = new Vector2(196f, 196f);

            // 고른 칸 — 칸 밖으로 3 띄운 잉크 테 2.5 (× 2)
            RectTransform selection = NewRect("Selection", root);
            Stretch(selection, -11f, -11f, -11f, -11f);
            Stretch(RoundImage("Ring", selection, Ink, 35f).rectTransform);
            Stretch(RoundImage("Gap", selection, Soft, 30f).rectTransform, 5f, 5f, 5f, 5f);

            Image shadow = RoundImage("Shadow", root, Ink, 24f);
            Stretch(shadow.rectTransform, 0f, 6f, 0f, -6f);

            RectTransform body = NewRect("Body", root);
            Stretch(body);
            Image frame = RoundImage("Frame", body, Ink2, 24f);
            Stretch(frame.rectTransform);
            Image fill = RoundImage("Fill", body, White, 18f, true);
            Stretch(fill.rectTransform, 6f, 6f, 6f, 6f);
            RectTransform inner = NewRect("Inner", body);
            Stretch(inner, 6f, 6f, 6f, 6f);

            // 그림 자리 — 상점 칸에서는 아래(이름 + 값)를 비운다 (LobbyItemCellView 가 높이를 바꾼다)
            RectTransform picture = NewRect("Picture", inner);
            Stretch(picture);
            CanvasGroup pictureGroup = picture.gameObject.AddComponent<CanvasGroup>();
            Image character = NewImage("Character", picture, White, _character);
            character.preserveAspect = true;
            Stretch(character.rectTransform, 10f, 6f, 10f, 0f);
            Image cardBack = BuildCover("CardBack", picture, _cardBack, 2f);
            RectTransform placeholder = NewRect("Placeholder", picture);
            Stretch(placeholder);
            StackCentered(placeholder, 8f);
            Image icon = Icon("Icon", placeholder, Lobby("icon_spark"), 80f, Ink);
            TMP_Text caption = NewText("Caption", placeholder, "기본", 26f, Ink);
            caption.rectTransform.sizeDelta = new Vector2(180f, 32f);

            Image lockIcon = Icon("Lock", inner, Lobby("icon_lock"), 44f, White);
            Anchor(lockIcon.rectTransform, Vector2.one, Vector2.one, new Vector2(-4f, -4f), new Vector2(44f, 44f));
            GameObject equipped = BuildStrip("Equipped", inner, true, 44f, Turn, "장착 중", 24f, 18f);
            GameObject owned = BuildStrip("Owned", inner, false, 40f, Band, "보유", 24f, 18f);

            // 상점 칸 아래 — 이름 12 · 값 꼬리표 높이 18 (× 2)
            RectTransform shopInfo = NewRect("ShopInfo", inner);
            BottomStrip(shopInfo, 0f, 96f);
            VerticalLayout(shopInfo, new RectOffset(12, 12, 4, 0), 4f, TextAnchor.UpperCenter);
            TMP_Text nameText = NewText("Name", shopInfo, "이름 미정", 24f, Ink);
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            Fixed(nameText, 0f, 32f);
            RectTransform priceRow = NewRect("PriceRow", shopInfo);
            HorizontalLayout(priceRow, new RectOffset(), 0f, TextAnchor.MiddleCenter, true, true);
            RectTransform price = NewRect("Price", priceRow);
            HorizontalLayout(price, new RectOffset(14, 14, 0, 0), 6f, TextAnchor.MiddleCenter, true, true);
            Fixed(price, 0f, 36f);
            RoundBox(price, Ink, 4f, 12f, White);
            Image priceBorder = price.GetChild(0).GetComponent<Image>();
            Image priceIcon = Fixed(Icon("Icon", price, Lobby("coin_bone"), 26f, White), 26f, 26f);
            TMP_Text priceText = NewText("Text", price, "300", 22f, Ink);

            // 충전 칸 보너스 — 오른쪽 위 노란 꼬리표
            RectTransform bonus = NewRect("Bonus", inner);
            Anchor(bonus, Vector2.one, Vector2.one, new Vector2(-2f, -2f), new Vector2(60f, 34f));
            HorizontalLayout(bonus, new RectOffset(10, 10, 2, 2), 0f, TextAnchor.MiddleCenter, true, true);
            FitBoth(bonus.gameObject);
            RoundBox(bonus, Ink, 4f, 12f, Turn);
            TMP_Text bonusText = NewText("Text", bonus, "+8", 22f, Ink);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            PressableButton press = root.gameObject.AddComponent<PressableButton>();
            Wire(press, ("body", body));
            SetFloat(press, "pressDepth", 4f);

            LobbyItemCellView view = root.gameObject.AddComponent<LobbyItemCellView>();
            Wire(view,
                ("frame", frame), ("selection", selection.gameObject), ("picture", picture), ("pictureGroup", pictureGroup),
                ("characterImage", character), ("cardBackImage", cardBack), ("iconImage", icon), ("captionText", caption),
                ("lockIcon", lockIcon.gameObject), ("equippedStrip", equipped), ("ownedStrip", owned),
                ("shopInfo", shopInfo.gameObject), ("nameText", nameText), ("priceBorder", priceBorder), ("priceIcon", priceIcon), ("priceText", priceText),
                ("bonusTag", bonus.gameObject), ("bonusText", bonusText));
            SetFloat(view, "shopInfoHeight", 96f);
            return view;
        }

        // 판 안 뽑기 — 상자 이름 · 대표 다섯 · 등급 확률 띠 · 개판 보장 막대 · 1회 / 10회 (시안 .gban · .gfoot)
        private static LobbyGachaPaneView BuildGachaPane(RectTransform paneContent)
        {
            RectTransform root = NewRect("GachaPane", paneContent);
            Stretch(root);
            RectTransform content = NewRect("Content", root);
            Stretch(content);

            RectTransform backdrop = NewRect("Backdrop", content);
            Stretch(backdrop, 0f, 0f, 0f, 136f);
            BuildRoundedEdge(backdrop, Soft, 22f, false);

            RectTransform banner = NewRect("Banner", content);
            Stretch(banner, 0f, 0f, 0f, 136f);
            VerticalLayout(banner, new RectOffset(32, 32, 28, 24), 22f, TextAnchor.UpperLeft);

            RectTransform top = NewRect("Top", banner);
            HorizontalLayout(top, new RectOffset(), 16f, TextAnchor.LowerLeft, true, true);
            FlexWidth(NewText("Title", top, LobbyHubData.GachaName, 52f, Ink, TextAlignmentOptions.Left).gameObject);
            NewText("Season", top, LobbyHubData.GachaSeason, 26f, Ink2, TextAlignmentOptions.Right);
            NewText("Label", banner, "개판 등급 · 이번 상자에서만", 26f, Ink2, TextAlignmentOptions.Left);

            // 대표 아이템 다섯 — 68 × 68 × 2, 금색 테. 그림이 없어 실루엣 · 슬롯 아이콘 자리
            RectTransform featured = NewRect("Featured", banner);
            HorizontalLayout(featured, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            var data = new LobbyHubData();
            var owned = new GameObject[LobbyHubData.FeaturedIds.Length];
            for (int i = 0; i < owned.Length; i++)
                owned[i] = BuildFeatured(featured, data.FindItem(LobbyHubData.FeaturedIds[i]));

            BuildRateBar(banner);
            RectTransform legend = NewRect("Legend", banner);
            HorizontalLayout(legend, new RectOffset(), 24f, TextAnchor.MiddleLeft, true, true);
            string inkHex = ColorUtility.ToHtmlStringRGB(Ink);
            foreach (HubGrade grade in new[] { HubGrade.Common, HubGrade.Uncommon, HubGrade.Rare, HubGrade.Legend })
            {
                RectTransform entry = NewRect(grade.ToString(), legend);
                HorizontalLayout(entry, new RectOffset(), 8f, TextAnchor.MiddleLeft, true, true);
                RectTransform swatch = NewRect("Swatch", entry);
                Fixed(swatch, 20f, 20f);
                RoundBox(swatch, Ink, 4f, 6f, LobbyStyle.GradeColor(grade));
                NewText("Text", entry, $"{LobbyHubData.GradeName(grade)} <color=#{inkHex}>{LobbyHubData.GachaRate(grade)}%</color>", 26f, Ink2, TextAlignmentOptions.Left);
            }

            // 개판 보장까지 — 글 + 막대 (높이 10 × 2)
            RectTransform pity = NewRect("Pity", banner);
            HorizontalLayout(pity, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            TMP_Text pityText = NewText("Text", pity, "개판까지 최대 37회", 28f, Ink, TextAlignmentOptions.Left);
            RectTransform pityFill = BuildBar("Bar", pity, 20f, Gold, out _);
            FlexWidth(pityFill.parent.parent.gameObject);

            Wrap(NewText("Note", banner, "10회는 희귀 이상 1개 보장 · 이미 있는 아이템은 뼈다귀로 돌려받아요", 24f, Ink2, TextAlignmentOptions.Left));

            Image footLine = NewImage("FootLine", content, Ink);
            BottomStrip(footLine.rectTransform, 132f, 4f);
            RectTransform foot = NewRect("Foot", content);
            BottomStrip(foot, 0f, 132f);
            HorizontalLayout(foot, new RectOffset(24, 24, 0, 0), 20f, TextAnchor.MiddleCenter, true, true).childForceExpandWidth = true;
            ButtonParts one = BuildRollButton("OneButton", foot, White, "1회", LobbyHubData.GachaCostOne);
            ButtonParts ten = BuildRollButton("TenButton", foot, Turn, "10회", LobbyHubData.GachaCostTen);

            LobbyGachaPaneView view = root.gameObject.AddComponent<LobbyGachaPaneView>();
            Wire(view, ("content", content.gameObject), ("pityText", pityText), ("pityFill", pityFill), ("oneButton", one.Button), ("tenButton", ten.Button));
            WireArray(view, "featuredOwned", owned);
            HideAfterBuild(content.gameObject);
            return view;
        }

        // 대표 아이템 칸 — 가진 것은 아래에 「보유」 띠. 그 띠(GameObject)를 돌려준다
        private static GameObject BuildFeatured(Transform parent, HubItem item)
        {
            RectTransform root = NewRect(item.Id, parent);
            Fixed(root, 136f, 136f);
            RoundBox(root, LobbyStyle.GradeColor(item.Grade), 6f, 22f, White);
            RectTransform inner = NewRect("Inner", root);
            Stretch(inner, 6f, 6f, 6f, 6f);
            if (item.Slot == HubSlot.Character)
            {
                Image character = NewImage("Character", inner, new Color(0f, 0f, 0f, 0.25f), _character);
                character.preserveAspect = true;
                Stretch(character.rectTransform, 4f, 4f, 4f, 4f);
            }
            else
            {
                Image icon = Icon("Icon", inner, SlotIcon(item.Slot), 60f, Ink);
                Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            }

            GameObject strip = BuildStrip("Owned", inner, true, 34f, Band, "보유", 22f, 16f);
            strip.SetActive(false);
            return strip;
        }

        // 등급 확률 띠 — 높이 16 × 2, 일반 · 고급 · 희귀 · 개판 순으로 폭 = 확률. 양 끝은 둥글게: 일반을 통으로 깔고 나머지를 위에 얹는다
        private static void BuildRateBar(Transform parent)
        {
            RectTransform root = NewRect("RateBar", parent);
            Fixed(root, 0f, 32f);
            Stretch(RoundImage("Border", root, Ink, 16f).rectTransform);
            RectTransform inner = NewRect("Inner", root);
            Stretch(inner, 4f, 4f, 4f, 4f);

            float common = LobbyHubData.GachaRate(HubGrade.Common) / 100f;
            float uncommon = common + LobbyHubData.GachaRate(HubGrade.Uncommon) / 100f;
            float rare = uncommon + LobbyHubData.GachaRate(HubGrade.Rare) / 100f;
            Stretch(RoundImage("Common", inner, LobbyStyle.GradeColor(HubGrade.Common), 12f).rectTransform);
            Span(RoundImage("Legend", inner, LobbyStyle.GradeColor(HubGrade.Legend), 12f).rectTransform, uncommon, 1f);
            Span(NewImage("Uncommon", inner, LobbyStyle.GradeColor(HubGrade.Uncommon)).rectTransform, common, uncommon);
            Span(NewImage("Rare", inner, LobbyStyle.GradeColor(HubGrade.Rare)).rectTransform, uncommon, rare);
            foreach (float at in new[] { common, uncommon, rare })
            {
                Image divider = NewImage("Divider", inner, Ink);
                divider.rectTransform.anchorMin = new Vector2(at, 0f);
                divider.rectTransform.anchorMax = new Vector2(at, 1f);
                divider.rectTransform.pivot = new Vector2(0f, 0.5f);
                divider.rectTransform.anchoredPosition = Vector2.zero;
                divider.rectTransform.sizeDelta = new Vector2(4f, 0f);
            }
        }

        // 「1회」 「10회」 — 높이 52 × 2, 글자 옆에 츄르 값 꼬리표 (시안 .gfoot .btn · .pc)
        private static ButtonParts BuildRollButton(string name, Transform parent, Color fill, string label, int cost)
        {
            ButtonParts parts = BuildButton(name, parent, 0f, 104f, fill, 22f, 5f, 6f, label, 38f);
            Flex(parts);
            RectTransform price = NewRect("Price", parts.Body);
            HorizontalLayout(price, new RectOffset(14, 14, 0, 0), 6f, TextAnchor.MiddleCenter, true, true);
            Fixed(price, 0f, 44f);
            RoundBox(price, Ink, 4f, 16f, White);
            Fixed(Icon("Icon", price, Lobby("coin_churu"), 32f, White), 32f, 32f);
            NewText("Text", price, cost.ToString(), 30f, Ink);
            return parts;
        }

        #endregion

        #region 창 — 친구 · 프로필 · 설정 · 퀘스트 · 확률

        // 친구 창 — 폭 640 × 2 · 몸통 높이 384 × 2. 탭 셋 · 추가 탭 머리(내 코드 + 검색) · 목록 · 줄 메뉴 (시안 friendsBody)
        private static LobbyFriendsPopupView BuildFriendsPopup(RectTransform canvas)
        {
            Layer layer = BuildLayer("FriendsPopup", canvas);
            Sheet sheet = BuildSheet("Frame", layer.Center, 1280f, "친구", Common("icon_friends"), Band, true, 768f);

            RectTransform tabRow = NewRect("TabRow", sheet.Body);
            HorizontalLayout(tabRow, new RectOffset(), 0f, TextAnchor.MiddleLeft, true, true);
            RectTransform seg = BuildSeg("Tabs", tabRow);
            string[] tabNames = { "친구", "요청", "추가" };
            var tabs = new LobbyTabView[tabNames.Length];
            for (int i = 0; i < tabs.Length; i++)
                tabs[i] = BuildTab($"Tab{i}", seg, tabNames[i], 0f, 64f, 14f, 4f, 30f, 28f);

            // 추가 탭 머리 — 내 코드(복사) · 검색 칸
            RectTransform addHead = NewRect("AddHead", sheet.Body);
            VerticalLayout(addHead, new RectOffset(), 16f, TextAnchor.UpperLeft);
            RectTransform myCode = NewRect("MyCode", addHead);
            HorizontalLayout(myCode, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            NewText("Label", myCode, "내 코드", 28f, Ink2, TextAlignmentOptions.Left);
            TMP_Text myCodeText = NewText("Code", myCode, "ML01-JY22", 30f, Ink, TextAlignmentOptions.Left, _mono);
            ButtonParts copy = BuildButton("CopyButton", myCode, 0f, 60f, White, 18f, 5f, 4f, "복사", 26f, Common("icon_copy"), 26f, 20f, 8f);
            RectTransform search = NewRect("Search", addHead);
            HorizontalLayout(search, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            (TMP_InputField searchField, _, _) = BuildField("Field", search, 84f, 32f, "닉네임 또는 친구 코드", 28f, 28f);
            FlexWidth(searchField.gameObject);
            ButtonParts searchButton = BuildButton("SearchButton", search, 0f, 84f, White, 22f, 5f, 6f, "검색", 30f);

            (ScrollRect scroll, RectTransform list) = BuildScrollList("List", sheet.Body, 12f);
            TMP_Text incoming = NewText("IncomingHeader", list, "받은 요청", 28f, Ink2, TextAlignmentOptions.Left);
            TMP_Text outgoing = NewText("OutgoingHeader", list, "보낸 요청", 28f, Ink2, TextAlignmentOptions.Left);
            TMP_Text empty = BuildEmptyText(list, "아직 친구가 없어요.");

            // 줄의 본 — 목록 밖에 꺼 두고, 필요한 만큼 복제해 쓴다
            LobbyPersonRowView template = BuildPersonRow(layer.Root);
            template.gameObject.SetActive(false);

            // 줄 메뉴 — 창 위에 뜬다. 바깥을 누르면 닫히게 투명 판을 밑에 깐다
            Image catcherHit = NewImage("MenuCatcher", sheet.Root, Color.clear, null, true);
            Anchor(catcherHit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f));
            catcherHit.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Button catcher = catcherHit.gameObject.AddComponent<Button>();
            catcher.targetGraphic = catcherHit;
            catcher.transition = Selectable.Transition.None;

            // 메뉴 — 폭 148 × 2 부터 · 테 2.5 × 2 · 항목 높이 38 × 2 (시안 .fmenu)
            RectTransform menu = NewRect("Menu", sheet.Root);
            Anchor(menu, new Vector2(0.5f, 0.5f), Vector2.one, Vector2.zero, new Vector2(296f, 240f));
            LayoutElement menuSize = menu.gameObject.AddComponent<LayoutElement>();
            menuSize.ignoreLayout = true;
            menuSize.minWidth = 296f;
            VerticalLayout(menu, new RectOffset(15, 15, 15, 15), 0f, TextAnchor.UpperLeft);
            FitBoth(menu.gameObject);
            RoundBox(menu, Ink, 5f, 24f, White, true);
            TMP_Text menuName = NewText("Name", menu, "닉네임", 26f, Ink2, TextAlignmentOptions.Left);
            menuName.margin = new Vector4(20f, 0f, 20f, 0f);
            Fixed(menuName, 0f, 52f);
            Fixed(NewImage("Line", menu, Line), 0f, 4f);
            Button menuDelete = BuildMenuItem(menu, "DeleteButton", "친구 삭제", Lobby("icon_unfriend"));
            Button menuBlock = BuildMenuItem(menu, "BlockButton", "차단", Lobby("icon_none"));

            LobbyFriendsPopupView view = layer.Root.gameObject.AddComponent<LobbyFriendsPopupView>();
            Wire(view,
                ("content", layer.Content.gameObject), ("frame", sheet.Root), ("closeButton", sheet.Close),
                ("addHead", addHead.gameObject), ("myCodeText", myCodeText), ("copyButton", copy.Button), ("searchField", searchField), ("searchButton", searchButton.Button),
                ("scroll", scroll), ("listContent", list), ("rowTemplate", template), ("incomingHeader", incoming), ("outgoingHeader", outgoing), ("emptyText", empty),
                ("menu", menu), ("menuName", menuName), ("menuDeleteButton", menuDelete), ("menuBlockButton", menuBlock), ("menuCatcher", catcher));
            WireArray(view, "tabs", tabs);

            HideAfterBuild(menu.gameObject);
            HideAfterBuild(catcherHit.gameObject);
            HideAfterBuild(layer.Content.gameObject);
            return view;
        }

        // 메뉴 항목 — 빨간 글자 + 아이콘, 높이 38 × 2
        private static Button BuildMenuItem(Transform parent, string name, string label, Sprite icon)
        {
            Image hit = NewImage(name, parent, Color.clear, null, true);
            HorizontalLayout(hit.rectTransform, new RectOffset(20, 20, 0, 0), 16f, TextAnchor.MiddleLeft, true, true);
            Fixed(hit, 0f, 76f);
            Fixed(Icon("Icon", hit.transform, icon, 34f, Hp), 34f, 34f);
            NewText("Label", hit.transform, label, 30f, Hp, TextAlignmentOptions.Left);
            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            return button;
        }

        // 사람 한 줄 — 높이 54 × 2 부터 · 사진 34 · 이름 17 · 상태 13 · 버튼 높이 34 (× 2, 시안 .prow)
        private static LobbyPersonRowView BuildPersonRow(Transform parent)
        {
            RectTransform root = NewRect("PersonRow", parent);
            HorizontalLayout(root, new RectOffset(24, 16, 14, 14), 24f, TextAnchor.MiddleLeft, true, true);
            root.gameObject.AddComponent<LayoutElement>().minHeight = 108f;
            Image background = RoundImage("Background", root, Soft, 24f);
            Stretch(background.rectTransform);
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            BuildAvatar(root, 68f, 5f);
            RectTransform texts = NewRect("Texts", root);
            FlexWidth(texts.gameObject);
            VerticalLayout(texts, new RectOffset(), 4f, TextAnchor.MiddleLeft);
            TMP_Text nameText = NewText("Name", texts, "닉네임", 34f, Ink, TextAlignmentOptions.Left);
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform statusRow = NewRect("Status", texts);
            HorizontalLayout(statusRow, new RectOffset(), 10f, TextAnchor.MiddleLeft, true, true);
            RectTransform dot = NewRect("Dot", statusRow);
            Fixed(dot, 22f, 22f);
            Image statusDot = RoundBox(dot, Ink, 4f, 11f, LobbyStyle.Ok);
            TMP_Text statusText = NewText("Text", statusRow, "온라인", 26f, Ink2, TextAlignmentOptions.Left);
            TMP_Text codeText = NewText("Code", texts, "KD10-TT44", 26f, Ink2, TextAlignmentOptions.Left, _mono);

            TMP_Text flat = NewText("Flat", root, "친구", 28f, Ink2);
            flat.margin = new Vector4(0f, 0f, 12f, 0f);
            ButtonParts primary = BuildButton("PrimaryButton", root, 0f, 68f, Turn, 18f, 5f, 4f, "수락", 28f, null, 0f, 24f);
            ButtonParts secondary = BuildButton("SecondaryButton", root, 0f, 68f, White, 18f, 5f, 4f, "거절", 28f, null, 0f, 24f);
            Dimmable(secondary);

            // 「⋯」 — 글꼴에 없을 수 있어 점 셋으로 그린다
            ButtonParts menu = BuildButton("MenuButton", root, 80f, 68f, White, 18f, 5f, 4f, null, 0f, null, 0f, 0f, 6f);
            for (int i = 0; i < 3; i++)
                Fixed(RoundImage("Dot", menu.Body, Ink, 4f), 8f, 8f);

            LobbyPersonRowView view = root.gameObject.AddComponent<LobbyPersonRowView>();
            Wire(view,
                ("background", background), ("nameText", nameText), ("statusRow", statusRow.gameObject), ("statusDot", statusDot), ("statusText", statusText),
                ("codeText", codeText), ("primaryButton", primary.Button), ("primaryLabel", primary.Label),
                ("secondaryButton", secondary.Button), ("secondaryLabel", secondary.Label), ("menuButton", menu.Button), ("flatText", flat));
            return view;
        }

        // 프로필 창 — 폭 480 × 2. 사진 72 · 닉네임 24(변경) · 친구 코드(복사) · 전적 넷 · 로그인 한 줄 (시안 profileBody)
        private static LobbyProfilePopupView BuildProfilePopup(RectTransform canvas)
        {
            Layer layer = BuildLayer("ProfilePopup", canvas);
            Sheet sheet = BuildSheet("Frame", layer.Center, 960f, "프로필", Common("icon_user"), Band, true);

            RectTransform top = NewRect("Top", sheet.Body);
            HorizontalLayout(top, new RectOffset(), 28f, TextAnchor.MiddleLeft, true, true);
            BuildAvatar(top, 144f, 6f);
            RectTransform texts = NewRect("Texts", top);
            FlexWidth(texts.gameObject);
            VerticalLayout(texts, new RectOffset(), 12f, TextAnchor.MiddleLeft);
            RectTransform nameRow = NewRect("Name", texts);
            HorizontalLayout(nameRow, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            TMP_Text nickname = NewText("Nickname", nameRow, "닉네임", 48f, Ink, TextAlignmentOptions.Left);
            nickname.overflowMode = TextOverflowModes.Ellipsis;
            ButtonParts change = BuildButton("ChangeButton", nameRow, 0f, 60f, White, 18f, 5f, 4f, "변경", 26f, null, 0f, 20f);
            Rigid(change);
            RectTransform codeRow = NewRect("Code", texts);
            HorizontalLayout(codeRow, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            TMP_Text code = NewText("Text", codeRow, "ML01-JY22", 30f, Ink2, TextAlignmentOptions.Left, _mono);
            ButtonParts copy = BuildButton("CopyButton", codeRow, 0f, 60f, White, 18f, 5f, 4f, "복사", 26f, Common("icon_copy"), 26f, 20f, 8f);

            // 전적 — 2 × 2, 칸 높이 46 × 2 · 사이 8 × 2. 칸 폭 = (창 960 − 테 12 − 여백 56 − 사이 16) / 2
            RectTransform stats = NewRect("Stats", sheet.Body);
            GridLayoutGroup grid = stats.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(438f, 92f);
            grid.spacing = new Vector2(16f, 16f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            TMP_Text wins = BuildStat(stats, "Wins", "승");
            TMP_Text losses = BuildStat(stats, "Losses", "패");
            TMP_Text killWins = BuildStat(stats, "KillWins", "처치 승리");
            TMP_Text chapchuWins = BuildStat(stats, "ChapchuWins", "찹츄 승리");

            TMP_Text login = NewText("Login", sheet.Body, "게스트", 26f, Ink2, TextAlignmentOptions.Left);

            LobbyProfilePopupView view = layer.Root.gameObject.AddComponent<LobbyProfilePopupView>();
            Wire(view,
                ("content", layer.Content.gameObject), ("closeButton", sheet.Close), ("nicknameText", nickname), ("changeButton", change.Button),
                ("codeText", code), ("copyButton", copy.Button), ("winsText", wins), ("lossesText", losses), ("killWinsText", killWins), ("chapchuWinsText", chapchuWins),
                ("loginText", login));
            HideAfterBuild(layer.Content.gameObject);
            return view;
        }

        private static TMP_Text BuildStat(Transform parent, string name, string label)
        {
            RectTransform root = NewRect(name, parent);
            Stretch(RoundImage("Background", root, Soft, 20f).rectTransform);
            Stretch(NewText("Label", root, label, 28f, Ink, TextAlignmentOptions.Left).rectTransform, 28f, 0f, 28f, 0f);
            TMP_Text value = NewText("Value", root, "0", 48f, Ink, TextAlignmentOptions.Right);
            Stretch(value.rectTransform, 28f, 0f, 28f, 0f);
            return value;
        }

        // 설정 창 — 폭 480 × 2 · 두 단(소리 · 알림 · 친구 | 계정 · 정보). 「관리」 를 누르면 같은 자리에 차단 목록 (시안 settingsBody · blockBody)
        private static LobbySettingsPopupView BuildSettingsPopup(RectTransform canvas)
        {
            RectTransform root = NewRect("SettingsPopup", canvas);
            Stretch(root);

            RectTransform settingsContent = BuildContent("Settings", root, out RectTransform settingsCenter);
            Sheet sheet = BuildSheet("Frame", settingsCenter, 960f, "설정", Hud("icon_gear"), Band, true);
            RectTransform columns = NewRect("Columns", sheet.Body);
            HorizontalLayout(columns, new RectOffset(), 36f, TextAnchor.UpperLeft, true, true).childForceExpandWidth = true;
            RectTransform left = BuildColumn("Left", columns);
            RectTransform right = BuildColumn("Right", columns);

            GroupLabel(left, "소리", true);
            LobbySwitchView sfx = BuildSwitch("SfxSwitch", SettingRow(left, "Sfx", "효과음"));
            LobbySwitchView bgm = BuildSwitch("BgmSwitch", SettingRow(left, "Bgm", "배경음"));
            GroupLabel(left, "알림", false);
            LobbySwitchView invite = BuildSwitch("InviteSwitch", SettingRow(left, "Invite", "초대 알림"));
            GroupLabel(left, "친구", false);
            RectTransform blockedRow = SettingRow(left, "Blocked", "차단 목록");
            TMP_Text blockedCount = NewText("Count", blockedRow, "0명", 28f, Ink2, TextAlignmentOptions.Right);
            ButtonParts manage = BuildButton("ManageButton", blockedRow, 0f, 64f, White, 18f, 5f, 4f, "관리", 28f, null, 0f, 24f);

            // 로그아웃 버튼은 두지 않는다 (23-settings 3절 #11)
            GroupLabel(right, "계정", true);
            RectTransform loginRow = SettingRow(right, "Login", "로그인");
            TMP_Text login = NewText("Value", loginRow, "게스트", 28f, Ink2, TextAlignmentOptions.Right);
            RectTransform deleteRow = NewRect("Delete", right);
            HorizontalLayout(deleteRow, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            deleteRow.gameObject.AddComponent<LayoutElement>().minHeight = 76f;
            ButtonParts delete = BuildButton("DeleteButton", deleteRow, 0f, 64f, White, 18f, 5f, 4f, "계정 삭제", 28f, null, 0f, 24f);
            delete.Label.color = Hp;
            GroupLabel(right, "정보", false);
            TMP_Text version = NewText("Version", right, "ver 0.1.0 · Project ChapChu", 26f, Ink2, TextAlignmentOptions.Left, _mono);
            Fixed(version, 0f, 76f);
            version.gameObject.AddComponent<VersionLabel>();
            RectTransform links = NewRect("Links", right);
            HorizontalLayout(links, new RectOffset(), 28f, TextAnchor.MiddleLeft, true, true);
            links.gameObject.AddComponent<LayoutElement>().minHeight = 60f;
            Button privacy = BuildLink(links, "PrivacyButton", "개인정보처리방침");
            Button contact = BuildLink(links, "ContactButton", "문의");

            // 차단 목록 — 「← 설정」 · 줄마다 「차단 해제」
            RectTransform blockContent = BuildContent("BlockList", root, out RectTransform blockCenter);
            Sheet block = BuildSheet("Frame", blockCenter, 960f, "차단 목록", Lobby("icon_none"), Band, true, 768f);
            RectTransform backRow = NewRect("BackRow", block.Body);
            HorizontalLayout(backRow, new RectOffset(), 0f, TextAnchor.MiddleLeft, true, true);
            ButtonParts back = BuildButton("BackButton", backRow, 0f, 64f, White, 18f, 5f, 4f, "설정", 28f, Common("icon_back"), 28f, 24f, 8f);
            (_, RectTransform blockList) = BuildScrollList("List", block.Body, 12f);
            TMP_Text blockEmpty = BuildEmptyText(blockList, "차단한 플레이어가 없어요.");
            LobbyPersonRowView template = BuildPersonRow(root);
            template.gameObject.SetActive(false);

            LobbySettingsPopupView view = root.gameObject.AddComponent<LobbySettingsPopupView>();
            Wire(view,
                ("settingsContent", settingsContent.gameObject), ("closeButton", sheet.Close), ("sfxSwitch", sfx), ("bgmSwitch", bgm), ("inviteSwitch", invite),
                ("blockedCountText", blockedCount), ("manageButton", manage.Button), ("loginText", login), ("deleteButton", delete.Button),
                ("privacyButton", privacy), ("contactButton", contact),
                ("blockContent", blockContent.gameObject), ("blockCloseButton", block.Close), ("backButton", back.Button), ("blockList", blockList),
                ("rowTemplate", template), ("blockEmpty", blockEmpty.gameObject));
            HideAfterBuild(settingsContent.gameObject);
            HideAfterBuild(blockContent.gameObject);
            return view;
        }

        // 설정의 한 단 — 두 단이 같은 폭
        private static RectTransform BuildColumn(string name, Transform parent)
        {
            RectTransform column = NewRect(name, parent);
            FlexWidth(column.gameObject);
            VerticalLayout(column, new RectOffset(), 0f, TextAnchor.UpperLeft);
            return column;
        }

        // 묶음 이름 — 13 × 2 회색, 위로 6 × 2 띄운다 (첫 묶음은 그대로)
        private static void GroupLabel(Transform column, string label, bool isFirst)
        {
            if (!isFirst)
                Fixed(NewRect("Space", column), 0f, 12f);

            Fixed(NewText("Group", column, label, 26f, Ink2, TextAlignmentOptions.Left), 0f, 40f);
        }

        // 설정 한 줄 — 높이 38 × 2 부터 · 이름 15 × 2, 오른쪽에 스위치 · 값 · 버튼 (시안 .srow)
        private static RectTransform SettingRow(Transform column, string name, string label)
        {
            RectTransform row = NewRect(name, column);
            HorizontalLayout(row, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 76f;
            FlexWidth(NewText("Label", row, label, 30f, Ink, TextAlignmentOptions.Left).gameObject);
            return row;
        }

        // 스위치 — 46 × 26 × 2 · 테 2.5 × 2 · 손잡이 17 × 2 (시안 .sw)
        private static LobbySwitchView BuildSwitch(string name, Transform parent)
        {
            RectTransform root = NewRect(name, parent);
            Fixed(root, 92f, 52f);
            Stretch(RoundImage("Border", root, Ink, 26f).rectTransform);
            Image fill = RoundImage("Fill", root, Track, 21f, true);
            Stretch(fill.rectTransform, 5f, 5f, 5f, 5f);
            RectTransform knob = NewRect("Knob", fill.transform);
            Anchor(knob, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(34f, 34f));
            RoundBox(knob, Ink, 4f, 17f, White);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            LobbySwitchView view = root.gameObject.AddComponent<LobbySwitchView>();
            Wire(view, ("fill", fill), ("knob", knob));
            return view;
        }

        // 밑줄 글자 버튼 (시안 .links button)
        private static Button BuildLink(Transform parent, string name, string label)
        {
            TMP_Text text = NewText(name, parent, $"<u>{label}</u>", 28f, Ink, TextAlignmentOptions.Left);
            text.raycastTarget = true;
            Button button = text.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.transition = Selectable.Transition.None;
            return button;
        }

        // 퀘스트 창 — 폭 560 × 2 · 몸통 높이 400 × 2. 일일 / 주간 · 남은 시간 · 줄 넷 + 보너스 줄 · 「모두 받기」 (시안 questBody)
        private static LobbyQuestPopupView BuildQuestPopup(RectTransform canvas)
        {
            Layer layer = BuildLayer("QuestPopup", canvas);
            Sheet sheet = BuildSheet("Frame", layer.Center, 1120f, "퀘스트", Lobby("icon_quest"), Band, true, 800f);

            RectTransform head = NewRect("Head", sheet.Body);
            HorizontalLayout(head, new RectOffset(), 8f, TextAnchor.MiddleLeft, true, true);
            RectTransform seg = BuildSeg("Tabs", head);
            LobbyTabView daily = BuildTab("Daily", seg, "일일", 0f, 64f, 14f, 4f, 30f, 28f);
            LobbyTabView weekly = BuildTab("Weekly", seg, "주간", 0f, 64f, 14f, 4f, 30f, 28f);
            FlexWidth(NewRect("Space", head).gameObject);
            Fixed(Icon("Clock", head, Hud("icon_clock"), 30f, Ink2), 30f, 30f);
            NewText("Label", head, "새로 바뀌기까지", 26f, Ink2);
            TMP_Text timeLeft = NewText("Time", head, "0시간 0분", 28f, Ink);

            (ScrollRect scroll, RectTransform list) = BuildScrollList("List", sheet.Body, 12f);
            var rows = new LobbyQuestRowView[4];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = BuildQuestRow(list, $"Row{i}", false);
            LobbyQuestRowView bonus = BuildQuestRow(list, "BonusRow", true);

            RectTransform foot = NewRect("Foot", sheet.Body);
            HorizontalLayout(foot, new RectOffset(), 16f, TextAnchor.MiddleRight, true, true);
            ButtonParts claimAll = BuildButton("ClaimAllButton", foot, 0f, 76f, Turn, 22f, 5f, 6f, "모두 받기", 30f);
            claimAll.Root.GetComponent<LayoutElement>().minWidth = 240f;
            Dimmable(claimAll);

            LobbyQuestPopupView view = layer.Root.gameObject.AddComponent<LobbyQuestPopupView>();
            Wire(view,
                ("content", layer.Content.gameObject), ("closeButton", sheet.Close), ("dailyTab", daily), ("weeklyTab", weekly), ("timeLeftText", timeLeft),
                ("scroll", scroll), ("bonusRow", bonus), ("claimAllButton", claimAll.Button), ("claimAllLabel", claimAll.Label),
                ("boneIcon", Lobby("coin_bone")), ("churuIcon", Lobby("coin_churu")));
            WireArray(view, "rows", rows);
            HideAfterBuild(layer.Content.gameObject);
            return view;
        }

        // 퀘스트 한 줄 — 높이 60 × 2 부터 · 제목 16 · 진행 13 · 막대 10 · 보상 16 · 「받기」 76 × 36 (× 2, 시안 .qrow). 보너스 줄은 노란 바탕 + 회색 테
        private static LobbyQuestRowView BuildQuestRow(Transform parent, string name, bool isBonus)
        {
            RectTransform root = NewRect(name, parent);
            HorizontalLayout(root, new RectOffset(28, 16, 16, 16), 24f, TextAnchor.MiddleLeft, true, true);
            root.gameObject.AddComponent<LayoutElement>().minHeight = 120f;
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            if (isBonus)
            {
                RoundBox(root, Ink2, 4f, 24f, BonusFill);
            }
            else
            {
                Image background = RoundImage("Background", root, Soft, 24f);
                Stretch(background.rectTransform);
                background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            RectTransform texts = NewRect("Texts", root);
            FlexWidth(texts.gameObject);
            VerticalLayout(texts, new RectOffset(), 10f, TextAnchor.MiddleLeft);
            RectTransform top = NewRect("Top", texts);
            HorizontalLayout(top, new RectOffset(), 16f, TextAnchor.MiddleLeft, true, true);
            TMP_Text title = NewText("Title", top, "판 3번 하기", 32f, Ink, TextAlignmentOptions.Left);
            title.overflowMode = TextOverflowModes.Ellipsis;
            FlexWidth(title.gameObject);
            TMP_Text progress = NewText("Progress", top, "0 / 3", 26f, Ink2, TextAlignmentOptions.Right);
            RectTransform barFill = BuildBar("Bar", texts, 20f, LobbyStyle.Ok, out Image barFillImage);

            RectTransform reward = NewRect("Reward", root);
            HorizontalLayout(reward, new RectOffset(), 8f, TextAnchor.MiddleLeft, true, true);
            reward.gameObject.AddComponent<LayoutElement>().minWidth = 140f;
            Image rewardIcon = Fixed(Icon("Icon", reward, Lobby("coin_bone"), 36f, White), 36f, 36f);
            TMP_Text rewardText = NewText("Amount", reward, "100", 32f, Ink, TextAlignmentOptions.Left);

            // 오른쪽 끝 한 자리 — 「받기」 또는 상태 글
            RectTransform slot = NewRect("Action", root);
            Fixed(slot, 152f, 72f);
            ButtonParts claim = BuildButton("ClaimButton", slot, 152f, 72f, Turn, 20f, 5f, 6f, "받기", 30f, null, 0f, 0f);
            Stretch(claim.Root);
            RectTransform state = NewRect("State", slot);
            Stretch(state);
            HorizontalLayout(state, new RectOffset(), 6f, TextAnchor.MiddleCenter, true, true);
            Image check = Fixed(Icon("Check", state, Common("icon_check"), 30f, Ink2), 30f, 30f);
            TMP_Text stateText = NewText("Text", state, "진행 중", 28f, Ink2);

            LobbyQuestRowView view = root.gameObject.AddComponent<LobbyQuestRowView>();
            Wire(view,
                ("group", group), ("titleText", title), ("progressText", progress), ("barFill", barFill), ("barFillImage", barFillImage),
                ("rewardIcon", rewardIcon), ("rewardText", rewardText), ("claimButton", claim.Button),
                ("state", state.gameObject), ("stateCheck", check.gameObject), ("stateText", stateText));
            return view;
        }

        // 확률 창 — 폭 440 × 2. 등급 · 확률 · 아이템 수 · 아이템 1개 확률, 아래에 보장 · 중복 안내 (시안 ratesBody)
        private static LobbyRatesPopupView BuildRatesPopup(RectTransform canvas)
        {
            Layer layer = BuildLayer("RatesPopup", canvas);
            Sheet sheet = BuildSheet("Frame", layer.Center, 880f, $"확률 보기 · {LobbyHubData.GachaName}", Hud("icon_list"), Band, true);

            RectTransform table = NewRect("Table", sheet.Body);
            VerticalLayout(table, new RectOffset(), 0f, TextAnchor.UpperLeft);
            RectTransform header = RateRow(table, "Header", 52f);
            RateCell(header, "등급", 26f, Ink2, TextAlignmentOptions.Left);
            RateCell(header, "확률", 26f, Ink2, TextAlignmentOptions.Right);
            RateCell(header, "아이템 수", 26f, Ink2, TextAlignmentOptions.Right);
            RateCell(header, "아이템 1개", 26f, Ink2, TextAlignmentOptions.Right);
            Fixed(NewImage("HeaderLine", table, Ink), 0f, 4f);

            HubGrade[] grades = LobbyHubData.GradesHighToLow;
            var rates = new TMP_Text[grades.Length];
            var counts = new TMP_Text[grades.Length];
            var perItems = new TMP_Text[grades.Length];
            for (int i = 0; i < grades.Length; i++)
            {
                if (i > 0)
                    Fixed(NewImage("Line", table, Line), 0f, 3f);

                RectTransform row = RateRow(table, grades[i].ToString(), 68f);
                RectTransform tagCell = NewRect("Grade", row);
                FlexWidth(tagCell.gameObject);
                HorizontalLayout(tagCell, new RectOffset(), 0f, TextAnchor.MiddleLeft, true, true);
                (_, Image tagBorder, TMP_Text tagText) = BuildGradeTag(tagCell, 40f, 14f, 12f, 24f);
                tagBorder.color = LobbyStyle.GradeColor(grades[i]);
                tagText.color = LobbyStyle.GradeColor(grades[i]);
                tagText.text = LobbyHubData.GradeName(grades[i]);
                rates[i] = RateCell(row, $"{LobbyHubData.GachaRate(grades[i])}%", 28f, Ink, TextAlignmentOptions.Right);
                counts[i] = RateCell(row, "0", 28f, Ink, TextAlignmentOptions.Right);
                perItems[i] = RateCell(row, "0%", 28f, Ink, TextAlignmentOptions.Right);
            }

            string refunds = $"일반 {LobbyHubData.DuplicateRefund(HubGrade.Common)} · 고급 {LobbyHubData.DuplicateRefund(HubGrade.Uncommon)}"
                + $" · 희귀 {LobbyHubData.DuplicateRefund(HubGrade.Rare)} · 개판 {LobbyHubData.DuplicateRefund(HubGrade.Legend)}";
            Wrap(NewText("Note", sheet.Body,
                $"{LobbyHubData.GachaPityMax}번째 뽑기까지 개판이 없으면 그 회차는 개판 · 10회 뽑기는 희귀 이상 1개 보장 · 중복은 뼈다귀로 ({refunds})",
                26f, Ink2, TextAlignmentOptions.Left));
            Wrap(NewText("Law", sheet.Body, "확률형 아이템 확률 공개 (게임산업법) — 시안 값", 26f, Ink2, TextAlignmentOptions.Left));

            LobbyRatesPopupView view = layer.Root.gameObject.AddComponent<LobbyRatesPopupView>();
            Wire(view, ("content", layer.Content.gameObject), ("closeButton", sheet.Close));
            WireArray(view, "rateTexts", rates);
            WireArray(view, "countTexts", counts);
            WireArray(view, "perItemTexts", perItems);
            HideAfterBuild(layer.Content.gameObject);
            return view;
        }

        private static RectTransform RateRow(Transform parent, string name, float height)
        {
            RectTransform row = NewRect(name, parent);
            HorizontalLayout(row, new RectOffset(8, 8, 0, 0), 0f, TextAnchor.MiddleLeft, true, true);
            row.gameObject.AddComponent<LayoutElement>().minHeight = height;
            return row;
        }

        // 표의 한 칸 — 네 칸이 같은 폭
        private static TMP_Text RateCell(Transform row, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            TMP_Text cell = NewText("Cell", row, text, size, color, alignment);
            FlexWidth(cell.gameObject);
            return cell;
        }

        #endregion

        #region 위층 — 입력 · 경고 · 요청 중 · 초대 · 뽑기 결과 · 알림

        // 입력 창(방 코드 · 닉네임 · 계정 삭제) · 경고 창 · 요청 중 — 다른 창 위에 뜨고 가림판을 따로 쓴다 (시안 modalHTML · .loadbox)
        private static LobbyModalView BuildModal(RectTransform canvas)
        {
            RectTransform root = NewRect("Modal", canvas);
            Stretch(root);
            Image dimmer = NewImage("Dimmer", root, Dim, null, true);
            Stretch(dimmer.rectTransform);
            RectTransform center = BuildCenter("Center", root);

            // 입력 창 — 폭 380 × 2 · 입력칸 높이 50 × 2 · 버튼 96 × 42 × 2 부터
            Sheet input = BuildSheet("Input", center, 760f, "방 코드 입력", Common("icon_key"), Band, true);
            TMP_Text message = Wrap(NewText("Message", input.Body, "'삭제' 를 입력하면 계정이 지워져요.", 32f, Ink, TextAlignmentOptions.Left));
            (TMP_InputField field, Image fieldBorder, TMP_Text placeholder) = BuildField("Field", input.Body, 100f, 34f, "방 코드", 28f, 28f);
            TMP_Text count = NewText("Count", field.transform, "0 / 16", 24f, Ink2, TextAlignmentOptions.Right, _mono);
            Anchor(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(140f, 40f));
            TMP_Text error = NewText("Error", input.Body, string.Empty, 26f, Hp, TextAlignmentOptions.Left);
            Fixed(error, 0f, 36f);
            RectTransform submitRow = NewRect("Buttons", input.Body);
            HorizontalLayout(submitRow, new RectOffset(), 16f, TextAnchor.MiddleRight, true, true);
            ButtonParts submit = BuildButton("SubmitButton", submitRow, 0f, 84f, Turn, 22f, 5f, 6f, "참가", 34f);
            submit.Root.GetComponent<LayoutElement>().minWidth = 192f;

            // 경고 · 확인 창 — 주황 머리 띠, X 없음 (버튼으로만 닫는다)
            Sheet warn = BuildSheet("Warn", center, 760f, "경고", Common("icon_alert"), Warn, false);
            TMP_Text warnMessage = Wrap(NewText("Message", warn.Body, "잠시 후 다시 시도해 주세요.", 32f, Ink));
            RectTransform warnRow = NewRect("Buttons", warn.Body);
            HorizontalLayout(warnRow, new RectOffset(), 16f, TextAnchor.MiddleCenter, true, true);
            ButtonParts cancel = BuildButton("CancelButton", warnRow, 0f, 84f, White, 22f, 5f, 6f, "취소", 34f);
            cancel.Root.GetComponent<LayoutElement>().minWidth = 192f;
            ButtonParts ok = BuildButton("OkButton", warnRow, 0f, 84f, Turn, 22f, 5f, 6f, "확인", 34f);
            ok.Root.GetComponent<LayoutElement>().minWidth = 192f;

            // 요청 중 — 링 44 × 2 + 한 줄 17 × 2
            RectTransform loading = NewRect("Loading", center);
            Anchor(loading, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 240f));
            VerticalLayout(loading, new RectOffset(60, 60, 40, 36), 24f, TextAnchor.MiddleCenter).childForceExpandWidth = false;
            FitBoth(loading.gameObject);
            RoundBox(loading, Ink, 6f, 32f, White);
            Fixed(BuildRing(loading, 88f), 88f, 88f);
            TMP_Text loadingText = NewText("Text", loading, "방을 찾는 중…", 34f, Ink);

            LobbyModalView view = root.gameObject.AddComponent<LobbyModalView>();
            Wire(view,
                ("dimmer", dimmer.gameObject), ("inputContent", input.Root.gameObject), ("inputBand", input.Band), ("inputIcon", input.Icon), ("inputTitle", input.Title),
                ("inputCloseButton", input.Close), ("inputMessage", message), ("field", field), ("fieldPlaceholder", placeholder), ("fieldBorder", fieldBorder),
                ("countText", count), ("errorText", error), ("submitButton", submit.Button), ("submitFill", submit.Fill), ("submitLabel", submit.Label),
                ("keyIcon", Common("icon_key")), ("userIcon", Common("icon_user")), ("crossIcon", Hud("icon_x")),
                ("warnContent", warn.Root.gameObject), ("warnTitle", warn.Title), ("warnMessage", warnMessage),
                ("warnCancelButton", cancel.Button), ("warnCancelLabel", cancel.Label), ("warnOkButton", ok.Button), ("warnOkLabel", ok.Label),
                ("loadingContent", loading.gameObject), ("loadingText", loadingText));
            HideAfterBuild(dimmer.gameObject);
            HideAfterBuild(input.Root.gameObject);
            HideAfterBuild(warn.Root.gameObject);
            HideAfterBuild(loading.gameObject);
            return view;
        }

        // 초대 배너 — 오른쪽 위 재화 줄 아래(위 100 × 2). 폭 480 × 2 · 노란 머리 띠 32 × 2 · 사진 44 · 버튼 80 × 40 (× 2, 시안 .invite)
        private static LobbyInviteBannerView BuildInvite(RectTransform canvas)
        {
            RectTransform root = NewRect("InviteBanner", canvas);
            Stretch(root);
            root.gameObject.AddComponent<SafeArea>();

            Frame frame = BuildFrame("Frame", root, 6f, 24f, Turn, 64f, 4f);
            Anchor(frame.Root, Vector2.one, Vector2.one, new Vector2(-48f, -200f), new Vector2(960f, 200f));
            frame.Root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HorizontalLayout(frame.Head, new RectOffset(24, 28, 0, 0), 12f, TextAnchor.MiddleLeft, true, true);
            Fixed(Icon("Icon", frame.Head, Common("icon_mail"), 36f, Ink), 36f, 36f);
            TMP_Text title = NewText("Title", frame.Head, "친구 님의 초대", 34f, Ink, TextAlignmentOptions.Left);
            title.overflowMode = TextOverflowModes.Ellipsis;
            FlexWidth(title.gameObject);

            HorizontalLayout(frame.Body, new RectOffset(24, 20, 16, 20), 20f, TextAnchor.MiddleLeft, true, true);
            BuildAvatar(frame.Body, 88f, 5f);
            RectTransform left = NewRect("Left", frame.Body);
            FlexWidth(left.gameObject);
            HorizontalLayout(left, new RectOffset(), 12f, TextAnchor.MiddleLeft, true, true);
            Fixed(Icon("Clock", left, Hud("icon_clock"), 36f, Ink2), 36f, 36f);
            TMP_Text seconds = NewText("Seconds", left, "30초", 32f, Ink2, TextAlignmentOptions.Left);
            ButtonParts accept = BuildButton("AcceptButton", frame.Body, 160f, 80f, Turn, 22f, 5f, 6f, "수락", 30f, null, 0f, 0f);
            ButtonParts decline = BuildButton("DeclineButton", frame.Body, 160f, 80f, White, 22f, 5f, 6f, "거절", 30f, null, 0f, 0f);

            LobbyInviteBannerView view = root.gameObject.AddComponent<LobbyInviteBannerView>();
            Wire(view, ("content", frame.Root.gameObject), ("titleText", title), ("secondsText", seconds),
                ("acceptButton", accept.Button), ("declineButton", decline.Button));
            HideAfterBuild(frame.Root.gameObject);
            return view;
        }

        // 뽑기 결과 — 짙은 가림판 위 폭 620 × 2 판. 카드 5칸(98 × 136, 1회는 120 × 166) · 「모두 열기」 → 「N회 더」 · 「꾸미기에서 보기」 · 「확인」 (× 2, 시안 .gres)
        private static LobbyGachaResultView BuildGachaResult(RectTransform canvas)
        {
            RectTransform root = NewRect("GachaResult", canvas);
            Stretch(root);
            Image content = NewImage("Content", root, ResultDim, null, true);
            Stretch(content.rectTransform);
            RectTransform center = BuildCenter("Center", content.transform);

            Frame frame = BuildFrame("Frame", center, 6f, 24f, Band, 76f, 4f);
            Anchor(frame.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 700f));
            frame.Root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HorizontalLayout(frame.Head, new RectOffset(24, 24, 0, 0), 12f, TextAnchor.MiddleLeft, true, true);
            Fixed(Icon("Icon", frame.Head, Lobby("icon_gift"), 36f, Ink), 36f, 36f);
            TMP_Text title = NewText("Title", frame.Head, $"{LobbyHubData.GachaName} · 10회", 36f, Ink, TextAlignmentOptions.Left);
            VerticalLayout(frame.Body, new RectOffset(28, 28, 28, 28), 24f, TextAnchor.UpperCenter);

            RectTransform gridRect = NewRect("Cards", frame.Body);
            GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(196f, 272f);
            grid.spacing = new Vector2(20f, 20f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            var cards = new LobbyGachaCardView[10];
            for (int i = 0; i < cards.Length; i++)
                cards[i] = BuildGachaCard(gridRect, i);

            RectTransform buttons = NewRect("Buttons", frame.Body);
            HorizontalLayout(buttons, new RectOffset(), 16f, TextAnchor.MiddleCenter, true, true);
            ButtonParts openAll = BuildButton("OpenAllButton", buttons, 0f, 84f, Turn, 22f, 5f, 6f, "모두 열기", 34f);
            openAll.Root.GetComponent<LayoutElement>().minWidth = 240f;
            RectTransform done = NewRect("Done", buttons);
            HorizontalLayout(done, new RectOffset(), 16f, TextAnchor.MiddleCenter, true, true);
            RectTransform refund = NewRect("Refund", done);
            HorizontalLayout(refund, new RectOffset(0, 12, 0, 0), 8f, TextAnchor.MiddleCenter, true, true);
            NewText("Label", refund, "중복 →", 28f, Ink2);
            Fixed(Icon("Icon", refund, Lobby("coin_bone"), 32f, White), 32f, 32f);
            TMP_Text refundText = NewText("Amount", refund, "+30", 28f, Ink2);
            ButtonParts again = BuildButton("AgainButton", done, 0f, 84f, White, 22f, 5f, 6f, "10회 더", 34f);
            again.Root.GetComponent<LayoutElement>().minWidth = 240f;
            ButtonParts wear = BuildButton("WearButton", done, 0f, 84f, White, 22f, 5f, 6f, "꾸미기에서 보기", 34f, Lobby("icon_shirt"), 34f);
            wear.Root.GetComponent<LayoutElement>().minWidth = 240f;
            ButtonParts ok = BuildButton("OkButton", done, 0f, 84f, Turn, 22f, 5f, 6f, "확인", 34f);
            ok.Root.GetComponent<LayoutElement>().minWidth = 240f;

            LobbyGachaResultView view = root.gameObject.AddComponent<LobbyGachaResultView>();
            Wire(view,
                ("content", content.gameObject), ("frame", frame.Root), ("titleText", title), ("grid", grid), ("openAllButton", openAll.Button),
                ("doneRow", done.gameObject), ("refund", refund.gameObject), ("refundText", refundText), ("againButton", again.Button), ("againLabel", again.Label),
                ("wearButton", wear.Button), ("okButton", ok.Button),
                ("enterIcon", Lobby("icon_enter")), ("ghostIcon", Lobby("icon_ghost")), ("cardIcon", Hud("icon_card")), ("sparkIcon", Lobby("icon_spark")));
            WireArray(view, "cards", cards);
            HideAfterBuild(done.gameObject);
            HideAfterBuild(content.gameObject);
            return view;
        }

        // 결과 카드 — 뒷면(잉크 테 3 · 등급 색 안쪽 테 4 · 카드 뒷면 그림) ↔ 앞면(등급 색 테 4 · 그림 · 등급 꼬리표 · 「새 아이템」 / 「중복 +N」) (× 2, 시안 .gcard)
        private static LobbyGachaCardView BuildGachaCard(Transform parent, int index)
        {
            Image hit = NewImage($"Card{index}", parent, Color.clear, null, true);
            RectTransform root = hit.rectTransform;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            RectTransform inner = NewRect("Inner", root);
            Stretch(inner);

            RectTransform back = NewRect("Back", inner);
            Stretch(back);
            Stretch(RoundImage("Border", back, Ink, 22f).rectTransform);
            Image ring = RoundImage("GradeRing", back, Ink2, 16f);
            Stretch(ring.rectTransform, 6f, 6f, 6f, 6f);
            BuildCover("Art", back, _cardBack, 14f);

            RectTransform front = NewRect("Front", inner);
            Stretch(front);
            Image frontBorder = RoundImage("Border", front, Ink2, 22f);
            Stretch(frontBorder.rectTransform);
            Image frontFill = RoundImage("Fill", front, White, 14f);
            Stretch(frontFill.rectTransform, 8f, 8f, 8f, 8f);
            RectTransform column = NewRect("Column", front);
            Stretch(column, 14f, 14f, 14f, 12f);
            VerticalLayout(column, new RectOffset(), 8f, TextAnchor.UpperCenter);

            RectTransform picture = NewRect("Picture", column);
            LayoutElement pictureSize = picture.gameObject.AddComponent<LayoutElement>();
            pictureSize.flexibleHeight = 1f;
            pictureSize.preferredHeight = 0f;
            Image character = NewImage("Character", picture, White, _character);
            character.preserveAspect = true;
            Stretch(character.rectTransform);
            RectTransform placeholder = NewRect("Placeholder", picture);
            Stretch(placeholder);
            StackCentered(placeholder, 8f);
            Image icon = Icon("Icon", placeholder, Lobby("icon_spark"), 68f, Ink);
            TMP_Text caption = NewText("Caption", placeholder, "오라", 24f, Ink);
            caption.rectTransform.sizeDelta = new Vector2(160f, 30f);

            RectTransform tagRow = NewRect("TagRow", column);
            HorizontalLayout(tagRow, new RectOffset(), 0f, TextAnchor.MiddleCenter, true, true);
            (_, Image tagBorder, TMP_Text tagText) = BuildGradeTag(tagRow, 36f, 14f, 12f, 22f);
            RectTransform duplicate = NewRect("Duplicate", column);
            HorizontalLayout(duplicate, new RectOffset(), 6f, TextAnchor.MiddleCenter, true, true);
            Fixed(Icon("Icon", duplicate, Lobby("coin_bone"), 24f, White), 24f, 24f);
            TMP_Text duplicateText = NewText("Text", duplicate, "중복 +30", 22f, Ink2);
            TMP_Text newText = NewText("New", column, "새 아이템", 22f, Hp);

            LobbyGachaCardView view = root.gameObject.AddComponent<LobbyGachaCardView>();
            Wire(view,
                ("inner", inner), ("back", back.gameObject), ("backGradeRing", ring), ("front", front.gameObject), ("frontBorder", frontBorder), ("frontFill", frontFill),
                ("characterImage", character), ("iconImage", icon), ("captionText", caption), ("tagBorder", tagBorder), ("tagText", tagText),
                ("duplicateRow", duplicate.gameObject), ("duplicateText", duplicateText), ("newText", newText.gameObject));
            HideAfterBuild(front.gameObject);
            return view;
        }

        // 맨 위층 — 말풍선 알림(가운데 조금 위, 새 것이 위) · 터짐 · 재화 「+N」 (시안 #toasts · .burst · .gain)
        private static LobbyFxView BuildFx(RectTransform canvas)
        {
            RectTransform root = NewRect("Fx", canvas);
            Stretch(root);

            // 알림 — 화면 위에서 44%, 사이 12 × 2
            RectTransform toastRoot = NewRect("Toasts", root);
            Anchor(toastRoot, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(800f, 100f));
            VerticalLayout(toastRoot, new RectOffset(), 24f, TextAnchor.UpperCenter).childForceExpandWidth = false;
            FitBoth(toastRoot.gameObject);
            var toasts = new RectTransform[3];
            var toastTexts = new TMP_Text[toasts.Length];
            var toastFills = new Image[toasts.Length];
            var toastTails = new Image[toasts.Length];
            var toastButtons = new Button[toasts.Length];
            for (int i = 0; i < toasts.Length; i++)
            {
                // 말풍선 — 여백 8 15 · 테 2.5 · 반경 14 · 글자 15, 꼬리 12 를 45° 돌려 왼쪽 아래에 (× 2, 시안 .toast)
                RectTransform toast = NewRect($"Toast{i}", toastRoot);
                HorizontalLayout(toast, new RectOffset(30, 30, 16, 16), 0f, TextAnchor.MiddleCenter, true, true);
                BuildToastTail("TailBorder", toast, Ink, -4f);
                Image fill = RoundBox(toast, Ink, 5f, 28f, White, true);
                toastTails[i] = BuildToastTail("Tail", toast, White, 3f);
                toastTexts[i] = NewText("Text", toast, "알림", 30f, Ink);
                toastButtons[i] = toast.gameObject.AddComponent<Button>();
                toastButtons[i].targetGraphic = fill;
                toastButtons[i].transition = Selectable.Transition.None;
                toastFills[i] = fill;
                toasts[i] = toast;
                HideAfterBuild(toast.gameObject);
            }

            // 터짐 — 뾰족한 별 판 + 흰 글자(잉크 외곽선, 8° 기울임). 크기 · 글자는 LobbyFxView 가 정한다
            RectTransform burst = NewRect("Burst", root);
            Anchor(burst, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(375f, 375f));
            CanvasGroup burstGroup = burst.gameObject.AddComponent<CanvasGroup>();
            burstGroup.blocksRaycasts = false;
            burstGroup.interactable = false;
            Image burstImage = NewImage("Image", burst, White, Lobby("fx_burst"));
            Stretch(burstImage.rectTransform);
            TMP_Text burstText = NewText("Text", burst, "개판!", 90f, White);
            Stretch(burstText.rectTransform);
            burstText.fontSharedMaterial = _outlineMaterial;
            burstText.rectTransform.localEulerAngles = new Vector3(0f, 0f, 8f);
            HideAfterBuild(burst.gameObject);

            // 재화 +N — 24 × 2, 흰 외곽선
            var gains = new TMP_Text[4];
            for (int i = 0; i < gains.Length; i++)
            {
                gains[i] = NewText($"Gain{i}", root, "+100", 48f, LobbyStyle.Ok, TextAlignmentOptions.Top);
                Anchor(gains[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400f, 64f));
                gains[i].fontSharedMaterial = _gainMaterial;
                HideAfterBuild(gains[i].gameObject);
            }

            LobbyFxView view = root.gameObject.AddComponent<LobbyFxView>();
            Wire(view, ("burst", burst), ("burstGroup", burstGroup), ("burstImage", burstImage), ("burstText", burstText));
            WireArray(view, "toasts", toasts);
            WireArray(view, "toastTexts", toastTexts);
            WireArray(view, "toastFills", toastFills);
            WireArray(view, "toastTails", toastTails);
            WireArray(view, "toastButtons", toastButtons);
            WireArray(view, "gains", gains);
            return view;
        }

        // 말풍선 꼬리 — 24 정사각형(+ 부드러운 가장자리 2)을 45° 돌려 왼쪽 아래 모서리에서 48 안쪽에
        private static Image BuildToastTail(string name, Transform toast, Color color, float y)
        {
            Image tail = SoftRect(name, toast, color);
            Anchor(tail.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(48f, y), new Vector2(28f, 28f));
            tail.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            tail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return tail;
        }

        #endregion

        #region 부품 — 창 · 판

        // 창 한 층 — Root(늘 켜짐, 뷰가 붙는다) → Content(켜고 끈다) → Center(가운데 1920 × 1080)
        private readonly struct Layer
        {
            public readonly RectTransform Root;
            public readonly RectTransform Content;
            public readonly RectTransform Center;

            public Layer(RectTransform root, RectTransform content, RectTransform center)
            {
                Root = root; Content = content; Center = center;
            }
        }

        private static Layer BuildLayer(string name, RectTransform canvas)
        {
            RectTransform root = NewRect(name, canvas);
            Stretch(root);
            RectTransform content = BuildContent("Content", root, out RectTransform center);
            return new Layer(root, content, center);
        }

        private static RectTransform BuildContent(string name, Transform root, out RectTransform center)
        {
            RectTransform content = NewRect(name, root);
            Stretch(content);
            center = BuildCenter("Center", content);
            return content;
        }

        // 창 = 카드 한 장 + 머리 띠(아이콘 · 제목) + X
        private readonly struct Sheet
        {
            public readonly RectTransform Root;
            public readonly RectTransform Body;
            public readonly Image Band;
            public readonly Image Icon;
            public readonly TMP_Text Title;
            public readonly Button Close;

            public Sheet(RectTransform root, RectTransform body, Image band, Image icon, TMP_Text title, Button close)
            {
                Root = root; Body = body; Band = band; Icon = icon; Title = title; Close = close;
            }
        }

        /// <summary>
        /// 가운데 뜨는 창 (시안 .pop .frame) — 테 3 × 2 · 반경 12 × 2 · 머리 띠 34 × 2(아이콘 18 · 제목 17) · 몸통 여백 12 14 14 × 2 · 사이 10 × 2.
        /// 높이는 내용이 정한다. bodyHeight 를 주면 몸통 높이를 고정한다 (목록이 있는 창).
        /// </summary>
        private static Sheet BuildSheet(string name, Transform parent, float width, string title, Sprite icon, Color band, bool hasClose, float bodyHeight = 0f)
        {
            Frame frame = BuildFrame(name, parent, 6f, 24f, band, 68f, 4f);
            Anchor(frame.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 600f));
            frame.Root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HorizontalLayout(frame.Head, new RectOffset(24, hasClose ? 80 : 28, 0, 0), 12f, TextAnchor.MiddleLeft, true, true);
            Image iconImage = Fixed(Icon("Icon", frame.Head, icon, 36f, Ink), 36f, 36f);
            TMP_Text titleText = NewText("Title", frame.Head, title, 34f, Ink, TextAlignmentOptions.Left);
            VerticalLayout(frame.Body, new RectOffset(28, 28, 24, 28), 20f, TextAnchor.UpperCenter);
            if (bodyHeight > 0f)
                Fixed(frame.Body, 0f, bodyHeight);

            Button close = hasClose ? BuildCloseButton(frame.Root).Button : null;
            return new Sheet(frame.Root, frame.Body, frame.BandImage, iconImage, titleText, close);
        }

        // 닫기 X — 지름 60 · 테 5 · 아래 두께 4, 창 오른쪽 위 모서리 밖 (오른쪽 −20 · 위 −24)
        private static ButtonParts BuildCloseButton(RectTransform frame)
        {
            ButtonParts close = BuildButton("CloseButton", frame, 60f, 60f, White, 30f, 5f, 4f, null, 0f, Hud("icon_x"), 20f, 0f);
            close.Root.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor(close.Root, Vector2.one, Vector2.one, new Vector2(20f, 24f), new Vector2(60f, 60f));
            return close;
        }

        // 판 = 카드 한 장 (TitleScreenBuilder.BuildFrame 과 같은 구조): 잉크 테두리 → 흰 몸통 → 머리 띠(위 모서리만 둥글게) → 띠 아래 잉크 선 → Head · Body
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

            RoundBox(root, Ink, border, radius, White);

            RectTransform bandMask = NewRect("BandMask", root);
            TopStrip(bandMask, border, bandHeight);
            bandMask.sizeDelta = new Vector2(-border * 2f, bandHeight);
            bandMask.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image band = BuildRoundedEdge(bandMask, bandColor, radius - border, false);

            Image line = NewImage("BandLine", root, Ink);
            TopStrip(line.rectTransform, border + bandHeight, bandLine);
            line.rectTransform.sizeDelta = new Vector2(-border * 2f, bandLine);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform head = NewRect("Head", root);
            TopStrip(head, border, bandHeight);
            head.sizeDelta = new Vector2(-border * 2f, bandHeight);
            head.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform body = NewRect("Body", root);
            return new Frame(root, band, head, body);
        }

        // 한쪽 모서리만 둥근 띠 — 자리를 마스크로 자르고, 둥근 사각형을 반대쪽으로 반경만큼 넘겨 그린다
        private static Image BuildRoundedEdge(RectTransform area, Color color, float radius, bool roundBottom)
        {
            area.gameObject.AddComponent<RectMask2D>();
            Image image = RoundImage("Round", area, color, radius);
            if (roundBottom)
                Stretch(image.rectTransform, 0f, -radius, 0f, 0f);
            else
                Stretch(image.rectTransform, 0f, 0f, 0f, -radius);
            return image;
        }

        // 칸 안의 띠 — 「장착 중」(아래) · 「보유」(위). 칸 쪽 가장자리에 잉크 선 2 × 2
        private static GameObject BuildStrip(string name, Transform parent, bool atBottom, float height, Color color, string label, float fontSize, float radius)
        {
            RectTransform root = NewRect(name, parent);
            if (atBottom)
                BottomStrip(root, 0f, height);
            else
                TopStrip(root, 0f, height);

            BuildRoundedEdge(root, color, radius, atBottom);
            Image line = NewImage("Line", root, Ink);
            float edge = atBottom ? 1f : 0f;
            line.rectTransform.anchorMin = new Vector2(0f, edge);
            line.rectTransform.anchorMax = new Vector2(1f, edge);
            line.rectTransform.pivot = new Vector2(0.5f, edge);
            line.rectTransform.anchoredPosition = Vector2.zero;
            line.rectTransform.sizeDelta = new Vector2(0f, 4f);
            Stretch(NewText("Label", root, label, fontSize, Ink).rectTransform);
            return root.gameObject;
        }

        // 테 안에 꽉 차게 자른 그림 (CSS object-fit: cover) — 마스크 안에서 비율을 지키며 넘치게
        private static Image BuildCover(string name, Transform parent, Sprite sprite, float inset)
        {
            RectTransform mask = NewRect(name, parent);
            Stretch(mask, inset, inset, inset, inset);
            mask.gameObject.AddComponent<RectMask2D>();
            Image image = NewImage("Image", mask, White, sprite);
            Stretch(image.rectTransform);
            AspectRatioFitter fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            return image;
        }

        // 진행 막대 — 잉크 테 2 × 2 · 옅은 바닥 · 채움(anchorMax.x 로 길이). 채움의 RectTransform 을 돌려준다
        private static RectTransform BuildBar(string name, Transform parent, float height, Color fillColor, out Image fillImage)
        {
            RectTransform root = NewRect(name, parent);
            Fixed(root, 0f, height);
            Stretch(RoundImage("Border", root, Ink, height * 0.5f).rectTransform);
            RectTransform inner = NewRect("Inner", root);
            Stretch(inner, 4f, 4f, 4f, 4f);
            float radius = Mathf.Max((height - 8f) * 0.5f, 1f);
            Stretch(RoundImage("Back", inner, Track, radius).rectTransform);
            fillImage = RoundImage("Fill", inner, fillColor, radius);
            RectTransform fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            return fill;
        }

        // 묶음 탭 상자 — 흰 바탕 · 테 2.5 × 2 · 반경 10 × 2, 안에 탭을 나란히 (시안 .seg)
        private static RectTransform BuildSeg(string name, Transform parent)
        {
            RectTransform seg = NewRect(name, parent);
            HorizontalLayout(seg, new RectOffset(9, 9, 9, 9), 4f, TextAnchor.MiddleCenter, true, true);
            RoundBox(seg, Ink, 5f, 20f, White);
            return seg;
        }

        /// <summary>
        /// 탭 한 칸 — 고르면 노랑 채움 + 잉크 테(Pressed), 아니면 회색 글자. width: 양수 = 고정 · 0 = 글자 폭 + padding · 음수 = 남는 폭을 같게 나눔.
        /// </summary>
        private static LobbyTabView BuildTab(string name, Transform parent, string label, float width, float height, float radius, float ring, float fontSize, float padding)
        {
            Image hit = NewImage(name, parent, Color.clear, null, true);
            RectTransform root = hit.rectTransform;
            root.sizeDelta = new Vector2(Mathf.Max(width, 0f), height);
            LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = height;
            size.minHeight = height;
            if (width > 0f)
            {
                size.preferredWidth = width;
                size.minWidth = width;
            }
            else if (width < 0f)
            {
                size.flexibleWidth = 1f;
                size.preferredWidth = 0f;
                size.minWidth = 0f;
            }

            int pad = Mathf.RoundToInt(padding);
            HorizontalLayout(root, new RectOffset(pad, pad, 0, 0), 0f, TextAnchor.MiddleCenter, true, true);
            RectTransform pressed = NewRect("Pressed", root);
            Stretch(pressed);
            pressed.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RoundBox(pressed, Ink, ring, radius, Turn);
            TMP_Text text = NewText("Label", root, label, fontSize, Ink2);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            LobbyTabView view = root.gameObject.AddComponent<LobbyTabView>();
            Wire(view, ("pressed", pressed.gameObject), ("label", text));
            pressed.gameObject.SetActive(false);
            return view;
        }

        // 등급 꼬리표 — 흰 바탕 · 등급 색 테 2 × 2 · 등급 색 글자 (시안 .gtag). 색 · 글은 뷰가 바꾼다
        private static (GameObject, Image, TMP_Text) BuildGradeTag(Transform parent, float height, float padding, float radius, float fontSize)
        {
            RectTransform root = NewRect("Grade", parent);
            int pad = Mathf.RoundToInt(padding);
            HorizontalLayout(root, new RectOffset(pad, pad, 0, 0), 0f, TextAnchor.MiddleCenter, true, true);
            Fixed(root, 0f, height);
            RoundBox(root, Ink2, 4f, radius, White);
            Image border = root.GetChild(0).GetComponent<Image>();
            TMP_Text text = NewText("Text", root, "일반", fontSize, Ink2);
            return (root.gameObject, border, text);
        }

        // 프로필 사진 — 잉크 원 테 안에 기본 그림 (시안 .av)
        private static void BuildAvatar(Transform parent, float size, float border)
        {
            RectTransform root = NewRect("Avatar", parent);
            Fixed(root, size, size);
            Stretch(RoundImage("Border", root, Ink, size * 0.5f).rectTransform);
            Image face = NewImage("Face", root, White, _avatar);
            Stretch(face.rectTransform, border, border, border, border);
        }

        // 세로 스크롤 목록 — 남는 높이를 다 쓴다. 목록(Content) 에 줄을 넣는다
        private static (ScrollRect, RectTransform) BuildScrollList(string name, Transform parent, float spacing)
        {
            Image hit = NewImage(name, parent, Color.clear, null, true); // 스크롤 드래그를 받는 투명 면
            LayoutElement size = hit.gameObject.AddComponent<LayoutElement>();
            size.flexibleHeight = 1f;
            size.preferredHeight = 0f;
            size.minHeight = 0f;
            RectTransform viewport = NewRect("Viewport", hit.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayout(content, new RectOffset(0, 0, 0, 8), spacing, TextAnchor.UpperCenter);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = hit.gameObject.AddComponent<ScrollRect>();
            SetupScroll(scroll, viewport, content);
            return (scroll, content);
        }

        private static void SetupScroll(ScrollRect scroll, RectTransform viewport, RectTransform content)
        {
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
        }

        // 빈 목록 한 줄 — 15 × 2 회색, 높이 59 × 2 (시안 .empty)
        private static TMP_Text BuildEmptyText(Transform list, string text)
            => Fixed(NewText("Empty", list, text, 30f, Ink2), 0f, 118f);

        // 입력칸 — 테 2.5 × 2 · 반경 11 × 2. 글자 자리는 왼쪽 left · 오른쪽 right 만큼 들여 쓴다 (시안 .field)
        private static (TMP_InputField, Image, TMP_Text) BuildField(string name, Transform parent, float height, float fontSize, string placeholderText, float left, float right)
        {
            RectTransform box = NewRect(name, parent);
            Fixed(box, 0f, height);
            Image border = RoundImage("Border", box, Ink, 22f);
            Stretch(border.rectTransform);
            Image fill = RoundImage("Fill", box, White, 17f, true);
            Stretch(fill.rectTransform, 5f, 5f, 5f, 5f);

            RectTransform area = NewRect("TextArea", box);
            Stretch(area, left, 0f, right, 0f);
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text placeholder = NewText("Placeholder", area, placeholderText, fontSize, Off, TextAlignmentOptions.Left);
            Stretch(placeholder.rectTransform);
            TMP_Text text = NewText("Text", area, string.Empty, fontSize, Ink, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);

            TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = fill;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = _font;
            input.pointSize = fontSize;
            input.caretColor = Ink;
            input.customCaretColor = true;
            input.caretWidth = 3;
            input.selectionColor = new Color(1f, 0.757f, 0.18f, 0.5f);
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.transition = Selectable.Transition.None;
            return (input, border, placeholder);
        }

        #endregion

        #region 부품 — 버튼 · 그림

        private readonly struct ButtonParts
        {
            public readonly RectTransform Root;
            public readonly Button Button;
            public readonly PressableButton Press;
            public readonly RectTransform Body;
            public readonly Image Shadow;
            public readonly Image Border;
            public readonly Image Fill;
            public readonly TMP_Text Label;
            public readonly Image Icon;

            public ButtonParts(RectTransform root, Button button, PressableButton press, RectTransform body, Image shadow, Image border, Image fill, TMP_Text label, Image icon)
            {
                Root = root; Button = button; Press = press; Body = body; Shadow = shadow; Border = border; Fill = fill; Label = label; Icon = icon;
            }
        }

        /// <summary>
        /// 누르는 버튼 (시안 .btn) — 루트(크기 · Button) → Shadow(잉크, 아래 두께) · Body(테두리 · 채움 · 아이콘 · 글자). 누르면 Body 가 (두께 − 2) 만큼 가라앉는다.
        /// width 0 이면 내용(글자 · 아이콘 + padding)이 폭을 정한다 — 글자를 실행 중에 바꿔도 따라간다. isVertical 이면 아이콘 아래에 글자.
        /// Body 에 더 넣는 것(값 꼬리표 · 사진 등)도 같은 줄에 선다.
        /// </summary>
        private static ButtonParts BuildButton(string name, Transform parent, float width, float height, Color fillColor, float radius, float border, float shadow,
            string label, float fontSize, Sprite icon = null, float iconSize = 0f, float padding = 28f, float spacing = 12f, bool isVertical = false)
        {
            // 아래 두께(그림자)는 레이아웃 높이에 넣지 않고 아래로 걸친다 (CSS box-shadow 와 같다)
            RectTransform root = NewRect(name, parent);
            root.sizeDelta = new Vector2(width > 0f ? width : 200f, height);
            LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = height;
            size.minHeight = height;
            size.flexibleWidth = 0f; // 아래 레이아웃 그룹이 「남는 폭을 달라」 고 하지 않게
            size.flexibleHeight = 0f;
            if (width > 0f)
            {
                size.preferredWidth = width;
                size.minWidth = width;
            }

            HorizontalLayoutGroup rootLayout = HorizontalLayout(root, new RectOffset(), 0f, TextAnchor.UpperLeft, true, true);
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = true;

            Image shadowImage = RoundImage("Shadow", root, Ink, radius);
            Stretch(shadowImage.rectTransform, 0f, shadow, 0f, -shadow);
            shadowImage.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // 몸체의 기준점은 왼쪽 위 — 레이아웃이 계산되기 전이든 뒤든 위치가 (0, 0) 이라 PressableButton 이 기억하는 원래 자리가 같다
            RectTransform body = NewRect("Body", root);
            Anchor(body, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, root.sizeDelta);
            int pad = Mathf.RoundToInt(padding);
            if (isVertical)
                VerticalLayout(body, new RectOffset(pad, pad, 0, 0), spacing, TextAnchor.MiddleCenter).childForceExpandWidth = false;
            else
                HorizontalLayout(body, new RectOffset(pad, pad, 0, 0), spacing, TextAnchor.MiddleCenter, true, true);

            Image fill = RoundBox(body, Ink, border, radius, fillColor, true);
            Image borderImage = body.GetChild(0).GetComponent<Image>();
            Image iconImage = icon != null ? Fixed(Icon("Icon", body, icon, iconSize, Ink), iconSize, iconSize) : null;
            TMP_Text text = label != null ? NewText("Label", body, label, fontSize, Ink) : null;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;

            PressableButton press = root.gameObject.AddComponent<PressableButton>();
            Wire(press, ("body", body));
            SetFloat(press, "pressDepth", Mathf.Max(shadow - 2f, 2f));
            return new ButtonParts(root, button, press, body, shadowImage, borderImage, fill, text, iconImage);
        }

        // 나란한 버튼을 똑같은 폭으로 (시안 flex:1 — 내용 폭과 상관없이 남는 폭을 같게 나눈다)
        private static void Flex(ButtonParts parts) => FlexWidth(parts.Root.gameObject);

        // 레이아웃 그룹 밖에 놓인 버튼 — 폭이 내용을 따라가게
        private static void FitWidth(ButtonParts parts)
            => parts.Root.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 옆의 긴 글자가 줄어들 때 이 버튼은 줄지 않게 — 지금 내용 폭을 최소 폭으로
        private static void Rigid(ButtonParts parts)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parts.Root);
            parts.Root.GetComponent<LayoutElement>().minWidth = LayoutUtility.GetPreferredWidth(parts.Root);
        }

        // 꺼진 모양(흰 채움 · 회색 테 · 회색 글자)을 쓰는 버튼 — LobbyStyle.SetEnabled 가 바꾼다
        private static void Dimmable(ButtonParts parts, params Graphic[] extraInk)
        {
            var ink = new List<Object> { parts.Shadow, parts.Border };
            if (parts.Label != null)
                ink.Add(parts.Label);
            if (parts.Icon != null)
                ink.Add(parts.Icon);
            ink.AddRange(extraInk);
            Wire(parts.Press, ("fill", parts.Fill));
            WireArray(parts.Press, "inkParts", ink.ToArray());
        }

        // 로딩 링 — 옅은 링 위에 잉크 1/4 호가 시계 방향으로 돈다 (시안 .ldring). 링 스프라이트는 timer_ring
        private static RectTransform BuildRing(RectTransform parent, float diameter)
        {
            RectTransform root = NewRect("Ring", parent);
            root.sizeDelta = new Vector2(diameter, diameter);
            root.gameObject.AddComponent<Spinner>();
            float image = diameter * RingImageRatio;

            Image track = NewImage("Track", root, Track, _ring);
            Anchor(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(image, image));
            Image arc = NewImage("Arc", root, Ink, _ring);
            Anchor(arc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(image, image));
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillOrigin = (int)Image.Origin360.Top;
            arc.fillClockwise = true;
            arc.fillAmount = 0.25f;
            arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f); // 위 가운데를 중심으로 ±45°
            return root;
        }

        // 가장자리가 부드러운 사각형 — ui_soft_rect(16px · 투명 여백 2 · 테두리 4) 를 Sliced 로. 돌려도 가장자리가 계단이 되지 않는다
        private static Image SoftRect(string name, Transform parent, Color color)
        {
            Image image = NewImage(name, parent, color, Load<Sprite>(SoftRectSpritePath));
            image.type = Image.Type.Sliced;
            return image;
        }

        private static Image Icon(string name, Transform parent, Sprite sprite, float size, Color color)
        {
            Image image = NewImage(name, parent, color, sprite);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.preserveAspect = true;
            return image;
        }

        /// <summary>둥근 모서리 사각형 — ui_round64(160px · 테두리 64) 를 Sliced 로, 모서리 반지름은 pixelsPerUnitMultiplier(64 / r).</summary>
        private static Image RoundImage(string name, Transform parent, Color color, float radius, bool raycastTarget = false)
        {
            Image image = NewImage(name, parent, color, _round, raycastTarget);
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

        private static Sprite SlotIcon(HubSlot slot) => slot switch
        {
            HubSlot.Enter => Lobby("icon_enter"),
            HubSlot.Death => Lobby("icon_ghost"),
            HubSlot.Card  => Hud("icon_card"),
            _             => Lobby("icon_spark"),
        };

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            // 프로젝트는 옛 Input Manager 를 쓴다 (다른 씬과 같은 StandaloneInputModule)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        #endregion

        #region 에셋 — 그림 임포트 · 머티리얼

        // 새로 들여온 PNG 를 스프라이트로 — 이미 맞으면 건드리지 않는다
        private static bool EnsureSprites()
        {
            AssetDatabase.Refresh();
            foreach (string name in NewSprites)
            {
                string path = $"Assets/Sprites/UI/{name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[{nameof(LobbyScreenBuilder)}] 그림이 없다: {path}");
                    return false;
                }

                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && !importer.mipmapEnabled && importer.alphaIsTransparency)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return true;
        }

        // 색 글자 + 흰 외곽선 (재화 「+N」 — 시안 -webkit-text-stroke 5px #fff). 외곽선이 글자 안쪽을 먹지 않게 글자를 부풀린다 (버전 표기 머티리얼과 같은 값)
        private static Material EnsureGainMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GainMaterialPath);
            if (existing != null)
                return existing;

            var material = new Material(_font.material) { name = Path.GetFileNameWithoutExtension(GainMaterialPath) };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, White);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.45f);
            ShaderUtilities.UpdateShaderRatios(material);
            AssetDatabase.CreateAsset(material, GainMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        #endregion

        #region 만들기 도우미

        private static Sprite Common(string name) => Load<Sprite>($"Assets/Sprites/UI/Common/{name}.png");

        private static Sprite Hud(string name) => Load<Sprite>($"Assets/Sprites/UI/Hud/{name}.png");

        private static Sprite Lobby(string name) => Load<Sprite>($"Assets/Sprites/UI/Lobby/{name}.png");

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogError($"[{nameof(LobbyScreenBuilder)}] 에셋이 없다: {path}");
            return asset;
        }

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

        // 레이아웃을 다 계산한 뒤에 끈다 — 꺼진 채로 만들면 크기 · 위치가 계산되지 않은 채 저장된다
        private static void HideAfterBuild(GameObject go) => _hidden.Add(go);

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

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, TMP_FontAsset font = null)
        {
            TextMeshProUGUI tmp = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        // 줄바꿈 글 — 단어 단위 (keep-all 과 같게 한글도 어절로 끊는다)
        private static TMP_Text Wrap(TMP_Text text)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        // 레이아웃 그룹 안에서 크기를 고정한다. width 0 이면 폭은 레이아웃이 정한다
        private static T Fixed<T>(T component, float width, float height) where T : Component
        {
            if (!component.TryGetComponent(out LayoutElement layout))
                layout = component.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
            {
                layout.preferredWidth = width;
                layout.minWidth = width;
            }

            layout.preferredHeight = height;
            layout.minHeight = height;
            ((RectTransform)component.transform).sizeDelta = new Vector2(width, height);
            return component;
        }

        // 가로 묶음 안에서 남는 폭을 다 쓴다 (시안 flex:1)
        private static void FlexWidth(GameObject go)
        {
            if (!go.TryGetComponent(out LayoutElement layout))
                layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.preferredWidth = 0f;
            layout.minWidth = 0f;
        }

        private static void Stretch(RectTransform rect) => Stretch(rect, 0f, 0f, 0f, 0f);

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // 부모 폭을 다 쓰는 가로 띠 — 위에서 top 만큼 내려온 높이 height
        private static void TopStrip(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void BottomStrip(RectTransform rect, float bottom, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottom);
            rect.sizeDelta = new Vector2(0f, height);
        }

        // 부모 폭의 from ~ to 구간을 채운다 (0 ~ 1)
        private static void Span(RectTransform rect, float from, float to)
        {
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
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

        // 세로 묶음 — 자식 폭을 꽉 채우고 높이는 자식이 정한다
        private static VerticalLayoutGroup VerticalLayout(RectTransform rect, RectOffset padding, float spacing, TextAnchor alignment)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        // 크기를 레이아웃이 건드리지 않는 세로 쌓기 — 그림 + 한 줄 설명을 가운데에
        private static void StackCentered(RectTransform rect, float spacing)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void FitBoth(GameObject go)
        {
            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void Wire(Component target, params (string field, Object value)[] references)
            => GameScreenBuilder.Wire(target, references);

        private static void WireArray(Component target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[{nameof(LobbyScreenBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
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
                Debug.LogError($"[{nameof(LobbyScreenBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
