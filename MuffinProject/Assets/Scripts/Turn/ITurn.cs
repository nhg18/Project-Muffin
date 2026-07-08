using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ITurn
{
    void SetTurn(int actorNumber);
    int CurrentTurnActor { get; }
    bool IsMyTurn { get; }
}
