using Photon.Pun.Demo.PunBasics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Chapchu.Core;
using Chapchu.Game.Cards;

namespace Chapchu.Presentation
{

    public class ClickManager : MonoBehaviour
    {
        #region field
        public PlayerHandPresenter playerHandPresenter;
        public PlayerHandView playerHandView;

        #endregion

        #region Singleton
        public static ClickManager Instance { get; private set; }
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        #endregion


        void Update()
        {
            // 카드 처리(대상 선택) 중에는 손패를 올리거나 내리지 않는다.
            // 좌석 탭은 물리 레이캐스트에 "Card" 로 잡히지 않아 HandsDown 이 불리던 문제도 함께 막는다.
            if (Input.GetMouseButtonDown(0) && !playerHandPresenter.IsCardPlayInProgress)
            {

                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

                if (hit.collider != null && hit.collider.CompareTag("Card")) //Click Cards
                {
                    if (!playerHandPresenter.IsHandMode())
                    {
                        playerHandView.HandsUp();
                    }
                    else
                    {

                    }
                }

                if (hit.collider == null || !hit.collider.CompareTag("Card"))
                {
                    if (playerHandPresenter.IsHandMode())
                    {
                        playerHandView.HandsDown();
                    }
                }
            }

            if (Input.GetMouseButtonDown(1))
            {
                //Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                //RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
                if(TargetSelectionManager.Instance != null)
                {
                    Debug.Log("cancel!!!");
                    TargetSelectionManager.Instance.ReceiveClick(0);

                }

            }

            //if (Input.GetKeyDown(KeyCode.Space))
            //{
            //    //PlayerHandsScripts.Instance.DrawCard();
            //    GameRule.Instance.EndTurn();
            //}
        }
    }
}
