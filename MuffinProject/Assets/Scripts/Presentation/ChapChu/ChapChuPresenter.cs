using Chapchu.Game;
using Chapchu.Presentation;
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

    private void Awake()
    {
        _requests = server as IGameRequests;

        if (_requests == null)
            Debug.LogError($"[{nameof(DeckPresenter)}] server 에 {nameof(IGameRequests)} 를 구현한 컴포넌트를 연결해야 한다.", this);
    }

    private void OnEnable()
    {
        chapChuView.DeclareChapchu += HandleDeclareChapchuRequest;
        GameEvents.OnRequestRejected += HandleRequestRejected;
    }



    private void OnDisable()
    {
        chapChuView.DeclareChapchu -= HandleDeclareChapchuRequest;
        GameEvents.OnRequestRejected -= HandleRequestRejected;
    }

    private void HandleDeclareChapchuRequest()
    {
        _requests?.RequestDeclareChapChu();
    }
    private void HandleRequestRejected(int arg1, string arg2)
    {
        Debug.LogWarning($"[GameEvent] RequestRejected {arg1} {arg2}");
    }
}
