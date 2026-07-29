using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Card_", menuName = "CardSystem/Card Data")]
public class CardData : ScriptableObject
{
    public int id;
    public string cardName;
    public Sprite cardImage;
    public int type;

    [TextArea]
    public string description;
}
