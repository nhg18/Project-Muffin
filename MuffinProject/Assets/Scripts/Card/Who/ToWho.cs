using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToWho : MonoBehaviour
{
    public enum TargetType
    {
        One,
        Two,
        All_Others,
        Everyone,
        Me
    }
    [Header("Target List")]
    [SerializeField] private List<TargetType> TargetList = new List<TargetType>();

    void Awake()
    {

    }
}
