using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum Type
{
    Action,
    Counter,
    Trap
}
[CreateAssetMenu(fileName = "Card_", menuName = "CardSystem/Card Data")]
public class CardData : ScriptableObject
{
    public int id;
    public string cardName;
    public Sprite cardImage;
    public Type type;

    [TextArea]
    public string description;
}
