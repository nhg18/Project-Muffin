using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public class TargetSelectionManager : MonoBehaviour
{
    public static TargetSelectionManager Instance { get; private set; }
    private int selectedTarget = 0;
    private bool isWaitingForSelection = false;
    private bool isSelected = false;

    private void Awake() { Instance = this; }

    public async Task<int> SelectPlayer(float timeoutSeconds)
    {
        isWaitingForSelection = true;
        selectedTarget = 0;
        float timer = 0f;
        isSelected = false;

        // 제한 시간이 남았고, 아직 아무도 선택되지 않았다면 계속 대기
        while (timer < timeoutSeconds && isSelected == false)
        {
            timer += Time.deltaTime;
            await Task.Yield(); // 1프레임 대기 (게임이 멈추지 않게 해줌)
        }

        isWaitingForSelection = false;

        // 시간 초과면 0, 선택했으면 해당 playerNumber 반환
        return selectedTarget;
    }

    public void ReceiveClick(int ActorNumber)
    {
        if (isWaitingForSelection)
        {
            selectedTarget = ActorNumber;
            isSelected = true;
        }
    }

    //public void OnPointerClick(PointerEventData eventData)
    //{
    //    if (eventData.button == PointerEventData.InputButton.Right)//selection cancel
    //    {
    //        ReceiveClick(0);
    //    }
    //}
}
