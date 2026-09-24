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
        /// <summary>Resources 기준 매니저 프리팹 경로</summary>
        private const string PrefabRoot = "Bootstrap/";

        /// <summary>생성 순서대로 나열한다. 먼저 필요한 매니저를 앞에 둔다.</summary>
        private static readonly string[] ManagerPrefabs =
        {
            "NetworkManager",
            "PopupManager",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SpawnManagers()
        {
            foreach (var prefabName in ManagerPrefabs)
                Spawn(prefabName);
        }

        private static void Spawn(string prefabName)
        {
            var path = PrefabRoot + prefabName;
            var prefab = Resources.Load<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogError($"[GameBootstrap] 매니저 프리팹을 찾을 수 없습니다: Resources/{path}");
                return;
            }

            // 프리팹의 Awake 에서 DontDestroyOnLoad 와 중복 검사가 수행된다.
            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
        }
    }
}
