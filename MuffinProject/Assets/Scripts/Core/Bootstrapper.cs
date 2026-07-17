using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    public class Bootstrapper : MonoBehaviour
    {
        private void Start()
        {
            SceneManager.LoadScene(ScenePaths.Get(SceneType.Title));
        }
    }
}
