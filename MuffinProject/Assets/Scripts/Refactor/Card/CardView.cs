using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{

    [Header("UI¿¬°á")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text Description;
    [SerializeField] private Image cardImage;
    [SerializeField] private int type;

    public void Setup(CardData data)
    {
        if (data == null) return;
        nameText.text = data.CardName;
        Description.text = data.Description;
        cardImage.sprite = data.CardImage;

        gameObject.name = $"Card_{data.ID}_{data.CardName}";
    }
}
