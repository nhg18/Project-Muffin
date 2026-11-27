using Chapchu.Game;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 카드 연출 테스트 씬의 허수아비 자리 (아티팩트 「찹츄 카드 이펙트」 의 vw-plate). 체력 바 · 숫자만 있다 (흔들림은 FxLayer.ShakeSeat) — 규칙 판정은 없다.
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

        /// <summary>지금 체력.</summary>
        public int Hp => _hp;

        private void Awake()
        {
            if (hpFill == null || hpText == null)
            {
                Debug.LogError($"[{nameof(FxDummySeat)}] 체력 바 · 체력 글자 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            ResetHp();
        }

        /// <summary>체력 바를 그 값으로 바꾼다 (흔들지 않는다 — 맞음 연출은 FxLayer.Hit 가 그린다).</summary>
        public void ShowHp(int hp)
        {
            SetHp(Mathf.Clamp(hp, 0, GameServer.MaxHp), true);
        }

        /// <summary>체력을 처음 값(기본 GameServer.MaxHp, 06-health 2절)으로 애니메이션 없이 되돌린다.</summary>
        public void ResetHp(int hp = GameServer.MaxHp)
        {
            SetHp(Mathf.Clamp(hp, 0, GameServer.MaxHp), false);
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
    }
}
