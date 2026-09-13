using System;

public static class GameEvents
{
    public static event Action OnTurnChanged;
    public static event Action<int, int> OnDrawn; // ActorNumber, CardID
    public static event Action<bool> OnHandModeChanged;

    public static void RaiseTurnChanged()
    {
        OnTurnChanged?.Invoke();
    }
    
    public static void RaiseDrawn(int actorNumber, int cardID)
    {
        OnDrawn?.Invoke(actorNumber, cardID);
    }

    public static void RaiseHandModeChanged(bool st)
    {
        OnHandModeChanged?.Invoke(st);
    }
}
