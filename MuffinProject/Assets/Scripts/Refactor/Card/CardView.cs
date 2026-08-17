using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class CardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI연결")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text description;
    [SerializeField] private SpriteRenderer cardImage; 
    [SerializeField] private  Type type;

    [Header("Hover Settings")]
    [SerializeField] private float basicScaleX = 2f;
    [SerializeField] private float basicScaleY = 3f;

    [SerializeField] private float UpScale = 2f;
    [SerializeField] private int basicLayerOrder = 2;

    [Header("Drag Settings")]
    [SerializeField] private float dragScale = 1.1f;
    [SerializeField] private int dragSortingOrder = 10;
    [SerializeField] private float returnSpeed = 0.15f;

    public CardPresenter cardPresenter;


    private Vector3 originalPosition;

    private SpriteRenderer spriteRenderer;
    private bool isDragging = false;
    private float zDepth; 

    private bool isHandMod = false;

    private SpriteRenderer[] childRenderers;

    public void Setup(CardData data)
    {
        if (data == null)
        {
            Debug.Log("null null null");
            return;
        }

        Debug.Log("data : "+ data.cardName+" "+ data.description);
        nameText.text = data.cardName;
        description.text = data.description;
        cardImage.sprite = data.cardImage;
        type = data.type;

        gameObject.name = $"Card_{data.id}_{data.cardName}";
    }

    private void OnEnable()
    {
        GameEvents.OnHandModeChanged += setHandMode;
    }

    private void OnDisable()
    {
        GameEvents.OnHandModeChanged -= setHandMode;
    }

    private void Start()
    {
        childRenderers = GetComponentsInChildren<SpriteRenderer>();//order in layer controls
    }

    private void setHandMode(bool handmod)
    {
        this.isHandMod = handmod;
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

    private void HoverCard()
    {
        BringToFront();
        transform.DOScaleX(basicScaleX * UpScale, 0.1f);
        transform.DOScaleY(basicScaleY * UpScale, 0.1f);
    }
    private void UnHoverCard()
    {
        BringToOriginal();
        transform.DOScaleX(basicScaleX, 0.1f);
        transform.DOScaleY(basicScaleY, 0.1f);
    }


    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("hover!");
        if (isHandMod)
        {
            HoverCard();
        }
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isHandMod)
        {
            UnHoverCard();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isHandMod)
            return;

        originalPosition = transform.localPosition;

        zDepth = Camera.main.WorldToScreenPoint(transform.localPosition).z;

        isDragging = true;
        Debug.Log("클릭");
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!isHandMod)
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


    private System.Collections.IEnumerator ReturnToOrigin()
    {
        Vector3 startPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < returnSpeed)
        {
            float t = elapsed / returnSpeed;
            t = t * t * (3f - 2f * t); // Smoothstep 보간
            transform.localPosition = Vector3.Lerp(startPos, originalPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPosition;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHandMod)
            return;

        if (!isDragging) return;
        isDragging = false;
        if (isDropArea(transform.position) && cardPresenter.LocalConditionCheck())
        {

            //StartCoroutine(StartCard());
            //StartCard(); 
            cardPresenter.OnCardDropped();
        }
        else
        {
            StartCoroutine(ReturnToOrigin());
        }
    }

    private bool isDropArea(Vector2 position)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(position);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.gameObject.tag == "CardDropArea")
            {
                return true;
            }
        }
        return false;
    }


}
