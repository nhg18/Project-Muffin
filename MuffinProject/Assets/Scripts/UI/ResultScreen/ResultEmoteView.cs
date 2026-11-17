using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면의 이모트(빠른 말) 버튼들 (07-win-condition.md 11-2, 시안 .emotes) — 누르면 내 캐릭터 위에 말풍선이 전원에게 뜬다.
    /// 누른 뒤 잠깐은 다시 누를 수 없다 (도배 방지). 말은 버튼에 적힌 글 그대로다.
    /// </summary>
    public class ResultEmoteView : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup group;
        [Tooltip("버튼 — 순서가 이모트 번호 (0 부터). 방장이 받는 번호 범위(PunResultServer.EmoteCount)와 같은 수.")]
        [SerializeField] private Button[] buttons;
        [Tooltip("버튼의 글 — buttons 와 같은 순서. 말풍선에 이 글이 뜬다.")]
        [SerializeField] private TMP_Text[] labels;
        [Tooltip("누른 뒤 다시 누를 수 있을 때까지 (초, 시안 1.2).")]
        [SerializeField] private float cooldownSeconds = 1.2f;
        [Tooltip("쉬는 동안의 투명도 (시안 .emo:disabled 0.5).")]
        [SerializeField] private float cooldownAlpha = 0.5f;

        /// <summary>이모트 버튼을 눌렀다 — 이모트 번호.</summary>
        public event Action<int> EmoteClicked;

        private void Awake()
        {
            if (root == null || group == null || buttons == null || labels == null || buttons.Length != labels.Length)
                Debug.LogError($"[{nameof(ResultEmoteView)}] 참조 중 비어 있는 것이 있습니다.", this);

            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                buttons[i].onClick.AddListener(() => HandleClicked(index));
            }
        }

        /// <summary>index 번 이모트의 글. 범위 밖이면 null.</summary>
        public string TextOf(int index) => index >= 0 && index < labels.Length ? labels[index].text : null;

        private void HandleClicked(int index)
        {
            EmoteClicked?.Invoke(index);

            group.interactable = false;
            group.alpha = cooldownAlpha;
            DOVirtual.DelayedCall(cooldownSeconds, () =>
            {
                group.interactable = true;
                group.alpha = 1f;
            }).SetLink(gameObject);
        }

        /// <summary>등장 연출 (시안 .stage.intro .bottom) — delay 초 뒤 떠오른다. 보이기 전에는 눌리지 않는다.</summary>
        public void PlayIntro(float delay)
        {
            Vector2 home = root.anchoredPosition;
            root.anchoredPosition = home + new Vector2(0f, -20f);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            root.DOAnchorPos(home, 0.4f).SetDelay(delay).SetEase(Ease.OutQuad).SetLink(gameObject);
            group.DOFade(1f, 0.4f).SetDelay(delay).SetLink(gameObject).OnComplete(() => group.blocksRaycasts = true);
        }
    }
}
