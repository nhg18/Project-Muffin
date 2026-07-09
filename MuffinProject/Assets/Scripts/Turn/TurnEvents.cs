using System;

public static class TurnEvents
{
    public static event Action OnTurnChanged;


    public static void RaiseTurnChanged()
    {
        OnTurnChanged?.Invoke();
    }
}
