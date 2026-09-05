using Photon.Pun.Demo.PunBasics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
                if (!playerHandPresenter.getHandMod())
                {
                    playerHandView.HandsUp();
                }
                else
                {

                }
            }

            if (hit.collider == null || !hit.collider.CompareTag("Card"))
            {
                if (playerHandPresenter.getHandMod())
                {
                    playerHandView.HandsDown();
                }
            }
        }

        //if (Input.GetKeyDown(KeyCode.Space))
        //{
        //    //PlayerHandsScripts.Instance.draw_A_Card();
        //    GameRule.Instance.EndTurn();
        //}
    }
}
