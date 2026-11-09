using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 상대 손패 — 뒷면만 장수에 맞춰 캐릭터 손 자리에 부채꼴로 든다 (10-ui.md 4절 「상대 손패」 HUD v2). 카드마다 손 아래 한 점을 축으로 회전한다 (카드 피벗이 아래 바깥).
    /// 카드 내용은 모르고 장수만 안다. 뒷면은 서로 구분되지 않으므로 줄일 때는 맨 뒤를 지운다.
    /// 무엇을 몇 장으로 할지는 OpponentSeatView 가 정한다. 이 뷰는 서버를 모른다.
    /// </summary>
    public class OpponentHandView : MonoBehaviour
    {
        [Tooltip("뒷면 카드 프리팹 — 피벗이 카드 아래 바깥(회전축).")]
        [SerializeField] private RectTransform backCardPrefab;
        [Tooltip("새 뒷면이 날아오기 시작하는 자리 — 덱 더미 (3D 층). 화면 좌표로 바꿔 캔버스에 놓는다.")]
        [SerializeField] private Transform drawOrigin;
        [Tooltip("자리 이동 연출 초. 내 손패(HandView)와 같게.")]
        [SerializeField] private float moveDuration = 1f;
        [Tooltip("카드 한 장이 더해질 때 벌어지는 각도 (시안 6°).")]
        [SerializeField] private float spreadPerCard = 6f;
        [Tooltip("부채꼴 전체 각도 상한 (시안 54°).")]
        [SerializeField] private float maxSpread = 54f;
        [Tooltip("부채꼴에 그리는 최대 장수 — 넘치면 숫자 배지로만 (시안 10).")]
        [SerializeField] private int maxShown = 10;

        private readonly List<RectTransform> _cards = new List<RectTransform>();

        public int Count => _cards.Count;

        private void Awake()
        {
            if (backCardPrefab == null || drawOrigin == null)
                Debug.LogError($"[{nameof(OpponentHandView)}] {nameof(backCardPrefab)} 또는 {nameof(drawOrigin)} 참조가 비어 있습니다.", this);
        }

        /// <summary>장수를 맞춘다 — 모자라면 덱 자리에서 뒷면이 날아오고, 넘치면 맨 뒤를 지운다.</summary>
        public void SetCount(int count)
        {
            int shown = Mathf.Min(count, maxShown);
            while (_cards.Count < shown)
            {
                RectTransform card = Instantiate(backCardPrefab, transform);
                card.position = DrawOriginOnCanvas();
                _cards.Add(card);
            }

            while (_cards.Count > shown)
            {
                RectTransform card = _cards[_cards.Count - 1];
                _cards.RemoveAt(_cards.Count - 1);
                card.DOKill();
                Destroy(card.gameObject);
            }

            Layout();
        }

        // 3D 층의 덱 자리 → 이 캔버스 위치. Overlay 캔버스는 월드 단위가 화면 px 라 화면 좌표를 그대로 쓴다.
        private Vector3 DrawOriginOnCanvas()
        {
            Camera camera = Camera.main;
            if (camera == null) return drawOrigin.position;

            Vector3 screen = camera.WorldToScreenPoint(drawOrigin.position);
            return new Vector3(screen.x, screen.y, 0f);
        }

        // 가운데를 기준으로 좌우 대칭. 전체 각도 = 장수 × 6°, 상한 54°. 오른쪽 카드가 위에 그려진다 — HandView 와 같다.
        private void Layout()
        {
            if (_cards.Count == 0) return;

            float spread = Mathf.Min(_cards.Count * spreadPerCard, maxSpread);
            for (int i = 0; i < _cards.Count; i++)
            {
                float angle = _cards.Count > 1 ? -spread / 2f + spread * i / (_cards.Count - 1) : 0f;

                RectTransform card = _cards[i];
                card.SetSiblingIndex(i);
                card.DOKill();
                card.DOLocalMove(Vector3.zero, moveDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
                card.DOLocalRotateQuaternion(Quaternion.Euler(0f, 0f, -angle), moveDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            }
        }
    }
}
