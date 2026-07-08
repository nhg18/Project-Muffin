using System;

public static class TurnEvents
{
    public static event Action OnEndTurnRequested;
    public static event Action OnTurnChanged;

    public static void RaiseEndTurnRequested()
    {
        OnEndTurnRequested?.Invoke();
    }

    public static void RaiseTurnChanged()
    {
        OnTurnChanged?.Invoke();
    }
}
