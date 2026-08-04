using System;

public static class GameEvents
{
    public static event Action OnTurnChanged;

    public static void RaiseTurnChanged()
    {
        OnTurnChanged?.Invoke();
    }
}
