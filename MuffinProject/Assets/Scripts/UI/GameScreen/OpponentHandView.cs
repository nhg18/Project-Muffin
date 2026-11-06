using System.Collections.Generic;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 상대 손패 — 뒷면만 장수에 맞춰 스플라인 부채꼴로 든다 (10-ui.md 4절 「상대 손패」). 내 손패(HandView)와 같은 배치 규칙.
    /// 카드 내용은 모르고 장수만 안다. 뒷면은 서로 구분되지 않으므로 줄일 때는 맨 뒤를 지운다.
    /// 무엇을 몇 장으로 할지는 OpponentSeatView 가 정한다. 이 뷰는 서버를 모른다.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class OpponentHandView : MonoBehaviour
    {
        // 10장까지는 스플라인 길이의 1/10 간격, 넘으면 1/(장수+1) 로 좁힌다 — 내 손패와 같다.
        private const int SpacingBaseCount = 10;

        [Tooltip("뒷면 카드 프리팹 (피벗 아래 가운데).")]
        [SerializeField] private RectTransform backCardPrefab;
        [Tooltip("새 뒷면이 날아오기 시작하는 자리 — 덱 더미 (3D 층). 화면 좌표로 바꿔 캔버스에 놓는다.")]
        [SerializeField] private Transform drawOrigin;
        [Tooltip("자리 이동 연출 초. 내 손패(HandView)와 같게.")]
        [SerializeField] private float moveDuration = 1f;

        private readonly List<RectTransform> _cards = new List<RectTransform>();
        private SplineContainer _spline;

        public int Count => _cards.Count;

        private void Awake()
        {
            _spline = GetComponent<SplineContainer>();

            if (backCardPrefab == null || drawOrigin == null)
                Debug.LogError($"[{nameof(OpponentHandView)}] {nameof(backCardPrefab)} 또는 {nameof(drawOrigin)} 참조가 비어 있습니다.", this);
        }

        // 3D 층의 덱 자리 → 이 캔버스 위치. Overlay 캔버스는 월드 단위가 화면 px 라 화면 좌표를 그대로 쓴다.
        private Vector3 DrawOriginOnCanvas()
        {
            Camera camera = Camera.main;
            if (camera == null) return drawOrigin.position;

            Vector3 screen = camera.WorldToScreenPoint(drawOrigin.position);
            return new Vector3(screen.x, screen.y, 0f);
        }

        /// <summary>장수를 맞춘다 — 모자라면 덱 자리에서 뒷면이 날아오고, 넘치면 맨 뒤를 지운다.</summary>
        public void SetCount(int count)
        {
            while (_cards.Count < count)
            {
                RectTransform card = Instantiate(backCardPrefab, transform);
                card.position = DrawOriginOnCanvas();
                _cards.Add(card);
            }

            while (_cards.Count > count)
            {
                RectTransform card = _cards[_cards.Count - 1];
                _cards.RemoveAt(_cards.Count - 1);
                card.DOKill();
                Destroy(card.gameObject);
            }

            Layout();
        }

        // 가운데(0.5)를 기준으로 좌우 대칭으로 놓는다. 오른쪽 카드가 위에 그려진다 — HandView.Layout 과 같다.
        private void Layout()
        {
            if (_cards.Count == 0) return;

            float spacing = _cards.Count > SpacingBaseCount ? 1f / (_cards.Count + 1f) : 1f / SpacingBaseCount;
            float first = 0.5f - (_cards.Count - 1) * spacing / 2f;
            Spline spline = _spline.Spline;

            for (int i = 0; i < _cards.Count; i++)
            {
                float t = first + i * spacing;
                float3 position = spline.EvaluatePosition(t);
                float3 tangent = spline.EvaluateTangent(t);
                float angle = math.degrees(math.atan2(tangent.y, tangent.x));

                RectTransform card = _cards[i];
                card.SetSiblingIndex(i);
                card.DOKill();
                card.DOLocalMove(new Vector3(position.x, position.y, 0f), moveDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
                card.DOLocalRotateQuaternion(Quaternion.Euler(0f, 0f, angle), moveDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            }
        }
    }
}
