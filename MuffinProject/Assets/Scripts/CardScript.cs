using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines.ExtrusionShapes;

public class CardScript : MonoBehaviour
{

    [SerializeField] private float basicScaleX = 2f;
    [SerializeField] private float basicScaleY = 3f;

    [SerializeField] private float UpScale = 2f;
    [SerializeField] private int basicLayerOrder = 2;

    private SpriteRenderer[] renderers;


    private void Start()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();//order in layer controls
    }



    private void OnMouseEnter()
    {
        Debug.Log("hover!");
        if (GameRule.Instance.isHandMod)
        {
            HoverCard();
        }
    }
    private void OnMouseExit()
    {
        if (GameRule.Instance.isHandMod)
        {
            UnHoverCard();
        }
    }

    private void HoverCard()
    {
        BringToFront();
        transform.DOScaleX(basicScaleX*UpScale,0.1f);
        transform.DOScaleY(basicScaleY*UpScale, 0.1f);
    }
    private void UnHoverCard()
    {
        BringToOriginal();
        transform.DOScaleX(basicScaleX , 0.1f);
        transform.DOScaleY(basicScaleY , 0.1f);
    }

    public void BringToFront()
    {
        foreach (SpriteRenderer sr in renderers)
        {
            sr.sortingOrder = basicLayerOrder + 1;
        }
    }

    public void BringToOriginal()
    {
        foreach (SpriteRenderer sr in renderers)
        {
            sr.sortingOrder = basicLayerOrder;
        }
    }


}
