using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickManager : MonoBehaviour
{


    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            if (hit.collider != null && hit.collider.CompareTag("Card")) //Click Cards
            {
                if (!GameRule.Instance.isHandMod)
                {
                    PlayerHandsScripts.Instance.HandsUp();
                }
                else
                {

                }
            }
            if (hit.collider == null || !hit.collider.CompareTag("Card"))
            {
                if (GameRule.Instance.isHandMod)
                {
                    PlayerHandsScripts.Instance.HandsDown();
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayerHandsScripts.Instance.draw_A_Card();
        }
    }
}
