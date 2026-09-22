using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Muffin.Core
{

    public enum SceneType
    {
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
            { SceneType.Title, "Title" },
            { SceneType.Lobby, "Lobby" },
            { SceneType.Room, "Room" },
            { SceneType.Game, "Game" },
            { SceneType.DebugLobby, "DebugLobby" },
        };

        /// <summary>SceneManager.LoadScene 용 경로 (예: "Scenes/GameScene")</summary>
        public static string Get(SceneType type)
        {
            return $"{ROOT}/{PathCache[type]}Scene";
        }

        /// <summary>
        /// 경로 없는 씬 이름 (예: "GameScene"). <b>PhotonNetwork.LoadLevel 에는 반드시 이것을 쓴다.</b>
        /// PUN 의 씬 동기화(AutomaticallySyncScene)는 마스터가 올린 룸 프로퍼티 값을
        /// 클라이언트의 활성 씬 "이름" 과 비교한다. 경로 형태("Scenes/GameScene")를 넘기면
        /// 이름이 영원히 일치하지 않아, 비마스터 클라이언트가 룸 프로퍼티가 바뀔 때마다
        /// 씬을 다시 로드한다. (PhotonNetworkPart.cs LoadLevelIfSynced 참고)
        /// </summary>
        public static string GetName(SceneType type)
        {
            return $"{PathCache[type]}Scene";
        }
    }
}
