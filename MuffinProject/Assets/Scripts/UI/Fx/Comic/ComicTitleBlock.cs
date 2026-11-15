using TMPro;
using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 모든 만화 컷인 공통 — 화면 왼쪽 아래의 제목 + 한마디 (시안 titleBlock: 색 띠가 쓱 지나가고, 제목이 찍히고, 한마디 상자가 미끄러져 들어온다).
    /// 이 RectTransform 의 피벗 = 시안 (x0, y0) — 보이는 화면 왼쪽 아래에서 오른쪽 60, 레터박스 위 74 (자리는 <see cref="ComicCutInFx"/> 가 맞춘다).
    /// 글자 · 색은 장면 값으로 <see cref="SetTexts"/>, 매 프레임 <see cref="Draw"/>(τ = 장면 시각 − titleAt).
    /// 장면 light 색(시안 제목 fill 의 아래 색)은 시안 캔버스에서 보이지 않아 쓰지 않는다 (<see cref="ComicWord"/> 참고).
    /// </summary>
    public class ComicTitleBlock : MonoBehaviour
    {
        // 시안 titleBlock: 제목 그림 크기 0.82, 띠 길이 = 제목 그림 너비·0.82 + 70, 제목 자리 = 그림 가운데·0.82 − 10
        private const float TitleScale = 0.82f;
        private const float BandExtra = 70f;
        private const float TitleShift = -10f;
        // 시안 tagSprite: 상자 너비 = 글자 폭 + 64, 그림 = 상자 + 16, 글자 시작 = 상자 왼쪽 + 38, 높이 = 글자 크기·1.9
        private const float TagExtra = 64f;
        private const float TagMargin = 16f;
        private const float TagTextLeft = 38f;
        private const float TagHeightRatio = 1.9f;
        private const float TagTextDrop = 1f;
        // 시안 titleBlock: 한마디 = (그림 가운데·0.92 − 16 + lerp(−60, 0, eo(k2)), −제목 그림 높이·0.82·0.5 − 30), 크기 0.92
        private const float TagScale = 0.92f;
        private const float TagShift = -16f;
        private const float TagSlide = -60f;
        private const float TagRise = -30f;

        [Tooltip("붓질 띠.")]
        [SerializeField] private ComicTitleBand band;
        [Tooltip("제목 글자 (찍히기 = 같은 오브젝트의 ComicStamp).")]
        [SerializeField] private ComicWord title;
        [Tooltip("한마디 묶음 — 투명도는 CanvasGroup.")]
        [SerializeField] private CanvasGroup tagGroup;
        [Tooltip("한마디 상자.")]
        [SerializeField] private ComicTagBox tagBox;
        [Tooltip("한마디 글자.")]
        [SerializeField] private TMP_Text tagLabel;
        [Tooltip("띠가 다 뻗는 시간 (초, 시안 0.22).")]
        [SerializeField] private float bandTime = 0.22f;
        [Tooltip("제목이 찍히는 시간 (초, 시안 0.3).")]
        [SerializeField] private float titleTime = 0.3f;
        [Tooltip("한마디가 시작하는 때 (제목 뒤 초, 시안 0.16).")]
        [SerializeField] private float tagDelay = 0.16f;
        [Tooltip("한마디가 미끄러져 들어오는 시간 (초, 시안 0.3).")]
        [SerializeField] private float tagTime = 0.3f;
        [Tooltip("한마디가 다 보이는 빠르기 (시안 3).")]
        [SerializeField] private float tagFade = 3f;

        private ComicStamp _titleStamp;
        private float _tagRestX;

        private ComicStamp TitleStamp => _titleStamp != null ? _titleStamp : _titleStamp = title.GetComponent<ComicStamp>();

        /// <summary>제목 · 한마디 · 색을 넣고 자리를 다시 잡는다 (시안 titleSprite · tagSprite). 에디터에서도 부를 수 있다 (프리팹 미리 보기).</summary>
        public void SetTexts(string titleText, string tagText, Color accent, Color deep)
        {
            title.Text = titleText;
            title.InnerColor = accent;
            title.DepthColor = deep;
            band.SetColors(accent, deep);
            band.Length = title.SpriteWidth * TitleScale + BandExtra;
            TitleStamp.RestPosition = new Vector2(title.SpriteWidth / 2f * TitleScale + TitleShift, 0f);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchoredPosition = new Vector2(TitleStamp.RestPosition.x, 0f);

            tagLabel.text = tagText;
            tagBox.Accent = accent;
            float fontSize = tagLabel.fontSize;
            float textWidth = tagLabel.GetPreferredValues(tagText).x;
            var boxSize = new Vector2(textWidth + TagExtra, fontSize * TagHeightRatio);
            tagBox.Size = boxSize;
            Vector2 topLeft = tagBox.TopLeft;
            var labelRect = tagLabel.rectTransform;
            labelRect.anchoredPosition = new Vector2(topLeft.x + TagTextLeft, -(topLeft.y + boxSize.y / 2f + TagTextDrop));
            _tagRestX = (boxSize.x + TagMargin) / 2f * TagScale + TagShift;
            var tagRect = (RectTransform)tagGroup.transform;
            tagRect.anchoredPosition = new Vector2(_tagRestX, -(-title.SpriteHeight * TitleScale * 0.5f + TagRise));
        }

        /// <summary>제목 등장 시각 τ (초, 장면 시각 − titleAt) 의 모습. τ ≤ 0 이면 안 보인다.</summary>
        public void Draw(float tau)
        {
            if (tau <= 0f)
            {
                band.Progress = 0f;
                TitleStamp.Apply(0f);
                tagGroup.alpha = 0f;
                return;
            }
            band.Progress = ComicKit.Eo(tau / bandTime);
            TitleStamp.Apply(tau / titleTime);
            float k = (tau - tagDelay) / tagTime;
            if (k <= 0f)
            {
                tagGroup.alpha = 0f;
                return;
            }
            tagGroup.alpha = ComicKit.Cl(k * tagFade);
            var tagRect = (RectTransform)tagGroup.transform;
            tagRect.anchoredPosition = new Vector2(_tagRestX + ComicKit.Lerp(TagSlide, 0f, ComicKit.Eo(k)), tagRect.anchoredPosition.y);
        }

        private void Awake()
        {
            var tagRect = (RectTransform)tagGroup.transform;
            _tagRestX = tagRect.anchoredPosition.x;
        }
    }
}
