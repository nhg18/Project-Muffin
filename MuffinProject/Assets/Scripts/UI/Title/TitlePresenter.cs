using Chapchu.Core;
using Chapchu.Network;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 타이틀 뷰와 네트워크 · 씬 전환을 잇는다. Scripts/UI/Title 에서 네트워크를 아는 유일한 파일이다.
    /// 서버 접속은 앱 시작 때 NetworkManager 가 이미 시작한다. 여기서는 그 결과를 기다렸다가 로비로 넘어간다.
    /// 기준 문서: docs/systems/12-title-ui.md 6절 · 10-5
    /// </summary>
    [RequireComponent(typeof(TitleView))]
    public class TitlePresenter : MonoBehaviour
    {
        private TitleView _view;

        // 접속 버튼을 누른 뒤 접속 결과를 기다리는 중. 누르기 전에 난 접속 실패는 화면에 띄우지 않는다.
        private bool _waitingForConnection;

        private void Awake()
        {
            _view = GetComponent<TitleView>();
        }

        private void OnEnable()
        {
            _view.ConnectRequested += HandleConnectRequested;
            ConnectionEvents.OnConnected += HandleConnected;
            ConnectionEvents.OnDisconnected += HandleDisconnected;
        }

        private void OnDisable()
        {
            _view.ConnectRequested -= HandleConnectRequested;
            ConnectionEvents.OnConnected -= HandleConnected;
            ConnectionEvents.OnDisconnected -= HandleDisconnected;
        }

        private void Start()
        {
            string savedNickname = PlayerPrefs.GetString(PlayerPrefsKeys.PlayerName, string.Empty);
            if (!string.IsNullOrEmpty(savedNickname))
                _view.SetNickname(savedNickname);
        }

        private void HandleConnectRequested(string nickname)
        {
            NetworkManager.Instance.SetNickname(nickname);

            if (NetworkManager.IsReady)
            {
                LoadLobby();
                return;
            }

            _waitingForConnection = true;

            // 앱 시작 때의 접속이 이미 실패했으면 다시 시도한다. 접속 중이면 그 결과를 기다린다.
            // 인터넷이 없으면 Connect 안에서 바로 OnDisconnected 가 오므로 대기 표시를 먼저 켠다.
            if (!NetworkManager.IsConnected)
                NetworkManager.Instance.Connect();
        }

        private void HandleConnected()
        {
            if (!_waitingForConnection) return;

            LoadLobby();
        }

        private void HandleDisconnected(DisconnectCause cause)
        {
            if (!_waitingForConnection) return;

            _waitingForConnection = false;
            _view.ShowError(GetDisconnectMessage(cause));
        }

        private void LoadLobby()
        {
            _waitingForConnection = false;
            SceneManager.LoadScene(ScenePaths.Lobby);
        }

        // 12-title-ui.md 10-5. Photon 원문 메시지는 보여주지 않는다.
        private static string GetDisconnectMessage(DisconnectCause cause)
        {
            return cause switch
            {
                DisconnectCause.ExceptionOnConnect or
                DisconnectCause.DnsExceptionOnConnect or
                DisconnectCause.ServerAddressInvalid => "서버에 연결할 수 없습니다. 인터넷을 확인해 주세요.",
                DisconnectCause.ClientTimeout or
                DisconnectCause.ServerTimeout => "연결 시간이 초과되었습니다. 다시 시도해 주세요.",
                DisconnectCause.MaxCcuReached => "접속자가 많습니다. 잠시 후 다시 시도해 주세요.",
                _ => "연결이 끊어졌습니다. 다시 시도해 주세요.",
            };
        }
    }
}
