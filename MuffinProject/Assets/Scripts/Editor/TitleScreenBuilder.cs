using System.IO;
using Chapchu.UI;
using Chapchu.UI.Title;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 타이틀 화면을 「통합 화면 UI v2」 타이틀(게시본 1794204029-915a)대로 조립한다 — 메뉴 Chapchu/Title Screen/Build UI.
    /// 열린 TitleScene 에 TitleCanvas 를 만든다. 이미 있으면 덮어쓰지 않는다 (사람이 손본 것을 지우지 않게).
    /// 수치는 시안 무대(960 × 540) × 2 = 캔버스 1920 × 1080. 판 · 버튼 · 그림자는 공용 스프라이트에 색만 (CODE_CONVENTION 10절).
    /// 기준 문서: docs/systems/20-title-ui-full.md · 12-title-ui.md
    /// </summary>
    public static class TitleScreenBuilder
    {
        private const string CanvasName = "TitleCanvas";
        private const string FontPath = "Assets/Fonts/Jua/Jua-Regular.ttf";
        private const string FontAssetPath = "Assets/Fonts/Jua/Jua SDF.asset";
        private const string LogoFontAssetPath = "Assets/Fonts/Jua/Jua Logo SDF.asset";
        private const string OutlineMaterialPath = "Assets/Fonts/Jua/Jua SDF - Outline.mat";
        private const string ArtPath = "Assets/Sprites/UI/Title/title_key_art.png"; // 「타이틀화면 v3.3」 원본 PNG — 시안이 쓰는 4:3 키 아트
        private const string RoundSpritePath = "Assets/Sprites/UI/Common/ui_round64.png";
        private const string RingSpritePath = "Assets/Sprites/UI/Hud/timer_ring.png";
        private const string TermsOfServicePath = "Assets/Texts/terms_of_service.txt";
        private const string PrivacyPolicyPath = "Assets/Texts/privacy_policy.txt";
        private const string LogoCharacters = "찹츄!";

        // 21-font 2절 #6 — 동적 · SDFAA · 아틀라스 2048. 여백은 버전 표기의 외곽선(글자 24 에 3)이 들어가게 10
        private const int FontSamplingPointSize = 48;
        private const int FontAtlasPadding = 10;
        private const int FontAtlasSize = 2048;

        // 링 스프라이트(timer_ring 184) 의 바깥 지름 160 — 이미지 크기 = 지름 × 184 / 160
        private const float RingImageRatio = 184f / 160f;

        // 시안 v2 키트 색
        private static readonly Color Ink = Hex(0x2a2420);
        private static readonly Color Ink2 = Hex(0x6e645c);
        private static readonly Color Band = Hex(0xcfeedd);
        private static readonly Color Turn = Hex(0xffc12e);
        private static readonly Color Hp = Hex(0xec3b46);
        private static readonly Color Off = Hex(0xb8aea4);
        private static readonly Color Soft = Hex(0xf6f0e7);
        private static readonly Color Line = Hex(0xe7ded2);
        private static readonly Color Track = Hex(0xefe8de);
        private static readonly Color White = Color.white;
        private static readonly Color Dim = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.42f);
        private static readonly Color Backdrop = Hex(0xe9a0b8);

        private static TMP_FontAsset _font;
        private static TMP_FontAsset _logoFont;
        private static Material _outlineMaterial;
        private static Sprite _round;
        private static Sprite _ring;

        /// <summary>
        /// 이 에디터를 처음 실행한 것처럼 — 약관 동의 기록 · 저장된 로그인(UGS 세션)을 지운다.
        /// 다음 플레이에서 「화면을 눌러 시작」 → 약관 동의 → 로그인 창부터 다시 본다. 서버의 계정은 지우지 않는다 (이 기기에서 버릴 뿐).
        /// </summary>
        [MenuItem("Chapchu/Title Screen/처음 실행으로 되돌리기")]
        public static void ResetFirstRun()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError($"[{nameof(TitleScreenBuilder)}] 플레이 중에는 되돌리지 않는다 — 멈춘 뒤 실행할 것.");
                return;
            }

            // UGS Authentication 이 기기에 남기는 키 — {클라우드 프로젝트 ID}.{프로필}.unity.services.authentication.{이름}
            string prefix = $"{Application.cloudProjectId}.default.unity.services.authentication.";
            PlayerPrefs.DeleteKey(prefix + "session_token");
            PlayerPrefs.DeleteKey(prefix + "player_id");
            PlayerPrefs.DeleteKey(Core.PlayerPrefsKeys.TermsAgreed);
            PlayerPrefs.DeleteKey(Core.PlayerPrefsKeys.PlayerName);
            PlayerPrefs.Save();
            Debug.Log($"[{nameof(TitleScreenBuilder)}] 처음 실행으로 되돌림 — 약관 동의 · 저장된 로그인 · 닉네임 기록을 지웠다.");
        }

        [MenuItem("Chapchu/Title Screen/Build UI")]
        public static void Build()
        {
            if (GameObject.Find(CanvasName) != null)
            {
                Debug.LogError($"[{nameof(TitleScreenBuilder)}] 씬에 {CanvasName} 가 이미 있다. 덮어쓰지 않는다 — 지우고 다시 실행할 것.");
                return;
            }

            _font = EnsureFontAsset();
            ApplyBodyWeight(_font);
            _logoFont = EnsureLogoFontAsset();
            _outlineMaterial = EnsureOutlineMaterial();
            _round = Load<Sprite>(RoundSpritePath);
            _ring = Load<Sprite>(RingSpritePath);
            if (_font == null || _logoFont == null || _round == null || _ring == null)
                return;

            RectTransform canvas = BuildCanvas(out CanvasGroup screen);

            Image backdrop = NewImage("Backdrop", canvas, Backdrop);
            Stretch(backdrop.rectTransform);
            BuildArt(canvas);

            // 「화면을 눌러 시작」 — 화면 어디든. 로고 · 글자는 raycastTarget 이 꺼져 있어 이 면이 받는다
            Image tapCatcher = NewImage("TapCatcher", canvas, Color.clear, null, true);
            Stretch(tapCatcher.rectTransform);
            Button tapButton = tapCatcher.gameObject.AddComponent<Button>();
            tapButton.transition = Selectable.Transition.None;

            RectTransform center = BuildCenter("Center", canvas);
            BuildLogo(center);
            TitleBottomView bottom = BuildBottom(center, tapButton);

            RectTransform edge = NewRect("Edge", canvas);
            Stretch(edge);
            edge.gameObject.AddComponent<SafeArea>();
            BuildVersion(edge);

            LoginSheetView sheet = BuildSheet(canvas);
            TitlePopupView popup = BuildPopup(canvas);

            EnsureEventSystem();

            TitlePresenter presenter = canvas.gameObject.AddComponent<TitlePresenter>();
            Wire(presenter, ("bottom", bottom), ("sheet", sheet), ("popup", popup), ("screen", screen));

            // 레이아웃을 지금 계산해 둔다 — 안 그러면 저장 뒤 첫 갱신에서 앵커 · 크기가 바뀌어 씬이 다시 더러워진다
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvas);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = canvas.gameObject;
            Debug.Log($"[{nameof(TitleScreenBuilder)}] {CanvasName} 조립 완료 — 씬을 저장할 것.");
        }

        #region 캔버스 · 배경

        private static RectTransform BuildCanvas(out CanvasGroup group)
        {
            var go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // 로비로 넘어가는 0.3초 동안 로비 화면 위에 남는다

            // 15-screen.md 3절 #3
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            group = go.GetComponent<CanvasGroup>();
            return (RectTransform)go.transform;
        }

        // 키 아트 — 화면을 덮고(cover) 넘치는 부분은 잘린다. 어둡게 막 없음 (시안 「타이틀화면 v3.3」)
        private static void BuildArt(RectTransform canvas)
        {
            Image art = NewImage("Art", canvas, White, Load<Sprite>(ArtPath));
            Anchor(art.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
            AspectRatioFitter fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (art.sprite != null)
                fitter.aspectRatio = art.sprite.rect.width / art.sprite.rect.height;
        }

        // 가운데 1920 × 1080 묶음 — 넓거나 높은 화면에서도 가운데 고정 (12 11절)
        private static RectTransform BuildCenter(string name, Transform parent)
        {
            RectTransform center = NewRect(name, parent);
            Anchor(center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
            return center;
        }

        #endregion

        #region 로고 · 바닥 · 버전

        // 「찹츄!」 — 글자 260 · 굵은 잉크 외곽선 + 아래 그림자. 찹 노랑 −8° · 츄 흰색 6° · ! 분홍 (아래 축). 부제 = 뼈다귀 리본 −2° (시안 .logo, 위 214 × 2)
        private static void BuildLogo(RectTransform center)
        {
            const float top = 428f;
            const float glyph = 260f;
            const float ribbonGap = 24f;

            RectTransform logo = NewRect("Logo", center);
            TopCenter(logo, top, 1200f, glyph + ribbonGap + 72f);

            var letters = new[] { ("Chap", "찹", Hex(0xF2C14E), 8f), ("Chu", "츄", White, -6f), ("Bang", "!", Hex(0xE68FB0), -12f) };
            float gap = glyph * 0.02f;
            float[] widths = new float[letters.Length];
            float total = gap * (letters.Length - 1);
            TMP_Text[] texts = new TMP_Text[letters.Length];
            for (int i = 0; i < letters.Length; i++)
            {
                (string name, string text, Color color, float angle) = letters[i];
                texts[i] = NewText(name, logo, text, glyph, color, TextAlignmentOptions.Bottom, _logoFont);
                widths[i] = texts[i].GetPreferredValues(text).x;
                total += widths[i];
            }

            float x = -total / 2f;
            RectTransform[] rects = new RectTransform[letters.Length];
            for (int i = 0; i < letters.Length; i++)
            {
                rects[i] = texts[i].rectTransform;
                bool bottomPivot = i == letters.Length - 1; // ! 는 아래를 축으로 흔든다
                rects[i].anchorMin = rects[i].anchorMax = new Vector2(0.5f, 1f);
                rects[i].pivot = bottomPivot ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
                rects[i].sizeDelta = new Vector2(widths[i], glyph);
                rects[i].anchoredPosition = new Vector2(x + widths[i] / 2f, bottomPivot ? -glyph : -glyph / 2f);
                rects[i].localEulerAngles = new Vector3(0f, 0f, letters[i].Item4);
                x += widths[i] + gap;
            }

            TitleLogoView view = logo.gameObject.AddComponent<TitleLogoView>();
            Wire(view, ("chap", rects[0]), ("chu", rects[1]), ("bang", rects[2]));

            BuildRibbon(logo, glyph + ribbonGap);
        }

        // 뼈다귀 리본 — 흰 띠(위아래 잉크 6) 72 + 네 모서리 동그라미 58. 점 두 개씩 양쪽 (시안 .rib · .sub)
        private static void BuildRibbon(RectTransform logo, float top)
        {
            RectTransform rib = NewRect("Ribbon", logo);
            rib.anchorMin = rib.anchorMax = new Vector2(0.5f, 1f);
            rib.pivot = new Vector2(0.5f, 0.5f);
            rib.anchoredPosition = new Vector2(0f, -top - 36f);
            rib.localEulerAngles = new Vector3(0f, 0f, 2f);
            HorizontalLayout(rib, new RectOffset(32, 32, 0, 0), 0f, TextAnchor.MiddleCenter, true, true);
            FitBoth(rib.gameObject);

            const float knob = 58f;
            const float knobOut = 72f * 0.3f;
            (string name, Vector2 anchor, float y)[] knobs =
            {
                ("KnobTopLeft", new Vector2(0f, 1f), knobOut), ("KnobBottomLeft", new Vector2(0f, 0f), -knobOut),
                ("KnobTopRight", new Vector2(1f, 1f), knobOut), ("KnobBottomRight", new Vector2(1f, 0f), -knobOut),
            };
            foreach ((string name, Vector2 anchor, float y) in knobs)
            {
                RectTransform k = NewRect(name, rib);
                k.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Anchor(k, anchor, anchor, new Vector2(0f, y), new Vector2(knob, knob));
                RoundBox(k, Ink, 6f, knob / 2f, White);
            }

            RectTransform sub = NewRect("Sub", rib);
            LayoutElement subSize = sub.gameObject.AddComponent<LayoutElement>();
            subSize.preferredHeight = 72f;
            subSize.minHeight = 72f; // 안 그러면 높이를 맡기지 않은 글자 Rect 의 기본 높이 100 이 띠 높이가 된다
            HorizontalLayout(sub, new RectOffset(30, 30, 0, 0), 24f, TextAnchor.MiddleCenter, true, true);
            // 기울어진 띠 — 스프라이트 없는 Image 는 가장자리가 계단이 된다. 가장자리가 투명한 ui_soft_rect 를 써서 텍스처 필터링으로 부드럽게 (투명 여백 2 만큼 밖으로 키운다)
            Image subInk = SoftRect("Ink", sub, Ink);
            Stretch(subInk.rectTransform, 0f, -SoftRectPadding, 0f, -SoftRectPadding); // 좌우는 흰 띠보다 안쪽 — 흰 띠의 반투명 가장자리 밑으로 잉크가 비치지 않게
            subInk.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image subFill = SoftRect("Fill", sub, White);
            Stretch(subFill.rectTransform, -SoftRectPadding, 6f - SoftRectPadding, -SoftRectPadding, 6f - SoftRectPadding);
            subFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            BuildDots(sub, Hex(0x2F8CF0), Hex(0xA8692E));
            NewText("Text", sub, "치와와개판오분전언찹츄찹찹츄", 34f, Ink);
            BuildDots(sub, Hex(0x8E5CD9), Hex(0x1FA37A));
        }

        private static void BuildDots(Transform parent, Color first, Color second)
        {
            RectTransform dots = NewRect("Dots", parent);
            HorizontalLayout(dots, new RectOffset(), 10f, TextAnchor.MiddleCenter, false, false);
            LayoutElement size = dots.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 42f;
            size.preferredHeight = 16f;
            foreach (Color color in new[] { first, second })
            {
                Image dot = RoundImage("Dot", dots, color, 8f);
                dot.rectTransform.sizeDelta = new Vector2(16f, 16f);
            }
        }

        // 바닥 자리 — 로고 아래, 가운데 묶음 바닥에서 130 (시안 bottom 65)
        private static TitleBottomView BuildBottom(RectTransform center, Button tapCatcher)
        {
            const float bottom = 130f;
            RectTransform root = NewRect("Bottom", center);
            Stretch(root);

            // 「화면을 눌러 시작」 — 흰 알약 98 · 테 6 · 아래 두께 10 · 글자 40, 깜빡임 (시안 .tap)
            RectTransform tap = NewRect("TapPill", root);
            BottomCenter(tap, bottom, 600f, 98f);
            CanvasGroup tapGroup = tap.gameObject.AddComponent<CanvasGroup>();
            tapGroup.blocksRaycasts = false;
            tapGroup.interactable = false;
            HorizontalLayout(tap, new RectOffset(76, 76, 0, 0), 20f, TextAnchor.MiddleCenter, true, false);
            ContentSizeFitter tapFit = tap.gameObject.AddComponent<ContentSizeFitter>();
            tapFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image tapShadow = RoundImage("Shadow", tap, Ink, 49f);
            Stretch(tapShadow.rectTransform, 0f, 10f, 0f, -10f);
            tapShadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RoundBox(tap, Ink, 6f, 49f, White);
            Fixed(Icon("Icon", tap, Load<Sprite>(CommonIcon("icon_tap")), 36f, Ink), 36f, 36f);
            NewText("Label", tap, "화면을 눌러 시작", 40f, Ink);

            // 자동 로그인 알약 — 96 · 테 5 · 링 56 + 글자 38 (시안 .chip.load)
            RectTransform chip = NewRect("LoadingChip", root);
            BottomCenter(chip, bottom, 400f, 96f);
            HorizontalLayout(chip, new RectOffset(28, 40, 0, 0), 24f, TextAnchor.MiddleCenter, true, false);
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            RoundBox(chip, Ink, 5f, 48f, new Color(1f, 1f, 1f, 0.95f));
            Fixed(BuildRing(chip, 56f), 56f, 56f);
            TMP_Text loadingText = NewText("Label", chip, "자동 로그인 중…", 38f, Ink);

            // 자동 로그인 실패 — 흰 상자 1280: 사유(제목 · 설명) + 다시 시도 · 다른 방법으로, 오류 코드는 오른쪽 아래 (시안 .failbox)
            RectTransform fail = NewRect("FailBox", root);
            BottomCenter(fail, bottom, 1280f, 140f);
            HorizontalLayout(fail, new RectOffset(32, 24, 20, 32), 28f, TextAnchor.MiddleLeft, true, false);
            fail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RoundBox(fail, Ink, 6f, 28f, White);
            RectTransform tx = NewRect("Text", fail);
            VerticalLayout(tx, new RectOffset(), 6f, TextAnchor.MiddleLeft);
            tx.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text failTitle = NewText("Title", tx, "로그인할 수 없음", 38f, Hp, TextAlignmentOptions.Left);
            TMP_Text failDesc = Wrap(NewText("Description", tx, "연결을 확인하고 다시 시도해 주세요.", 28f, Ink, TextAlignmentOptions.Left));
            RectTransform acts = NewRect("Buttons", fail);
            HorizontalLayout(acts, new RectOffset(), 16f, TextAnchor.MiddleCenter, true, true);
            ButtonParts retry = BuildButton("RetryButton", acts, 0f, 80f, Turn, 22f, 5f, 6f, "다시 시도", 32f);
            ButtonParts other = BuildButton("OtherButton", acts, 0f, 80f, White, 22f, 5f, 6f, "다른 방법으로", 32f);
            TMP_Text failCode = NewText("Code", fail, "TransportError", 22f, new Color(Ink2.r, Ink2.g, Ink2.b, 0.75f), TextAlignmentOptions.BottomRight);
            failCode.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Anchor(failCode.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 6f), new Vector2(600f, 28f));

            TitleBottomView view = root.gameObject.AddComponent<TitleBottomView>();
            Wire(view,
                ("tapCatcher", tapCatcher), ("tapPill", tapGroup), ("loadingChip", chip.gameObject), ("loadingText", loadingText),
                ("failBox", fail.gameObject), ("failTitle", failTitle), ("failDescription", failDesc), ("failCode", failCode),
                ("retryButton", retry.Button), ("otherButton", other.Button));

            chip.gameObject.SetActive(false);
            fail.gameObject.SetActive(false);
            return view;
        }

        // 좌하단 버전 표기 — 글자 24 흰색 + 잉크 외곽선 3 (시안 .ver, 왼쪽 16 · 아래 12 × 2)
        private static void BuildVersion(RectTransform edge)
        {
            TMP_Text text = NewText("Version", edge, "ver 0.1.0 · Project ChapChu", 24f, White, TextAlignmentOptions.BottomLeft);
            text.fontSharedMaterial = _outlineMaterial;
            Anchor(text.rectTransform, Vector2.zero, Vector2.zero, new Vector2(32f, 24f), new Vector2(800f, 32f));
            text.gameObject.AddComponent<VersionLabel>();
        }

        #endregion

        #region 로그인 창

        // 가림판 + 카드 한 장 (시안 .lsheet — 폭 420 · 테 3 · 머리 띠 36 × 2). 단계별 몸통 5개
        private static LoginSheetView BuildSheet(RectTransform canvas)
        {
            RectTransform root = NewRect("Sheet", canvas);
            Stretch(root);
            RectTransform content = NewRect("Content", root);
            Stretch(content);
            Image dimmer = NewImage("Dimmer", content, Dim, null, true); // 바탕 탭을 먹는다 — 눌러도 닫히지 않는다 (22 1절 #4)
            Stretch(dimmer.rectTransform);
            RectTransform center = BuildCenter("Center", content);

            Frame frame = BuildFrame("Frame", center, 6f, 24f, Band, 72f, 4f);
            TopCenter(frame.Root, 300f, 840f, 400f);
            frame.Root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HorizontalLayout(frame.Head, new RectOffset(28, 88, 0, 0), 12f, TextAnchor.MiddleLeft, true, false);
            Image bandIcon = Fixed(Icon("Icon", frame.Head, Load<Sprite>(CommonIcon("icon_key")), 36f, Ink), 36f, 36f);
            TMP_Text bandTitle = NewText("Title", frame.Head, "시작하기", 36f, Ink, TextAlignmentOptions.Left);
            ButtonParts close = BuildCloseButton(frame.Root);
            VerticalLayout(frame.Body, new RectOffset(32, 32, 28, 28), 20f, TextAnchor.UpperCenter);

            TermsAgreeView agree = BuildAgreeBody(frame.Body);
            (RectTransform login, Button guest, Button google, Button apple) = BuildLoginBody(frame.Body);
            (RectTransform loading, TMP_Text loadingText) = BuildSheetLoading(frame.Body);
            (RectTransform nickname, NicknameFieldView field, Button dice, Button back, Button start) = BuildNicknameBody(frame.Body);
            SheetFail fail = BuildSheetFail(frame.Body);

            LoginSheetView view = root.gameObject.AddComponent<LoginSheetView>();
            Wire(view,
                ("content", content.gameObject), ("frame", frame.Root), ("band", frame.BandImage), ("bandIcon", bandIcon), ("bandTitle", bandTitle), ("closeButton", close.Button),
                ("agreeBody", agree), ("loginBody", login.gameObject), ("loadingBody", loading.gameObject), ("nicknameBody", nickname.gameObject), ("failBody", fail.Root.gameObject),
                ("guestButton", guest), ("googleButton", google), ("appleButton", apple),
                ("loadingText", loadingText), ("failTitle", fail.Title), ("failDescription", fail.Description), ("failCode", fail.Code),
                ("retryButton", fail.Retry), ("otherButton", fail.Other),
                ("nicknameField", field), ("diceButton", dice), ("backButton", back), ("startButton", start),
                ("keyIcon", Load<Sprite>(CommonIcon("icon_key"))), ("listIcon", Load<Sprite>(HudIcon("icon_list"))),
                ("userIcon", Load<Sprite>(CommonIcon("icon_user"))), ("alertIcon", Load<Sprite>(CommonIcon("icon_alert"))));

            agree.gameObject.SetActive(false);
            loading.gameObject.SetActive(false);
            nickname.gameObject.SetActive(false);
            fail.Root.gameObject.SetActive(false);
            content.gameObject.SetActive(false);
            return view;
        }

        // 약관 동의 — 「모두 동의하기」 · 두 약관 칸 나란히 · 안내 줄 · 「동의하고 계속」 (20 7-11, 시안 .agreesheet)
        private static TermsAgreeView BuildAgreeBody(RectTransform body)
        {
            RectTransform root = NewRect("Agree", body);
            VerticalLayout(root, new RectOffset(), 18f, TextAnchor.UpperCenter);

            // 「모두 동의하기」 — 줄 92 · 테 5 · 상자 52
            RectTransform allRow = NewRect("AllAgree", root);
            allRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 92f;
            HorizontalLayout(allRow, new RectOffset(28, 28, 0, 0), 20f, TextAnchor.MiddleLeft, true, false);
            Image allHit = RoundBox(allRow, Ink, 5f, 22f, White, true);
            Button allButton = allRow.gameObject.AddComponent<Button>();
            allButton.targetGraphic = allHit;
            allButton.transition = Selectable.Transition.None;
            (Image allBox, GameObject allCheck) = BuildCheckBox(allRow, 52f, 36f);
            NewText("Label", allRow, "모두 동의하기", 36f, Ink, TextAlignmentOptions.Left);

            RectTransform cols = NewRect("Columns", root);
            HorizontalLayoutGroup colsLayout = HorizontalLayout(cols, new RectOffset(), 20f, TextAnchor.UpperCenter, true, true);
            colsLayout.childForceExpandWidth = true;
            colsLayout.childForceExpandHeight = true;

            string[] names = { "이용약관", "개인정보 처리방침" };
            string[] checkTexts = { "만 14세 이상이며 이용약관에 동의합니다", "개인정보 처리방침에 동의합니다" };
            string[] docs = { TermsOfServicePath, PrivacyPolicyPath };
            var termButtons = new Button[2];
            var termBoxes = new Image[2];
            var termChecks = new GameObject[2];
            var columns = new RectTransform[2];
            var columnEdges = new Image[2];
            var columnFills = new Image[2];
            var scrolls = new ScrollRect[2];
            var texts = new TMP_Text[2];
            var documents = new TextAsset[2];
            for (int i = 0; i < 2; i++)
            {
                // 칸 = 레이아웃 자리(Slot) 안의 판(Column) — 흔들 때 레이아웃이 위치를 되돌리지 않게
                RectTransform slot = NewRect("Slot" + i, cols);
                LayoutElement slotSize = slot.gameObject.AddComponent<LayoutElement>(); // 두 칸 같은 폭 (시안 grid 1fr 1fr)
                slotSize.flexibleWidth = 1f;
                slotSize.preferredWidth = 0f;
                slotSize.minWidth = 0f;
                VerticalLayout(slot, new RectOffset(), 0f, TextAnchor.UpperCenter).childForceExpandHeight = true;

                // 판 = 테두리(평소 칸 색 · 빠지면 빨강, 안쪽 테 5) + 채움 (시안 .tcol.need — inset 2.5 × 2)
                RectTransform column = NewRect("Column", slot);
                VerticalLayout(column, new RectOffset(16, 16, 16, 16), 12f, TextAnchor.UpperLeft);
                columns[i] = column;
                columnFills[i] = RoundBox(column, Soft, 5f, 20f, Soft);
                columnEdges[i] = column.GetChild(0).GetComponent<Image>();

                TMP_Text name = NewText("Name", column, $"{names[i]}<size=24><color=#6e645c>  (필수)</color></size>", 30f, Ink, TextAlignmentOptions.Left);
                name.richText = true;

                (scrolls[i], texts[i]) = BuildTermsBox(column);
                documents[i] = Load<TextAsset>(docs[i]);

                RectTransform checkRow = NewRect("Check", column);
                HorizontalLayout(checkRow, new RectOffset(0, 0, 0, 0), 20f, TextAnchor.UpperLeft, true, true);
                Image checkHit = NewImage("Hit", checkRow, Color.clear, null, true);
                Stretch(checkHit.rectTransform);
                checkHit.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                termButtons[i] = checkRow.gameObject.AddComponent<Button>();
                termButtons[i].targetGraphic = checkHit;
                termButtons[i].transition = Selectable.Transition.None;
                (termBoxes[i], termChecks[i]) = BuildCheckBox(checkRow, 44f, 30f);
                TMP_Text checkText = Wrap(NewText("Label", checkRow, checkTexts[i], 28f, Ink, TextAlignmentOptions.TopLeft));
                checkText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }

            TMP_Text need = NewText("NeedMessage", root, "두 약관에 모두 동의해야 시작할 수 있어요.", 26f, Hp);
            need.gameObject.SetActive(false);

            ButtonParts confirm = BuildButton("ConfirmButton", root, 0f, 108f, Turn, 28f, 6f, 8f, "동의하고 계속", 42f);
            Wire(confirm.Press, ("fill", confirm.Fill));
            WireArray(confirm.Press, "inkParts", new Object[] { confirm.Border, confirm.Shadow, confirm.Label });

            TermsAgreeView view = root.gameObject.AddComponent<TermsAgreeView>();
            Wire(view, ("allButton", allButton), ("allBox", allBox), ("allCheck", allCheck), ("needMessage", need.gameObject),
                ("confirmButton", confirm.Button), ("confirmLook", confirm.Press));
            WireArray(view, "termButtons", termButtons);
            WireArray(view, "termBoxes", termBoxes);
            WireArray(view, "termChecks", termChecks);
            WireArray(view, "columns", columns);
            WireArray(view, "columnEdges", columnEdges);
            WireArray(view, "columnFills", columnFills);
            WireArray(view, "scrolls", scrolls);
            WireArray(view, "termTexts", texts);
            WireArray(view, "termDocuments", documents);
            return view;
        }

        // 약관 본문 상자 — 높이 280 · 테 5 · 반경 20 · 글자 27, 세로 스크롤 (시안 .terms)
        private static (ScrollRect, TMP_Text) BuildTermsBox(RectTransform column)
        {
            RectTransform box = NewRect("Terms", column);
            box.gameObject.AddComponent<LayoutElement>().preferredHeight = 280f;
            RoundBox(box, Ink, 5f, 20f, White);

            RectTransform viewport = NewRect("Viewport", box);
            Stretch(viewport, 28f, 20f, 20f, 20f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>(); // 스크롤 드래그를 받는 투명 면
            hit.color = Color.clear;

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VerticalLayout(content, new RectOffset(), 0f, TextAnchor.UpperLeft);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text text = Wrap(NewText("Text", content, "", 27f, Ink, TextAlignmentOptions.TopLeft));
            text.richText = true;
            text.lineSpacing = 18f;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return (scroll, text);
        }

        // 체크 상자 — 테 5 · 반경 12, 켜지면 초록 + 흰 체크 (시안 .chk .box)
        private static (Image, GameObject) BuildCheckBox(RectTransform row, float size, float checkSize)
        {
            RectTransform box = NewRect("Box", row);
            Fixed(box, size, size);
            Image fill = RoundBox(box, Ink, 5f, 12f, White);
            Image check = Icon("Check", box, Load<Sprite>(CommonIcon("icon_check")), checkSize, White);
            check.rectTransform.anchoredPosition = Vector2.zero;
            check.gameObject.SetActive(false);
            return (fill, check.gameObject);
        }

        // 로그인 고르기 — 게스트(주) · 안내 · 「계정으로 이어 하기」 · Google · Apple (시안 step login)
        private static (RectTransform, Button, Button, Button) BuildLoginBody(RectTransform body)
        {
            RectTransform root = NewRect("Login", body);
            VerticalLayout(root, new RectOffset(), 20f, TextAnchor.UpperCenter);

            ButtonParts guest = BuildButton("GuestButton", root, 0f, 108f, Turn, 28f, 6f, 8f, "게스트로 시작", 42f);
            Wrap(NewText("Note", root, "기록은 이 기기에 묶여요. 나중에 설정에서 계정을 연결할 수 있어요.", 26f, Ink2));

            RectTransform or = NewRect("Or", root);
            HorizontalLayout(or, new RectOffset(), 16f, TextAnchor.MiddleCenter, true, false);
            or.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            Image left = NewImage("Line", or, Line);
            Fixed(left, 0f, 4f).GetComponent<LayoutElement>().flexibleWidth = 1f;
            NewText("Label", or, "계정으로 이어 하기", 26f, Ink2);
            Image right = NewImage("Line", or, Line);
            Fixed(right, 0f, 4f).GetComponent<LayoutElement>().flexibleWidth = 1f;

            RectTransform row = NewRect("Accounts", root);
            HorizontalLayout(row, new RectOffset(), 16f, TextAnchor.MiddleCenter, true, true);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            ButtonParts google = BuildButton("GoogleButton", row, 0f, 92f, White, 26f, 6f, 8f, "Google", 32f);
            BuildGoogleMark(google.Content);
            Flex(google);
            ButtonParts apple = BuildButton("AppleButton", row, 0f, 92f, White, 26f, 6f, 8f, "Apple", 32f, Load<Sprite>(CommonIcon("icon_apple")), 38f);
            Flex(apple);

            root.gameObject.SetActive(true);
            return (root, guest.Button, google.Button, apple.Button);
        }

        // G 표 — 지름 44 · 테 4 · 글자 26 (시안 .gmark), 글자 앞에
        private static void BuildGoogleMark(RectTransform content)
        {
            RectTransform mark = NewRect("Mark", content);
            mark.SetAsFirstSibling();
            LayoutElement button = content.parent.parent.GetComponent<LayoutElement>();
            button.preferredWidth += 56f;
            button.minWidth += 56f;
            Fixed(mark, 44f, 44f);
            RoundBox(mark, Ink, 4f, 22f, White);
            TMP_Text g = NewText("G", mark, "G", 26f, Ink);
            Stretch(g.rectTransform);
        }

        private static (RectTransform, TMP_Text) BuildSheetLoading(RectTransform body)
        {
            RectTransform root = NewRect("Loading", body);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 192f;
            HorizontalLayout(root, new RectOffset(), 24f, TextAnchor.MiddleCenter, true, false);
            Fixed(BuildRing(root, 64f), 64f, 64f);
            TMP_Text text = NewText("Label", root, "시작하는 중…", 38f, Ink);
            return (root, text);
        }

        // 닉네임 — 입력 줄(입력칸 + 주사위) · 에러 칩 · 안내 · 「← 뒤로」 「시작」 (시안 step nick)
        private static (RectTransform, NicknameFieldView, Button, Button, Button) BuildNicknameBody(RectTransform body)
        {
            RectTransform root = NewRect("Nickname", body);
            VerticalLayout(root, new RectOffset(), 20f, TextAnchor.UpperCenter);

            RectTransform row = NewRect("Row", root);
            HorizontalLayout(row, new RectOffset(), 20f, TextAnchor.UpperCenter, true, true);

            // 입력칸 — 높이 100 · 테 6 · 반경 26 · 글자 36 · 카운터 28. 포커스면 바깥 노란 테두리 8. 흔들 때 레이아웃과 다투지 않게 안쪽 Box 를 흔든다
            RectTransform slot = NewRect("Field", row);
            LayoutElement slotSize = slot.gameObject.AddComponent<LayoutElement>();
            slotSize.flexibleWidth = 1f;
            slotSize.preferredHeight = 100f;
            RectTransform box = NewRect("Box", slot);
            Stretch(box);
            RectTransform focus = NewRect("FocusRing", box);
            Stretch(focus, -8f, -8f, -8f, -8f);
            Stretch(RoundImage("Ring", focus, Turn, 34f).rectTransform);
            Image border = RoundImage("Border", box, Ink, 26f);
            Stretch(border.rectTransform);
            Image fill = RoundImage("Fill", box, White, 20f, true);
            Stretch(fill.rectTransform, 6f, 6f, 6f, 6f);

            TMP_InputField input = BuildInput(box);
            TMP_Text count = NewText("Count", box, "0 / 16", 28f, Ink2, TextAlignmentOptions.Right);
            Anchor(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(120f, 40f));

            ButtonParts dice = BuildButton("DiceButton", row, 100f, 100f, White, 26f, 6f, 8f, null, 0f, Load<Sprite>(CommonIcon("icon_dice")), 48f);

            // 에러 칩 — 자리 28 (시안 높이 22 · 위아래 −4), 칩은 48 로 넘쳐 보인다
            RectTransform slotErr = NewRect("ErrorSlot", root);
            slotErr.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
            RectTransform chip = NewRect("ErrorChip", slotErr);
            Anchor(chip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 48f));
            HorizontalLayout(chip, new RectOffset(20, 20, 0, 0), 0f, TextAnchor.MiddleCenter, true, false);
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            RoundBox(chip, Hp, 5f, 20f, new Color(1f, 1f, 1f, 0.95f));
            TMP_Text errorText = NewText("Label", chip, "사용할 수 없는 단어가 있어요.", 28f, Hp);
            chip.gameObject.SetActive(false);

            Wrap(NewText("Note", root, "로비 프로필에서 언제든 바꿀 수 있어요.", 26f, Ink2));

            RectTransform pair = NewRect("Buttons", root);
            HorizontalLayout(pair, new RectOffset(), 20f, TextAnchor.MiddleCenter, true, true);
            pair.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            ButtonParts back = BuildButton("BackButton", pair, 0f, 108f, White, 28f, 6f, 8f, "뒤로", 42f, Load<Sprite>(CommonIcon("icon_back")), 40f, 28f);
            Flex(back);
            ButtonParts start = BuildButton("StartButton", pair, 0f, 108f, Turn, 28f, 6f, 8f, "시작", 42f);
            Flex(start);

            NicknameFieldView field = root.gameObject.AddComponent<NicknameFieldView>();
            Wire(field, ("inputField", input), ("countText", count), ("border", border), ("focusRing", focus.gameObject), ("errorChip", chip.gameObject), ("errorText", errorText));
            return (root, field, dice.Button, back.Button, start.Button);
        }

        // TMP 입력칸 — 왼쪽 28 · 카운터 자리 140 비움. 안내 "닉네임을 입력해 주세요." (시안 placeholder)
        private static TMP_InputField BuildInput(RectTransform box)
        {
            RectTransform area = NewRect("TextArea", box);
            Stretch(area, 28f, 0f, 150f, 0f);
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text placeholder = NewText("Placeholder", area, "닉네임을 입력해 주세요.", 36f, Off, TextAlignmentOptions.Left);
            Stretch(placeholder.rectTransform);
            TMP_Text text = NewText("Text", area, "", 36f, Ink, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);

            TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = _font;
            input.pointSize = 36f;
            input.caretColor = Ink;
            input.customCaretColor = true;
            input.caretWidth = 3;
            input.selectionColor = new Color(1f, 0.757f, 0.18f, 0.5f);
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.transition = Selectable.Transition.None;
            input.characterLimit = NicknameValidator.MaxLength;
            return input;
        }

        private readonly struct SheetFail
        {
            public readonly RectTransform Root;
            public readonly TMP_Text Title;
            public readonly TMP_Text Description;
            public readonly TMP_Text Code;
            public readonly Button Retry;
            public readonly Button Other;

            public SheetFail(RectTransform root, TMP_Text title, TMP_Text description, TMP_Text code, Button retry, Button other)
            {
                Root = root; Title = title; Description = description; Code = code; Retry = retry; Other = other;
            }
        }

        // 창 안 실패 — 제목 38 빨강 · 설명 28 · 「다시 시도」 · 「다른 방법으로」 · 오류 코드 오른쪽 (시안 step fail)
        private static SheetFail BuildSheetFail(RectTransform body)
        {
            RectTransform root = NewRect("Fail", body);
            VerticalLayout(root, new RectOffset(), 20f, TextAnchor.UpperCenter);

            RectTransform tx = NewRect("Text", root);
            VerticalLayout(tx, new RectOffset(0, 0, 8, 12), 12f, TextAnchor.UpperCenter);
            TMP_Text title = NewText("Title", tx, "로그인 실패", 38f, Hp);
            TMP_Text desc = Wrap(NewText("Description", tx, "잠시 후 다시 시도해 주세요.", 28f, Ink));

            ButtonParts retry = BuildButton("RetryButton", root, 0f, 108f, Turn, 28f, 6f, 8f, "다시 시도", 42f);
            ButtonParts other = BuildButton("OtherButton", root, 0f, 72f, White, 22f, 5f, 6f, "다른 방법으로", 30f);
            TMP_Text code = NewText("Code", root, "TransportError", 22f, new Color(Ink2.r, Ink2.g, Ink2.b, 0.75f), TextAlignmentOptions.Right);
            return new SheetFail(root, title, desc, code, retry.Button, other.Button);
        }

        // 닫기 X — 지름 60 · 테 5 · 아래 두께 4, 창 오른쪽 위 모서리 밖 (오른쪽 −20 · 위 −24)
        private static ButtonParts BuildCloseButton(RectTransform frame)
        {
            ButtonParts close = BuildButton("CloseButton", frame, 60f, 60f, White, 30f, 5f, 4f, null, 0f, Load<Sprite>(HudIcon("icon_x")), 20f);
            close.Button.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor((RectTransform)close.Button.transform, Vector2.one, Vector2.one, new Vector2(20f, 24f), new Vector2(60f, 60f));
            return close;
        }

        #endregion

        #region 팝업

        // 업데이트 필요 · 점검 중 · 게임 종료 — 폭 400 × 2 · 머리 띠 34 × 2 · 위 200 × 2 (시안 .pop)
        private static TitlePopupView BuildPopup(RectTransform canvas)
        {
            RectTransform root = NewRect("Popup", canvas);
            Stretch(root);
            RectTransform content = NewRect("Content", root);
            Stretch(content);
            Image dimmer = NewImage("Dimmer", content, Dim, null, true);
            Stretch(dimmer.rectTransform);
            RectTransform center = BuildCenter("Center", content);

            Frame frame = BuildFrame("Frame", center, 6f, 24f, Band, 68f, 4f);
            TopCenter(frame.Root, 400f, 800f, 300f);
            frame.Root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HorizontalLayout(frame.Head, new RectOffset(24, 28, 0, 0), 12f, TextAnchor.MiddleLeft, true, false);
            Image icon = Fixed(Icon("Icon", frame.Head, Load<Sprite>(CommonIcon("icon_alert")), 36f, Ink), 36f, 36f);
            TMP_Text title = NewText("Title", frame.Head, "업데이트 필요", 36f, Ink, TextAlignmentOptions.Left);
            VerticalLayout(frame.Body, new RectOffset(32, 32, 28, 28), 24f, TextAnchor.UpperCenter);

            TMP_Text body = Wrap(NewText("Body", frame.Body, "새 버전으로 업데이트해 주세요.", 28f, Ink));
            ButtonParts store = BuildButton("StoreButton", frame.Body, 0f, 72f, Turn, 22f, 5f, 6f, "스토어로", 30f);
            ButtonParts ok = BuildButton("OkButton", frame.Body, 0f, 72f, Turn, 22f, 5f, 6f, "확인", 30f);
            RectTransform row = NewRect("QuitRow", frame.Body);
            HorizontalLayout(row, new RectOffset(), 16f, TextAnchor.MiddleCenter, true, true);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            ButtonParts cancel = BuildButton("CancelButton", row, 0f, 76f, White, 22f, 5f, 6f, "취소", 30f);
            Flex(cancel);
            ButtonParts quit = BuildButton("QuitButton", row, 0f, 76f, Turn, 22f, 5f, 6f, "종료", 30f);
            Flex(quit);

            TitlePopupView view = root.gameObject.AddComponent<TitlePopupView>();
            Wire(view,
                ("content", content.gameObject), ("frame", frame.Root), ("band", frame.BandImage), ("bandIcon", icon), ("bandTitle", title), ("body", body),
                ("storeButton", store.Button), ("okButton", ok.Button), ("quitRow", row.gameObject), ("cancelButton", cancel.Button), ("quitButton", quit.Button),
                ("alertIcon", Load<Sprite>(CommonIcon("icon_alert"))), ("clockIcon", Load<Sprite>(HudIcon("icon_clock"))), ("doorIcon", Load<Sprite>(CommonIcon("icon_door"))));
            content.gameObject.SetActive(false);
            return view;
        }

        #endregion

        #region 부품

        // 판 = 카드 한 장 (GameScreenBuilder.BuildFrame 과 같은 구조): 잉크 테두리 → 흰 몸통 → 머리 띠(위 모서리만 둥글게) → 띠 아래 잉크 선 → Head · Body
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

        private readonly struct ButtonParts
        {
            public readonly Button Button;
            public readonly PressableButton Press;
            public readonly RectTransform Content;
            public readonly Image Shadow;
            public readonly Image Border;
            public readonly Image Fill;
            public readonly TMP_Text Label;

            public ButtonParts(Button button, PressableButton press, RectTransform content, Image shadow, Image border, Image fill, TMP_Text label)
            {
                Button = button; Press = press; Content = content; Shadow = shadow; Border = border; Fill = fill; Label = label;
            }
        }

        /// <summary>
        /// 누르는 버튼 (시안 .btn) — 루트(크기 · Button) → Shadow(잉크, 아래 두께) · Body(테두리 · 채움 · 내용). 누르면 Body 가 (두께 − 2) 만큼 가라앉는다.
        /// width 0 이면 레이아웃이 폭을 정한다. 아이콘은 글자 앞에 (iconHeight 0 이면 정사각형).
        /// </summary>
        private static ButtonParts BuildButton(string name, Transform parent, float width, float height, Color fillColor, float radius, float border, float shadow,
            string label, float fontSize, Sprite icon = null, float iconWidth = 0f, float iconHeight = 0f)
        {
            // 아래 두께(그림자)는 레이아웃 높이에 넣지 않고 아래로 걸친다 (CSS box-shadow 와 같다)
            RectTransform root = NewRect(name, parent);
            LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = height;
            size.minHeight = height;
            root.sizeDelta = new Vector2(width > 0f ? width : 200f, height);

            Image shadowImage = RoundImage("Shadow", root, Ink, radius);
            Stretch(shadowImage.rectTransform, 0f, shadow, 0f, -shadow);

            RectTransform body = NewRect("Body", root);
            Stretch(body);
            Image fill = RoundBox(body, Ink, border, radius, fillColor, true);
            Image borderImage = body.GetChild(0).GetComponent<Image>();

            RectTransform content = NewRect("Content", body);
            Stretch(content);
            float padding = label != null ? 28f : 0f;
            HorizontalLayout(content, new RectOffset((int)padding, (int)padding, 0, 0), 12f, TextAnchor.MiddleCenter, true, true);

            float contentWidth = padding * 2f;
            if (icon != null)
            {
                Fixed(Icon("Icon", content, icon, iconWidth, Ink), iconWidth, iconHeight > 0f ? iconHeight : iconWidth);
                contentWidth += iconWidth + (label != null ? 12f : 0f);
            }

            TMP_Text text = label != null ? NewText("Label", content, label, fontSize, Ink) : null;
            if (text != null)
                contentWidth += text.GetPreferredValues(label).x;

            // 폭 0 = 레이아웃이 정한다 — 그래도 내용보다 좁아지지 않게
            size.preferredWidth = width > 0f ? width : contentWidth;
            size.minWidth = width > 0f ? width : contentWidth;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;

            PressableButton press = root.gameObject.AddComponent<PressableButton>();
            Wire(press, ("body", body));
            SetFloat(press, "pressDepth", Mathf.Max(shadow - 2f, 2f));
            return new ButtonParts(button, press, content, shadowImage, borderImage, fill, text);
        }

        // 나란한 버튼을 똑같은 폭으로 (시안 flex:1 — 내용 폭과 상관없이 남는 폭을 같게 나눈다)
        private static void Flex(ButtonParts parts)
        {
            LayoutElement size = parts.Button.GetComponent<LayoutElement>();
            size.flexibleWidth = 1f;
            size.preferredWidth = 0f;
            size.minWidth = 0f;
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
        private const string SoftRectSpritePath = "Assets/Sprites/UI/Common/ui_soft_rect.png";
        private const float SoftRectPadding = 2f;

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
            if (fillColor.a <= 0f)
                fill.enabled = false;
            return fill;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            // 프로젝트는 옛 Input Manager 를 쓴다 (다른 씬과 같은 StandaloneInputModule)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        #endregion

        #region 글꼴 · 머티리얼

        private static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            TMP_FontAsset asset = CreateFontAsset(FontAssetPath, FontSamplingPointSize, FontAtlasPadding, FontAtlasSize, true);
            if (asset != null)
                asset.isMultiAtlasTexturesEnabled = true;
            return asset;
        }

        // 본문 글자 굵기 — TMP SDF 는 브라우저(시안)보다 가늘게 보여 조금 부풀린다. 값은 시안과 겹쳐 보며 맞췄다
        private const float BodyFaceDilate = 0.05f;

        private static void ApplyBodyWeight(TMP_FontAsset font)
        {
            if (font == null || Mathf.Approximately(font.material.GetFloat(ShaderUtilities.ID_FaceDilate), BodyFaceDilate))
                return;

            font.material.SetFloat(ShaderUtilities.ID_FaceDilate, BodyFaceDilate);
            ShaderUtilities.UpdateShaderRatios(font.material);
            EditorUtility.SetDirty(font.material);
            AssetDatabase.SaveAssets();
        }

        // 로고 전용 — 「찹츄!」 세 글자만 굽는다. 굵은 외곽선(글자 260 에 11) · 아래 그림자가 들어가게 여백을 크게
        private static TMP_FontAsset EnsureLogoFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LogoFontAssetPath);
            if (existing != null)
                return existing;

            TMP_FontAsset asset = CreateFontAsset(LogoFontAssetPath, 128, 30, 512, false);
            if (asset == null)
                return null;

            asset.TryAddCharacters(LogoCharacters);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            Material material = asset.material;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            // 값은 시안 1920 × 1080 화면과 겹쳐 보며 맞췄다 — 외곽선 바깥 11 · 그림자 아래 14
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.13f);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.065f);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, Ink);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.065f);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // 흰 글자 + 잉크 외곽선 (버전 표기). 외곽선이 글자 안쪽을 먹지 않게 글자를 부풀린다 — 값은 시안과 겹쳐 보며 맞췄다
        private static Material EnsureOutlineMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (existing != null)
                return existing;

            TMP_FontAsset font = Load<TMP_FontAsset>(FontAssetPath);
            if (font == null)
                return null;

            var material = new Material(font.material) { name = Path.GetFileNameWithoutExtension(OutlineMaterialPath) };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.45f);
            ShaderUtilities.UpdateShaderRatios(material);
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static TMP_FontAsset CreateFontAsset(string assetPath, int samplingPointSize, int padding, int atlasSize, bool dynamic)
        {
            var font = Load<Font>(FontPath);
            if (font == null)
                return null;

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, samplingPointSize, padding, GlyphRenderMode.SDFAA,
                atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);

            AssetDatabase.CreateAsset(asset, assetPath);
            asset.material.name = asset.name + " Material";
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[{nameof(TitleScreenBuilder)}] TMP 글꼴 에셋 생성: {assetPath} ({(dynamic ? "동적" : "정적")})");
            return asset;
        }

        #endregion

        #region 만들기 도우미

        private static string CommonIcon(string name) => $"Assets/Sprites/UI/Common/{name}.png";

        private static string HudIcon(string name) => $"Assets/Sprites/UI/Hud/{name}.png";

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogError($"[{nameof(TitleScreenBuilder)}] 에셋이 없다: {path}");
            return asset;
        }

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

        private static void Stretch(RectTransform rect) => Stretch(rect, 0f, 0f, 0f, 0f);

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void TopCenter(RectTransform rect, float top, float width, float height)
            => Anchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(width, height));

        private static void BottomCenter(RectTransform rect, float bottom, float width, float height)
            => Anchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom), new Vector2(width, height));

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
                Debug.LogError($"[{nameof(TitleScreenBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
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
                Debug.LogError($"[{nameof(TitleScreenBuilder)}] {target.GetType().Name} 에 {field} 필드가 없다.");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
