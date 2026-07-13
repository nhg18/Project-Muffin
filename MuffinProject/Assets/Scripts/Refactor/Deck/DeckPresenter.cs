using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckPresenter : MonoBehaviour
{
    private DeckModel deckModel;
    [SerializeField] private DeckView deckView; // 인스펙터에서 할당

    private void Awake()
    {
        // 1. 모델 생성
        deckModel = new DeckModel();

        // 2. 임시 카드로 덱 초기화 (실제 게임에서는 별도의 데이터 매니저에서 받아옴)
        List<CardData> startingCards = new List<CardData>
        {
            new CardData { CardID = 1 },
            new CardData { CardID = 2 },
            new CardData { CardID = 3 }
        };
        deckModel.InitDeck(startingCards);

        // 3. View의 버튼 클릭 이벤트 구독
        if (deckView != null)
        {
            deckView.OnDrawButtonClicked += HandleDrawRequest;
        }
    }

    // View에서 버튼을 눌렀을 때 실행되는 로직
    private void HandleDrawRequest()
    {
        int cardID = deckModel.Draw();

        if (cardID != -1)
        {
            Debug.Log($"드로우 성공! 카드 ID: {cardID}");
            // 이벤트 버스에 카드 생성 명령 하달 -> HandView가 듣고 카드를 생성함
            DeckEvent.RaiseDrawn(cardID);
        }
        else
        {
            Debug.LogWarning("덱이 비어있습니다.");
        }
    }
}
