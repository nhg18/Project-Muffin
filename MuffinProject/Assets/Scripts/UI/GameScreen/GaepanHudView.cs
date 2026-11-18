using Chapchu.UI.Fx;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 개판 오분전 자명종 (프로토타입 v2 .gp-hud · gpClock — 1021). 크림색 스티커 + 5칸 자명종 + 「개판 오분전 N 바퀴 남음」.
    /// 한 바퀴마다 분홍 칸이 하나 비고 바늘이 한 칸 넘어간다 · 숫자가 튄다 · 시계가 흔들린다. 마지막 바퀴는 분홍 바탕 + 따르릉 흔들림 (깜빡임 없음).
    /// v2 는 판 효과 줄 대신 왼쪽 위(턴 순서 띠 아래) — 자리 고정이라 나타날 때 미끄러지지 않는다 (v2 의 transform:none). 숨길 때만 0.3초 흐려진다.
    /// </summary>
    public class GaepanHudView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [Tooltip("크림색 바탕 — 마지막 바퀴면 분홍.")]
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text unitText;
        [Tooltip("자명종 전체 — 흔들림의 중심(시안 50% 62%)이 피벗.")]
        [SerializeField] private RectTransform clock;
        [Tooltip("분홍 칸 — 위(12시)부터 시계 방향.")]
        [SerializeField] private UIPie[] segments;
        [Tooltip("바늘 — 피벗이 시계 가운데.")]
        [SerializeField] private RectTransform hand;

        [Header("색")]
        [Tooltip("평소 바탕색 (크림) — 마지막 바퀴에는 「개판 오분전」 글자색.")]
        [SerializeField] private Color cream = new Color32(0xFF, 0xFC, 0xF6, 0xFF);
        [Tooltip("마지막 바퀴 바탕색 (분홍).")]
        [SerializeField] private Color hotFill = new Color32(0xFF, 0x7E, 0xA4, 0xFF);
        [Tooltip("평소 「개판 오분전」 · 「바퀴 남음」 글자색.")]
        [SerializeField] private Color ink = new Color32(0x2B, 0x1E, 0x1A, 0xFF);
        [Tooltip("평소 남은 바퀴 숫자 색.")]
        [SerializeField] private Color number = new Color32(0xFF, 0x5C, 0x8A, 0xFF);
        [Tooltip("마지막 바퀴 숫자 색 (노랑).")]
        [SerializeField] private Color hotNumber = new Color32(0xFF, 0xE2, 0x7A, 0xFF);
        [Tooltip("남은 바퀴 칸 색.")]
        [SerializeField] private Color segmentOn = new Color32(0xFF, 0x7E, 0xA4, 0xFF);
        [Tooltip("지난 바퀴 칸 색 (빈 칸).")]
        [SerializeField] private Color segmentOff = new Color32(0xF1, 0xE2, 0xCF, 0xFF);
        [Tooltip("평소 「바퀴 남음」 글자 투명도 (색은 「개판 오분전」 글자와 같다).")]
        [SerializeField] private float unitAlpha = 0.7f;
        [Tooltip("마지막 바퀴 「바퀴 남음」 글자 투명도.")]
        [SerializeField] private float hotUnitAlpha = 0.9f;

        [Header("한 바퀴 지날 때 (시안 GP_FX.tick)")]
        [Tooltip("숫자가 튀었다 돌아오는 초 — 시계 흔들림 · 칸 비우기도 이 시간에 맞춘다.")]
        [SerializeField] private float popSeconds = 0.42f;
        [Tooltip("숫자가 튀어 오를 때 크기 배율.")]
        [SerializeField] private float numberPopScale = 1.9f;
        [Tooltip("숫자가 튀어 오를 때 Z 회전 (도).")]
        [SerializeField] private float numberPopAngle = 10f;
        [Tooltip("시계 흔들림 Z 회전 순서 (도) — 시안 rotate(−14 → 12 → −8 → 0), 시계 방향이 + 라 뒤집었다. popSeconds 를 칸 수로 나눠 하나씩.")]
        [SerializeField] private float[] wobbleAngles = { 14f, -12f, 8f, 0f };
        [Tooltip("비워지는 칸이 처음 커지는 배율.")]
        [SerializeField] private float segmentPopScale = 1.25f;
        [Tooltip("비워지는 칸이 줄어드는 배율 — 그 뒤 1 로 돌아온다.")]
        [SerializeField] private float segmentSqueezeScale = 0.6f;
        [Tooltip("바늘이 한 칸 넘어가는 초.")]
        [SerializeField] private float handSeconds = 0.5f;

        [Header("마지막 바퀴 따르릉 (시안 gpRing)")]
        [Tooltip("따르릉 한 번의 주기 초.")]
        [SerializeField] private float ringSeconds = 1.5f;
        [Tooltip("흔들림 한 칸(아래 각도 하나)이 주기에서 차지하는 비율 — 나머지 시간은 멈춰 있다.")]
        [SerializeField] private float ringStepPortion = 0.04f;
        [Tooltip("따르릉 흔들림 Z 회전 순서 (도).")]
        [SerializeField] private float[] ringAngles = { 15f, -13f, 12f, -10f, 7f, -5f, 3f, -2f, 0f };

        [Tooltip("숨길 때 흐려지는 초.")]
        [SerializeField] private float hideSeconds = 0.3f;

        private int _total = 1;
        private Tween _ring;

        public bool IsShown => gameObject.activeSelf;

        private void Awake()
        {
            if (group == null || fill == null || labelText == null || numberText == null || unitText == null || clock == null || hand == null || segments == null || segments.Length == 0)
                Debug.LogError($"[{nameof(GaepanHudView)}] 참조가 비어 있습니다.", this);
        }

        /// <summary>바로 보인다 (남은 바퀴 left, 칸 수 total).</summary>
        public void Show(int left, int total)
        {
            KillTweens();
            _total = Mathf.Max(1, total);
            gameObject.SetActive(true);
            group.alpha = 1f;
            numberText.text = Mathf.Max(0, left).ToString();
            Dial(left, false);
            SetHot(left <= 1);
        }

        /// <summary>한 바퀴 지났다 — 칸이 비고 숫자가 튀고 시계가 흔들린다 (시안 GP_FX.tick).</summary>
        public void Tick(int left)
        {
            if (!IsShown)
            {
                Show(left, _total);
                return;
            }

            numberText.text = Mathf.Max(0, left).ToString();
            Dial(left, true);

            var numberRect = numberText.rectTransform;
            numberRect.DOKill();
            numberRect.localScale = Vector3.one * numberPopScale;
            numberRect.localRotation = Quaternion.Euler(0f, 0f, numberPopAngle);
            numberRect.DOScale(1f, popSeconds).SetEase(Ease.OutBack);
            numberRect.DOLocalRotate(Vector3.zero, popSeconds).SetEase(Ease.OutBack);

            if (left > 1)
            {
                clock.DOKill();
                clock.localRotation = Quaternion.identity;
                Sequence wobble = DOTween.Sequence().SetTarget(clock);
                foreach (float angle in wobbleAngles)
                    wobble.Append(clock.DOLocalRotate(new Vector3(0f, 0f, angle), popSeconds / wobbleAngles.Length));
            }
            SetHot(left <= 1);
        }

        /// <summary>숨긴다. animate 면 0.3초 흐려진다.</summary>
        public void Hide(bool animate)
        {
            KillTweens();
            if (!IsShown)
                return;
            if (!animate)
            {
                gameObject.SetActive(false);
                return;
            }
            group.DOFade(0f, hideSeconds).SetTarget(this).OnComplete(() => gameObject.SetActive(false));
        }

        // 지난 바퀴 수만큼 칸을 비우고 바늘을 넘긴다 (시안 dial)
        private void Dial(int left, bool animate)
        {
            int used = _total - Mathf.Max(0, left);
            for (int i = 0; i < segments.Length; i++)
            {
                bool off = i < used;
                UIPie segment = segments[i];
                bool wasOff = segment.color == segmentOff;
                segment.color = off ? segmentOff : segmentOn;
                if (off && !wasOff && animate)
                {
                    var rect = segment.rectTransform;
                    rect.DOKill();
                    rect.localScale = Vector3.one * segmentPopScale;
                    DOTween.Sequence().SetTarget(rect)
                        .Append(rect.DOScale(segmentSqueezeScale, popSeconds / 2f).SetEase(Ease.OutQuad))
                        .Append(rect.DOScale(1f, popSeconds / 2f).SetEase(Ease.OutQuad));
                }
            }

            float angle = -used * 360f / _total;
            hand.DOKill();
            if (animate)
                hand.DOLocalRotate(new Vector3(0f, 0f, angle), handSeconds).SetEase(Ease.OutBack);
            else
                hand.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // 마지막 바퀴 — 분홍 바탕 · 크림 글자 · 노랑 숫자, 자명종이 1.5초마다 따르릉 (시안 gpRing: 처음 32% 동안 −15 → 13 → … → 0)
        private void SetHot(bool hot)
        {
            fill.color = hot ? hotFill : cream;
            labelText.color = hot ? cream : ink;
            unitText.color = new Color(labelText.color.r, labelText.color.g, labelText.color.b, hot ? hotUnitAlpha : unitAlpha);
            numberText.color = hot ? hotNumber : number;

            if (!hot)
            {
                _ring?.Kill();
                _ring = null;
                return;
            }
            if (_ring != null)
                return;

            clock.DOKill();
            clock.localRotation = Quaternion.identity;
            float step = ringSeconds * ringStepPortion;
            Sequence ring = DOTween.Sequence().SetTarget(clock);
            foreach (float angle in ringAngles)
                ring.Append(clock.DOLocalRotate(new Vector3(0f, 0f, angle), step));
            ring.AppendInterval(ringSeconds - step * ringAngles.Length);
            _ring = ring.SetLoops(-1);
        }

        private void KillTweens()
        {
            DOTween.Kill(this);
            group.DOKill();
            _ring?.Kill();
            _ring = null;
            clock.DOKill();
            clock.localRotation = Quaternion.identity;
        }
    }
}
