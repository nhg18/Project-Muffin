using Chapchu.Network;
using Chapchu.UI.Popups;
using UnityEngine;

namespace Chapchu.Core
{
    /// <summary>
    /// 어느 씬에서 실행하더라도 전역 매니저가 먼저 생성되도록 보장한다.
    /// 씬에 매니저를 배치할 필요가 없으며, 씬에 이미 배치돼 있다면
    /// 각 싱글톤의 중복 방지 로직이 씬 쪽 사본을 정리한다.
    /// </summary>
    public static class GameBootstrap
    {
        /// <summary>생성 순서대로 나열한다. 먼저 필요한 매니저를 앞에 둔다. 프리팹 이름 = 매니저 클래스 이름 (ResourcePaths.Bootstrap 아래).</summary>
        private static readonly string[] ManagerPrefabs =
        {
            nameof(NetworkManager),
            nameof(PopupManager),
            nameof(SoundManager),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SpawnManagers()
        {
            // 방 대기 · 남의 턴처럼 터치 없이 있을 때 화면이 잠겨 모바일 앱이 멈추면 Photon 연결이 끊긴다.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            foreach (string prefabName in ManagerPrefabs)
                Spawn(prefabName);
        }

        private static void Spawn(string prefabName)
        {
            string path = ResourcePaths.Bootstrap + prefabName;
            var prefab = Resources.Load<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogError($"[{nameof(GameBootstrap)}] 매니저 프리팹을 찾을 수 없습니다: Resources/{path}");
                return;
            }

            // 프리팹의 Awake 에서 DontDestroyOnLoad 와 중복 검사가 수행된다.
            GameObject instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
        }
    }
}
