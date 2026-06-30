using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    [SerializeField] private float selectDuration = 30f;

    //public IEnumerator GetTargetNumber(System.Action<List<int>> onResult)
    //{
    //    List<int> result = new List<int>();
    //    switch (Target)
    //    {
    //        case TargetType.One:
    //            yield return StartCoroutine(ClickDetectionCorutine(selectDuration, clicked =>
    //            {
                    
    //                if (clicked != null)
    //                {
    //                    Debug.Log($"클릭한 오브젝트: {clicked.name}");
    //                    OtherPlayerHands oph = clicked.GetComponentInParent<OtherPlayerHands>();
    //                    result.Add(oph.PlayerNumber);
    //                }
    //                else
    //                {
    //                    result.Add(-1);
    //                }
    //            }));
    //            break;
    //        case TargetType.Two:
    //            yield return StartCoroutine(ClickDetectionCorutine(selectDuration, clicked =>
    //            {
                    
    //                if (clicked != null)
    //                {
    //                    OtherPlayerHands oph = clicked.GetComponentInParent<OtherPlayerHands>();
    //                    result.Add(oph.PlayerNumber);
    //                }
    //                else
    //                {
    //                    result.Add(-1);
    //                }
    //            }));
    //            yield return StartCoroutine(ClickDetectionCorutine(selectDuration, clicked =>
    //            {
                    
    //                if (clicked != null)
    //                {
    //                    OtherPlayerHands oph = clicked.GetComponentInParent<OtherPlayerHands>();
    //                    result.Add(oph.PlayerNumber);
    //                }
    //                else
    //                {
    //                    result.Add(-1);
    //                }
    //            }));
    //            break;
    //        case TargetType.All_Others:
    //            for (int i = 1; i <= PhotonNetwork.CurrentRoom.PlayerCount; i++)
    //            {
    //                if(i!= PhotonNetwork.LocalPlayer.ActorNumber)
    //                {
    //                    result.Add(i);
    //                }
    //            }
    //            break;
    //        case TargetType.Everyone:
    //            for(int i=1;i<= PhotonNetwork.CurrentRoom.PlayerCount; i++)
    //            {
    //                result.Add(i);
    //            }
    //            break;
    //        case TargetType.Me:
    //            result.Add(PhotonNetwork.LocalPlayer.ActorNumber);
    //            break;
    //        default:
    //            result.Add(-1);//타겟을 찾을수 없음
    //            break;
    //    }
    //    onResult(result);
    //    yield break;
    //}

    //IEnumerator ClickDetectionCorutine(float duration, System.Action<GameObject> onResult)
    //{
    //    float timer = 0f;

    //    while (timer < duration)
    //    {
    //        // Update처럼 매 프레임 실행
    //        if (Input.GetMouseButtonDown(0))
    //        {
    //            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    //            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

    //            if (hit.collider != null && hit.collider.CompareTag("CardBack"))
    //            {
    //                onResult(hit.collider.gameObject);
    //                yield break;
    //            }
    //            else
    //            {
    //                onResult(null);
    //                yield break;
    //            }
    //        }

    //        timer += Time.deltaTime;
    //        yield return null; // 다음 프레임까지 대기
    //    }
    //}


    public async Task<List<int>> GetTargetNum()//턴종료, 게임종료, 튕김시 CancellationToken 필요
    {
        List<int> result = new List<int>();
        switch (Target)
        {
            case TargetType.One:
                int selected = await WaitInputOrTimeAsync(20.0f);
                result.Add(selected);
                break;
            case TargetType.Two:
                int selected2 = await WaitInputOrTimeAsync(20.0f);
                result.Add(selected2);
                int selected3 = await WaitInputOrTimeAsync(20.0f);
                result.Add(selected3);
                break;
            case TargetType.All_Others:
                for (int i = 1; i <= PhotonNetwork.CurrentRoom.PlayerCount; i++)
                {
                    if (i != PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        result.Add(i);
                    }
                }
                break;
            case TargetType.Everyone:
                for (int i = 1; i <= PhotonNetwork.CurrentRoom.PlayerCount; i++)
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

        return result;
    }

    async Task<int> WaitInputOrTimeAsync(float waitTime)
    {
        float timer = 0f;
        while(timer < waitTime)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

                if (hit.collider != null && hit.collider.CompareTag("CardBack"))
                {
                    GameObject cards = hit.collider.gameObject;
                    OtherPlayerHands oph = cards.GetComponentInParent<OtherPlayerHands>();
                    return oph.PlayerNumber;
                }
                else
                {
                    return -1;
                }
            }
            timer += Time.deltaTime;
            await Task.Yield();
        }
        return -1;
    }




    }
