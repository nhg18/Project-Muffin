using System.Collections.Generic;
using Chapchu.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 키워드 설명 창 (10-ui.md 7-1 「대표 키워드」, 11-card-list.md 5-1, 시안 「카드 키워드 v2」 #tip).
    /// PC: 커서가 카드 문구의 키워드 위에 있으면 그 키워드 하나를 보인다 — 머리(아이콘 · 키워드 · 분류, 분류 색 옅은 바탕) → 사전의 뜻.
    /// 보는 곳: 3D 층의 카드(손패 · 낸 카드 · 함정 — 앞면이 보일 때)와 고르기 판의 카드 칸. 창은 키워드 위에, 위 자리가 모자라면 아래에.
    /// 모바일의 「길게 누르기 크게 보기 동안 카드 옆에 모두」는 크게 보기(10-ui 4절)가 아직 없어 하지 않는다 — 모바일에서는 이 컴포넌트를 끈다.
    /// </summary>
    public class KeywordTipView : MonoBehaviour
    {
        private const float MaxRayDistance = 1000f;

        [SerializeField] private HandView hand;
        [Tooltip("창 — 켜고 끄며 자리를 옮긴다. 세로 레이아웃 + 내용 크기 맞춤이라 높이는 뜻 줄 수를 따른다.")]
        [SerializeField] private RectTransform panel;
        [Tooltip("머리 바탕 (분류 색을 흰색에 옅게 섞는다).")]
        [SerializeField] private Graphic headFill;
        [Tooltip("머리 바탕의 아래 반 — 머리 아래 모서리를 각지게 덮는다. 머리 바탕과 같은 색.")]
        [SerializeField] private Graphic headSquare;
        [Tooltip("머리 글 — 아이콘 + 키워드 이름.")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("머리 오른쪽 분류 이름.")]
        [SerializeField] private TMP_Text categoryText;
        [Tooltip("사전의 뜻.")]
        [SerializeField] private TMP_Text meaningText;
        [Tooltip("머리 바탕에 섞는 분류 색의 양 (시안 color-mix 16%).")]
        [SerializeField, Range(0f, 1f)] private float headTint = 0.16f;
        [Tooltip("키워드와 창 사이 (캔버스 px, 시안 10 × 2).")]
        [SerializeField] private float gap = 20f;
        [Tooltip("화면 가장자리에서 띄우는 거리 (캔버스 px, 시안 12 × 2).")]
        [SerializeField] private float edgeMargin = 24f;

        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;
        private Camera _camera;
        private Canvas _canvas;
        private TMP_Text _shownText;   // 지금 보이는 창의 글 · 링크 — 같으면 다시 놓지 않는다
        private int _shownLink = -1;

        private void Awake()
        {
            if (hand == null || panel == null || headFill == null || headSquare == null || nameText == null || categoryText == null || meaningText == null)
                Debug.LogError($"[{nameof(KeywordTipView)}] 손패 · 창 · 머리 · 글 참조 중 비어 있는 것이 있습니다.", this);

            _canvas = GetComponentInParent<Canvas>();

            // 모바일은 커서가 없다 — 터치를 흉내 낸 마우스 자리에 창이 남지 않게 끈다 (10-ui 7-1: 모바일은 크게 보기 동안)
            if (Application.isMobilePlatform)
                enabled = false;
        }

        private void Start()
        {
            _camera = Camera.main;
            if (_camera == null)
                Debug.LogError($"[{nameof(KeywordTipView)}] MainCamera 태그의 카메라가 없습니다 — 3D 카드의 키워드를 못 찾는다.", this);
        }

        private void OnDisable() => Hide();

        private void Update()
        {
            if (!TryFindKeyword(Input.mousePosition, out TMP_Text text, out int linkIndex, out Camera textCamera))
            {
                Hide();
                return;
            }

            if (text == _shownText && linkIndex == _shownLink) return;

            Show(text, linkIndex, textCamera);
        }

        private void Hide()
        {
            _shownText = null;
            _shownLink = -1;
            if (panel != null && panel.gameObject.activeSelf)
                panel.gameObject.SetActive(false);
        }

        // 포인터 아래 키워드 링크 — 맨 앞이 HUD 면 고르기 판의 카드 칸만, 아니면 손패 → 그 밖의 3D 카드
        private bool TryFindKeyword(Vector2 position, out TMP_Text text, out int linkIndex, out Camera textCamera)
        {
            text = null;
            linkIndex = -1;
            textCamera = null;
            if (_camera == null || hand.IsDragging) return false;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                _pointer ??= new PointerEventData(eventSystem);
                _pointer.position = position;
                _hits.Clear();
                eventSystem.RaycastAll(_pointer, _hits);
                if (_hits.Count > 0 && _hits[0].module is GraphicRaycaster)
                {
                    ChoiceCardTile tile = _hits[0].gameObject.GetComponentInParent<ChoiceCardTile>();
                    if (tile == null) return false;

                    text = tile.DescriptionText;
                    Canvas canvas = text.canvas;
                    textCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                    linkIndex = TMP_TextUtilities.FindIntersectingLink(text, position, textCamera);
                    return linkIndex >= 0;
                }
            }

            CardView card = hand.CardAt(position);
            if (card == null && Physics.Raycast(_camera.ScreenPointToRay(position), out RaycastHit hit, MaxRayDistance))
                card = hit.collider.GetComponentInParent<CardView>();
            if (card == null) return false;

            text = card.DescriptionText;
            // 뒤집힌 카드(뒷면이 보이는 카드)의 글은 읽을 수 없다 — TMP 글은 카메라와 같은 쪽을 볼 때 읽힌다
            if (Vector3.Dot(text.transform.forward, _camera.transform.forward) <= 0f) return false;

            textCamera = _camera; // 위 층 카메라들은 메인 카메라와 같은 자리 · 같은 화각 (TopCardLayerView)
            linkIndex = TMP_TextUtilities.FindIntersectingLink(text, position, textCamera);
            return linkIndex >= 0;
        }

        private void Show(TMP_Text text, int linkIndex, Camera textCamera)
        {
            TMP_LinkInfo link = text.textInfo.linkInfo[linkIndex];
            if (!CardKeywords.TryGet(link.GetLinkID(), out CardKeyword keyword))
            {
                Hide();
                return;
            }

            _shownText = text;
            _shownLink = linkIndex;

            Color tint = Color.Lerp(Color.white, keyword.Color, headTint);
            headFill.color = tint;
            headSquare.color = tint;
            nameText.text = $"<sprite=\"{CardKeywords.IconAssetName}\" name=\"{keyword.Icon}\"> {keyword.Name}";
            categoryText.text = keyword.Category;
            meaningText.text = keyword.Meaning;
            panel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            // 링크 글자들의 줄 위 · 아래 가운데 → 캔버스 좌표
            TMP_TextInfo info = text.textInfo;
            TMP_CharacterInfo first = info.characterInfo[link.linkTextfirstCharacterIndex];
            TMP_CharacterInfo last = info.characterInfo[link.linkTextfirstCharacterIndex + link.linkTextLength - 1];
            TMP_LineInfo line = info.lineInfo[first.lineNumber];
            float centerX = (first.bottomLeft.x + last.topRight.x) / 2f;
            Vector2 top = ToCanvas(text.transform.TransformPoint(new Vector3(centerX, line.ascender, 0f)), textCamera);
            Vector2 bottom = ToCanvas(text.transform.TransformPoint(new Vector3(centerX, line.descender, 0f)), textCamera);

            Rect area = ((RectTransform)transform).rect;
            Vector2 size = panel.rect.size;
            bool above = top.y + gap + size.y <= area.yMax - edgeMargin;
            panel.pivot = new Vector2(0.5f, above ? 0f : 1f);
            float x = Mathf.Clamp(top.x, area.xMin + edgeMargin + size.x / 2f, area.xMax - edgeMargin - size.x / 2f);
            panel.localPosition = new Vector3(x, above ? top.y + gap : bottom.y - gap, 0f);
        }

        private Vector2 ToCanvas(Vector3 world, Camera textCamera)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(textCamera, world);
            Camera canvasCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, canvasCamera, out Vector2 local);
            return local;
        }
    }
}
