using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 투명도 바꾸기 — 대상(CanvasGroup 이 있으면 판째, 없으면 Graphic 하나)을 정한 투명도로 바꾸고, 머무는 시간이 있으면 그 뒤 사라진다.
    /// 암전 · 화면 어둡게(공통 프리팹 Blackout) · 남은 빛 · 컷인 사라짐 같은 데 쓴다.
    /// </summary>
    public class FadeFx : CardFxStep
    {
        [Tooltip("바꿀 오브젝트. 비우면 이 오브젝트.")]
        [SerializeField] private GameObject target;
        [Tooltip("재생 전에 맞춰 둘 투명도. 0 미만이면 건드리지 않는다.")]
        [SerializeField] private float resetAlpha;
        [Tooltip("바꿀 투명도.")]
        [Range(0f, 1f)]
        [SerializeField] private float alpha = 1f;
        [Tooltip("바뀌는 시간 (ms). 0 = 바로.")]
        [SerializeField] private float inMs = 40f;
        [Tooltip("바뀐 채 머무는 시간 (ms). 0 미만 = 계속 머문다.")]
        [SerializeField] private float holdMs = -1f;
        [Tooltip("머문 뒤 투명해지는 시간 (ms). 0 = 바로.")]
        [SerializeField] private float outMs;

        private CanvasGroup _group;
        private Graphic _graphic;
        private bool _found;

        public override float DurationMs => holdMs < 0f ? inMs : inMs + holdMs + outMs;

        public override RectTransform Visual => (RectTransform)(target != null ? target.transform : transform);

        private void Awake()
        {
            Find();
            if (_group == null && _graphic == null)
                Debug.LogError($"[{nameof(FadeFx)}] 바꿀 오브젝트에 CanvasGroup 이나 Graphic 이 없습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            Find();
            Kill();
            if (resetAlpha < 0f)
                return;
            if (_group != null)
                _group.alpha = resetAlpha;
            else
                SetAlpha(_graphic, resetAlpha);
        }

        public override void Play(CardFxContext context, RectTransform targetSeat)
        {
            Find();
            Kill();
            Sequence sequence = DOTween.Sequence().SetLink(gameObject).Append(Fade(alpha, inMs));
            if (holdMs >= 0f)
                sequence.AppendInterval(Seconds(holdMs)).Append(Fade(0f, outMs));
        }

        // 꺼진 오브젝트는 Awake 가 늦게 돌 수 있어 쓸 때 찾는다
        private void Find()
        {
            if (_found)
                return;
            _found = true;
            GameObject owner = target != null ? target : gameObject;
            _group = owner.GetComponent<CanvasGroup>();
            _graphic = owner.GetComponent<Graphic>();
        }

        private void Kill()
        {
            if (_group != null)
                _group.DOKill();
            else
                _graphic.DOKill();
        }

        private Tween Fade(float to, float ms)
        {
            return _group != null ? _group.DOFade(to, Seconds(ms)) : _graphic.DOFade(to, Seconds(ms));
        }
    }
}
