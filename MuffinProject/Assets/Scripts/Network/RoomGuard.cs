using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.Network
{
    /// <summary>
    /// 믿는 방장 · 방장이 쓴 값 지키기 (09-network.md 2절 #3 · 13절).
    /// Photon 은 아무 클라나 방 속성 · 남의 Player 속성 · 방 설정(열림 · 공개 · 정원) · 방장 자리를 바꿀 수 있게 둔다.
    /// 그래서 방장이 쓴 값을 기억해 두고, 남이 바꾸면 다음 프레임에 되돌린다. 방장 자리를 뺏기면 되찾는다.
    /// NetworkManager 가 갖고, 방장의 속성 쓰기는 모두 이것을 거친다 (NetworkManager.SetRoomProps · SetPlayerProps).
    /// </summary>
    internal sealed class RoomGuard
    {
        // 방장이 쓴 값 — 문자열 키만 (방 설정은 아래 필드)
        private readonly Hashtable _room = new Hashtable();
        private readonly Dictionary<int, Hashtable> _players = new Dictionary<int, Hashtable>();
        private bool _isOpen;
        private bool _isVisible;
        private int _maxPlayers;
        private int _playerTtl;
        private int _emptyRoomTtl;

        // 다음 프레임에 되돌릴 것 — 한 프레임에 여러 번 바뀌어도 한 번만 쓴다
        private readonly Hashtable _roomRestore = new Hashtable();
        private readonly Dictionary<int, Hashtable> _playerRestore = new Dictionary<int, Hashtable>();
        private bool _optionsRestore;
        private bool _expectedUsersRestore;
        private bool _nicknameRestore;

        private static readonly MethodInfo SetPlayerNameProperty = typeof(Player).GetMethod("SetPlayerNameProperty", BindingFlags.NonPublic | BindingFlags.Instance);

        // 방에 있는 동안 믿는 방장이 내게로 바뀌면 부른다 — 처리 번호 이어받기 (PunGameServer)
        private readonly Action _becameTrustedMaster;

        public RoomGuard(Action becameTrustedMaster) => _becameTrustedMaster = becameTrustedMaster;

        /// <summary>믿는 방장의 actorNumber — 방에 들어올 때의 방장, 그 뒤엔 방장이 나가서 바뀐 사람만. 방 밖이면 -1.</summary>
        public int TrustedMaster { get; private set; } = -1;

        public bool IsTrustedMaster => PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == TrustedMaster;

        /// <summary>방장이 정한 열림 — 방에 보이는 값은 남이 바꿨을 수 있다. 방장만 뜻이 있다.</summary>
        public bool IsOpen => _isOpen;

        #region 방장이 쓰기

        public bool SetRoomProps(Hashtable props)
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (room == null)
                return false;

            Remember(_room, props);
            return room.SetCustomProperties(props);
        }

        public bool SetPlayerProps(Player player, Hashtable props)
        {
            if (player == null)
                return false;

            if (!_players.TryGetValue(player.ActorNumber, out Hashtable intended))
                _players[player.ActorNumber] = intended = new Hashtable();
            Remember(intended, props);
            return player.SetCustomProperties(props);
        }

        public void SetOptions(bool? isVisible, bool? isOpen)
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (room == null)
                return;

            if (isVisible.HasValue) room.IsVisible = _isVisible = isVisible.Value;
            if (isOpen.HasValue) room.IsOpen = _isOpen = isOpen.Value;
        }

        public void SetMaxPlayers(int maxPlayers)
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (room == null)
                return;

            room.MaxPlayers = _maxPlayers = maxPlayers;
        }

        /// <summary>
        /// 방 속성 읽기 — 믿는 방장은 자기가 쓴 원본을, 다른 클라는 방 속성 사본을 (09-network.md 3절). 없으면 null.
        /// 방장은 자기가 쓴 값을 서버 확인 뒤에야 방에서 본다 — 쓴 직후에 다시 읽어 판정하는 값은 이것으로 읽는다.
        /// </summary>
        public object RoomValue(string key) => IsTrustedMaster ? _room[key] : PhotonNetwork.CurrentRoom?.CustomProperties[key];

        private static void Remember(Hashtable intended, Hashtable props)
        {
            foreach (DictionaryEntry entry in props)
            {
                if (!(entry.Key is string key))
                    continue;

                if (entry.Value == null)
                    intended.Remove(key); // Photon 도 null 이면 키를 지운다
                else
                    intended[key] = entry.Value;
            }
        }

        #endregion

        #region 방 입장 · 방장 바뀜

        public void OnJoinedRoom()
        {
            Forget();
            TrustedMaster = PhotonNetwork.CurrentRoom.MasterClientId;
            if (IsTrustedMaster)
                Adopt();
        }

        public void OnLeftRoom()
        {
            Forget();
            TrustedMaster = -1;
        }

        /// <summary>
        /// 방장 바뀜을 받아들일지 (true = 정상 이주). 앞 방장이 아직 방에 있으면 누가 방장 자리를 가져간 것이다 —
        /// 믿지 않고, 내가 앞 방장이면 되찾는다. 되찾아 원래 방장으로 돌아온 알림도 false (처음부터 바뀐 적 없는 것으로).
        /// </summary>
        public bool AcceptMasterSwitch(Player newMaster)
        {
            if (newMaster == null || newMaster.ActorNumber == TrustedMaster)
                return false;

            Player previous = PhotonNetwork.CurrentRoom?.GetPlayer(TrustedMaster);
            if (previous != null && !previous.IsInactive)
            {
                Debug.LogWarning($"[{nameof(RoomGuard)}] P{newMaster.ActorNumber} 가 방장 자리를 가져감 — 믿지 않는다 (방장 P{TrustedMaster} 가 방에 있다)");
                if (previous.IsLocal)
                    PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);
                return false;
            }

            TrustedMaster = newMaster.ActorNumber;
            Forget();
            if (IsTrustedMaster)
            {
                Adopt();
                _becameTrustedMaster?.Invoke();
            }
            return true;
        }

        /// <summary>나간 사람 정리. 믿는 방장이 바뀌었으면 true — AcceptMasterSwitch 의 true 와 같은 뜻이다.</summary>
        public bool OnPlayerLeft(Player player)
        {
            _players.Remove(player.ActorNumber);
            _playerRestore.Remove(player.ActorNumber);

            // 방장 자리를 뺏긴 채로 믿는 방장이 나갔다 — 남은 방장을 믿을 수밖에 없다
            if (player.ActorNumber == TrustedMaster && PhotonNetwork.CurrentRoom != null)
            {
                TrustedMaster = PhotonNetwork.CurrentRoom.MasterClientId;
                Forget();
                if (IsTrustedMaster)
                {
                    Adopt();
                    _becameTrustedMaster?.Invoke();
                }
                return true;
            }
            return false;
        }

        // 방장이 되면 지금 방의 값을 원본으로 이어받는다 — 앞 방장이 쓴 값
        private void Adopt()
        {
            Room room = PhotonNetwork.CurrentRoom;
            foreach (DictionaryEntry entry in room.CustomProperties)
            {
                if (entry.Key is string key)
                    _room[key] = entry.Value;
            }

            foreach (Player player in room.Players.Values)
            {
                var intended = new Hashtable();
                Remember(intended, player.CustomProperties);
                _players[player.ActorNumber] = intended;
            }

            _isOpen = room.IsOpen;
            _isVisible = room.IsVisible;
            _maxPlayers = room.MaxPlayers;
            _playerTtl = room.PlayerTtl;       // 방을 만들 때 정한 값 — 재접속(09-network.md 12절)이 쓰면 0 이 아니다
            _emptyRoomTtl = room.EmptyRoomTtl;
        }

        private void Forget()
        {
            _room.Clear();
            _players.Clear();
            _roomRestore.Clear();
            _playerRestore.Clear();
            _optionsRestore = _expectedUsersRestore = _nicknameRestore = false;
        }

        #endregion

        #region 남이 바꾼 것 되돌리기

        /// <summary>속성 바뀜 이벤트(PropertiesChanged) — Photon 이 방 · Player 사본을 고친 뒤에 온다.</summary>
        public void OnPropertiesChanged(EventData photonEvent)
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (room == null || photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber)
                return; // 내가 쓴 것

            int target = photonEvent.Parameters.ContainsKey(ParameterCode.TargetActorNr) && photonEvent[ParameterCode.TargetActorNr] is int actor ? actor : 0;
            if (!(photonEvent[ParameterCode.Properties] is Hashtable props))
                return;

            // 내 이름은 누구든 지킨다 — Photon 은 내 기기의 이름을 그대로 두고 남의 화면에서만 바꾼다
            if (target == PhotonNetwork.LocalPlayer.ActorNumber && props.ContainsKey(ActorProperties.PlayerName)
                && !Equals(props[ActorProperties.PlayerName], PhotonNetwork.LocalPlayer.NickName))
                _nicknameRestore = true;

            if (!IsTrustedMaster)
                return;

            if (target == 0)
                CheckRoom(props);
            else
                CheckPlayer(target, props);
        }

        private void CheckRoom(Hashtable props)
        {
            foreach (DictionaryEntry entry in props)
            {
                switch (entry.Key)
                {
                    case string key when !Same(entry.Value, _room[key]):
                        _roomRestore[key] = _room[key]; // 방장이 쓴 적 없는 키면 null — 지운다
                        break;
                    case byte code when code == GamePropertyKey.IsOpen || code == GamePropertyKey.IsVisible || code == GamePropertyKey.MaxPlayers
                                        || code == GamePropertyKey.MaxPlayersInt || code == GamePropertyKey.PlayerTtl || code == GamePropertyKey.EmptyRoomTtl:
                        _optionsRestore = true;
                        break;
                    case byte code when code == GamePropertyKey.ExpectedUsers:
                        _expectedUsersRestore = true;
                        break;
                }
            }
        }

        private void CheckPlayer(int actor, Hashtable props)
        {
            _players.TryGetValue(actor, out Hashtable intended);
            foreach (DictionaryEntry entry in props)
            {
                if (!(entry.Key is string key))
                    continue;

                object value = intended?[key];
                if (Same(entry.Value, value))
                    continue;

                if (!_playerRestore.TryGetValue(actor, out Hashtable restore))
                    _playerRestore[actor] = restore = new Hashtable();
                restore[key] = value;
            }
        }

        /// <summary>매 프레임 — 모아 둔 것을 한 번에 되돌린다.</summary>
        public void Flush()
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (room == null)
                return;

            if (_nicknameRestore)
            {
                _nicknameRestore = false;
                Debug.LogWarning($"[{nameof(RoomGuard)}] 남이 내 이름을 바꿈 — 되돌린다");
                if (SetPlayerNameProperty == null)
                    Debug.LogError($"[{nameof(RoomGuard)}] Player.SetPlayerNameProperty 를 찾지 못했다 — PUN 버전이 바뀌었으면 이 파일을 고친다");
                else
                    SetPlayerNameProperty.Invoke(PhotonNetwork.LocalPlayer, null);
            }

            if (!IsTrustedMaster)
            {
                _roomRestore.Clear();
                _playerRestore.Clear();
                _optionsRestore = _expectedUsersRestore = false;
                return;
            }

            if (_roomRestore.Count > 0)
            {
                Debug.LogWarning($"[{nameof(RoomGuard)}] 방장이 쓰지 않은 방 속성 [{string.Join(", ", _roomRestore.Keys.Cast<object>())}] — 되돌린다");
                var restore = new Hashtable();
                foreach (DictionaryEntry entry in _roomRestore)
                    restore[entry.Key] = entry.Value;
                _roomRestore.Clear();
                room.SetCustomProperties(restore);
            }

            foreach (KeyValuePair<int, Hashtable> pair in _playerRestore)
            {
                Debug.LogWarning($"[{nameof(RoomGuard)}] 방장이 쓰지 않은 P{pair.Key} 속성 [{string.Join(", ", pair.Value.Keys.Cast<object>())}] — 되돌린다");
                room.GetPlayer(pair.Key)?.SetCustomProperties(pair.Value);
            }
            _playerRestore.Clear();

            if (_optionsRestore)
            {
                _optionsRestore = false;
                Debug.LogWarning($"[{nameof(RoomGuard)}] 방장이 쓰지 않은 방 설정 — 되돌린다");
                room.IsOpen = _isOpen;
                room.IsVisible = _isVisible;
                room.MaxPlayers = _maxPlayers;
                room.PlayerTtl = _playerTtl;
                room.EmptyRoomTtl = _emptyRoomTtl;
            }

            if (_expectedUsersRestore)
            {
                _expectedUsersRestore = false;
                Debug.LogWarning($"[{nameof(RoomGuard)}] 방장이 쓰지 않은 자리 예약 — 지운다");
                room.ClearExpectedUsers(); // 이 게임은 자리 예약을 쓰지 않는다
            }
        }

        // 속성 값 비교 — 배열은 내용으로
        private static bool Same(object a, object b)
        {
            if (Equals(a, b))
                return true;
            if (a is Array x && b is Array y)
                return x.Length == y.Length && x.Cast<object>().SequenceEqual(y.Cast<object>());
            return false;
        }

        #endregion
    }
}
