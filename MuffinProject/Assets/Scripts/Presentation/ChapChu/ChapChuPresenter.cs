using Chapchu.Game;
using Chapchu.Presentation;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChapChuPresenter : MonoBehaviour
{
    [SerializeField] private ChapChuView chapChuView; // 인스펙터에서 할당
                                                // 인터페이스는 인스펙터에 직렬화되지 않아 컴포넌트로 받고 Awake 에서 꺼낸다.
                                                // IGameRequests 를 구현한 컴포넌트를 연결한다.
    [SerializeField] private MonoBehaviour server;

    private IGameRequests _requests;
    private IGameState _state;
    private int myHandCount = 0;
    private int actorNumber;

    private void Awake()
    {
        _requests = server as IGameRequests;
        _state = server as IGameState;

        if (_requests == null)
            Debug.LogError($"[{nameof(ChapChuPresenter)}] server 에 {nameof(IGameRequests)} 를 구현한 컴포넌트를 연결해야 한다.", this);

        actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
    }

    // 켜지기 전에 지나간 OnHandCountChanged 는 못 받으므로 현재 손패 수를 한 번 읽어 그린다.
    private void Start()
    {
        if (_state == null) return;
        myHandCount = _state.GetCurrentHandCount(actorNumber);
        chapChuView.UpdateOutline(myHandCount == 10);
    }

    private void OnEnable()
    {
        chapChuView.DeclareChapchu += HandleDeclareChapchuRequest;
        GameEvents.OnRequestRejected += HandleRequestRejected;
        GameEvents.OnHandCountChanged += HandleHandCountChanged;
        GameEvents.OnTurnChanged += HandleTurnChanged;
    }


    private void OnDisable()
    {
        chapChuView.DeclareChapchu -= HandleDeclareChapchuRequest;
        GameEvents.OnRequestRejected -= HandleRequestRejected;
        GameEvents.OnHandCountChanged -= HandleHandCountChanged;
        GameEvents.OnTurnChanged -= HandleTurnChanged;
    }

    private void HandleDeclareChapchuRequest()
    {
        _requests?.RequestDeclareChapChu();
    }

    // 거절 사유는 코드(int, RejectCode). 문구는 RejectText 에서 꺼낸다 (약속 변경 ad2dab6).
    private void HandleRequestRejected(int actorNumber, int rejectCode)
    {
        if (this.actorNumber != actorNumber) return;
        Debug.LogWarning($"[GameEvent] RequestRejected {actorNumber} {RejectText.Get(rejectCode)}");
    }

    private void HandleHandCountChanged(int actorNumber, int handCount)
    {
        if (this.actorNumber != actorNumber) return;
        myHandCount = handCount;
        Debug.Log("handCountChanged! : " + handCount);
        chapChuView.UpdateOutline(myHandCount == 10);
    }

    private void HandleTurnChanged(int actorNumber)
    {
        if (this.actorNumber != actorNumber) return;
        chapChuView.UpdateOutline(myHandCount == 10);
    }
}
