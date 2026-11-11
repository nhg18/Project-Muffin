using System;
using Chapchu.UI.Lobby;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 시작 자리 — 같은 자리에 셋 중 하나만 보인다: 방장의 「게임 시작」 버튼 / 참가자의 대기 안내 / 전원의 「게임을 시작합니다」.
    /// 기준 문서: docs/systems/16-room-ui-full.md 7-7 · 7-12
    /// </summary>
    public class RoomStartView : MonoBehaviour
    {
        [Header("방장 — 게임 시작")]
        [SerializeField] private Button startButton;

        [Tooltip("시작할 수 없을 때 버튼 안에 뜨는 이유 글")]
        [SerializeField] private GameObject disabledReason;

        [Header("참가자 — 대기 안내")]
        [SerializeField] private GameObject waitPanel;

        [Tooltip("대기 안내의 발자국 — 차례로 까딱인다")]
        [SerializeField] private RectTransform[] waitPaws;

        [Tooltip("발자국이 올라가는 높이 (캔버스 px) · 한 번 오르내리는 시간 · 다음 발자국이 늦게 시작하는 시간 (초)")]
        [SerializeField] private float pawLift = 8f;
        [SerializeField] private float pawSeconds = 1f;
        [SerializeField] private float pawStagger = 0.15f;

        [Header("시작 중")]
        [SerializeField] private GameObject startingPanel;

        private float[] _pawBaseY;

        /// <summary>방장이 「게임 시작」 을 눌렀다.</summary>
        public event Action OnStartRequested;

        private void Awake()
        {
            if (startButton == null || disabledReason == null || waitPanel == null || waitPaws == null || startingPanel == null)
                Debug.LogError($"[{nameof(RoomStartView)}] 참조가 비어 있습니다.", this);

            _pawBaseY = new float[waitPaws.Length];
            for (int i = 0; i < waitPaws.Length; i++)
                _pawBaseY[i] = waitPaws[i].anchoredPosition.y;
        }

        private void OnEnable() => startButton.onClick.AddListener(HandleStartClicked);

        private void OnDisable()
        {
            startButton.onClick.RemoveListener(HandleStartClicked);
            StopPaws();
        }

        /// <summary>방장 — 버튼. 시작할 수 없으면 꺼진 모양 + 이유 글.</summary>
        public void ShowHost(bool canStart)
        {
            Show(startButton.gameObject);
            LobbyStyle.SetEnabled(startButton, canStart);
            disabledReason.SetActive(!canStart);
        }

        /// <summary>참가자 — 눌리지 않는 버튼 대신 대기 안내.</summary>
        public void ShowGuest()
        {
            bool wasShown = waitPanel.activeSelf;
            Show(waitPanel);
            if (!wasShown)
                PlayPaws();
        }

        public void ShowStarting() => Show(startingPanel);

        private void Show(GameObject shown)
        {
            if (shown != waitPanel)
                StopPaws();

            startButton.gameObject.SetActive(shown == startButton.gameObject);
            waitPanel.SetActive(shown == waitPanel);
            startingPanel.SetActive(shown == startingPanel);
        }

        // 시안 .waitp .dots — bob 1초, 0.15초씩 늦게
        private void PlayPaws()
        {
            StopPaws();
            for (int i = 0; i < waitPaws.Length; i++)
            {
                waitPaws[i].DOAnchorPosY(_pawBaseY[i] + pawLift, pawSeconds * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(pawStagger * i)
                    .SetUpdate(true);
            }
        }

        private void StopPaws()
        {
            if (_pawBaseY == null)
                return;

            for (int i = 0; i < waitPaws.Length; i++)
            {
                waitPaws[i].DOKill();
                waitPaws[i].anchoredPosition = new Vector2(waitPaws[i].anchoredPosition.x, _pawBaseY[i]);
            }
        }

        private void HandleStartClicked() => OnStartRequested?.Invoke();
    }
}
