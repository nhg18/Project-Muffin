using System;
using Refactor;

public static class GameEvents
{
    public static event Action OnTurnChanged;
    public static event Action<Player> OnPlayerDrawn;

    public static void RaiseTurnChanged()
    {
        OnTurnChanged?.Invoke();
    }
}
