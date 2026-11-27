using Chapchu.UI.Fx;
using Chapchu.UI.GameScreen;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.EditorTools
{
    // GameScreenBuilder.Gaepan.cs — 1021 개판 오분전 연출 (프로토타입 v2 .gp-hud · .gp-title · .gp-five · GP_FX.judge, 960 시안 × 2)
    public static partial class GameScreenBuilder
    {
        // 스티커 글자 = 게임 제목 글꼴(태백체, 여백 10) + 머티리얼 둘. 「Sticker」 = 잉크 선 + 흰 바깥 테(밑깔기) — 제목 · 띠. 「Outline」 = 잉크 선만 — 숫자 · 하트.
        private const string StickerMaterialPath = "Assets/Fonts/Taebaek/Taebaek SDF - Sticker.mat";
        private const string StickerInkMaterialPath = "Assets/Fonts/Taebaek/Taebaek SDF - Outline.mat";

        private static readonly Color GpInk = Hex(0x2B1E1A);
        private static readonly Color GpCream = Hex(0xFFFCF6);
        private static readonly Color GpPink = Hex(0xFF7EA4);
        private static readonly Color GpPinkDeep = Hex(0xFF5C8A);
        private static readonly Color GpYellow = Hex(0xFFE27A);
        private static readonly Color GpSegmentOff = Hex(0xF1E2CF);
        private static readonly Color GpCardIcon = Hex(0x74B8F5);

        /// <summary>
        /// 개판 연출을 만든다 — 자명종(hudParent = BoardLayer, 왼쪽 위) · 판정 팻말(GaepanLayer — 캔버스 전체, 부르는 쪽이 서랍 앞에 둔다).
        /// 시작 제목은 카드의 3 단계 컷인(GaepanFx)이 보인다 (PM 2026-11-27).
        /// 이미 만든 씬에 더할 때도 이 함수를 부른다.
        /// </summary>
        public static GaepanPresenter BuildGaepan(RectTransform canvasRect, RectTransform hudParent, OpponentSeatsPresenter seats, RectTransform myCharacter)
        {
            TMP_FontAsset stickerFont = LoadStickerFont(out Material stickerMaterial, out Material inkMaterial);
            GaepanHudView hud = BuildGaepanHud(hudParent, inkMaterial, stickerFont);

            RectTransform layer = NewRect("GaepanLayer", canvasRect);
            Stretch(layer);
            GaepanJudgeView judge = BuildGaepanJudge(layer, stickerFont, stickerMaterial, inkMaterial);

            var presenter = layer.gameObject.AddComponent<GaepanPresenter>();
            Wire(presenter, ("hud", hud), ("judge", judge), ("seats", seats), ("myCharacter", myCharacter));
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

        // 판정 팻말 (GP_FX.judge) — 캔버스 전체, 숨김으로 시작. 어둡게(43,30,26 · 42%) · 띠(위 17%, 10vmin=108, −3°) · 기준 팻말(위 31%, 5.6vmin=60, −3°) · 머리 위 값 표 템플릿(4.4vmin=48).
        private static GaepanJudgeView BuildGaepanJudge(RectTransform parent, TMP_FontAsset font, Material stickerMaterial, Material inkMaterial)
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
            StickerWordView bannerWord = AddStickerWord(banner, font, stickerMaterial, 108f);

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

        private static StickerWordView AddStickerWord(RectTransform line, TMP_FontAsset font, Material material, float size)
        {
            RectTransform cell = NewRect("CharTemplate", line);
            cell.gameObject.AddComponent<LayoutElement>();
            TMP_Text glyph = NewText("Glyph", cell, "개", size, Color.white, true);
            glyph.font = font;
            glyph.fontSharedMaterial = material;
            Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.4f, size * 1.4f));
            cell.gameObject.SetActive(false);

            var view = line.gameObject.AddComponent<StickerWordView>();
            Wire(view, ("charTemplate", cell));
            return view;
        }

        // 스티커 글꼴(태백체) + 머티리얼 둘 — Fonts/Taebaek 의 프리셋. 시안: 잉크 선 .2em(바깥 .1em) · 흰 테 .36em(바깥 .18em, .06em 아래로). 값은 화면을 보며 맞췄다
        private static TMP_FontAsset LoadStickerFont(out Material stickerMaterial, out Material inkMaterial)
        {
            stickerMaterial = AssetDatabase.LoadAssetAtPath<Material>(StickerMaterialPath);
            inkMaterial = AssetDatabase.LoadAssetAtPath<Material>(StickerInkMaterialPath);
            if (stickerMaterial == null || inkMaterial == null)
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 스티커 글자 머티리얼이 없다: {StickerMaterialPath} · {StickerInkMaterialPath}");
            return BoldFont;
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
