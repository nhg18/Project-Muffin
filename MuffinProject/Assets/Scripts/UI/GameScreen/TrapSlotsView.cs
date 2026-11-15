using System;
using System.Linq;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 한 사람의 함정 칸 3개 (04-card.md 2절 「최대 3장」 · 10-ui.md 4-1 「함정 슬롯」). 설치된 장수만큼 앞에서부터 뒷면으로 채운다 — 개수는 공개, 종류는 비공개.
    /// 값을 받아 표시만 한다. 탭은 칸 번호로 알린다.
    /// </summary>
    public class TrapSlotsView : MonoBehaviour
    {
        [Tooltip("칸 3개 — 채워지는 순서대로.")]
        [SerializeField] private TrapSlotView[] slots;

        /// <summary>칸을 탭했다 (칸 번호 0 ~). 콜라이더가 있는 내 칸만 온다.</summary>
        public event Action<int> OnSlotClicked;

        /// <summary>지금 채워 보이는 칸 수.</summary>
        public int Count { get; private set; }

        private void Awake()
        {
            if (slots == null || slots.Length == 0 || slots.Any(slot => slot == null))
            {
                Debug.LogError($"[{nameof(TrapSlotsView)}] {nameof(slots)} 가 비어 있습니다.", this);
                return;
            }

            SetCount(0);
        }

        private void OnEnable()
        {
            foreach (TrapSlotView slot in slots)
                slot.OnClicked += HandleSlotClicked;
        }

        private void OnDisable()
        {
            foreach (TrapSlotView slot in slots)
                slot.OnClicked -= HandleSlotClicked;
        }

        public void SetCount(int count)
        {
            Count = Mathf.Clamp(count, 0, slots.Length);
            for (int i = 0; i < slots.Length; i++)
                slots[i].SetFilled(i < Count);
        }

        /// <summary>칸의 자리 — 카드가 들어가고 나오는 연출의 끝 · 시작점. 범위를 넘으면 가까운 칸.</summary>
        public Transform SlotAnchor(int index) => slots[Mathf.Clamp(index, 0, slots.Length - 1)].transform;

        private void HandleSlotClicked(TrapSlotView slot) => OnSlotClicked?.Invoke(Array.IndexOf(slots, slot));
    }
}
