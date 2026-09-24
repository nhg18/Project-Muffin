using UnityEngine.SceneManagement;

namespace Chapchu.Core
{
    /// <summary>
    /// 로컬 씬 전환의 단일 진입점. UI · 디버그 도구는 SceneManager 를 직접 부르지 않는다.
    /// 방 안에서 전원이 함께 넘어가는 전환(게임 시작)은 마스터가 PhotonNetwork.LoadLevel(ScenePaths.GetName(...)) 을 쓴다.
    /// </summary>
    public static class SceneLoader
    {
        public static void Load(SceneType type) => SceneManager.LoadScene(ScenePaths.GetName(type));
    }
}
