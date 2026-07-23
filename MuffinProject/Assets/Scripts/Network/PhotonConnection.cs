using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Network
{
    public class PhotonConnection
    {
        public static bool ValidConnect
        {
            get
            {
                if (!PhotonNetwork.IsConnectedAndReady)
                {
                    Debug.LogWarning("Not connected and ready");
                    // 팝업 매니저 호출
                    return false;
                }

                Debug.LogWarning("Connected and ready");
                return true;
            }
        }

        private void SetupInitNickname()
        {
            // PlayerPrefs 저장된 닉네임이 존재하면 닉네임 설정
            if (!PlayerPrefs.HasKey(PlayerPrefsKeys.PlayerName)) return;
            var defaultName = PlayerPrefs.GetString(PlayerPrefsKeys.PlayerName);
            if (string.IsNullOrEmpty(defaultName)) return;
            // 연결 체크
            if (!PhotonNetwork.IsConnected) return;
            SetNickname(defaultName);
        }

        /// <summary>
        /// 포톤 네트워크 접속 전 환경 세팅 함수
        /// </summary>
        public void Initialize()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                // 인터넷 없음 팝업 띄우기
                return;
            }
            PhotonNetwork.AutomaticallySyncScene = true;
        }

        public void Connect()
        {
            if (PhotonNetwork.IsConnected) return;
            PhotonNetwork.ConnectUsingSettings();
        }
    
        public void SetNickname(string nickname)
        {
            if (string.IsNullOrEmpty(nickname))
            {
                Debug.LogError("Nickname cannot be null or empty");
                return;
            }
        
            PhotonNetwork.NickName = nickname;
            PlayerPrefs.SetString(PlayerPrefsKeys.PlayerName, nickname);
        }
    
        /// <summary>
        /// 서버 연결 완료시 호출되는 콜백 함수
        /// 서버 연결시 자동으로 로비 참가
        /// </summary>
        public void OnConnectedToMaster()
        {
            Debug.Log("On Connected To Master");
            ConnectionEvents.RaiseConnected();
        }

        /// <summary>
        /// 서버 연결 끊어졌을 때 호출되는 콜백 함수
        /// </summary>
        /// <param name="cause">
        /// 끊긴 사유가 담긴 Enum 집합체
        /// </param>
        public void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log($"On Disconnected: {cause}");
            ConnectionEvents.RaiseDisconnected(cause);

            switch (cause)
            {
                // 클라이언트에 의해 의도적 종료
                case DisconnectCause.DisconnectByClientLogic:
                    // SceneManager.LoadScene(SceneType.Title);
                    break;

                // 서버, 클라이언트 타임 아웃, 일시 끊김
                case DisconnectCause.ServerTimeout:
                case DisconnectCause.ClientTimeout:
                case DisconnectCause.Exception:
                    // TryReconnect();
                    break;

                // 서버가 강제 종료 → 이유 표시 후 타이틀
                case DisconnectCause.DisconnectByServerLogic:
                case DisconnectCause.InvalidAuthentication:
                    // ShowErrorAndGoTitle(cause);
                    break;
            }
        }
    }
}
