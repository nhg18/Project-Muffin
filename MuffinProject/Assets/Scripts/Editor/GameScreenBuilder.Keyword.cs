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
    /// 키워드 설명 창 (10-ui.md 7-1 「대표 키워드」, 시안 「카드 키워드 v2」 #tip — 시안 px × 2). 캔버스 맨 앞에 하나, 창은 꺼 둔 채로 저장한다.
    /// 흰 판 + 잉크 테 · 그림자, 머리(분류 색 옅은 바탕 · 아래 잉크 줄 · 아이콘 + 키워드 · 오른쪽 분류) → 뜻. 높이는 뜻 줄 수를 따른다.
    /// 열린 씬은 메뉴 Add Keyword Tip 으로 더한다 — 이미 있으면 그것만 새로 짓는다.
    /// </summary>
    public static partial class GameScreenBuilder
    {
        private const string KeywordTipName = "KeywordTip";
        private const float TipWidth = 540f, TipRadius = 24f, TipHeadHeight = 68f, TipHeadLine = 4f, TipPadX = 22f;
        private const float TipNameSize = 36f, TipCategorySize = 24f, TipMeaningSize = 28f;
        private static readonly Vector4 TipMeaningMargin = new Vector4(TipPadX, 16f, TipPadX, 20f); // 왼 · 위 · 오른 · 아래 (시안 9 11 10 11)
        private static readonly Color TipShadow = new Color(0x2a / 255f, 0x24 / 255f, 0x20 / 255f, 0.22f); // 시안 0 8px 20px rgba(42,36,32,.22)
        private const float TipShadowDrop = 16f, TipShadowFeather = 40f;

        [MenuItem("Chapchu/Game Screen/Add Keyword Tip")]
        public static void AddKeywordTip()
        {
            GameObject canvas = GameObject.Find(CanvasName);
            HandView hand = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            if (canvas == null || hand == null)
            {
                Debug.LogError($"[{nameof(GameScreenBuilder)}] 열린 씬에 {CanvasName} · 손패(HandView) 중 없는 것이 있다.");
                return;
            }

            _regularFont = EnsureFontAsset(RegularFontPath, RegularFontAssetPath);
            _boldFont = EnsureFontAsset(BoldFontPath, BoldFontAssetPath);

            Transform old = canvas.transform.Find(KeywordTipName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            BuildKeywordTip((RectTransform)canvas.transform, hand);
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            Debug.Log($"[{nameof(GameScreenBuilder)}] 키워드 설명 창을 더함 — 씬을 저장할 것.");
        }

        private static KeywordTipView BuildKeywordTip(RectTransform canvasRect, HandView hand)
        {
            RectTransform root = NewRect(KeywordTipName, canvasRect);
            Stretch(root);
            root.SetAsLastSibling(); // 서랍 · 팝업 위 — 커서 아래 글을 설명한다

            RectTransform panel = NewRect("Panel", root);
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(TipWidth, 0f));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)Line4, (int)Line4, (int)Line4, (int)Line4); // 머리 · 뜻은 테 안쪽
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UIShape shadow = NewShape("Shadow", panel, TipShadow, TipRadius, 0f, Color.clear);
            shadow.Set(UIShape.ShapeKind.RoundRect, TipRadius, 0f, Color.clear, TipShadowFeather);
            IgnoreLayout(shadow.rectTransform, 0f, -TipShadowDrop);
            UIShape fill = NewShape("Fill", panel, White, TipRadius, Line4, Ink);
            IgnoreLayout(fill.rectTransform, 0f, 0f);

            // 머리 — 둥근 바탕(테 안쪽 반지름) + 아래 반을 덮는 네모로 아래 모서리를 각지게
            RectTransform head = NewRect("Head", panel);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = TipHeadHeight;
            UIShape headFill = NewShape("HeadFill", head, White, TipRadius - Line4, 0f, Color.clear);
            Stretch(headFill.rectTransform);
            Image headSquare = NewImage("HeadSquare", head, White);
            AnchorBox(headSquare.rectTransform, 0f, 0f, 1f, 0.5f);
            Image line = NewImage("Line", head, Ink);
            Anchor(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, TipHeadLine));
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = new Vector2(1f, 0f);

            TMP_Text nameText = NewText("Name", head, "피해", TipNameSize, Ink, true, TextAlignmentOptions.Left);
            Stretch(nameText.rectTransform, TipPadX, 0f, TipPadX, TipHeadLine);
            TMP_Text categoryText = NewText("Category", head, "피해", TipCategorySize, Ink2, false, TextAlignmentOptions.Right);
            Stretch(categoryText.rectTransform, TipPadX, 0f, TipPadX, TipHeadLine);

            TMP_Text meaningText = NewText("Meaning", panel, "HP 를 그만큼 깎는다.", TipMeaningSize, Ink, false, TextAlignmentOptions.TopLeft);
            meaningText.textWrappingMode = TextWrappingModes.Normal;
            meaningText.margin = TipMeaningMargin;
            meaningText.lineSpacing = 6f;

            panel.gameObject.SetActive(false); // 커서가 키워드 위에 올 때만

            var view = root.gameObject.AddComponent<KeywordTipView>();
            Wire(view, ("hand", hand), ("panel", panel), ("headFill", headFill), ("headSquare", headSquare),
                ("nameText", nameText), ("categoryText", categoryText), ("meaningText", meaningText));
            return view;
        }

        // 레이아웃에서 빼고 판 전체를 덮는다 — 그림자는 아래로 내린다
        private static void IgnoreLayout(RectTransform rect, float x, float y)
        {
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(rect);
            rect.anchoredPosition = new Vector2(x, y);
        }
    }
}
