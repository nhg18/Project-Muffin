using System.IO;
using Chapchu.UI.Fx;
using Chapchu.UI.GameScreen;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Chapchu.EditorTools
{
    // GameScreenBuilder.Gaepan.cs — 121 개판 오분전 연출 (프로토타입 v2 .gp-hud · .gp-title · .gp-five · GP_FX.judge, 960 시안 × 2)
    public static partial class GameScreenBuilder
    {
        // 스티커 글꼴 — 게임 글꼴(Galmuri11 Bold)에서 쓰는 글자만 구운 정적 에셋. 게임 글꼴 에셋은 여백이 5 라 시안의 굵은 흰 테 · 잉크 선이 들어가지 않는다.
        // 이미 있는 에셋은 다시 만들지 않는다 — 글자를 더하면 에셋을 지우고 다시 만들 것
        private const string StickerFontAssetPath = "Assets/Fonts/GlaMuri/Galmuri11-Bold Sticker SDF.asset";
        private const string StickerInkMaterialPath = "Assets/Fonts/GlaMuri/Galmuri11-Bold Sticker SDF - Ink.mat";
        private const string StickerCharacters = "개판오분전!땡끝꼴찌는?♥0123456789";
        private const int StickerSamplingSize = 96;
        private const int StickerPadding = 24;
        private const int StickerAtlasSize = 1024;

        private static readonly Color GpInk = Hex(0x2B1E1A);
        private static readonly Color GpCream = Hex(0xFFFCF6);
        private static readonly Color GpPink = Hex(0xFF7EA4);
        private static readonly Color GpPinkDeep = Hex(0xFF5C8A);
        private static readonly Color GpPaw = Hex(0xFF8FB0);
        private static readonly Color GpPawShine = Hex(0xFFC3D5);
        private static readonly Color GpYellow = Hex(0xFFE27A);
        private static readonly Color GpSegmentOff = Hex(0xF1E2CF);
        private static readonly Color GpCardIcon = Hex(0x74B8F5);

        /// <summary>
        /// 개판 연출 셋을 만든다 — 자명종(hudParent = BoardLayer, 왼쪽 위) · 시작 제목 · 판정 팻말(GaepanLayer — 캔버스 전체, 부르는 쪽이 서랍 앞에 둔다).
        /// 이미 만든 씬에 더할 때도 이 함수를 부른다.
        /// </summary>
        public static GaepanPresenter BuildGaepan(RectTransform canvasRect, RectTransform hudParent, OpponentSeatsPresenter seats, RectTransform myCharacter)
        {
            TMP_FontAsset stickerFont = EnsureStickerFont(out Material inkMaterial);
            GaepanHudView hud = BuildGaepanHud(hudParent, inkMaterial, stickerFont);

            RectTransform layer = NewRect("GaepanLayer", canvasRect);
            Stretch(layer);
            GaepanTitleView title = BuildGaepanTitle(layer, stickerFont, inkMaterial);
            GaepanJudgeView judge = BuildGaepanJudge(layer, stickerFont, inkMaterial);

            var presenter = layer.gameObject.AddComponent<GaepanPresenter>();
            Wire(presenter, ("hud", hud), ("title", title), ("judge", judge), ("seats", seats), ("myCharacter", myCharacter));
            return presenter;
        }

        // 자명종 (.gp-hud, v2 는 왼쪽 위 16 · 52 → × 2). 글자 14 → 28 기준: 시계 2.4em(67) · 「개판 오분전」 .95em · 숫자 1.85em · 「바퀴 남음」 .85em.
        // 크림 바탕 + 잉크 테 6 + 흰 바깥 고리 6 + 아래 잉크 그림자 (0 .22em 0 3px). 숨김으로 시작.
        private static GaepanHudView BuildGaepanHud(RectTransform parent, Material inkMaterial, TMP_FontAsset stickerFont)
        {
            RectTransform root = NewRect("GaepanHud", parent);
            TopLeft(root, EdgeX, 104f, 0f, 79f);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            HorizontalLayout(root, new RectOffset(10, 28, 6, 6), 11f, TextAnchor.MiddleLeft, true, true);
            FitBoth(root.gameObject);

            Image shadow = RoundImage("Shadow", root, GpInk, 46f);
            Stretch(shadow.rectTransform, -6f, 0.2f, -6f, -12.2f);
            Image ring = RoundImage("Ring", root, White, 46f);
            Stretch(ring.rectTransform, -6f, -6f, -6f, -6f);
            Image fill = RoundBox(root, GpInk, 6f, 40f, GpCream);
            foreach (Image image in new[] { shadow, ring })
                image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform clock = BuildGaepanClock(root, out UIPie[] segments, out RectTransform hand);

            TMP_Text label = NewText("Label", root, "개판 오분전", 27f, GpInk, false);
            TMP_Text number = NewText("Number", root, "5", 52f, GpPinkDeep, true);
            number.font = stickerFont;
            number.fontSharedMaterial = inkMaterial;
            number.gameObject.AddComponent<LayoutElement>().minWidth = 30f;
            TMP_Text unit = NewText("Unit", root, "바퀴 남음", 24f, new Color(GpInk.r, GpInk.g, GpInk.b, 0.7f), false);

            var view = root.gameObject.AddComponent<GaepanHudView>();
            Wire(view, ("group", group), ("fill", fill), ("labelText", label), ("numberText", number), ("unitText", unit), ("clock", clock), ("hand", hand));
            WireArray(view, "segments", segments);
            root.gameObject.SetActive(false);
            return view;
        }

        // 5칸 자명종 (gpClock, viewBox 48 → 67.2, 1 = 1.4). 다리 2 · 종 2(분홍) · 얼굴(크림, 잉크 테 2.6) · 분홍 칸 5(반지름 13, 칸 사이 크림 틈) · 바늘(12) · 가운데 점.
        // 흔들림 중심은 시안 transform-origin 50% 62% — 피벗 (0.5, 0.38). 자식은 가운데 기준이라 피벗과 상관없다.
        private static RectTransform BuildGaepanClock(RectTransform parent, out UIPie[] segments, out RectTransform hand)
        {
            const float s = 1.4f;
            Vector2 P(float x, float y) => new Vector2((x - 24f) * s, (24f - y) * s);

            RectTransform clock = NewRect("Clock", parent);
            clock.pivot = new Vector2(0.5f, 0.38f);
            clock.sizeDelta = new Vector2(48f * s, 48f * s);
            LayoutElement size = clock.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 48f * s;
            size.preferredHeight = 48f * s;

            GpBar("LegLeft", clock, P(13f, 44f), P(16f, 40f), 2.6f * s, GpInk);
            GpBar("LegRight", clock, P(35f, 44f), P(32f, 40f), 2.6f * s, GpInk);
            GpDisc("BellLeft", clock, P(10f, 11f), (12f + 2.6f) * s, GpInk);
            GpDisc("BellLeftFill", clock, P(10f, 11f), (12f - 2.6f) * s, GpPink);
            GpDisc("BellRight", clock, P(38f, 11f), (12f + 2.6f) * s, GpInk);
            GpDisc("BellRightFill", clock, P(38f, 11f), (12f - 2.6f) * s, GpPink);
            GpDisc("Face", clock, P(24f, 27f), (34f + 2.6f) * s, GpInk);
            GpDisc("FaceFill", clock, P(24f, 27f), (34f - 2.6f) * s, GpCream);

            const int count = 5;
            const float gap = 7f; // 칸 사이 크림 선 1.6 — 바깥 끝에서 약 7°
            segments = new UIPie[count];
            for (int i = 0; i < count; i++)
            {
                RectTransform rect = NewRect($"Segment{i}", clock);
                Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), P(24f, 27f), new Vector2(26f * s, 26f * s));
                UIPie pie = rect.gameObject.AddComponent<UIPie>();
                pie.color = GpPink;
                pie.raycastTarget = false;
                pie.Set(i * 360f / count + gap / 2f, 360f / count - gap);
                segments[i] = pie;
            }

            hand = NewRect("Hand", clock);
            Anchor(hand, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), P(24f, 27f), Vector2.zero);
            Image needle = RoundImage("Needle", hand, GpInk, 1.5f * s);
            Anchor(needle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, -1.5f * s), new Vector2(3f * s, 13.5f * s));
            GpDisc("Pin", clock, P(24f, 27f), 5.2f * s, GpInk);
            return clock;
        }

        // 시작 제목 (.gp-title · .gp-five, 컷인 장면 없이) — 캔버스 전체, 숨김으로 시작.
        // 제목: 위 22% 가운데, −4°. 첫 줄 21vmin(227) 털색 「개판」, 둘째 줄 12.5vmin(135) 분홍 「오분전!」 · 위로 .12em · 오른쪽 1.1em.
        // 도장: 82% · 26% 에 25vmin(270) 발바닥, 12°. 숫자 11.5vmin(124) · 「바퀴 동안」 3.6vmin(39) 알약.
        private static GaepanTitleView BuildGaepanTitle(RectTransform parent, TMP_FontAsset font, Material inkMaterial)
        {
            RectTransform root = NewRect("GaepanTitle", parent);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            RectTransform lines = NewRect("Lines", root);
            Anchor(lines, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1080f * 0.22f), Vector2.zero);
            lines.localRotation = Quaternion.Euler(0f, 0f, 4f);
            VerticalLayoutGroup column = lines.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.UpperCenter;
            column.spacing = -0.12f * 135f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            FitBoth(lines.gameObject);

            StickerWordView first = BuildStickerLine("First", lines, font, 227f, new RectOffset());
            StickerWordView second = BuildStickerLine("Second", lines, font, 135f, new RectOffset(Mathf.RoundToInt(1.1f * 135f), 0, 0, 0));

            // 발바닥 도장 (PAW, viewBox 100 → 270, 1 = 2.7)
            const float s = 2.7f;
            Vector2 P(float x, float y) => new Vector2((x - 50f) * s, (50f - y) * s);
            RectTransform stamp = NewRect("Stamp", root);
            Anchor(stamp, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(1920f * 0.82f, -1080f * 0.26f), new Vector2(100f * s, 100f * s));
            stamp.localRotation = Quaternion.Euler(0f, 0f, -12f);
            stamp.gameObject.AddComponent<CanvasGroup>();

            (Vector2 c, Vector2 r, float rot, Vector2 shine, Vector2 shineR)[] beanSpecs =
            {
                (new Vector2(20f, 38f), new Vector2(10.5f, 12.5f), -24f, new Vector2(17f, 34f), new Vector2(3f, 4f)),
                (new Vector2(38f, 20f), new Vector2(11f, 13.5f), -8f, new Vector2(34f, 17f), new Vector2(3.5f, 4.5f)),
                (new Vector2(62f, 20f), new Vector2(11f, 13.5f), 8f, new Vector2(58f, 17f), new Vector2(3.5f, 4.5f)),
                (new Vector2(80f, 38f), new Vector2(10.5f, 12.5f), 24f, new Vector2(76f, 34f), new Vector2(3f, 4f)),
            };
            var beans = new RectTransform[beanSpecs.Length];
            for (int i = 0; i < beanSpecs.Length; i++)
            {
                (Vector2 c, Vector2 r, float rot, Vector2 shine, Vector2 shineR) = beanSpecs[i];
                RectTransform bean = NewRect($"Bean{i}", stamp);
                Anchor(bean, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), P(c.x, c.y), Vector2.zero);
                bean.localRotation = Quaternion.Euler(0f, 0f, -rot); // SVG rotate(+) = 시계 방향
                GpOval("Ink", bean, Vector2.zero, (r * 2f + Vector2.one * 4.5f) * s, GpInk);
                GpOval("Fill", bean, Vector2.zero, (r * 2f - Vector2.one * 4.5f) * s, GpPaw);
                Vector2 local = Quaternion.Euler(0f, 0f, rot) * (P(shine.x, shine.y) - P(c.x, c.y));
                GpOval("Shine", bean, local, shineR * 2f * s, GpPawShine);
                beans[i] = bean;
            }
            // 발바닥 (시안 path — 위가 좁고 아래가 넓은 둥근 덩어리, 가로 21 ~ 79 · 세로 42 ~ 86) — 둥근 사각형으로 비슷하게
            GpOval("PadInk", stamp, P(50f, 64f), new Vector2(58f + 4.5f, 44f + 4.5f) * s, GpInk);
            GpOval("PadFill", stamp, P(50f, 64f), new Vector2(58f - 4.5f, 44f - 4.5f) * s, GpPaw);

            TMP_Text rounds = NewText("Rounds", stamp, "5", 124f, GpCream, true);
            rounds.font = font;
            rounds.fontSharedMaterial = inkMaterial;
            Anchor(rounds.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), P(50f, 63f), new Vector2(200f, 160f));

            RectTransform pill = NewRect("Caption", stamp);
            Anchor(pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), P(50f, 104f), Vector2.zero);
            pill.localRotation = Quaternion.Euler(0f, 0f, 12f);
            HorizontalLayout(pill, new RectOffset(23, 23, 2, 5), 0f, TextAnchor.MiddleCenter, true, true);
            FitBoth(pill.gameObject);
            Image pillShadow = RoundImage("Shadow", pill, GpInk, 40f);
            Stretch(pillShadow.rectTransform, 0f, 5f, 0f, -5f);
            pillShadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RoundBox(pill, GpInk, 6f, 40f, GpCream);
            NewText("Text", pill, "바퀴 동안", 39f, GpInk, true);

            var view = root.gameObject.AddComponent<GaepanTitleView>();
            Wire(view, ("group", group), ("firstLine", first), ("secondLine", second), ("stamp", stamp), ("roundsText", rounds));
            WireArray(view, "beans", beans);
            root.gameObject.SetActive(false);
            return view;
        }

        // 판정 팻말 (GP_FX.judge) — 캔버스 전체, 숨김으로 시작. 어둡게(43,30,26 · 42%) · 띠(위 17%, 10vmin=108, −3°) · 기준 팻말(위 31%, 5.6vmin=60, −3°) · 머리 위 값 표 템플릿(4.4vmin=48).
        private static GaepanJudgeView BuildGaepanJudge(RectTransform parent, TMP_FontAsset font, Material inkMaterial)
        {
            RectTransform root = NewRect("GaepanJudge", parent);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            Image dim = NewImage("Dim", root, new Color(GpInk.r, GpInk.g, GpInk.b, 0f));
            Anchor(dim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6000f, 6000f));

            RectTransform tagTemplate = BuildJudgeTag(root);

            RectTransform banner = NewRect("Banner", root);
            Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1080f * 0.17f), Vector2.zero);
            banner.localRotation = Quaternion.Euler(0f, 0f, 3f);
            banner.gameObject.AddComponent<CanvasGroup>();
            HorizontalLayout(banner, new RectOffset(), 0f, TextAnchor.MiddleCenter, true, true);
            FitBoth(banner.gameObject);
            StickerWordView bannerWord = AddStickerWord(banner, font, 108f);

            // 기준 팻말 — 잉크 테 .08em(5) · 흰 고리 .07em(4) · 아래 그림자 .12em · 반지름 .5em. 안: 하트(.9em) 또는 카드 아이콘(.62 × .84em, −8°) + 「HP 꼴찌」
            RectTransform roll = NewRect("Roll", root);
            Anchor(roll, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1080f * 0.31f), Vector2.zero);
            roll.localRotation = Quaternion.Euler(0f, 0f, 3f);
            var rollGroup = roll.gameObject.AddComponent<CanvasGroup>();
            HorizontalLayout(roll, new RectOffset(27, 36, 8, 12), 15f, TextAnchor.MiddleCenter, true, true);
            FitBoth(roll.gameObject);
            Image rollShadow = RoundImage("Shadow", roll, GpInk, 34f);
            Stretch(rollShadow.rectTransform, -4f, 3f, -4f, -11f);
            Image rollRing = RoundImage("Ring", roll, White, 34f);
            Stretch(rollRing.rectTransform, -4f, -4f, -4f, -4f);
            foreach (Image image in new[] { rollShadow, rollRing })
                image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image rollFill = RoundBox(roll, GpInk, 5f, 30f, Hex(0xFFE0EA));

            TMP_Text heart = NewText("Heart", roll, "♥", 54f, GpPinkDeep, true);
            heart.font = font;
            heart.fontSharedMaterial = inkMaterial;
            RectTransform cardIcon = NewRect("CardIcon", roll);
            LayoutElement cardSize = cardIcon.gameObject.AddComponent<LayoutElement>();
            cardSize.preferredWidth = 37f;
            cardSize.preferredHeight = 50f;
            RectTransform cardShape = NewRect("Shape", cardIcon);
            Anchor(cardShape, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(37f, 50f));
            cardShape.localRotation = Quaternion.Euler(0f, 0f, 8f);
            RoundBox(cardShape, GpInk, 5f, 7f, GpCardIcon);
            cardIcon.gameObject.SetActive(false);
            TMP_Text rollText = NewText("Text", roll, "HP 꼴찌", 60f, GpInk, true);

            var view = root.gameObject.AddComponent<GaepanJudgeView>();
            Wire(view, ("group", group), ("dim", dim), ("banner", banner), ("bannerWord", bannerWord), ("roll", roll), ("rollGroup", rollGroup), ("rollFill", rollFill),
                ("rollText", rollText), ("heartIcon", heart), ("cardIcon", cardIcon.gameObject), ("tagTemplate", tagTemplate));
            root.gameObject.SetActive(false);
            return view;
        }

        // 머리 위 값 표 템플릿 (.gp-tag) — Tag(가운데 피벗, CanvasGroup) → Body(알약: 그림자 · 흰 고리 · 잉크 테 .09em · 크림, 글자 48) + LowBadge(「꼴찌」 노랑, −8°, 꼴찌일 때만)
        private static RectTransform BuildJudgeTag(RectTransform parent)
        {
            RectTransform tag = NewRect("TagTemplate", parent);
            Anchor(tag, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            tag.gameObject.AddComponent<CanvasGroup>();

            RectTransform body = NewRect("Body", tag);
            Anchor(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            HorizontalLayout(body, new RectOffset(24, 24, 4, 7), 0f, TextAnchor.MiddleCenter, true, true);
            FitBoth(body.gameObject);
            Image shadow = RoundImage("Shadow", body, GpInk, 50f);
            Stretch(shadow.rectTransform, -4f, 2f, -4f, -10f);
            Image ring = RoundImage("Ring", body, White, 50f);
            Stretch(ring.rectTransform, -4f, -4f, -4f, -4f);
            foreach (Image image in new[] { shadow, ring })
                image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image border = RoundImage("Border", body, GpInk, 46f);
            Stretch(border.rectTransform);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image fill = RoundImage("Fill", body, GpCream, 42f);
            Stretch(fill.rectTransform, 4f, 4f, 4f, 4f);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            NewText("Text", body, "<size=58%>HP</size>100", 48f, GpInk, true);

            RectTransform badge = NewRect("LowBadge", body);
            Anchor(badge, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), Vector2.zero);
            badge.localRotation = Quaternion.Euler(0f, 0f, 8f);
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HorizontalLayout(badge, new RectOffset(17, 17, 2, 4), 0f, TextAnchor.MiddleCenter, true, true);
            FitBoth(badge.gameObject);
            Image badgeShadow = RoundImage("Shadow", badge, GpInk, 30f);
            Stretch(badgeShadow.rectTransform, 0f, 4f, 0f, -4f);
            badgeShadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RoundBox(badge, GpInk, 4f, 30f, GpYellow);
            NewText("Text", badge, "꼴찌", 35f, GpInk, true);
            badge.gameObject.SetActive(false);

            tag.gameObject.SetActive(false);
            return tag;
        }

        // 스티커 글자 한 줄 — HorizontalLayoutGroup + StickerWordView + 글자 칸 템플릿
        private static StickerWordView BuildStickerLine(string name, RectTransform parent, TMP_FontAsset font, float size, RectOffset padding)
        {
            RectTransform line = NewRect(name, parent);
            HorizontalLayout(line, padding, 0f, TextAnchor.MiddleCenter, true, true);
            return AddStickerWord(line, font, size);
        }

        private static StickerWordView AddStickerWord(RectTransform line, TMP_FontAsset font, float size)
        {
            RectTransform cell = NewRect("CharTemplate", line);
            cell.gameObject.AddComponent<LayoutElement>();
            TMP_Text glyph = NewText("Glyph", cell, "개", size, Color.white, true);
            glyph.font = font;
            glyph.fontSharedMaterial = font.material;
            Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.4f, size * 1.4f));
            cell.gameObject.SetActive(false);

            var view = line.gameObject.AddComponent<StickerWordView>();
            Wire(view, ("charTemplate", cell));
            return view;
        }

        // 스티커 글꼴 + 머티리얼 둘: 에셋 기본 = 잉크 선 + 흰 바깥 테(밑깔기) — 제목 · 띠. 「Ink」 = 잉크 선만 — 숫자 · 하트.
        // 시안: 잉크 선 .2em(바깥 .1em) · 흰 테 .36em(바깥 .18em, .06em 아래로). 값은 화면을 보며 맞췄다
        private static TMP_FontAsset EnsureStickerFont(out Material inkMaterial)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StickerFontAssetPath);
            if (asset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(BoldFontPath);
                asset = TMP_FontAsset.CreateFontAsset(font, StickerSamplingSize, StickerPadding, GlyphRenderMode.SDFAA,
                    StickerAtlasSize, StickerAtlasSize, AtlasPopulationMode.Dynamic, true);
                asset.name = Path.GetFileNameWithoutExtension(StickerFontAssetPath);
                AssetDatabase.CreateAsset(asset, StickerFontAssetPath);
                asset.material.name = asset.name + " Material";
                asset.atlasTextures[0].name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
                asset.TryAddCharacters(StickerCharacters);
                asset.atlasPopulationMode = AtlasPopulationMode.Static;

                Material sticker = asset.material;
                sticker.EnableKeyword(ShaderUtilities.Keyword_Outline);
                sticker.SetColor(ShaderUtilities.ID_OutlineColor, GpInk);
                sticker.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.4f);
                sticker.SetFloat(ShaderUtilities.ID_FaceDilate, 0.4f);
                sticker.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                sticker.SetColor(ShaderUtilities.ID_UnderlayColor, White);
                sticker.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.85f);
                sticker.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.25f);
                ShaderUtilities.UpdateShaderRatios(sticker);
                EditorUtility.SetDirty(sticker);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }

            inkMaterial = AssetDatabase.LoadAssetAtPath<Material>(StickerInkMaterialPath);
            if (inkMaterial == null)
            {
                inkMaterial = new Material(asset.material) { name = Path.GetFileNameWithoutExtension(StickerInkMaterialPath) };
                inkMaterial.DisableKeyword(ShaderUtilities.Keyword_Underlay);
                inkMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                inkMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.3f);
                ShaderUtilities.UpdateShaderRatios(inkMaterial);
                AssetDatabase.CreateAsset(inkMaterial, StickerInkMaterialPath);
                AssetDatabase.SaveAssets();
            }
            return asset;
        }

        private static Image GpDisc(string name, Transform parent, Vector2 center, float diameter, Color color)
            => GpOval(name, parent, center, new Vector2(diameter, diameter), color);

        // 타원 대신 둥근 사각형(짧은 변의 반 = 반지름) — 시안의 타원 · 원과 거의 같다
        private static Image GpOval(string name, Transform parent, Vector2 center, Vector2 size, Color color)
        {
            Image image = RoundImage(name, parent, color, Mathf.Min(size.x, size.y) / 2f);
            Anchor(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, size);
            return image;
        }

        // 둥근 끝 선 (stroke-linecap round)
        private static void GpBar(string name, Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            Image bar = RoundImage(name, parent, color, width / 2f);
            Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), (from + to) / 2f, new Vector2(delta.magnitude + width, width));
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
