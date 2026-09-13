using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrunEndButton : MonoBehaviour
{
    SpriteRenderer sp;
    private void Start()
    {
        sp = GetComponent<SpriteRenderer>();
    }
    private void OnMouseDown()
    {
        Debug.Log("MouseClick!");
        int result = GameRule.Instance.EndTurn();
        if (result != -1)
        {
            //sp.color = new Color(0, 255, 255);
        }
    }
}
