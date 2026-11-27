using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 설정 「화면」 — PC 에서 전체 화면 · 창 크기를 「&lt;」 「&gt;」 로 고르면 바로 바꾼다. 모바일에서는 묶음째 숨긴다.
    /// 고른 값은 Unity 가 저장해 다음 실행에도 그대로 연다 (Standalone Player 기본 동작).
    /// 기준: 전시판 요청 (2026-11-27 PM) — 17-lobby-ui-full 7-4 에 아직 없다.
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

        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text valueText;

        [Tooltip("PC 가 아니면 끄는 것 — 묶음 이름 · 묶음 앞 빈칸 · 이 줄")]
        [SerializeField] private GameObject[] pcOnly;

        // 0 = 전체 화면, 1~ = 창 크기
        private readonly List<Vector2Int> _windowSizes = new List<Vector2Int>();
        private int _index;

        private void Awake()
        {
            if (prevButton == null || nextButton == null || valueText == null)
                Debug.LogError($"[{nameof(LobbyResolutionView)}] 참조가 비어 있습니다.", this);

#if !UNITY_STANDALONE
            foreach (GameObject target in pcOnly)
                target.SetActive(false);
#endif

            int maxWidth = Display.main.systemWidth;
            int maxHeight = Display.main.systemHeight;
            foreach (Vector2Int size in WindowSizes)
            {
                if (size.x < maxWidth && size.y < maxHeight)
                    _windowSizes.Add(size);
            }
        }

        private void OnEnable()
        {
            prevButton.onClick.AddListener(HandlePrevClicked);
            nextButton.onClick.AddListener(HandleNextClicked);
            ReadCurrent();
        }

        private void OnDisable()
        {
            prevButton.onClick.RemoveListener(HandlePrevClicked);
            nextButton.onClick.RemoveListener(HandleNextClicked);
        }

        // 설정을 열 때마다 지금 화면을 다시 읽는다 — Alt+Enter 로 바뀌었을 수 있다.
        private void ReadCurrent()
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                _index = 0;
                valueText.text = FullScreenText;
                return;
            }

            int found = _windowSizes.FindIndex(size => size.x == Screen.width && size.y == Screen.height);
            _index = found + 1; // 목록에 없는 크기(창을 끌어서 바꾼 것)면 0 — 다음 누름은 첫 창 크기로
            valueText.text = string.Format(WindowFormat, Screen.width, Screen.height);
        }

        private void HandlePrevClicked() => Select(_index - 1);

        private void HandleNextClicked() => Select(_index + 1);

        private void Select(int index)
        {
            int count = _windowSizes.Count + 1;
            _index = (index % count + count) % count;
            if (_index == 0)
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
                valueText.text = FullScreenText;
                return;
            }

            Vector2Int size = _windowSizes[_index - 1];
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            valueText.text = string.Format(WindowFormat, size.x, size.y);
        }
    }
}
