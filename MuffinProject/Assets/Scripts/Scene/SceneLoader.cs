using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    private static readonly Dictionary<SceneType, string> scenes = new Dictionary<SceneType, string>()
    {
        { SceneType.BootStrap, "BootStrap" },
        { SceneType.Title , "Title" },
        { SceneType.Lobby, "Lobby" },
        { SceneType.Room , "Room" },
        { SceneType.Game, "Game" },
    };

    public static void LoadScene(SceneType sceneType)
    {
        SceneManager.LoadScene(scenes[sceneType]);
    }
}
