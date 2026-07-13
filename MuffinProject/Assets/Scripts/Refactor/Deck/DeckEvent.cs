using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckEvent
{
    public static event Action<int> OnDrawn;
    public static event Action<int> OnDiscarded;

    public static void RaiseDrawn(int actorNumber)
    {
        OnDrawn?.Invoke(actorNumber);
    }

    public static void RaiseDiscarded(int actorNumber)
    {
        OnDiscarded?.Invoke(actorNumber);
    }
}
