using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 맨 위층의 알림 · 손맛 — 말풍선 알림(3초 · 누르면 닫힘 · 한 번에 하나) · 터짐(「입장!」 · 「개판!」) · 재화 「+N」.
    /// 알림은 새 것이 앞 것을 갈아 끼우고, 창이 떠 있는 동안은 위쪽 HUD 줄 가운데로 올라가 창 내용을 가리지 않는다.
    /// 기준: 「통합 화면 UI v2」 키트 toast · fxBurst, 로비 gainFx · 「통합 화면 UI v3」 v3.25 알림 규칙
    /// </summary>
    public class LobbyFxView : MonoBehaviour
    {
        [Header("알림")]
        [Tooltip("알림이 놓이는 자리 — 평소 자리는 씬에 놓인 그대로, 창이 떠 있으면 위쪽 HUD 줄 가운데로 옮긴다.")]
        [SerializeField] private RectTransform toastArea;
        [SerializeField] private RectTransform toast;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Image toastFill;
        [SerializeField] private Image toastTail;
        [SerializeField] private Button toastButton;

        [Header("터짐")]
        [SerializeField] private RectTransform burst;
        [SerializeField] private CanvasGroup burstGroup;
        [SerializeField] private Image burstImage;
        [SerializeField] private TMP_Text burstText;

        [Header("재화 +N")]
        [SerializeField] private TMP_Text[] gains;

        [Tooltip("「+N」 이 뜨는 자리 — 재화 칸 위 끝에서 아래로 (캔버스 px)")]
        [SerializeField] private float gainDrop = 92f;

        [Header("연출")]
        [Tooltip("알림 말풍선이 떠 있는 시간 (초)")]
        [SerializeField] private float toastSeconds = 3f;

        [Tooltip("알림 말풍선이 튀어나오는 시간 (초)")]
        [SerializeField] private float toastPopSeconds = 0.25f;

        [Tooltip("나쁜 소식 알림의 바탕색")]
        [SerializeField] private Color badToastFill = LobbyStyle.HpFill;

        [Tooltip("창이 떠 있는 동안 알림 위 끝의 자리 — 캔버스 위 끝에서 (px). 위쪽 HUD 줄 가운데.")]
        [SerializeField] private float toastHudTop = 72f;

        [Tooltip("터짐 그림이 커졌다 사라지기까지 (초)")]
        [SerializeField] private float burstSeconds = 0.7f;

        [Tooltip("터짐 그림(fx_burst)이 기준 크기의 몇 배 판에 그려져 있는지")]
        [SerializeField] private float burstSpriteScale = 1.25f;

        [Tooltip("터짐 글자 크기 = 터짐 크기 × 이 값")]
        [SerializeField] private float burstFontScale = 0.3f;

        [Tooltip("터짐 시간 비율 — 커짐 · 제 크기로 · 머묾 · 사라짐 (터짐 시간에 곱한다, 합 1)")]
        [SerializeField] private Vector4 burstPhaseRatios = new Vector4(0.3f, 0.2f, 0.3f, 0.2f);

        [Tooltip("터짐이 시작할 때의 크기 (배) · 기울기 (도)")]
        [SerializeField] private Vector2 burstStartScaleAndAngle = new Vector2(0.2f, 14f);

        [Tooltip("터짐이 가장 커질 때의 크기 (배) · 기울기 (도)")]
        [SerializeField] private Vector2 burstPeakScaleAndAngle = new Vector2(1.14f, -4f);

        [Tooltip("터짐이 머무는 동안 · 사라질 때 천천히 커지는 크기 (배)")]
        [SerializeField] private Vector2 burstHoldAndEndScale = new Vector2(1.03f, 1.06f);

        [Tooltip("「+N」 이 떠올랐다 사라지기까지 (초)")]
        [SerializeField] private float gainSeconds = 1.1f;

        [Tooltip("「+N」 시간 비율 — 나타남 · 머묾 · 사라짐 (「+N」 시간에 곱한다, 합 1)")]
        [SerializeField] private Vector3 gainPhaseRatios = new Vector3(0.25f, 0.45f, 0.3f);

        [Tooltip("「+N」 이 각 단계 끝에 처음 자리에서 내려가 있는 거리 — 나타남 · 머묾 · 사라짐 (캔버스 px)")]
        [SerializeField] private Vector3 gainFallDistances = new Vector3(16f, 36f, 56f);

        [Tooltip("「+N」 이 시작할 때 · 나타남 끝의 크기 (배) — 머무는 동안 1 로 돌아온다")]
        [SerializeField] private Vector2 gainStartAndPeakScale = new Vector2(0.6f, 1.1f);

        private Coroutine _toastLife;
        private Vector2 _toastAnchor; // 평소 자리 — 씬에 놓인 그대로
        private Vector2 _toastPosition;
        private bool _toastOverHud;
        private int _nextGain;

        private void Awake()
        {
            if (toastArea == null || toast == null || toastText == null || toastFill == null || toastTail == null || toastButton == null)
                Debug.LogError($"[{nameof(LobbyFxView)}] 알림 참조가 비어 있습니다.", this);

            if (burst == null || burstGroup == null || burstImage == null || burstText == null || gains == null || gains.Length == 0)
                Debug.LogError($"[{nameof(LobbyFxView)}] 터짐 · 재화 참조가 비어 있습니다.", this);

            _toastAnchor = toastArea.anchorMin;
            _toastPosition = toastArea.anchoredPosition;
        }

        private void OnEnable()
        {
            toastButton.onClick.AddListener(HideToast);
        }

        private void OnDisable()
        {
            toastButton.onClick.RemoveListener(HideToast);
            HideToast();

            burst.DOKill();
            burst.gameObject.SetActive(false);
            foreach (TMP_Text gain in gains)
            {
                gain.rectTransform.DOKill(); // 「+N」 연출(Sequence)의 타깃
                gain.DOKill();
                gain.gameObject.SetActive(false);
            }
        }

        /// <summary>말풍선 알림 한 줄 — 떠 있던 알림은 바로 갈아 끼운다. bad 면 붉은 바탕 (실패).</summary>
        public void Toast(string message, bool bad = false)
        {
            HideToast();

            Color fill = bad ? badToastFill : Color.white;
            toastText.text = message;
            toastFill.color = fill;
            toastTail.color = fill;
            toast.gameObject.SetActive(true);
            toast.localScale = Vector3.zero;
            toast.DOScale(1f, toastPopSeconds).SetEase(Ease.OutBack).SetUpdate(true);
            _toastLife = StartCoroutine(ToastRoutine());
        }

        /// <summary>창(팝업 · 확인 창 · 뽑기 결과)이 떠 있으면 알림을 위쪽 HUD 줄 가운데로 — 창은 그 줄 아래에만 놓여 내용을 가리지 않는다.</summary>
        public void SetToastOverHud(bool overHud)
        {
            if (overHud == _toastOverHud)
                return;

            _toastOverHud = overHud;
            toastArea.anchorMin = toastArea.anchorMax = overHud ? new Vector2(0.5f, 1f) : _toastAnchor;
            toastArea.anchoredPosition = overHud ? new Vector2(0f, -toastHudTop) : _toastPosition;
        }

        /// <summary>터짐 — worldPosition 을 가운데로, size 는 캔버스 px (시안 크기 × 2).</summary>
        public void Burst(Vector3 worldPosition, string text, float size, Color fill)
        {
            burst.DOKill();
            burst.gameObject.SetActive(true);
            burst.position = worldPosition;
            burst.sizeDelta = Vector2.one * size * burstSpriteScale;
            burstImage.color = fill;
            burstText.text = text;
            burstText.fontSize = size * burstFontScale;
            burstGroup.alpha = 1f;
            burst.localScale = Vector3.one * burstStartScaleAndAngle.x;
            burst.localEulerAngles = new Vector3(0f, 0f, burstStartScaleAndAngle.y);

            DOTween.Sequence().SetTarget(burst).SetUpdate(true)
                .Append(burst.DOScale(burstPeakScaleAndAngle.x, burstSeconds * burstPhaseRatios.x).SetEase(Ease.OutCubic))
                .Join(burst.DOLocalRotate(new Vector3(0f, 0f, burstPeakScaleAndAngle.y), burstSeconds * burstPhaseRatios.x).SetEase(Ease.OutCubic))
                .Append(burst.DOScale(1f, burstSeconds * burstPhaseRatios.y))
                .Join(burst.DOLocalRotate(Vector3.zero, burstSeconds * burstPhaseRatios.y))
                .Append(burst.DOScale(burstHoldAndEndScale.x, burstSeconds * burstPhaseRatios.z))
                .Append(burst.DOScale(burstHoldAndEndScale.y, burstSeconds * burstPhaseRatios.w))
                .Join(burstGroup.DOFade(0f, burstSeconds * burstPhaseRatios.w))
                .OnComplete(() => burst.gameObject.SetActive(false));
        }

        /// <summary>화면 가운데에서 offset 만큼 떨어진 곳에 터짐 (캔버스 px).</summary>
        public void BurstAtCenter(Vector2 offset, string text, float size, Color fill)
        {
            var root = (RectTransform)transform;
            Burst(root.TransformPoint((Vector3)(root.rect.center + offset)), text, size, fill);
        }

        /// <summary>재화 칸 아래에 「+N」 (빼면 빨강 「-N」) 이 떴다 내려가며 사라지고, 칸이 톡 튄다.</summary>
        public void Gain(RectTransform coin, int amount)
        {
            if (amount == 0)
                return;

            TMP_Text gain = gains[_nextGain];
            _nextGain = (_nextGain + 1) % gains.Length;

            var corners = new Vector3[4];
            coin.GetWorldCorners(corners);
            RectTransform rect = gain.rectTransform;
            rect.DOKill();
            gain.DOKill();
            gain.gameObject.SetActive(true);
            gain.text = (amount > 0 ? "+" : string.Empty) + LobbyStyle.Number(amount);
            gain.color = amount > 0 ? LobbyStyle.Ok : LobbyStyle.Hp;
            gain.alpha = 0f;
            rect.position = (corners[1] + corners[2]) * 0.5f;
            Vector2 start = rect.anchoredPosition + new Vector2(0f, -gainDrop);
            rect.anchoredPosition = start;
            rect.localScale = Vector3.one * gainStartAndPeakScale.x;

            DOTween.Sequence().SetTarget(rect).SetUpdate(true)
                .Append(rect.DOAnchorPos(start + new Vector2(0f, -gainFallDistances.x), gainSeconds * gainPhaseRatios.x).SetEase(Ease.OutQuad))
                .Join(rect.DOScale(gainStartAndPeakScale.y, gainSeconds * gainPhaseRatios.x))
                .Join(gain.DOFade(1f, gainSeconds * gainPhaseRatios.x))
                .Append(rect.DOAnchorPos(start + new Vector2(0f, -gainFallDistances.y), gainSeconds * gainPhaseRatios.y))
                .Join(rect.DOScale(1f, gainSeconds * gainPhaseRatios.y))
                .Append(rect.DOAnchorPos(start + new Vector2(0f, -gainFallDistances.z), gainSeconds * gainPhaseRatios.z))
                .Join(gain.DOFade(0f, gainSeconds * gainPhaseRatios.z))
                .OnComplete(() => gain.gameObject.SetActive(false));

            UIJuice.Pop(coin, 0.9f);
        }

        private void HideToast()
        {
            if (_toastLife != null)
                StopCoroutine(_toastLife);

            _toastLife = null;
            toast.DOKill();
            toast.gameObject.SetActive(false);
        }

        private IEnumerator ToastRoutine()
        {
            yield return new WaitForSecondsRealtime(toastSeconds);
            _toastLife = null;
            HideToast();
        }
    }
}
