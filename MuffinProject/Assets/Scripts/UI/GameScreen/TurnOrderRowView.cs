using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 턴 순서의 한 사람 (통합 화면 UI v3 .torow) — 이름 · 얼굴 원(HP 만큼 차는 고리) · 다음 사람을 가리키는 꺾쇠. 프리팹 TurnOrderRow 를 TurnOrderView 가 복제한다.
    /// 지금 턴이면 얼굴 원이 커지고(.cur) 이름은 옆 꼬리표(TurnOrderView 의 currentTab)로 옮겨 간다. 최종 사망이면 흐리게 + 얼굴 흑백.
    /// </summary>
    public class TurnOrderRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [Tooltip("얼굴 원 묶음 — 지금 턴이면 커지고(currentScale) 위로 올라간다.")]
        [SerializeField] private RectTransform ring;
        [Tooltip("HP 만큼 위에서 시계 방향으로 차는 고리 — Filled · Radial 360.")]
        [SerializeField] private Image hpFill;
        [Tooltip("얼굴 원 안 캐릭터 머리 — 그 사람 좌석의 캐릭터 그림.")]
        [SerializeField] private Image head;
        [Tooltip("머리 위에 겹치는 꾸미기(봇 스킨의 리본) — head 와 같은 크기 · 자리. 꾸미기가 없으면 끈다.")]
        [SerializeField] private Image accessory;
        [Tooltip("다음 사람을 가리키는 꺾쇠 — 마지막 줄은 숨긴다. 역방향이면 위를 가리킨다.")]
        [SerializeField] private RectTransform chevron;
        [Tooltip("최종 사망이면 흐리게.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("최종 사망이면 얼굴을 흑백으로 (시안 .trow.dead grayscale 1) — Chapchu/UI Gray.")]
        [SerializeField] private Material grayMaterial;

        [Header("모양 (통합 화면 UI v3)")]
        [Tooltip("평소 얼굴 원 가운데 — 줄 위에서 아래로 (이름 26 + 6 아래 92 원의 가운데).")]
        [SerializeField] private float ringCenter = 78f;
        [Tooltip("지금 턴일 때 얼굴 원 가운데 — 이름이 빠지고 116 원이 위아래 틈 4 사이에 놓인다 (4 + 58).")]
        [SerializeField] private float currentRingCenter = 62f;
        [Tooltip("지금 턴일 때 얼굴 원 배율 (92 → 116).")]
        [SerializeField] private float currentScale = 116f / 92f;
        [SerializeField] private float deadAlpha = 0.55f;
        [SerializeField] private Color hpHighColor = new Color32(0x3c, 0xbf, 0x72, 0xFF);
        [SerializeField] private Color hpMidColor = new Color32(0xff, 0x9f, 0x1a, 0xFF);
        [SerializeField] private Color hpLowColor = new Color32(0xec, 0x3b, 0x46, 0xFF);

        private void Awake()
        {
            if (nameText == null || ring == null || hpFill == null || head == null || accessory == null || chevron == null || group == null || grayMaterial == null)
                Debug.LogError($"[{nameof(TurnOrderRowView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>이름 · 얼굴(그 사람의 캐릭터 그림 · 꾸미기 — 없으면 null).</summary>
        public void SetPlayer(string playerName, Sprite face, Sprite accessorySprite)
        {
            nameText.text = playerName;
            if (face != null)
                head.sprite = face;
            accessory.sprite = accessorySprite;
            accessory.enabled = accessorySprite != null;
        }

        /// <summary>지금 턴 — 얼굴 원을 키우고 이름을 숨긴다 (이름은 옆 꼬리표에).</summary>
        public void SetCurrent(bool current)
        {
            nameText.gameObject.SetActive(!current);
            ring.localScale = Vector3.one * (current ? currentScale : 1f);
            ring.anchoredPosition = new Vector2(ring.anchoredPosition.x, -(current ? currentRingCenter : ringCenter));
        }

        /// <summary>HP 비율(0~1) — 고리 길이 · 색 (50% 넘으면 초록, 20% 넘으면 주황, 아니면 빨강).</summary>
        public void SetHp(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            hpFill.fillAmount = fraction;
            int percent = Mathf.RoundToInt(fraction * 100f);
            hpFill.color = percent > 50 ? hpHighColor : percent > 20 ? hpMidColor : hpLowColor;
        }

        /// <summary>최종 사망 — 흐리게 + 얼굴 흑백. 이름 줄긋기는 시안 화면에 그려지지 않아 넣지 않는다.</summary>
        public void SetDead(bool dead)
        {
            group.alpha = dead ? deadAlpha : 1f;
            head.material = dead ? grayMaterial : null;
            accessory.material = head.material;
        }

        /// <summary>꺾쇠 — 마지막 줄이면 숨긴다. 역방향이면 위를 가리킨다.</summary>
        public void SetChevron(bool visible, bool reversed)
        {
            chevron.gameObject.SetActive(visible);
            chevron.localRotation = Quaternion.Euler(0f, 0f, reversed ? 180f : 0f);
        }
    }
}
