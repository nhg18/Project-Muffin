using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    private const string ROOT = "Scenes/";

    private static readonly Dictionary<SceneType, string> scenes = new()
    {
        { SceneType.BootStrap, "BootStrap" },
        { SceneType.Title, "Title" },
        { SceneType.Lobby, "Lobby" },
        { SceneType.Room, "Room" },
        { SceneType.Game, "Game" },
    };

    /// <summary>
    /// SceneType Enum으로 안전하게 씬 로드
    /// </summary>
    /// <param name="sceneType">Enum</param>
    public static void LoadScene(SceneType sceneType)
    {
        SceneManager.LoadScene(ROOT + scenes[sceneType] + "Scene");
    }
}
