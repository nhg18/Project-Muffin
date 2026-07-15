using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Card_", menuName = "CardSystem/Card Data")]
public class CardData : ScriptableObject
{
    public int ID;
    public string CardName;
    public Sprite CardImage;
    public int type;

    [TextArea]
    public string Description;
}
