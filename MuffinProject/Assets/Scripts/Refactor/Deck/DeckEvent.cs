using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckEvent
{
    public static event Action<int, Card> OnDrawn; // ActorNumber, CardID

    public static void RaiseDrawn(int actorNumber, Card card)
    {
        OnDrawn?.Invoke(actorNumber, card);
    }
}
