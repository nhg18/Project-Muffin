using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Card_Effect : MonoBehaviour
{
    public abstract void Excute(int casterViewID, int []targetViewID);
}
