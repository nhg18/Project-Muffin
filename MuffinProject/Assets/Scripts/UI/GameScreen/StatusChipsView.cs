using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 이름표 옆 상태 알약 — 그 사람에게 걸린 효과 (PM 2026-11-27). 「행동 금지」(1007 짖기) · 「스킵」(다음 턴 스킵, 10-ui.md 3절 「턴 스킵 표시」).
    /// 걸린 동안만 켠다. 받은 값을 표시만 한다.
    /// </summary>
    public class StatusChipsView : MonoBehaviour
    {
        [Tooltip("「행동 금지」 알약 — 행동 카드 금지가 걸린 동안 (다음 턴 · 이번 턴).")]
        [SerializeField] private GameObject blockChip;
        [Tooltip("「스킵」 알약 — 다음 턴 스킵이 걸린 동안. 스킵된 턴이 지나가면 뗀다.")]
        [SerializeField] private GameObject skipChip;

        private void Awake()
        {
            if (blockChip == null || skipChip == null)
                Debug.LogError($"[{nameof(StatusChipsView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetBlocked(bool blocked) => blockChip.SetActive(blocked);

        public void SetSkip(bool skip) => skipChip.SetActive(skip);

        public void Clear()
        {
            SetBlocked(false);
            SetSkip(false);
        }
    }
}
