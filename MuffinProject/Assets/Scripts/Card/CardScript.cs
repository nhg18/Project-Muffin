using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Splines.ExtrusionShapes;

public class CardScript : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{

    [Header("Hover Settings")]
    [SerializeField] private float basicScaleX = 2f;
    [SerializeField] private float basicScaleY = 3f;

    [SerializeField] private float UpScale = 2f;
    [SerializeField] private int basicLayerOrder = 2;

    private SpriteRenderer[] childRenderers;
    private CardCondition cardCondition;

    [Header("Drag Settings")]
    [SerializeField] private float dragScale = 1.1f;
    [SerializeField] private int dragSortingOrder = 10;
    [SerializeField] private float returnSpeed = 0.15f;
    [SerializeField] private GameObject DropArea;

    private Vector3 originalPosition;

    private SpriteRenderer spriteRenderer;
    private bool isDragging = false;
    private float zDepth;


    private void Start()
    {
        childRenderers = GetComponentsInChildren<SpriteRenderer>();//order in layer controls
        cardCondition = GetComponent<CardCondition>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }


    #region PlayerAction

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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!GameRule.Instance.isHandMod)
            return;

        originalPosition = transform.position;

        zDepth = Camera.main.WorldToScreenPoint(transform.position).z;

        isDragging = true;
        Debug.Log("클릭");
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!GameRule.Instance.isHandMod)
            return;

        if (!isDragging) return;

        Vector3 screenPos = new Vector3(
            eventData.position.x,
            eventData.position.y,
            zDepth
        );
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        worldPos.z = originalPosition.z;
        transform.position = worldPos;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!GameRule.Instance.isHandMod)
            return;

        if (!isDragging) return;
        isDragging = false;
        if (isDropArea(transform.position))
        {

        }
        else
        {
            //transform.position = originalPosition;
            StartCoroutine(ReturnToOrigin());
            //PlayerHandsScripts.Instance.PutAwayMyCards();
        }
    }
    #endregion
    private System.Collections.IEnumerator ReturnToOrigin()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < returnSpeed)
        {
            float t = elapsed / returnSpeed;
            t = t * t * (3f - 2f * t); // Smoothstep 보간
            transform.position = Vector3.Lerp(startPos, originalPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPosition;
    }

    private bool isDropArea(Vector2 position)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(position);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if(hit.gameObject.tag == "CardDropArea")
            {
                return true;
            }
        }
        return false;
    } 


    #region CardMove
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
        foreach (SpriteRenderer sr in childRenderers)
        {
            sr.sortingOrder = basicLayerOrder + 1;
        }
    }

    public void BringToOriginal()
    {
        foreach (SpriteRenderer sr in childRenderers)
        {
            sr.sortingOrder = basicLayerOrder;
        }
    }


    #endregion

}
