using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 설정 「화면」 줄 — 지금 화면(전체 화면 · 창 크기)을 보이고, 「변경」 을 누르면 고를 수 있는 것을 목록으로 연다(목록 창은 LobbySettingsPopupView).
    /// 고르면 바로 바꾼다. 고른 값은 Unity 가 저장해 다음 실행에도 그대로 연다 (Standalone Player 기본 동작). 모바일에서는 묶음째 숨긴다.
    /// 기준: 전시판 요청 (2026-11-27 PM) · 목록으로 고르기 (2026-11-28 PM, 플레이 테스트 「하나씩 넘겨보지 말고 리스트로」) — 17-lobby-ui-full 7-4.
    /// </summary>
    public class LobbyResolutionView : MonoBehaviour
    {
        private const string FullScreenText = "전체 화면";
        private const string WindowFormat = "창 · {0} × {1}";

        // 창 크기 후보 — 기준 해상도 16:9 (15-screen 2절 #2). 모니터보다 작은 것만 고른다.
        private static readonly Vector2Int[] WindowSizes =
        {
            new Vector2Int(3840, 2160),
            new Vector2Int(2560, 1440),
            new Vector2Int(1920, 1080),
            new Vector2Int(1600, 900),
            new Vector2Int(1280, 720),
        };

        [SerializeField] private TMP_Text valueText;
        [Tooltip("「변경」 — 목록 창을 연다")]
        [SerializeField] private Button changeButton;

        [Tooltip("PC 가 아니면 끄는 것 — 묶음 이름 · 묶음 앞 빈칸 · 이 줄")]
        [SerializeField] private GameObject[] pcOnly;

        // 0 = 전체 화면, 1~ = 창 크기
        private readonly List<Vector2Int> _windowSizes = new List<Vector2Int>();
        private readonly List<string> _labels = new List<string>();

        /// <summary>「변경」 을 눌렀다.</summary>
        public event Action OnListRequested;

        /// <summary>고를 수 있는 것의 이름 — 0 = 전체 화면, 그 뒤 창 크기 (큰 것부터).</summary>
        public IReadOnlyList<string> Labels => _labels;

        /// <summary>지금 화면이 목록의 몇 번째인지. 목록에 없는 창 크기(창을 끌어서 바꾼 것)면 −1.</summary>
        public int CurrentIndex
        {
            get
            {
                if (Screen.fullScreenMode != FullScreenMode.Windowed)
                    return 0;
                int found = _windowSizes.FindIndex(size => size.x == Screen.width && size.y == Screen.height);
                return found < 0 ? -1 : found + 1;
            }
        }

        private void Awake()
        {
            if (valueText == null || changeButton == null)
                Debug.LogError($"[{nameof(LobbyResolutionView)}] 참조가 비어 있습니다.", this);

#if !UNITY_STANDALONE
            foreach (GameObject target in pcOnly)
                target.SetActive(false);
#endif

            int maxWidth = Display.main.systemWidth;
            int maxHeight = Display.main.systemHeight;
            _labels.Add(FullScreenText);
            foreach (Vector2Int size in WindowSizes)
            {
                if (size.x < maxWidth && size.y < maxHeight)
                {
                    _windowSizes.Add(size);
                    _labels.Add(string.Format(WindowFormat, size.x, size.y));
                }
            }
        }

        private void OnEnable()
        {
            changeButton.onClick.AddListener(HandleChangeClicked);
            ShowCurrent();
        }

        private void OnDisable()
        {
            changeButton.onClick.RemoveListener(HandleChangeClicked);
        }

        /// <summary>목록에서 골랐다 — 바로 바꾼다. 화면 크기는 다음 프레임에 바뀌어서 글자는 고른 값으로 쓴다.</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= _labels.Count)
                return;

            if (index == 0)
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Vector2Int size = _windowSizes[index - 1];
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            }
            valueText.text = _labels[index];
        }

        // 설정을 열 때마다 지금 화면을 다시 읽는다 — Alt+Enter · 창 끌기로 바뀌었을 수 있다.
        private void ShowCurrent()
        {
            valueText.text = Screen.fullScreenMode != FullScreenMode.Windowed ? FullScreenText : string.Format(WindowFormat, Screen.width, Screen.height);
        }

        private void HandleChangeClicked() => OnListRequested?.Invoke();
    }
}
