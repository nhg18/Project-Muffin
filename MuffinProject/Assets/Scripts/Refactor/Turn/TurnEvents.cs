using System;

public static class TurnEvents
{
    public static event Action OnTurnChanged;
    public static event Action OnTurnEnded;

    public static void RaiseTurnChanged()
    {
        OnTurnChanged?.Invoke();
    }

    public static void RaiseTurnEnded()
    {
        OnTurnEnded?.Invoke();
    }
}
