using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 손패 입력 — 포인터를 한 곳에서 읽어 HandView 에 넘긴다 (10-ui.md 4절 「PC (마우스)」).
    /// 카드 위에 올리면 손패가 올라오고, 누른 채 움직이면 끌기, 움직이지 않고 클릭하면 집기 — 카드가 버튼을 떼도 포인터를 따라오고 다음 클릭에 그 자리에 놓는다.
    /// 놓은 자리의 판정(사용 · 대상 · 제자리)은 끌다 놓을 때와 같다 — HandPresenter. 우클릭 · Esc 는 쥔 카드를 제자리로(쥔 카드가 없으면 손패를 내린다).
    /// HUD(버튼 · 서랍) 위의 포인터는 손패가 받지 않는다. 손패가 잠겨 있으면 아무것도 하지 않는다 — 대상 선택의 취소는 OpponentSeatsPresenter 가 받는다.
    /// 터치는 구 Input Manager 가 마우스로 흉내 낸다(simulateMouseWithTouches) — 탭 = 집기, 다음 탭 = 놓기. 터치 전용 손짓(훑기 · 쓸어 올리기 · 길게 누르기)은 아직 없다.
    /// </summary>
    [RequireComponent(typeof(HandView))]
    public class HandInput : MonoBehaviour
    {
        private const int LeftButton = 0;
        private const int RightButton = 1;
        private const float ReferenceHeight = HandView.ReferenceHeight; // 캔버스 기준 높이 (15-screen 3절) — 거리 값을 화면 px 로 바꾼다

        [Tooltip("누른 채 이만큼(캔버스 px) 넘게 움직이면 끌기 — 시안 6 × 2. 그보다 덜 움직이고 떼면 집기.")]
        [SerializeField] private float dragThreshold = 12f;

        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private HandView _view;
        private PointerEventData _pointer;
        private CardView _pressed;
        private Vector2 _pressPosition;
        private bool _carrying; // 클릭으로 집은 카드가 버튼 없이 포인터를 따라오는 중 — 다음 클릭에 놓는다

        private void Awake()
        {
            _view = GetComponent<HandView>();
        }

        private void OnDisable()
        {
            _pressed = null;
            _carrying = false;
            // 씬이 내려가는 중이면 카드를 자리로 돌리지 않는다 — 카메라 · HUD 위 층이 먼저 파괴됐을 수 있다
            if (gameObject.scene.isLoaded)
                _view.CancelDrag();
        }

        // 창이 가려지면 끌던 카드는 제자리 (10-ui.md 4절).
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) return;

            _pressed = null;
            _carrying = false;
            _view.CancelDrag();
        }

        private void Update()
        {
            if (_view.IsLocked)
            {
                _pressed = null;
                _carrying = false;
                return;
            }

            // 집은 카드가 그 사이 손패에서 빠졌다 (승인 · 버림 · 훔쳐짐)
            if (_carrying && !_view.IsDragging)
                _carrying = false;

            Vector2 position = Input.mousePosition;

            if (Input.GetMouseButtonDown(RightButton) || Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            if (_carrying)
            {
                // 다음 클릭 = 그 자리에 놓기. 판정은 끌다 놓을 때와 같다 (손패 위면 제자리)
                if (Input.GetMouseButtonDown(LeftButton))
                {
                    _carrying = false;
                    _view.EndDrag(position);
                }
                else
                {
                    _view.Drag(position);
                }
                return;
            }

            if (Input.GetMouseButtonDown(LeftButton))
                Press(position);
            else if (_pressed != null && Input.GetMouseButton(LeftButton))
                Hold(position);
            else if (_pressed != null)
                Release(position);
            else if (!Input.GetMouseButton(LeftButton))
                Hover(position);
        }

        private void Press(Vector2 position)
        {
            if (IsOverHud(position)) return;

            CardView card = _view.CardAt(position);
            if (card == null)
            {
                _view.Lower();
                return;
            }

            _pressed = card;
            _pressPosition = position;
        }

        private void Hold(Vector2 position)
        {
            if (!_view.IsDragging)
            {
                float threshold = dragThreshold * Screen.height / ReferenceHeight;
                if ((position - _pressPosition).sqrMagnitude <= threshold * threshold) return;

                _view.BeginDrag(_pressed, _pressPosition);
                if (!_view.IsDragging)
                {
                    _pressed = null; // 그 사이 카드가 손패에서 빠졌다
                    return;
                }
            }

            _view.Drag(position);
        }

        private void Release(Vector2 position)
        {
            CardView card = _pressed;
            _pressed = null;

            if (_view.IsDragging)
            {
                _view.EndDrag(position);
                return;
            }

            if (card == null) return; // 누르고 있는 사이 지워졌다

            // 움직이지 않고 뗐다 — 집기. 잡은 지점을 유지한 채 버튼 없이 따라온다
            _view.BeginDrag(card, _pressPosition);
            if (!_view.IsDragging) return; // 그 사이 카드가 손패에서 빠졌다

            _carrying = true;
            _view.Drag(position);
        }

        private void Hover(Vector2 position)
        {
            // 창 밖 · HUD 위는 손패를 내린다 (10-ui.md 4절) — 올라온 손패가 덮는 HUD 는 그동안 레이를 막지 않아 여기 걸리지 않는다
            bool inWindow = position.x >= 0f && position.y >= 0f && position.x <= Screen.width && position.y <= Screen.height;
            if (!inWindow || IsOverHud(position))
                _view.ClearPointer(true);
            else
                _view.SetPointer(position);
        }

        private void Cancel()
        {
            _pressed = null;
            _carrying = false;
            if (_view.IsDragging)
                _view.CancelDrag();
            else
                _view.Lower();
        }

        // 포인터 아래 맨 앞이 HUD(캔버스의 그림)인가 — 버튼 · 서랍 위의 클릭을 손패가 가로채지 않게. 3D 물체(덱 · 함정 칸)는 HUD 가 아니다.
        private bool IsOverHud(Vector2 position)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            if (_pointer == null)
                _pointer = new PointerEventData(eventSystem);
            _pointer.position = position;
            _hits.Clear();
            eventSystem.RaycastAll(_pointer, _hits);
            return _hits.Count > 0 && _hits[0].module is GraphicRaycaster;
        }
    }
}
