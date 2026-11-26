using System;
using Chapchu.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비 홈 — 프로필 칩 · 아래 왼쪽 줄(퀘스트 · 꾸미기 · 상점) · 플레이 묶음. 섹션으로 가면 0.2초에 사라진다.
    /// 플레이 묶음의 작은 버튼 셋(혼자 하기 · 방 만들기 · 코드로 참가)은 개발자 모드에서만 풀린다 — 평소엔 자물쇠 · 꺼진 모양 (14-lobby-ui 5절).
    /// 기준: 「통합 화면 UI v2」 로비 #home
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class LobbyHomeView : MonoBehaviour
    {
        [SerializeField] private Button profileButton;
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private LayoutElement nicknameSize;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private Button questButton;
        [SerializeField] private GameObject questBadge;
        [SerializeField] private TMP_Text questBadgeText;
        [SerializeField] private Button decoButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button soloButton;
        [SerializeField] private Button createButton;
        [SerializeField] private Button codeButton;
        [SerializeField] private Button randomButton;

        [Header("개발자 모드 잠금 — 작은 버튼 셋의 아이콘")]
        [SerializeField] private Image soloIcon;
        [SerializeField] private Image createIcon;
        [SerializeField] private Image codeIcon;
        [SerializeField] private Sprite lockSprite;

        [Tooltip("닉네임 글자 폭 한도 (캔버스 px) — 넘으면 말줄임")]
        [SerializeField] private float nicknameMaxWidth = 560f;

        [Header("연출")]
        [Tooltip("홈 묶음이 나타나고 사라지는 시간 (초)")]
        [SerializeField] private float fadeSeconds = 0.2f;

        private CanvasGroup _group;
        private Sprite[] _openSprites; // 작은 버튼 셋의 원래 아이콘 — 개발자 모드를 켜면 되돌린다

        public event Action OnProfileRequested;
        public event Action OnQuestsRequested;
        public event Action OnDecoRequested;
        public event Action OnShopRequested;
        public event Action OnSoloRequested;
        public event Action OnCreateRequested;
        public event Action OnCodeRequested;
        public event Action OnRandomRequested;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (profileButton == null || nicknameText == null || nicknameSize == null || codeText == null
                || questButton == null || questBadge == null || questBadgeText == null || decoButton == null || shopButton == null)
                Debug.LogError($"[{nameof(LobbyHomeView)}] 칩 · 아래 줄 참조가 비어 있습니다.", this);

            if (soloButton == null || createButton == null || codeButton == null || randomButton == null)
                Debug.LogError($"[{nameof(LobbyHomeView)}] 플레이 버튼 참조가 비어 있습니다.", this);

            if (soloIcon == null || createIcon == null || codeIcon == null || lockSprite == null)
                Debug.LogError($"[{nameof(LobbyHomeView)}] 개발자 모드 잠금 참조가 비어 있습니다.", this);

            _openSprites = new[] { soloIcon.sprite, createIcon.sprite, codeIcon.sprite };
        }

        // PressableButton 이 Awake 에서 원래 색을 기억한 뒤에 꺼진 모양으로 바꾼다
        private void Start() => SetDevMode(DevMode.IsOn);

        /// <summary>개발자 모드 — 켜면 작은 버튼 셋이 원래 아이콘으로 풀리고, 끄면 자물쇠 · 꺼진 모양으로 잠긴다.</summary>
        public void SetDevMode(bool on)
        {
            Button[] buttons = { soloButton, createButton, codeButton };
            Image[] icons = { soloIcon, createIcon, codeIcon };
            for (int i = 0; i < buttons.Length; i++)
            {
                icons[i].sprite = on ? _openSprites[i] : lockSprite;
                LobbyStyle.SetEnabled(buttons[i], on);
            }
        }

        private void OnEnable()
        {
            profileButton.onClick.AddListener(HandleProfileClicked);
            questButton.onClick.AddListener(HandleQuestClicked);
            decoButton.onClick.AddListener(HandleDecoClicked);
            shopButton.onClick.AddListener(HandleShopClicked);
            soloButton.onClick.AddListener(HandleSoloClicked);
            createButton.onClick.AddListener(HandleCreateClicked);
            codeButton.onClick.AddListener(HandleCodeClicked);
            randomButton.onClick.AddListener(HandleRandomClicked);
        }

        private void OnDisable()
        {
            profileButton.onClick.RemoveListener(HandleProfileClicked);
            questButton.onClick.RemoveListener(HandleQuestClicked);
            decoButton.onClick.RemoveListener(HandleDecoClicked);
            shopButton.onClick.RemoveListener(HandleShopClicked);
            soloButton.onClick.RemoveListener(HandleSoloClicked);
            createButton.onClick.RemoveListener(HandleCreateClicked);
            codeButton.onClick.RemoveListener(HandleCodeClicked);
            randomButton.onClick.RemoveListener(HandleRandomClicked);
            _group.DOKill();
        }

        public void SetProfile(string nickname, string friendCode)
        {
            nicknameText.text = nickname;
            nicknameSize.preferredWidth = Mathf.Min(nicknameText.GetPreferredValues(nickname).x, nicknameMaxWidth);
            codeText.text = friendCode;
        }

        /// <summary>퀘스트 배지 = 받을 수 있는 보상 수.</summary>
        public void SetQuestBadge(int count, bool bump = false)
        {
            questBadge.SetActive(count > 0);
            questBadgeText.text = count.ToString();
            if (bump && count > 0)
                UIJuice.Pop((RectTransform)questBadge.transform, 0.6f);
        }

        public void SetVisible(bool visible)
        {
            _group.blocksRaycasts = visible;
            _group.DOKill();
            _group.DOFade(visible ? 1f : 0f, fadeSeconds).SetUpdate(true);
        }

        private void HandleProfileClicked() => OnProfileRequested?.Invoke();

        private void HandleQuestClicked() => OnQuestsRequested?.Invoke();

        private void HandleDecoClicked() => OnDecoRequested?.Invoke();

        private void HandleShopClicked() => OnShopRequested?.Invoke();

        private void HandleSoloClicked() => OnSoloRequested?.Invoke();

        private void HandleCreateClicked() => OnCreateRequested?.Invoke();

        private void HandleCodeClicked() => OnCodeRequested?.Invoke();

        private void HandleRandomClicked() => OnRandomRequested?.Invoke();
    }
}
