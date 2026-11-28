using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Chapchu.UI.Fx;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 「낸 카드 기록」 한 줄 (통합 화면 UI v3 .hitem) — 카드 이름 · 작은 카드(종류 색 테 + 그림) · 상자(누가 → 누구에게 / 결과). 프리팹 HistoryItem 을 HistoryListView 가 복제한다.
    /// 무효가 된 카드는 그림이 흐려지고 결과에 줄. 그림이 없는 카드는 카드의 「그림 준비 중」 처럼 옅은 종류 색 바탕 + 발바닥. 값은 HistoryPresenter 가 만든 글을 그대로 넣는다.
    /// 마우스를 올리면 · 탭하면 알린다 — 크게 보기는 HistoryPresenter 가 띄운다 (10-ui.md 2-1 「낸 카드 기록」).
    /// </summary>
    public class HistoryItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>마우스가 줄 위로 올라왔다 (터치는 알리지 않는다).</summary>
        public event Action<HistoryItemView> OnHovered;
        /// <summary>마우스가 줄 밖으로 나갔다.</summary>
        public event Action<HistoryItemView> OnUnhovered;
        /// <summary>줄을 눌렀다 (클릭 · 탭).</summary>
        public event Action<HistoryItemView> OnClicked;

        private const float TextSlack = 12f;

        [Tooltip("줄 내용 전체 — 새 줄이 튀어나올 때 가운데를 축으로 키운다.")]
        [SerializeField] private RectTransform body;
        [SerializeField] private CanvasGroup bodyGroup;
        [SerializeField] private TMP_Text labelText;
        [Tooltip("작은 카드 — 흰 바탕 + 종류 색 테.")]
        [SerializeField] private UIShape thumb;
        [Tooltip("카드 그림 — 칸을 꽉 채우게 잘라 위쪽(25%)을 보인다 (시안 object-position 50% 25%). 그림이 없으면 옅은 종류 색 바탕.")]
        [SerializeField] private RawImage art;
        [Tooltip("그림 없는 카드의 발바닥 — 카드 「그림 준비 중」 과 같은 모양 (옅은 종류 색 바탕 위).")]
        [SerializeField] private Image noArtPaw;
        [SerializeField] private TMP_Text whoText;
        [SerializeField] private TMP_Text fxText;

        [Header("모양 (통합 화면 UI v3)")]
        [Tooltip("두 줄(누가 · 결과) 사이 틈 (시안 gap 4 × 2).")]
        [SerializeField] private float lineGap = 8f;
        [Tooltip("결과 줄 높이 — HP 변화(큰 숫자 19 × 2)일 때.")]
        [SerializeField] private float bigFxHeight = 38f;
        [Tooltip("결과 줄 높이 — 한 마디 결과(15 × 2)일 때.")]
        [SerializeField] private float fxHeight = 30f;
        [Tooltip("그림에서 보일 세로 자리 — 0 = 위끝, 1 = 아래끝.")]
        [SerializeField] private float artFocusY = 0.25f;
        [SerializeField] private Color voidArtColor = new Color(0.62f, 0.62f, 0.62f, 0.6f);
        [Tooltip("그림 없을 때 바탕 · 발바닥에 섞는 종류 색 비율 — 카드 그림 칸과 같다 (CardArt.shader 0.05 · 0.3).")]
        [SerializeField] private float noArtFillMix = 0.05f;
        [SerializeField] private float noArtPawMix = 0.3f;
        [Tooltip("새 줄이 튀어나오는 시간 (시안 320ms) — 0.4 → 1.12 → 1.")]
        [SerializeField] private float popSeconds = 0.32f;

        private Coroutine _pop;

        private void Awake()
        {
            if (body == null || bodyGroup == null || labelText == null || thumb == null || art == null || noArtPaw == null || whoText == null || fxText == null)
                Debug.LogError($"[{nameof(HistoryItemView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>줄 내용 — 카드 이름 · 그림 · 종류 색 · 「누가 → 누구」 · 결과 (서식 태그 포함). bigFx = 결과가 HP 변화(큰 숫자).</summary>
        public void Show(string label, Sprite picture, Color kind, string who, string fx, bool bigFx, bool isVoid)
        {
            labelText.text = label;
            thumb.BorderColor = kind;
            whoText.text = who;
            fxText.text = fx;
            fxText.fontStyle = isVoid ? fxText.fontStyle | FontStyles.Strikethrough : fxText.fontStyle & ~FontStyles.Strikethrough;
            LayoutLines(bigFx ? bigFxHeight : fxHeight);

            // 그림이 없으면 빈 흰 칸 대신 카드의 「그림 준비 중」 처럼 — 옅은 종류 색 바탕(빈 텍스처 = 흰색 × 색) + 발바닥
            bool hasArt = picture != null;
            art.texture = hasArt ? picture.texture : null;
            art.uvRect = hasArt ? CoverUv(picture) : new Rect(0f, 0f, 1f, 1f);
            Color tint = isVoid ? voidArtColor : Color.white;
            art.color = hasArt ? tint : Color.Lerp(Color.white, kind, noArtFillMix) * tint;
            noArtPaw.enabled = !hasArt;
            noArtPaw.color = Color.Lerp(Color.white, kind, noArtPawMix) * tint;
        }

        // 터치는 손가락 번호(0 이상), 마우스는 음수 — 올리기는 마우스만 (탭은 누를 때 들어오고 뗄 때 나간다)
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.pointerId < 0)
                OnHovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId < 0)
                OnUnhovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                OnClicked?.Invoke(this);
        }

        /// <summary>새 줄 — 작게 나와 살짝 커졌다 제자리로.</summary>
        public void PlayPop()
        {
            if (_pop != null)
                StopCoroutine(_pop);
            _pop = StartCoroutine(PopRoutine());
        }

        private IEnumerator PopRoutine()
        {
            for (float t = 0f; t < popSeconds; t += Time.deltaTime)
            {
                float k = t / popSeconds;
                // 0 → 0.6: 0.4 → 1.12 (ease-out) · 투명 → 보임, 0.6 → 1: 1.12 → 1
                float rise = 1f - (1f - k / 0.6f) * (1f - k / 0.6f);
                float scale = k < 0.6f ? Mathf.Lerp(0.4f, 1.12f, rise) : Mathf.Lerp(1.12f, 1f, (k - 0.6f) / 0.4f);
                body.localScale = Vector3.one * scale;
                bodyGroup.alpha = k < 0.6f ? rise : 1f;
                yield return null;
            }

            body.localScale = Vector3.one;
            bodyGroup.alpha = 1f;
            _pop = null;
        }

        // 두 줄을 상자 세로 가운데에 — 결과 줄 높이가 HP 변화(38)와 한 마디(30)로 달라 위 줄 자리도 따라 움직인다 (시안 flex 가운데)
        // 결과 글 칸은 줄 높이보다 위아래로 TextSlack 씩 넓힌다 — TMP 는 글자가 칸 높이를 넘으면 말줄임(…)으로 자른다. 세로 가운데 정렬이라 보이는 자리는 같다.
        private void LayoutLines(float fxLineHeight)
        {
            var whoRect = whoText.rectTransform;
            var fxRect = fxText.rectTransform;
            float boxHeight = ((RectTransform)whoRect.parent).rect.height;
            float top = (boxHeight - (whoRect.sizeDelta.y + lineGap + fxLineHeight)) / 2f;
            whoRect.anchoredPosition = new Vector2(whoRect.anchoredPosition.x, -top);
            fxRect.sizeDelta = new Vector2(fxRect.sizeDelta.x, fxLineHeight + TextSlack * 2f);
            fxRect.anchoredPosition = new Vector2(fxRect.anchoredPosition.x, -(top + whoRect.sizeDelta.y + lineGap) + TextSlack);
        }

        // 그림을 칸(정사각형)에 꽉 채우는 uv — 남는 쪽을 자른다. 세로가 남으면 artFocusY 자리를 보인다.
        private Rect CoverUv(Sprite picture)
        {
            Rect area = picture.textureRect;
            Vector2 size = new Vector2(picture.texture.width, picture.texture.height);
            RectTransform box = art.rectTransform;
            float boxAspect = box.rect.height > 0f ? box.rect.width / box.rect.height : 1f;
            float aspect = area.width / area.height;

            Rect cut = area;
            if (aspect > boxAspect)
            {
                cut.width = area.height * boxAspect;
                cut.x = area.x + (area.width - cut.width) / 2f;
            }
            else
            {
                cut.height = area.width / boxAspect;
                cut.y = area.y + (area.height - cut.height) * (1f - artFocusY); // uv 는 아래가 0
            }

            return new Rect(cut.x / size.x, cut.y / size.y, cut.width / size.x, cut.height / size.y);
        }
    }
}
