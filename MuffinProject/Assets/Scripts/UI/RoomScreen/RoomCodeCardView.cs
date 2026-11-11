using System.Collections;
using Chapchu.UI.Lobby;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 방 코드 판 — 코드는 늘 가려져 있고(점 4개) 「보기」 를 누르고 있는 동안만 보인다. 코드를 누르면 가려진 채로 복사하고 판 아래에 말풍선을 띄운다.
    /// 머리 띠 오른쪽에 「공개」 / 「비공개」 글자.
    /// 기준 문서: docs/systems/16-room-ui-full.md 7-1 · 5절
    /// </summary>
    public class RoomCodeCardView : MonoBehaviour
    {
        [Header("코드")]
        [SerializeField] private Button codeButton;
        [SerializeField] private TMP_Text codeText;

        [Tooltip("가려진 코드 — 점 4개")]
        [SerializeField] private GameObject hiddenDots;

        [Tooltip("코드가 보이는 동안 코드 뒤에 깔리는 바탕")]
        [SerializeField] private GameObject revealedFill;
        [SerializeField] private Image copyIcon;

        [Header("보기")]
        [SerializeField] private HoldButton peekButton;

        [Tooltip("「보기」 를 누르고 있는 동안 버튼에 깔리는 노랑")]
        [SerializeField] private GameObject peekPressedFill;

        [Header("공개 여부")]
        [SerializeField] private GameObject publicTag;
        [SerializeField] private GameObject privateTag;

        [Header("복사 말풍선")]
        [SerializeField] private RectTransform copiedBubble;

        [Tooltip("「복사했어요!」 말풍선이 떠 있는 시간 (초)")]
        [SerializeField] private float copiedSeconds = 2f;

        private Button _peekButtonButton;
        private Coroutine _bubbleRoutine;

        private void Awake()
        {
            if (codeButton == null || codeText == null || hiddenDots == null || revealedFill == null || copyIcon == null || peekButton == null
                || peekPressedFill == null || publicTag == null || privateTag == null || copiedBubble == null)
                Debug.LogError($"[{nameof(RoomCodeCardView)}] 참조가 비어 있습니다.", this);

            _peekButtonButton = peekButton.GetComponent<Button>();
        }

        private void OnEnable()
        {
            codeButton.onClick.AddListener(HandleCodeClicked);
            peekButton.OnHeldChanged += HandlePeekHeldChanged;
        }

        private void OnDisable()
        {
            codeButton.onClick.RemoveListener(HandleCodeClicked);
            peekButton.OnHeldChanged -= HandlePeekHeldChanged;
            HideBubble();
        }

        public void SetCode(string code) => codeText.text = code;

        public void SetPublic(bool isPublic)
        {
            publicTag.SetActive(isPublic);
            privateTag.SetActive(!isPublic);
        }

        /// <summary>시작 중 — 복사 · 「보기」 를 막는다. 코드는 가려진 채 보인다.</summary>
        public void SetLocked(bool locked)
        {
            if (locked)
            {
                peekButton.Release();
                HideBubble();
            }

            codeButton.interactable = !locked;
            copyIcon.color = locked ? LobbyStyle.Off : LobbyStyle.Ink2;
            LobbyStyle.SetEnabled(_peekButtonButton, !locked);
        }

        private void HandleCodeClicked()
        {
            GUIUtility.systemCopyBuffer = codeText.text;

            HideBubble();
            copiedBubble.gameObject.SetActive(true);
            UIJuice.Pop(copiedBubble, 0f);
            _bubbleRoutine = StartCoroutine(HideBubbleLater());
        }

        private IEnumerator HideBubbleLater()
        {
            yield return new WaitForSecondsRealtime(copiedSeconds);
            _bubbleRoutine = null;
            copiedBubble.gameObject.SetActive(false);
        }

        private void HideBubble()
        {
            if (_bubbleRoutine != null)
                StopCoroutine(_bubbleRoutine);

            _bubbleRoutine = null;
            copiedBubble.DOKill();
            copiedBubble.gameObject.SetActive(false);
        }

        private void HandlePeekHeldChanged(bool held)
        {
            codeText.gameObject.SetActive(held);
            hiddenDots.SetActive(!held);
            revealedFill.SetActive(held);
            peekPressedFill.SetActive(held);
        }
    }
}
