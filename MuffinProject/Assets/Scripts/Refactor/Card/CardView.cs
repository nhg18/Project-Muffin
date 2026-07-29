using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    [Header("UI연결")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text description;
    [SerializeField] private SpriteRenderer cardImage;
    [SerializeField] private int type;

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

        gameObject.name = $"Card_{data.id}_{data.cardName}";
    }
}
