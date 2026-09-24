using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Chapchu.Core;

namespace Chapchu.Network
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

        /// <summary>
        /// 접속 시작부터 마스터 서버 접속 완료까지 기다리는 최대 시간 (초)
        /// </summary>
        public const float ConnectTimeoutSeconds = 15f;

        // 제한 시간 초과로 직접 끊었는지. 사용자 의도 종료(DisconnectByClientLogic)와 구분한다.
        private bool _isTimedOut;

        /// <summary>
        /// 포톤 네트워크 접속 전 환경 세팅 함수
        /// 연결 가능 여부와 무관하게 항상 설정한다. (꺼진 채 연결되면 LoadLevel 이 동기화되지 않는다)
        /// </summary>
        public void Initialize()
        {
            PhotonNetwork.AutomaticallySyncScene = true;
        }

        /// <summary>
        /// 네트워크 접속 함수
        /// 실패하면 ConnectionEvents.OnDisconnected 로 사유를 알린다.
        /// </summary>
        /// <returns>접속 시도를 새로 시작했으면 true</returns>
        public bool Connect()
        {
            if (PhotonNetwork.IsConnected) return false;

            Debug.Log($"Connect Start: {Application.internetReachability}");
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                // Photon 을 거치지 않고, PUN 이 연결 실패 시 보내는 것과 같은 사유로 알린다.
                Debug.LogWarning($"No Internet: {DisconnectCause.ExceptionOnConnect}");
                ConnectionEvents.RaiseDisconnected(DisconnectCause.ExceptionOnConnect);
                return false;
            }

            _isTimedOut = false;
            return PhotonNetwork.ConnectUsingSettings();
        }

        /// <summary>
        /// 접속 제한 시간이 지났을 때 호출. 아직 접속 중이면 끊는다.
        /// </summary>
        public void OnConnectTimeout()
        {
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsConnectedAndReady) return;

            Debug.LogWarning($"Connect Timeout: {ConnectTimeoutSeconds}s");
            _isTimedOut = true;
            PhotonNetwork.Disconnect();
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
        /// 안내 · 화면 전환 · 재시도(NetworkManager.Connect)는 UI 가 사유를 보고 결정한다.
        /// </summary>
        /// <param name="cause">
        /// 끊긴 사유가 담긴 Enum 집합체
        /// </param>
        public void OnDisconnected(DisconnectCause cause)
        {
            if (_isTimedOut && cause == DisconnectCause.DisconnectByClientLogic)
                cause = DisconnectCause.ClientTimeout;
            _isTimedOut = false;

            Debug.Log($"On Disconnected: {cause}");

            // 앱 종료 — 알릴 화면이 없다
            if (cause == DisconnectCause.ApplicationQuit) return;

            ConnectionEvents.RaiseDisconnected(cause);
        }
    }
}
