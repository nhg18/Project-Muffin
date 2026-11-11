using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 맨 위층의 알림 · 손맛 — 말풍선 알림(3초 · 누르면 닫힘 · 새 것이 위) · 터짐(「입장!」 · 「개판!」) · 재화 「+N」.
    /// 기준: 「통합 화면 UI v2」 키트 toast · fxBurst, 로비 gainFx
    /// </summary>
    public class LobbyFxView : MonoBehaviour
    {
        private const float ToastSeconds = 3f;
        private const float ToastPopSeconds = 0.25f;
        private const float BurstSeconds = 0.7f;
        private const float GainSeconds = 1.1f;

        // 터짐 그림(fx_burst)은 기준 크기의 1.25배 판에 그려져 있다 · 글자 = 크기 × 0.3
        private const float BurstSpriteScale = 1.25f;
        private const float BurstFontScale = 0.3f;

        private static readonly Color BadToastFill = new Color32(0xFF, 0xF1, 0xEF, 0xFF);

        [Header("알림")]
        [SerializeField] private RectTransform[] toasts;
        [SerializeField] private TMP_Text[] toastTexts;
        [SerializeField] private Image[] toastFills;
        [SerializeField] private Image[] toastTails;
        [SerializeField] private Button[] toastButtons;

        [Header("터짐")]
        [SerializeField] private RectTransform burst;
        [SerializeField] private CanvasGroup burstGroup;
        [SerializeField] private Image burstImage;
        [SerializeField] private TMP_Text burstText;

        [Header("재화 +N")]
        [SerializeField] private TMP_Text[] gains;

        [Tooltip("「+N」 이 뜨는 자리 — 재화 칸 위 끝에서 아래로 (캔버스 px)")]
        [SerializeField] private float gainDrop = 92f;

        private Coroutine[] _toastLives;
        private UnityAction[] _toastClicks; // 말풍선마다 「누르면 닫힘」 — 붙인 것을 그대로 떼려고 들고 있는다
        private int _nextGain;

        private void Awake()
        {
            if (toasts == null || toasts.Length == 0 || toastTexts == null || toastTexts.Length != toasts.Length || toastFills == null || toastFills.Length != toasts.Length
                || toastTails == null || toastTails.Length != toasts.Length || toastButtons == null || toastButtons.Length != toasts.Length)
                Debug.LogError($"[{nameof(LobbyFxView)}] 알림 참조가 비어 있습니다.", this);

            if (burst == null || burstGroup == null || burstImage == null || burstText == null || gains == null || gains.Length == 0)
                Debug.LogError($"[{nameof(LobbyFxView)}] 터짐 · 재화 참조가 비어 있습니다.", this);

            _toastLives = new Coroutine[toasts.Length];
            _toastClicks = new UnityAction[toasts.Length];
            for (int i = 0; i < toasts.Length; i++)
            {
                int index = i;
                _toastClicks[i] = () => HideToast(index);
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < toastButtons.Length; i++)
                toastButtons[i].onClick.AddListener(_toastClicks[i]);
        }

        private void OnDisable()
        {
            for (int i = 0; i < toasts.Length; i++)
            {
                toastButtons[i].onClick.RemoveListener(_toastClicks[i]);
                toasts[i].DOKill();
                toasts[i].gameObject.SetActive(false);
                _toastLives[i] = null; // 꺼지면 코루틴도 멈춘다
            }

            burst.DOKill();
            burst.gameObject.SetActive(false);
            foreach (TMP_Text gain in gains)
            {
                gain.DOKill();
                gain.gameObject.SetActive(false);
            }
        }

        /// <summary>말풍선 알림 한 줄. bad 면 붉은 바탕 (실패).</summary>
        public void Toast(string message, bool bad = false)
        {
            int index = FreeToast();
            HideToast(index);

            Color fill = bad ? BadToastFill : Color.white;
            toastTexts[index].text = message;
            toastFills[index].color = fill;
            toastTails[index].color = fill;
            toasts[index].SetAsFirstSibling();
            toasts[index].gameObject.SetActive(true);
            toasts[index].localScale = Vector3.zero;
            toasts[index].DOScale(1f, ToastPopSeconds).SetEase(Ease.OutBack).SetUpdate(true);
            _toastLives[index] = StartCoroutine(ToastRoutine(index));
        }

        /// <summary>터짐 — worldPosition 을 가운데로, size 는 캔버스 px (시안 크기 × 2).</summary>
        public void Burst(Vector3 worldPosition, string text, float size, Color fill)
        {
            burst.DOKill();
            burst.gameObject.SetActive(true);
            burst.position = worldPosition;
            burst.sizeDelta = Vector2.one * size * BurstSpriteScale;
            burstImage.color = fill;
            burstText.text = text;
            burstText.fontSize = size * BurstFontScale;
            burstGroup.alpha = 1f;
            burst.localScale = Vector3.one * 0.2f;
            burst.localEulerAngles = new Vector3(0f, 0f, 14f);

            DOTween.Sequence().SetTarget(burst).SetUpdate(true)
                .Append(burst.DOScale(1.14f, BurstSeconds * 0.3f).SetEase(Ease.OutCubic))
                .Join(burst.DOLocalRotate(new Vector3(0f, 0f, -4f), BurstSeconds * 0.3f).SetEase(Ease.OutCubic))
                .Append(burst.DOScale(1f, BurstSeconds * 0.2f))
                .Join(burst.DOLocalRotate(Vector3.zero, BurstSeconds * 0.2f))
                .Append(burst.DOScale(1.03f, BurstSeconds * 0.3f))
                .Append(burst.DOScale(1.06f, BurstSeconds * 0.2f))
                .Join(burstGroup.DOFade(0f, BurstSeconds * 0.2f))
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
            rect.localScale = Vector3.one * 0.6f;

            DOTween.Sequence().SetTarget(rect).SetUpdate(true)
                .Append(rect.DOAnchorPos(start + new Vector2(0f, -16f), GainSeconds * 0.25f).SetEase(Ease.OutQuad))
                .Join(rect.DOScale(1.1f, GainSeconds * 0.25f))
                .Join(gain.DOFade(1f, GainSeconds * 0.25f))
                .Append(rect.DOAnchorPos(start + new Vector2(0f, -36f), GainSeconds * 0.45f))
                .Join(rect.DOScale(1f, GainSeconds * 0.45f))
                .Append(rect.DOAnchorPos(start + new Vector2(0f, -56f), GainSeconds * 0.3f))
                .Join(gain.DOFade(0f, GainSeconds * 0.3f))
                .OnComplete(() => gain.gameObject.SetActive(false));

            UIJuice.Pop(coin, 0.9f);
        }

        // 꺼져 있는 말풍선, 다 떠 있으면 가장 아래(가장 오래된) 것
        private int FreeToast()
        {
            for (int i = 0; i < toasts.Length; i++)
            {
                if (!toasts[i].gameObject.activeSelf)
                    return i;
            }

            int oldest = 0;
            for (int i = 1; i < toasts.Length; i++)
            {
                if (toasts[i].GetSiblingIndex() > toasts[oldest].GetSiblingIndex())
                    oldest = i;
            }

            return oldest;
        }

        private void HideToast(int index)
        {
            if (_toastLives[index] != null)
                StopCoroutine(_toastLives[index]);

            _toastLives[index] = null;
            toasts[index].DOKill();
            toasts[index].gameObject.SetActive(false);
        }

        private IEnumerator ToastRoutine(int index)
        {
            yield return new WaitForSecondsRealtime(ToastSeconds);
            _toastLives[index] = null;
            HideToast(index);
        }
    }
}
