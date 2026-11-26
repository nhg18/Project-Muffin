using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 고르기 판의 고를 카드 한 장 — 이름 띠(키워드 색) · 그림 칸 · 결과 숫자(지금 → 고르면) · 한 줄 (1019 목표 조준, PM 2026-11-27).
    /// 받은 값을 표시만 한다. 누르면 Clicked — 고를 수 있는지는 ChoicePopupView 가 정한다.
    /// </summary>
    public class ChoiceOptionView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Tooltip("이름 띠 바탕 — 키워드 색.")]
        [SerializeField] private Graphic ribbon;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("그림 칸 바탕 — 키워드 색을 옅게.")]
        [SerializeField] private Graphic art;
        [SerializeField] private Image icon;
        [Tooltip("HP 결과일 때만 숫자 앞에 켜는 하트.")]
        [SerializeField] private GameObject heart;
        [SerializeField] private TMP_Text fromText;
        [SerializeField] private TMP_Text toText;
        [SerializeField] private TMP_Text subText;

        /// <summary>이 카드를 눌렀다.</summary>
        public event Action Clicked;

        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            if (button == null || ribbon == null || nameText == null || art == null || icon == null || heart == null || fromText == null || toText == null || subText == null)
            {
                Debug.LogError($"[{nameof(ChoiceOptionView)}] 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            button.onClick.AddListener(() => Clicked?.Invoke());
        }

        /// <summary>카드 생김새 — 이름 · 키워드 색 · 그림 칸 색 · 그림.</summary>
        public void SetLook(string cardName, Color keyword, Color artColor, Sprite iconSprite)
        {
            nameText.text = cardName;
            ribbon.color = keyword;
            art.color = artColor;
            icon.sprite = iconSprite;
            toText.color = keyword;
        }

        /// <summary>결과 — 「from → to」 (to 는 키워드 색) 와 아래 한 줄. withHeart 면 숫자 앞에 하트 (HP).</summary>
        public void SetResult(string from, string to, bool withHeart, string sub, Color subColor)
        {
            heart.SetActive(withHeart);
            fromText.text = from;
            toText.text = to;
            subText.text = sub;
            subText.color = subColor;
        }

        public void SetInteractable(bool interactable) => button.interactable = interactable;
    }
}
