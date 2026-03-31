using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrapper : MonoBehaviour
{
    private void Start()
    {
        SceneLoader.LoadScene(SceneType.Lobby);
    }
}
