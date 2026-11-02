using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패를 스플라인 부채꼴로 놓는다 (10-ui.md 4절 · 2-1 「손패」). 순서는 드로우 순 — 정렬하지 않는다 (05-deck.md 6절).
    /// 카드는 이 오브젝트의 자식이고, 위치 · 회전은 스플라인의 로컬 좌표를 그대로 쓴다.
    /// 무엇을 넣고 뺄지는 프레젠터가 정한다. 이 뷰는 서버를 모른다.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class HandView : MonoBehaviour
    {
        // 10장까지는 스플라인 길이의 1/10 간격, 넘으면 1/(장수+1) 로 좁힌다 (10-ui.md 4절 「10장 초과 시 간격 축소」).
        private const int SpacingBaseCount = 10;

        [Header("종류별 카드 프리팹")]
        [SerializeField] private HandCardView actionCardPrefab;
        [SerializeField] private HandCardView counterCardPrefab;
        [SerializeField] private HandCardView trapCardPrefab;

        [Header("연출")]
        [SerializeField] private float moveDuration = 1f;

        private readonly List<HandCardView> _cards = new List<HandCardView>();
        private SplineContainer _spline;

        public int Count => _cards.Count;

        private void Awake()
        {
            _spline = GetComponent<SplineContainer>();

            if (actionCardPrefab == null || counterCardPrefab == null || trapCardPrefab == null)
                Debug.LogError($"[{nameof(HandView)}] 종류별 카드 프리팹 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>카드 종류에 맞는 프리팹을 spawnWorldPosition(덱 자리)에 만들어 손패 끝에 붙이고 다시 놓는다.</summary>
        public HandCardView AddCard(int instanceId, CardData data, Vector3 spawnWorldPosition)
        {
            HandCardView card = Instantiate(PrefabOf(data.type), transform);
            card.transform.position = spawnWorldPosition;
            card.Setup(instanceId, data);

            _cards.Add(card);
            Layout();
            return card;
        }

        /// <summary>해당 장을 없애고 나머지를 다시 놓는다. 없으면 false.</summary>
        public bool RemoveCard(int instanceId)
        {
            int index = _cards.FindIndex(c => c.InstanceId == instanceId);
            if (index < 0) return false;

            HandCardView card = _cards[index];
            _cards.RemoveAt(index);
            card.transform.DOKill();
            Destroy(card.gameObject);

            Layout();
            return true;
        }

        /// <summary>가운데(0.5)를 기준으로 좌우 대칭으로 놓는다. 오른쪽 카드가 위에 그려진다.</summary>
        public void Layout()
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

                Transform card = _cards[i].transform;
                card.SetSiblingIndex(i);
                card.DOKill();
                card.DOLocalMove(new Vector3(position.x, position.y, 0f), moveDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
                card.DOLocalRotateQuaternion(Quaternion.Euler(0f, 0f, angle), moveDuration).SetEase(Ease.OutQuart).SetLink(card.gameObject);
            }
        }

        private HandCardView PrefabOf(CardType type)
        {
            switch (type)
            {
                case CardType.Counter: return counterCardPrefab;
                case CardType.Trap:    return trapCardPrefab;
                default:               return actionCardPrefab;
            }
        }
    }
}
