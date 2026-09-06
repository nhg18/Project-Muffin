using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public abstract class CardCollection
{
    [SerializeField] protected List<Card> cards = new();
    
    public int Count => cards.Count;
    public void Add(Card card) => cards.Add(card);
    public void AddRange(IEnumerable<Card> cards) => this.cards.AddRange(cards);
    public void AddRange(params Card[] cards) => this.cards.AddRange(cards);
    public void Remove(Card card) => cards.Remove(card);
    public bool Contains(Card card) => cards.Contains(card);
    public void Clear() => cards.Clear();
}
