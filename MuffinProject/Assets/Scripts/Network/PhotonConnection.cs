using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Chapchu.Core;
using Chapchu.Network.Events;

namespace Chapchu.Network
{
    /// <summary>서버 연결 · 끊김 처리 (09-network.md). NetworkManager 가 갖고 있고, 결과는 ConnectionEvents 로 알린다.</summary>
    public class PhotonConnection
    {
        /// <summary>
        /// 접속 시작부터 마스터 서버 접속 완료까지 기다리는 최대 시간 (초)
        /// </summary>
        public const float ConnectTimeoutSeconds = 15f;

        // 제한 시간 초과로 직접 끊었는지. 사용자 의도 종료(DisconnectByClientLogic)와 구분한다.
        private bool _isTimedOut;

        public static bool ValidConnect
        {
            get
            {
                if (!PhotonNetwork.IsConnectedAndReady)
                {
                    Debug.LogWarning($"[{nameof(PhotonConnection)}] 서버에 연결되지 않아 요청을 보낼 수 없다");
                    // 실패 통지는 부른 쪽이 요청 종류에 맞는 이벤트로 한다 (PhotonRoom)
                    return false;
                }

                Debug.LogWarning($"[{nameof(PhotonConnection)}] 서버 연결됨 — 요청을 보낸다");
                return true;
            }
        }

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

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                // Photon 을 거치지 않고, PUN 이 연결 실패 시 보내는 것과 같은 사유로 알린다.
                Debug.LogWarning($"[{nameof(PhotonConnection)}] 인터넷 연결 없음 — {DisconnectCause.ExceptionOnConnect} 로 알린다");
                ConnectionEvents.RaiseDisconnected(DisconnectCause.ExceptionOnConnect);
                return false;
            }

            _isTimedOut = false;
            bool started = PhotonNetwork.ConnectUsingSettings();

            // 앱 버전(Player Settings Version, versioning.md)이 다르면 서로 매칭되지 않게 한다 — 패치 차이도 나눈다.
            // ConnectUsingSettings 가 설정 파일의 AppVersion(비어 있음)으로 덮어쓰므로 그 뒤에 넣는다. 서버 인증 전이라 이 값이 쓰인다.
            PhotonNetwork.GameVersion = Application.version;
            return started;
        }

        /// <summary>
        /// 접속 제한 시간이 지났을 때 호출. 아직 접속 중이면 끊는다.
        /// </summary>
        public void OnConnectTimeout()
        {
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsConnectedAndReady) return;

            Debug.LogWarning($"[{nameof(PhotonConnection)}] 접속 제한 시간 {ConnectTimeoutSeconds}초 초과 — 연결을 끊는다");
            _isTimedOut = true;
            PhotonNetwork.Disconnect();
        }
    
        public void SetNickname(string nickname)
        {
            if (string.IsNullOrEmpty(nickname))
            {
                Debug.LogError($"[{nameof(PhotonConnection)}] 닉네임이 비어 있다");
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

            Debug.LogWarning($"[{nameof(PhotonConnection)}] 연결 끊김: {cause}");

            // 앱 종료 — 알릴 화면이 없다
            if (cause == DisconnectCause.ApplicationQuit) return;

            ConnectionEvents.RaiseDisconnected(cause);
        }
    }
}
