using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 카드 연출 테스트 씬의 허수아비 자리. 체력 바 · 숫자와 피해 숫자 팝업만 있다 — 규칙 판정은 없다.
    /// </summary>
    public class FxDummySeat : MonoBehaviour
    {
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("피해 숫자 원본. 꺼 둔 채로 두면 맞을 때마다 복제해 띄운다.")]
        [SerializeField] private TMP_Text damagePopTemplate;

        [Tooltip("시작 체력 (06-health 기본값과 같게).")]
        [SerializeField] private int maxHp = 100;
        [Tooltip("맞았을 때 자리가 흔들리는 세기.")]
        [SerializeField] private float shakeStrength = 18f;

        private int _hp;

        private void Awake()
        {
            if (hpFill == null || hpText == null || damagePopTemplate == null)
            {
                Debug.LogError($"[{nameof(FxDummySeat)}] 체력 바 · 체력 글자 · 피해 숫자 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            damagePopTemplate.gameObject.SetActive(false);
            ResetHp();
        }

        /// <summary>체력을 처음 값으로 되돌린다.</summary>
        public void ResetHp()
        {
            _hp = maxHp;
            hpFill.DOKill();
            hpFill.fillAmount = 1f;
            hpText.text = $"{_hp}";
        }

        /// <summary>피해 숫자를 띄우고 체력 바를 줄인다.</summary>
        public void TakeDamage(int amount)
        {
            _hp = Mathf.Max(0, _hp - amount);
            hpText.text = $"{_hp}";
            hpFill.DOKill();
            hpFill.DOFillAmount((float)_hp / maxHp, 0.4f).SetLink(gameObject);

            RectTransform rect = (RectTransform)transform;
            rect.DOKill(true);
            rect.DOShakeAnchorPos(0.38f, shakeStrength, 30, 90f, false, true).SetLink(gameObject);

            ShowDamagePop(amount);
        }

        private void ShowDamagePop(int amount)
        {
            TMP_Text pop = Instantiate(damagePopTemplate, damagePopTemplate.transform.parent);
            pop.gameObject.SetActive(true);
            pop.text = $"-{amount}";

            RectTransform rect = pop.rectTransform;
            Vector2 start = rect.anchoredPosition;
            rect.localScale = Vector3.one * 0.4f;
            DOTween.Sequence().SetLink(pop.gameObject)
                .Append(rect.DOScale(1.3f, 0.14f).SetEase(Ease.OutBack))
                .Append(rect.DOScale(1f, 0.12f))
                .Join(rect.DOAnchorPosY(start.y + 40f, 0.9f).SetEase(Ease.OutCubic))
                .AppendInterval(0.35f)
                .Append(pop.DOFade(0f, 0.3f))
                .OnComplete(() => Destroy(pop.gameObject));
        }
    }
}
