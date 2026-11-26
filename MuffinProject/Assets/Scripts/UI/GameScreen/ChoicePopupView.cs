using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 카드 효과 고르기 판 (1019 목표 조준, PM 2026-11-27) — 판을 어둡게 하지 않고 판 위에 놓는다.
    /// 왼쪽 = 쓴 카드 자리(체인의 카드가 ChainCardsPresenter.Focus 로 온다) + 아래 「쓴 사람 → 고르는 사람」 꼬리표,
    /// 오른쪽 = 고를 카드 두 장 (카드 글 순서: 손패 모두 버리기 · n 피해) — 결과 숫자를 키워드 색으로 (11-card-list 5-1).
    /// 위 = 남은 시간 막대 + 한 줄 안내 (새 카드 보여 주기와 같은 꼴). 고르면 고른 카드가 튀어나오고 다른 카드는 줄어 사라진다.
    /// 받은 값을 표시만 한다.
    /// </summary>
    public class ChoicePopupView : MonoBehaviour
    {
        private const string DiscardName = "손패 모두 버리기";
        private const string DamageNameFormat = "{0} 피해";
        private const string HandFormat = "{0}장";
        private const string DiscardSub = "찹츄에서 멀어짐";
        private const string HpSubFormat = "남은 HP {0}";

        [Tooltip("켜고 끄는 판 — 이 컴포넌트의 오브젝트는 켜 둔다.")]
        [SerializeField] private GameObject panel;
        [Tooltip("남은 시간 막대의 채움 — 왼쪽 기준, 폭 = 남은 비율 × 처음 폭.")]
        [SerializeField] private RectTransform barDrain;
        [SerializeField] private TMP_Text hintText;
        [Tooltip("쓴 카드 아래 「쓴 사람 → 고르는 사람」.")]
        [SerializeField] private TMP_Text tagText;
        [Tooltip("쓴 카드 자리 — 체인의 카드가 이 사각형에 맞춰 온다.")]
        [SerializeField] private RectTransform usedCardSlot;
        [Tooltip("고를 카드 — 0 손패 모두 버리기 · 1 피해 (서버의 고르기 번호 순서).")]
        [SerializeField] private ChoiceOptionView[] options = new ChoiceOptionView[2];

        [Header("키워드 색 · 그림 (11-card-list 5-1)")]
        [SerializeField] private Color handColor = new Color32(0x2f, 0x63, 0xd6, 0xFF);
        [SerializeField] private Color handArtColor = new Color32(0xf3, 0xf6, 0xfd, 0xFF);
        [SerializeField] private Sprite handIcon;
        [SerializeField] private Color damageColor = new Color32(0xe0, 0x30, 0x3c, 0xFF);
        [SerializeField] private Color damageArtColor = new Color32(0xfd, 0xf2, 0xf3, 0xFF);
        [SerializeField] private Sprite damageIcon;
        [SerializeField] private Color subInk = new Color32(0x6e, 0x64, 0x5c, 0xFF);

        [Header("고른 뒤")]
        [Tooltip("고른 카드가 튀어나오는 배율 · 높이(캔버스 px) · 시간.")]
        [SerializeField] private float popScale = 1.08f;
        [SerializeField] private float popRise = 26f;
        [SerializeField] private float popSeconds = 0.22f;
        [Tooltip("다른 카드가 줄어 사라지는 시간.")]
        [SerializeField] private float shrinkSeconds = 0.25f;
        [Tooltip("고른 카드를 보여 준 채 기다리는 시간 — 그 뒤 판을 닫는다.")]
        [SerializeField] private float holdSeconds = 0.6f;

        private float _barWidth;
        private Vector2[] _rest;
        private int _damage;

        /// <summary>고를 카드를 눌렀다 — 0 손패 모두 버리기 · 1 피해.</summary>
        public event Action<int> Picked;

        private void Awake()
        {
            if (panel == null || barDrain == null || hintText == null || tagText == null || usedCardSlot == null || options == null || options.Length != 2 || options[0] == null || options[1] == null || handIcon == null || damageIcon == null)
            {
                Debug.LogError($"[{nameof(ChoicePopupView)}] 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            _barWidth = barDrain.sizeDelta.x;
            _rest = new Vector2[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int option = i;
                _rest[i] = options[i].Rect.anchoredPosition;
                options[i].Clicked += () => Picked?.Invoke(option);
            }

            panel.SetActive(false);
        }

        public bool IsOpen => panel.activeSelf;

        /// <summary>쓴 카드 자리의 가운데 (캔버스 px, 화면 가운데가 0 · 위가 +) — 체인의 카드를 옮길 자리.</summary>
        public Vector2 UsedCardCenter
        {
            get
            {
                Transform root = usedCardSlot.GetComponentInParent<Canvas>().rootCanvas.transform;
                return root.InverseTransformPoint(usedCardSlot.TransformPoint(usedCardSlot.rect.center));
            }
        }

        /// <summary>쓴 카드 자리의 높이 (캔버스 px).</summary>
        public float UsedCardHeight => usedCardSlot.rect.height;

        /// <summary>
        /// 판을 연다. hand = 고르는 사람의 손패 장수, hp = 지금 HP, damage = 피해 양.
        /// interactive 면 고르는 사람(나) — 카드를 누를 수 있다. 아니면 보는 사람 — 같은 카드에 고르는 사람의 숫자.
        /// </summary>
        public void Show(string hint, string tag, int hand, int hp, int damage, bool interactive)
        {
            _damage = damage;
            hintText.text = hint;
            tagText.text = tag;

            options[0].SetLook(DiscardName, handColor, handArtColor, handIcon);
            options[0].SetResult(string.Format(HandFormat, hand), string.Format(HandFormat, 0), false, DiscardSub, subInk);
            int left = Mathf.Max(0, hp - damage);
            options[1].SetLook(string.Format(DamageNameFormat, damage), damageColor, damageArtColor, damageIcon);
            options[1].SetResult(hp.ToString(), left.ToString(), true, string.Format(HpSubFormat, left), damageColor);

            for (int i = 0; i < options.Length; i++)
            {
                RectTransform rect = options[i].Rect;
                rect.DOKill();
                rect.anchoredPosition = _rest[i];
                rect.localScale = Vector3.one;
                options[i].SetInteractable(interactive);
            }

            SetFraction(1f);
            panel.SetActive(true);
        }

        /// <summary>남은 시간 비율 (0~1) — 막대가 왼쪽으로 준다.</summary>
        public void SetFraction(float fraction) => barDrain.sizeDelta = new Vector2(_barWidth * Mathf.Clamp01(fraction), barDrain.sizeDelta.y);

        public void SetHint(string hint) => hintText.text = hint;

        /// <summary>고르기 번호의 이름 — 「손패 모두 버리기」 · 「80 피해」.</summary>
        public string OptionName(int option) => option == 0 ? DiscardName : string.Format(DamageNameFormat, _damage);

        /// <summary>고른 카드가 튀어나오고 다른 카드는 줄어 사라진 뒤, 잠깐 두고 판을 닫는다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator PickRoutine(int option)
        {
            for (int i = 0; i < options.Length; i++)
            {
                RectTransform rect = options[i].Rect;
                options[i].SetInteractable(false);
                rect.DOKill();
                if (i == option)
                {
                    rect.DOScale(popScale, popSeconds).SetEase(Ease.OutBack);
                    rect.DOAnchorPosY(_rest[i].y + popRise, popSeconds).SetEase(Ease.OutQuad);
                }
                else
                {
                    rect.DOScale(0f, shrinkSeconds).SetEase(Ease.InBack);
                }
            }

            yield return new WaitForSeconds(Mathf.Max(popSeconds, shrinkSeconds) + holdSeconds);
            Hide();
        }

        public void Hide()
        {
            foreach (ChoiceOptionView option in options)
                option.Rect.DOKill();
            panel.SetActive(false);
        }
    }
}
