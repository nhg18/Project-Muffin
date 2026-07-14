using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckEvent
{
    public static event Action<int, int> OnDrawn; // ActorNumber, CardID
    public static event Action<int> OnDiscarded; // ActorNumber

    public static void RaiseDrawn(int actorNumber, int cardID)
    {
        OnDrawn?.Invoke(actorNumber, cardID);
    }

    public static void RaiseDiscarded(int actorNumber)
    {
        OnDiscarded?.Invoke(actorNumber);
    }
}
