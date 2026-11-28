using System.Collections.Generic;
using Chapchu.Core;
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
    /// 터치는 손가락을 직접 읽는다 (10-ui.md 4절 모바일) — 닿으면 · 좌우로 훑으면 손가락 x 에 가장 가까운 카드가 올라오고(진동 4ms), 위로 쓸어 올리면 손가락 위로 집고(10ms),
    /// 움직이지 않고 길게 누르면 크게 보기(15ms, 떼면 닫힘), 탭하면 집기 · 다음 탭에 놓기. 첫 손가락만 본다 — 두 번째 손가락은 무시.
    /// 터치가 있는 동안 마우스(터치를 흉내 낸 것)는 읽지 않는다. 마우스가 다시 움직이면 마우스로 돌아간다.
    /// </summary>
    [RequireComponent(typeof(HandView))]
    public class HandInput : MonoBehaviour
    {
        private const int LeftButton = 0;
        private const int RightButton = 1;
        private const float ReferenceHeight = HandView.ReferenceHeight; // 캔버스 기준 높이 (15-screen 3절) — 거리 값을 화면 px 로 바꾼다

        [Tooltip("누른 채 이만큼(캔버스 px) 넘게 움직이면 끌기 — 시안 6 × 2. 그보다 덜 움직이고 떼면 집기.")]
        [SerializeField] private float dragThreshold = 12f;

        [Header("터치 (10-ui.md 4절 모바일 — 거리는 캔버스 px, 시안 × 2)")]
        [Tooltip("탭 — 이 초 안에 떼야 한다.")]
        [SerializeField] private float tapSeconds = 0.35f;
        [Tooltip("탭 · 길게 누르기 — 이 거리 안에서만 (시안 10 × 2).")]
        [SerializeField] private float tapSlop = 20f;
        [Tooltip("위로 이만큼 넘게 쓸어 올리면 집는다 (시안 40 × 2).")]
        [SerializeField] private float swipeUpDistance = 80f;
        [Tooltip("움직이지 않고 이 초를 누르면 크게 보기.")]
        [SerializeField] private float holdSeconds = 0.45f;
        [Tooltip("쓸어 올려 집은 카드의 가운데 — 손가락보다 카드 높이 × 이 값 위.")]
        [SerializeField] private float liftAboveFinger = 0.45f;
        [Tooltip("진동 ms — 훑다가 고른 카드가 바뀔 때 · 쓸어 올려 집을 때 · 크게 보기.")]
        [SerializeField] private int scrubVibrateMs = 4;
        [SerializeField] private int pickVibrateMs = 10;
        [SerializeField] private int inspectVibrateMs = 15;

        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private HandView _view;
        private PointerEventData _pointer;
        private CardView _pressed;
        private Vector2 _pressPosition;
        private bool _carrying; // 클릭으로 집은 카드가 버튼 없이 포인터를 따라오는 중 — 다음 클릭에 놓는다

        // 터치 — 따라가는 손가락 하나
        private int _fingerId = -1;
        private Vector2 _touchStart;
        private float _touchStartTime;
        private bool _touchMoved;      // 탭 · 길게 누르기 거리를 넘었다
        private bool _touchConsumed;   // 이 손가락은 쥔 카드를 놓는 데 썼다 — 뗄 때까지 아무것도 하지 않는다
        private bool _lastWasTouch;    // 마지막 입력이 터치였다 — 흉내 낸 마우스를 읽지 않는다
        private Vector2 _lastTouchPosition;

        private void Awake()
        {
            _view = GetComponent<HandView>();
        }

        private void OnDisable()
        {
            _pressed = null;
            _carrying = false;
            _fingerId = -1;
            _view.EndInspect();
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
            _fingerId = -1;
            _view.EndInspect();
            _view.CancelDrag();
        }

        private void Update()
        {
            if (_view.IsLocked || ScreenTransition.IsPlaying) // 전환 중 입력은 무시한다 (15-screen.md 9-5)
            {
                _pressed = null;
                _carrying = false;
                _fingerId = -1;
                return;
            }

            // 집은 카드가 그 사이 손패에서 빠졌다 (승인 · 버림 · 훔쳐짐)
            if (_carrying && !_view.IsDragging)
                _carrying = false;

            if (Input.touchCount > 0 || _fingerId >= 0)
            {
                UpdateTouch();
                return;
            }

            Vector2 position = Input.mousePosition;
            // 터치를 흉내 낸 마우스는 손가락을 뗀 자리에 머문다 — 마우스가 실제로 움직여야 마우스 입력으로 돌아간다
            if (_lastWasTouch)
            {
                if (position == _lastTouchPosition) return;

                _lastWasTouch = false;
                _pressed = null;
            }

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

        // ── 터치 (10-ui.md 4절 모바일) ──

        private void UpdateTouch()
        {
            if (_fingerId < 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch began = Input.GetTouch(i);
                    if (began.phase == TouchPhase.Began)
                    {
                        BeginTouch(began);
                        break;
                    }
                }
                return;
            }

            if (!TryGetFinger(out Touch touch))
            {
                EndTouch(_lastTouchPosition, true); // 손가락을 놓쳤다 (창 전환 등)
                return;
            }

            _lastTouchPosition = touch.position;
            switch (touch.phase)
            {
                case TouchPhase.Ended:
                    EndTouch(touch.position, false);
                    break;
                case TouchPhase.Canceled:
                    EndTouch(touch.position, true);
                    break;
                default:
                    MoveTouch(touch.position);
                    break;
            }
        }

        private bool TryGetFinger(out Touch touch)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                touch = Input.GetTouch(i);
                if (touch.fingerId == _fingerId) return true;
            }
            touch = default;
            return false;
        }

        private void BeginTouch(Touch touch)
        {
            Vector2 position = touch.position;
            _lastWasTouch = true;
            _lastTouchPosition = position;
            _pressed = null;
            _fingerId = touch.fingerId;
            _touchStart = position;
            _touchStartTime = Time.unscaledTime;
            _touchMoved = false;
            _touchConsumed = false;

            // 탭으로 집은 카드 — 다음 탭 자리에 놓는다. 판정은 끌다 놓을 때와 같다
            if (_carrying)
            {
                _carrying = false;
                _touchConsumed = true;
                _view.Drag(position); // 놓는 자리로 옮긴 뒤 — 조준 · 「놓으면 사용」 표시가 그 자리를 본다
                _view.EndDrag(position);
                return;
            }

            if (IsOverHud(position))
            {
                _touchConsumed = true; // 버튼 · 서랍이 받는다
                return;
            }

            // 손패 밖 탭 — 손패 내림
            if (_view.CardAt(position) == null && !(_view.IsRaised && IsNearRaisedHand(position)))
            {
                _touchConsumed = true;
                _view.Lower();
                return;
            }

            if (_view.SetTouchPointer(position))
                Haptics.Vibrate(scrubVibrateMs);
        }

        private void MoveTouch(Vector2 position)
        {
            if (_touchConsumed || _view.IsInspecting) return; // 크게 보기는 손을 뗄 때까지

            if (_view.IsDragging)
            {
                _view.Drag(position);
                return;
            }

            Vector2 moved = position - _touchStart;
            float slop = ToScreen(tapSlop);
            if (!_touchMoved && moved.sqrMagnitude > slop * slop)
                _touchMoved = true;

            // 위로 쓸어 올리기 — 손가락 위로 집는다. 그 뒤는 끌기와 같다
            CardView card = _view.HoveredCard;
            if (card != null && moved.y > ToScreen(swipeUpDistance) && moved.y > Mathf.Abs(moved.x))
            {
                _view.BeginDragAbove(card, position, liftAboveFinger);
                if (_view.IsDragging)
                {
                    Haptics.Vibrate(pickVibrateMs);
                    _view.Drag(position);
                }
                return;
            }

            // 길게 누르기 — 움직이지 않은 채
            if (!_touchMoved && card != null && Time.unscaledTime - _touchStartTime >= holdSeconds)
            {
                _view.Inspect(card);
                if (_view.IsInspecting)
                    Haptics.Vibrate(inspectVibrateMs);
                return;
            }

            if (_touchMoved && _view.SetTouchPointer(position))
                Haptics.Vibrate(scrubVibrateMs);
        }

        private void EndTouch(Vector2 position, bool canceled)
        {
            _fingerId = -1;
            if (_touchConsumed) return;

            if (_view.IsInspecting)
            {
                _view.EndInspect();
                return;
            }

            if (_view.IsDragging)
            {
                if (canceled)
                    _view.CancelDrag();
                else
                    _view.EndDrag(position);
                return;
            }

            // 탭 — 그 카드를 집는다. 다음 탭에 놓는다. 손을 떼도 손패는 올라온 채
            bool tap = !canceled && !_touchMoved && Time.unscaledTime - _touchStartTime <= tapSeconds;
            CardView card = _view.HoveredCard;
            if (!tap || card == null) return;

            _view.BeginDrag(card, _touchStart);
            if (!_view.IsDragging) return;

            _carrying = true;
            _view.Drag(position);
        }

        // 올라온 손패의 카드 사이 틈 · 바로 위 — 그 손가락으로 계속 훑을 수 있게 손패 밖으로 치지 않는다
        private bool IsNearRaisedHand(Vector2 position) => !_view.IsAbovePlayLine(position);

        private static float ToScreen(float canvasPixels) => canvasPixels * Screen.height / ReferenceHeight;

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
