using Chapchu.Game;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 카드 연출 테스트 씬의 허수아비 자리 (아티팩트 「찹츄 카드 이펙트」 의 vw-plate). 체력 바 · 숫자와 맞았을 때 흔들림만 있다 — 규칙 판정은 없다.
    /// </summary>
    public class FxDummySeat : MonoBehaviour
    {
        // 시안 vw-plate .hpb i.low — 30 이하면 빨강
        private const int LowHp = 30;

        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;

        [SerializeField] private Color hpColor = new Color32(0x3d, 0xdc, 0x84, 0xff);
        [SerializeField] private Color lowHpColor = new Color32(0xff, 0x4f, 0x2e, 0xff);

        private int _hp;
        private Vector2 _base;

        private void Awake()
        {
            if (hpFill == null || hpText == null)
            {
                Debug.LogError($"[{nameof(FxDummySeat)}] 체력 바 · 체력 글자 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            _base = ((RectTransform)transform).anchoredPosition;
            ResetHp();
        }

        /// <summary>체력을 처음 값(GameServer.MaxHp, 06-health 2절)으로 되돌린다.</summary>
        public void ResetHp()
        {
            SetHp(GameServer.MaxHp, false);
        }

        /// <summary>체력을 줄이고 자리를 흔든다 (시안 hit 의 setHP · pshake).</summary>
        public void TakeDamage(int amount)
        {
            SetHp(Mathf.Max(0, _hp - amount), true);
            Shake();
        }

        private void SetHp(int hp, bool animate)
        {
            _hp = hp;
            hpText.text = $"{_hp}";
            hpFill.color = _hp <= LowHp ? lowHpColor : hpColor;
            hpFill.DOKill();
            float fill = (float)_hp / GameServer.MaxHp;
            if (animate)
                hpFill.DOFillAmount(fill, 0.45f).SetEase(Ease.OutCubic).SetLink(gameObject);
            else
                hpFill.fillAmount = fill;
        }

        // 시안 @keyframes shake (.38s): 20% (-9, -3°) · 40% (9, 3°) · 60% -6 · 80% 4 · 100% 0
        private void Shake()
        {
            var rect = (RectTransform)transform;
            float[] keys = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };
            float[] xs = { 0f, -9f, 9f, -6f, 4f, 0f };
            float[] angles = { 0f, -3f, 3f, 2f, 1f, 0f };
            DOTween.Kill(rect);
            DOVirtual.Float(0f, 1f, 0.38f, t =>
            {
                int i = 0;
                while (i < keys.Length - 2 && t > keys[i + 1])
                    i++;
                float f = Mathf.InverseLerp(keys[i], keys[i + 1], t);
                rect.anchoredPosition = _base + new Vector2(Mathf.Lerp(xs[i], xs[i + 1], f), 0f);
                // 시안은 시계 방향 + — 유니티는 반시계 방향 +
                rect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(angles[i], angles[i + 1], f));
            }).SetEase(Ease.Linear).SetTarget(rect).SetLink(gameObject);
        }
    }
}
