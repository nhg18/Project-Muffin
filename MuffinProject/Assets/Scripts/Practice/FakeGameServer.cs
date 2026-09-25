using UnityEngine;

namespace Chapchu.Practice
{
    public class FakeGameServer : MonoBehaviour, IGameRequests, IGameState
    {
        public int CurrentTurnActor { get; private set; } = -1;

        public void RequestEndTurn()
        {
            Debug.Log("RequestEndTurn");
        }
    }
}
