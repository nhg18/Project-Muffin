using System;
using Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    public class Bootstrapper : MonoBehaviour
    {
        private void Start()
        {
            NetworkManager.Instance.Initialize();
            
            if (!NetworkManager.IsConnected)
                NetworkManager.Instance.Connect();
        }

        private void OnEnable()
        {
            ConnectionEvents.OnConnected += OnConnected;
        }

        private void OnDisable()
        {
            ConnectionEvents.OnConnected -= OnConnected;
        }

        private void OnConnected()
        {
            SceneManager.LoadScene(ScenePaths.Get(SceneType.Title));
        }
    }
}
