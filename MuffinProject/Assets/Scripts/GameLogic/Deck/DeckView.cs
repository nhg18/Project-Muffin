using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
public class DeckView : MonoBehaviour
{
    [SerializeField] private Button DrawButton;
    public event Action OnDrawButtonClicked;

    private void Awake()
    {
        if (DrawButton != null)
        {
            DrawButton.onClick.AddListener(() => OnDrawButtonClicked?.Invoke());
        }
    }
}
