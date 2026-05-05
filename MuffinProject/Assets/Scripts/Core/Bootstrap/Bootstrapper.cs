using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrapper : MonoBehaviour
{
    private void Awake()
    {
        if (PlayerPrefs.HasKey(PlayerPrefsKeys.playerName))
        {
            PlayerData.Nickname = PlayerPrefs.GetString(PlayerPrefsKeys.playerName);
        }
    }

    private void Start()
    {
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Title));
    }
}
