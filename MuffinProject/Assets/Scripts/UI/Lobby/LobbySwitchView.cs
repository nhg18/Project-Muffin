using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>켜고 끄는 스위치 (시안 .sw) — 켜지면 초록 바탕, 손잡이가 오른쪽으로 0.15초.</summary>
    [RequireComponent(typeof(Button))]
    public class LobbySwitchView : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private RectTransform knob;

        [Tooltip("꺼졌을 때 손잡이 왼쪽 끝 위치 (캔버스 px)")]
        [SerializeField] private float offX = 4f;

        [Tooltip("켜졌을 때 손잡이 왼쪽 끝 위치 (캔버스 px)")]
        [SerializeField] private float onX = 42f;

        [Header("연출")]
        [Tooltip("손잡이가 움직이는 시간 (초)")]
        [SerializeField] private float knobSeconds = 0.15f;

        private Button _button;

        public bool IsOn { get; private set; }

        public event Action<LobbySwitchView> OnChanged;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (fill == null || knob == null)
                Debug.LogError($"[{nameof(LobbySwitchView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable() => _button.onClick.AddListener(HandleClicked);

        private void OnDisable()
        {
            _button.onClick.RemoveListener(HandleClicked);
            knob.DOKill(true);
        }

        /// <summary>값만 맞춘다 — 움직임 · 알림 없음.</summary>
        public void Set(bool isOn)
        {
            IsOn = isOn;
            knob.DOKill();
            knob.anchoredPosition = new Vector2(isOn ? onX : offX, knob.anchoredPosition.y);
            fill.color = isOn ? LobbyStyle.Ok : LobbyStyle.Track;
        }

        private void HandleClicked()
        {
            IsOn = !IsOn;
            fill.color = IsOn ? LobbyStyle.Ok : LobbyStyle.Track;
            knob.DOKill();
            knob.DOAnchorPosX(IsOn ? onX : offX, knobSeconds).SetEase(Ease.OutBack).SetUpdate(true);
            OnChanged?.Invoke(this);
        }
    }
}
