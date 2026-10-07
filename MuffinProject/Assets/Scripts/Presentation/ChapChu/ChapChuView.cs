using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChapChuView : MonoBehaviour
{
    [SerializeField] private Button ChapChuButton;
    public event Action DeclareChapchu;

    private void OnEnable()
    {
        if (ChapChuButton != null)
            ChapChuButton.onClick.AddListener(HandleChapChuButton);
    }


    private void OnDisable()
    {
        if (ChapChuButton != null)
            ChapChuButton.onClick.RemoveListener(HandleChapChuButton);
    }


    private void HandleChapChuButton() => DeclareChapchu?.Invoke();
}
