using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneType
{
    BootStrap,
    Title,
    Lobby,
    Room,
    Game,
    DebugLobby,
}


public static class ScenePaths
{
    private const string ROOT = "Scenes";

    private static readonly Dictionary<SceneType, string> PathCache = new()
    {
        { SceneType.BootStrap, "BootStrap" },
        { SceneType.Title, "Title" },
        { SceneType.Lobby, "Lobby" },
        { SceneType.Room, "Room" },
        { SceneType.Game, "Game" },
        { SceneType.DebugLobby, "DebugLobby" },
    };

    public static string Get(SceneType type)
    {
        return $"{ROOT}/{PathCache[type]}Scene";
    }
}
