using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToWho : MonoBehaviourPunCallbacks
{
    public enum TargetType
    {
        One,
        Two,
        All_Others,
        Everyone,
        Me
    }
    [Header("Target")]
    [SerializeField] private TargetType Target = new TargetType();

    public IEnumerator GetTargetNumber(System.Action<List<int>> onResult)
    {
        List<int> result = new List<int>();
        switch (Target)
        {
            case TargetType.One:
                yield return StartCoroutine(ClickDetectionCorutine(30, clicked =>
                {
                    Debug.Log($"클릭한 오브젝트: {clicked.name}");
                    OtherPlayerHands oph = clicked.GetComponentInParent<OtherPlayerHands>();
                    result.Add(oph.PlayerNumber);
                }));
                break;
            case TargetType.Two:
                yield return StartCoroutine(ClickDetectionCorutine(30, clicked =>
                {
                    Debug.Log($"클릭한 오브젝트: {clicked.name}");
                    OtherPlayerHands oph = clicked.GetComponentInParent<OtherPlayerHands>();
                    result.Add(oph.PlayerNumber);
                }));
                yield return StartCoroutine(ClickDetectionCorutine(30, clicked =>
                {
                    Debug.Log($"클릭한 오브젝트: {clicked.name}");
                    OtherPlayerHands oph = clicked.GetComponentInParent<OtherPlayerHands>();
                    result.Add(oph.PlayerNumber);
                }));
                break;
            case TargetType.All_Others:
                for (int i = 1; i <= PhotonNetwork.CurrentRoom.PlayerCount; i++)
                {
                    if(i!= PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        result.Add(i);
                    }
                }
                break;
            case TargetType.Everyone:
                for(int i=1;i<= PhotonNetwork.CurrentRoom.PlayerCount; i++)
                {
                    result.Add(i);
                }
                break;
            case TargetType.Me:
                result.Add(PhotonNetwork.LocalPlayer.ActorNumber);
                break;
            default:
                result.Add(-1);//타겟을 찾을수 없음
                break;
        }
        onResult(result);
        yield break;
    }

    IEnumerator ClickDetectionCorutine(float duration, System.Action<GameObject> onResult)
    {
        float timer = 0f;

        while (timer < duration)
        {
            Debug.Log("TICK");
            // Update처럼 매 프레임 실행
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

                if (hit.collider != null && hit.collider.CompareTag("CardBack"))
                {
                    Debug.Log("SELECT");
                    onResult(hit.collider.gameObject);
                    yield break;
                }
            }

            timer += Time.deltaTime;
            yield return null; // 다음 프레임까지 대기
        }
    }

}
