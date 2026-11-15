using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Post
{
    /// <summary>
    /// 「파헤치기」 4 단계 장식 — 대상의 함정 카드가 깨진다 (시안 shatter): 파편 · 「파괴!」 · 카드가 커지고 기울며 사라진다. 3 단계는 없다 (10-ui 5-1).
    /// 연출은 함정 카드가 놓인 자리를 모른다 — 대상 자리 옆(시안 .vw-trap 자리)에 카드 뒷면을 직접 놓고 깨뜨린다.
    /// 시안: 아티팩트 「찹츄 카드 이펙트 v2」 FXA 파헤치기 · shatter · setTrap.
    /// </summary>
    public class TrapShatterFx : CardFx
    {
        [Tooltip("깨질 함정 카드 (그림자 · 뒷면 · 테두리). 시작은 투명, 크기는 재생할 때 아래 크기로 맞춘다.")]
        [SerializeField] private CanvasGroup trap;
        [Tooltip("함정 카드 크기 (시안 px — 시안 .vw-trap 62 × 92). 재생할 때 연출 레이어 배율(PixelScale)을 곱한다.")]
        [SerializeField] private Vector2 trapSize = new Vector2(62f, 92f);
        [Tooltip("함정 카드 뒷면 그림 — 재생할 때 카드 뒷면(CardFxContext.CardBack)을 넣는다.")]
        [SerializeField] private Image back;
        [Tooltip("대상 자리 가장자리와 함정 카드 사이 (시안 px — 시안 .vw-trap 의 182px − 자리 너비 170px).")]
        [SerializeField] private float gap = 12f;

        [Header("파편 (시안 shatter 의 burst)")]
        [Tooltip("파편 개수.")]
        [SerializeField] private int burstCount = 36;
        [Tooltip("파편 색 — 파편마다 이 중 하나를 무작위로 고른다.")]
        [SerializeField] private Color[] burstColors = { new Color32(0x8a, 0x6b, 0x5a, 0xff), Color.white, new Color32(0xff, 0xd2, 0x3f, 0xff) };
        [Tooltip("파편 속도 범위 (시안 px/초, 최소 · 최대).")]
        [SerializeField] private Vector2 burstSpeed = new Vector2(200f, 560f);
        [Tooltip("파편이 떨어지는 가속 (시안 px/초²).")]
        [SerializeField] private float burstGravity = 700f;

        [Header("터짐 글자")]
        [Tooltip("함정 카드 자리에 터지는 글자.")]
        [SerializeField] private string boomText = "파괴!";
        [Tooltip("터짐 글자 색.")]
        [SerializeField] private Color boomColor = new Color32(0xff, 0x4f, 0x2e, 0xff);

        [Header("카드가 사라짐 (시안 300ms, 일정한 속도)")]
        [Tooltip("깨지기 시작해서 카드가 다 사라질 때까지 (ms, 시안 300ms).")]
        [SerializeField] private float vanishMs = 300f;
        [Tooltip("사라질 때까지 커지는 배율.")]
        [SerializeField] private float vanishScale = 1.3f;
        [Tooltip("사라질 때까지 기우는 각도 (도, 시안 rotate — + 가 시계 방향).")]
        [SerializeField] private float vanishAngle = 12f;
        [Tooltip("깨지기 시작해서 장식이 끝날 때까지 (ms, 시안 s4 의 wait).")]
        [SerializeField] private float holdMs = 500f;

        private void Awake()
        {
            if (trap == null || back == null || burstColors == null || burstColors.Length == 0)
                Debug.LogError($"[{nameof(TrapShatterFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            FxLayer layer = context.Layer;
            var card = (RectTransform)trap.transform;
            back.sprite = context.CardBack;
            card.sizeDelta = trapSize * layer.PixelScale;
            card.anchoredPosition = TrapSpot(context.MainTarget.Anchor, card.sizeDelta, gap * layer.PixelScale);
            trap.alpha = 1f;

            Vector2 center = layer.CenterOf(card);
            layer.Burst(center, burstCount, burstColors, burstSpeed, burstGravity);
            layer.Boom(layer.BoomPoint(center), boomColor, boomText);

            float seconds = vanishMs / 1000f;
            DOTween.Sequence().SetLink(card.gameObject)
                .Join(card.DOScale(vanishScale, seconds).SetEase(Ease.Linear))
                // 시안은 시계 방향 + — 유니티는 반시계 방향 +
                .Join(card.DOLocalRotate(new Vector3(0f, 0f, -vanishAngle), seconds).SetEase(Ease.Linear))
                .Join(trap.DOFade(0f, seconds).SetEase(Ease.Linear));

            yield return new WaitForSeconds(holdMs / 1000f);
        }

        // 시안 .vw-trap.foe(오른쪽 위 자리) = 자리 왼쪽 · 위 맞춤, .vw-trap.me(왼쪽 아래 자리) = 자리 오른쪽 · 아래 맞춤.
        // 판 가운데 쪽 옆에 붙이고, 위쪽 자리면 위를, 아래쪽 자리면 아래를 맞춘다. 좌표는 이 연출(화면 전체) 기준
        private Vector2 TrapSpot(RectTransform seat, Vector2 size, float spacing)
        {
            var root = (RectTransform)transform;
            Vector2 min = root.InverseTransformPoint(seat.TransformPoint(seat.rect.min));
            Vector2 max = root.InverseTransformPoint(seat.TransformPoint(seat.rect.max));
            Vector2 middle = root.rect.center;
            Vector2 seatCenter = (min + max) / 2f;

            float x = seatCenter.x > middle.x ? min.x - spacing - size.x / 2f : max.x + spacing + size.x / 2f;
            float y = seatCenter.y > middle.y ? max.y - size.y / 2f : min.y + size.y / 2f;
            return new Vector2(x, y);
        }
    }
}
