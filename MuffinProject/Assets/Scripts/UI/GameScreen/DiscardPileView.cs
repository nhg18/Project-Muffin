using Chapchu.Cards;
using Chapchu.Game;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 버림 더미 — 받침 흰 카드 위에 맨 위 카드 앞면 + 장수 (10-ui.md 2-1 「버림 더미」, 05-deck.md 7 · 9절).
    /// 맨 위 카드는 종류별 카드 프리팹을 맨 위 자리에 새로 만들어 CardData 를 입힌다. 날아온 카드는 보낸 쪽이 지운다 — 같은 자리 · 같은 자세라 바뀌는 것이 보이지 않는다.
    /// 카드 프리팹은 손패 층(HandCards)이라 이 오브젝트의 레이어로 바꿔 덱과 같은 층에 둔다 — 내 손패가 항상 더미 위에 그려지게.
    /// 누를 수 없다 (더미 내용 보기는 규칙에 없다). 무엇이 맨 위인지는 프레젠터가 정한다.
    /// </summary>
    public class DiscardPileView : MonoBehaviour
    {
        private const string CountFormat = "버림 {0}";
        // 맨 위 카드는 종류만 보인다 — 어느 장인지는 쓰지 않는다.
        private const int NoInstanceId = -1;

        [Header("종류별 카드 프리팹 (HandView 와 같은 것)")]
        [SerializeField] private CardView actionCardPrefab;
        [SerializeField] private CardView counterCardPrefab;
        [SerializeField] private CardView trapCardPrefab;

        [Tooltip("맨 위 카드가 놓이는 자리 — 카드 기준점(아래 가운데)의 위치 · 회전 · 배율. 날아오는 카드도 이 자세로 닿는다.")]
        [SerializeField] private Transform topAnchor;
        [Tooltip("받침 흰 카드, 아래부터. 맨 위 카드 밑에 깔린 장수만큼만 켠다.")]
        [SerializeField] private GameObject[] underCards;
        [SerializeField] private TMP_Text countText;

        private CardView _top;

        /// <summary>맨 위 카드가 놓이는 자리 — 버림 더미로 날아오는 카드의 도착 자세.</summary>
        public Transform TopAnchor => topAnchor;

        private void Awake()
        {
            if (actionCardPrefab == null || counterCardPrefab == null || trapCardPrefab == null || topAnchor == null || underCards == null || countText == null)
            {
                Debug.LogError($"[{nameof(DiscardPileView)}] 카드 프리팹 · {nameof(topAnchor)} · {nameof(underCards)} · {nameof(countText)} 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            // 빈 더미로 시작한다 — 값은 프레젠터가 넣는다.
            Set(null, 0);
        }

        /// <summary>맨 위 카드와 장수를 바꾼다. count 가 0 이면 카드 없이 장수만 남는다. top 이 null 이면 맨 위 카드를 비운다.</summary>
        public void Set(CardData top, int count)
        {
            if (_top != null)
            {
                Destroy(_top.gameObject);
                _top = null;
            }

            if (top != null && count > 0)
            {
                _top = Instantiate(PrefabOf(top.type), topAnchor);
                _top.SetGradeVisible(false); // 등급 연출은 손패 · 가운데 연출 · 크게 보기에서만 (10-ui.md 2-1 「카드 등급 연출」)
                _top.Setup(NoInstanceId, top);
                _top.SetRaycastBlocking(false);
                foreach (Transform child in _top.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = gameObject.layer;
            }

            // 맨 위 카드 밑에 깔린 장수 = count − 1. 위쪽 받침부터 켠다.
            int under = Mathf.Max(0, count - 1);
            for (int i = 0; i < underCards.Length; i++)
                underCards[i].SetActive(underCards.Length - i <= under);

            countText.text = string.Format(CountFormat, Mathf.Max(0, count));
        }

        private CardView PrefabOf(CardType type)
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
