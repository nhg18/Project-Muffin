using Photon.Pun.Demo.PunBasics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Muffin.Core;
using Muffin.Game.Cards;

namespace Muffin.Presentation
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
            if (Input.GetMouseButtonDown(0))
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
