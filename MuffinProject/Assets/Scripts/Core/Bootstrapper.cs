using System;
using Muffin.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Muffin.Core
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
