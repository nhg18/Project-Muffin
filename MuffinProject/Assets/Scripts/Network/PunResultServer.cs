using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using Chapchu.Network.Events;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.Network
{
    /// <summary>
    /// 결과 화면의 「한 판 더」 (07-win-condition.md 11-1) — 준비 모으기 · 봇 준비 · 카운트다운 · 다음 판 시작. ResultScene 에 하나 둔다.
    /// 요청: RequestSetReady → RPC_ResultRequestSetReady → (방장).
    /// 결과: 알림 없이 방 속성(지금 값)으로만 — 준비한 사람 · 시작 시각. 화면은 StateChanged 를 듣고 다시 읽는다.
    /// 이모트(빠른 말, 07 11-2)도 방장을 거친다: RequestEmote → RPC_ResultRequestEmote → (방장) → RPC_OnResultEmote → EmoteShown.
    /// 원본은 방장 메모리다 (NetworkManager.RoomProp). 방 속성 사본은 아무 클라나 쓸 수 있어 읽을 때 모양 · 범위를 확인한다 (09-network.md 13절).
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PunResultServer : MonoBehaviourPun
    {
        /// <summary>결과 화면이 열리고 이 시간이 지나면 각자 방을 나가 로비로 간다 (초, 07 11-1).</summary>
        public const float AutoLeaveSeconds = 45f;
        /// <summary>전원 준비부터 다음 판 시작까지 (초) — 3 · 2 · 1 각 0.8초 + 「다음 판 시작!」 1.1초.</summary>
        public const float CountdownSeconds = 3.5f;
        /// <summary>이모트(빠른 말)의 수 — 번호는 0 부터 (07 11-2).</summary>
        public const int EmoteCount = 4;

        // 봇은 화면이 열리고 차례로 준비한다 (초, 18-bot.md) — 첫 봇 · 그 뒤 봇마다
        private const float BotReadyDelay = 2.9f;
        private const float BotReadyStep = 0.7f;

        // 마감 시각은 방장이 게임 종료 때 쓴다 — 내 화면이 열린 시각 + AutoLeaveSeconds 보다 이만큼까지만 이르게 읽는다 (씬을 여는 데 걸린 시간).
        // 남이 마감을 당겨 써서 방 전원을 내보내지 못하게
        private const float DeadlineSlack = 10f;

        // 방장이 한 사람의 이모트를 받아 주는 간격 (초) — 화면의 버튼 쿨다운(1.2초)보다 조금 짧게, 늦게 도착한 정상 요청은 버리지 않게
        private const float EmoteInterval = 1f;

        private readonly RequestLimiter _limiter = new RequestLimiter(nameof(PunResultServer));
        private readonly Dictionary<int, float> _emoteAt = new Dictionary<int, float>(); // 방장 — 사람마다 마지막으로 이모트를 알린 때
        private double _openedAt;
        private bool _started; // 방장 — 다음 판 씬 이동을 보냈다 (씬이 바뀔 때까지 몇 프레임 더 돈다)

        /// <summary>준비한 사람 · 카운트다운 · 방 인원이 바뀜. 값은 Ready · StartAt · Members 로 다시 읽는다.</summary>
        public event Action StateChanged;
        /// <summary>누가 이모트를 보냄 — actorNumber · 이모트 번호(0 ~ EmoteCount − 1).</summary>
        public event Action<int, int> EmoteShown;

        /// <summary>지금 방에 있는 사람 + 봇 — 다음 판에 들어갈 사람들. 사람(actorNumber 순) 뒤에 봇.</summary>
        public static int[] Members => PhotonNetwork.InRoom
            ? PhotonNetwork.PlayerList.Select(player => player.ActorNumber).Concat(BotRoster.Actors).ToArray()
            : Array.Empty<int>();

        /// <summary>이 서버 시각이 지나면 각자 방을 나간다 — 카운트다운 중이면 나가지 않는다.</summary>
        public double Deadline
        {
            get
            {
                double latest = _openedAt + AutoLeaveSeconds;
                return NetworkManager.RoomProp(RoomProps.ResultDeadline) is double written ? Math.Clamp(written, latest - DeadlineSlack, latest) : latest;
            }
        }

        /// <summary>다음 판이 시작하는 서버 시각 — 전원이 준비해 카운트다운 중일 때. 아니면 0.</summary>
        public double StartAt => NetworkManager.RoomProp(RoomProps.ResultStartAt) is double at && at > 0 ? at : 0;

        /// <summary>준비한 사람 · 봇 (Members 순). 나간 사람은 빠진다.</summary>
        public int[] Ready => NetworkManager.RoomProp(RoomProps.ResultReady) is int[] written
            ? Members.Where(written.Contains).ToArray()
            : Array.Empty<int>();

        private void Awake() => _openedAt = PhotonNetwork.Time;

        private void OnEnable()
        {
            RoomEvents.OnRoomPropsChanged += HandleRoomPropsChanged;
            RoomEvents.OnPlayerLeft += HandlePlayerLeft;
        }

        private void OnDisable()
        {
            RoomEvents.OnRoomPropsChanged -= HandleRoomPropsChanged;
            RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
        }

        private void HandleRoomPropsChanged(Hashtable changed)
        {
            if (changed.ContainsKey(RoomProps.ResultReady) || changed.ContainsKey(RoomProps.ResultStartAt) || changed.ContainsKey(RoomProps.ResultDeadline) || changed.ContainsKey(RoomProps.Bots))
                StateChanged?.Invoke();
        }

        // 방장이 나가도 같다 — 새 방장이 방 속성의 준비 · 마감 · 카운트다운을 이어받아 Update 에서 잇는다 (07-win-condition.md 11절, PM 2026-11-20)
        private void HandlePlayerLeft(Player player) => StateChanged?.Invoke();

        #region 요청 (각 클라 → 방장)

        /// <summary>「한 판 더」 — 준비 · 취소. 결과는 방 속성으로 온다 (StateChanged).</summary>
        public void RequestSetReady(bool ready) => SendRequest(nameof(RPC_ResultRequestSetReady), ready);

        // 요청은 믿는 방장에게 보낸다 — 「지금 방장」 은 아무 클라나 가져갈 수 있다 (09-network.md 13절)
        private void SendRequest(string rpcName, params object[] parameters)
        {
            Player master = PhotonNetwork.CurrentRoom?.GetPlayer(NetworkManager.TrustedMaster);
            if (master == null)
            {
                Debug.LogWarning($"[{nameof(PunResultServer)}] 믿는 방장이 없다 — {rpcName} 요청을 보내지 않는다");
                return;
            }

            photonView.RPC(rpcName, master, parameters);
        }

        [PunRPC]
        private void RPC_ResultRequestSetReady(bool ready, PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            if (_started) return; // 이미 다음 판으로 넘어가는 중

            int actor = info.Sender.ActorNumber;
            int[] now = Ready;
            if (now.Contains(actor) == ready)
                return;

            Write(RoomProps.ResultReady, ready ? now.Append(actor).ToArray() : now.Where(a => a != actor).ToArray());
        }

        /// <summary>이모트(빠른 말)를 보낸다. 방장이 받아 주면 전원에게 EmoteShown 으로 온다 — 나에게도.</summary>
        public void RequestEmote(int index) => SendRequest(nameof(RPC_ResultRequestEmote), (byte)index);

        [PunRPC]
        private void RPC_ResultRequestEmote(byte index, PhotonMessageInfo info)
        {
            if (!NetworkManager.IsTrustedMaster) return;
            if (!_limiter.Allow(info)) return;
            if (index >= EmoteCount) return;

            int actor = info.Sender.ActorNumber;
            float now = Time.unscaledTime;
            if (_emoteAt.TryGetValue(actor, out float last) && now - last < EmoteInterval)
                return; // 도배 — 답 없이 버린다

            _emoteAt[actor] = now;
            photonView.RPC(nameof(RPC_OnResultEmote), RpcTarget.All, actor, index);
        }

        #endregion

        #region 알림 (방장 → 전원)

        [PunRPC]
        private void RPC_OnResultEmote(int actorNumber, byte index, PhotonMessageInfo info)
        {
            // 믿는 방장이 보낸 것만 — 아무 클라나 남의 이름으로 말풍선을 띄우지 못하게 (09-network.md 13절)
            if (info.Sender == null || info.Sender.ActorNumber != NetworkManager.TrustedMaster || index >= EmoteCount)
                return;

            EmoteShown?.Invoke(actorNumber, index);
        }

        #endregion

        #region 진행 (방장)

        // 봇 준비 · 카운트다운 · 다음 판 시작은 방장만 한다. 타이머를 따로 두지 않고 방 속성의 시각으로 판정한다 — 방장이 바뀌어도 이어진다.
        private void Update()
        {
            if (!NetworkManager.IsTrustedMaster || _started)
                return;

            // 명단 · 봇 · 준비 · 마감은 프레임마다 한 번씩만 읽는다 (Members · Ready · Deadline 과 같은 값)
            double now = PhotonNetwork.Time;
            double deadline = Deadline;
            int[] bots = BotRoster.Actors;
            int[] members = PhotonNetwork.PlayerList.Select(player => player.ActorNumber).Concat(bots).ToArray();
            int[] written = NetworkManager.RoomProp(RoomProps.ResultReady) as int[] ?? Array.Empty<int>();
            int[] ready = members.Where(written.Contains).ToArray();
            double startAt = StartAt;

            // 준비해 둔 사람이 방에 없다 (나감) — 카운트다운을 취소하고 준비를 남은 사람으로 다시 쓴다. 판정은 다음 프레임에 다시 (07 11-1)
            if (written.Length != ready.Length)
            {
                if (startAt > 0)
                    Write(RoomProps.ResultStartAt, null);
                Write(RoomProps.ResultReady, ready);
                return;
            }

            // 봇은 화면이 열린 시각(마감 − AutoLeaveSeconds)부터 차례로 준비한다
            double openedAt = deadline - AutoLeaveSeconds;
            int[] due = bots.Where((bot, i) => now >= openedAt + BotReadyDelay + BotReadyStep * i && !ready.Contains(bot)).ToArray();
            if (due.Length > 0)
            {
                ready = ready.Concat(due).ToArray();
                Write(RoomProps.ResultReady, ready);
            }

            // 지금 방에 있는 사람 + 봇이 2명 이상이고 전원 준비 — 나간 사람은 기다리지 않는다
            bool allReady = members.Length >= NetworkManager.MinPlayers && ready.Length == members.Length;
            if (startAt > 0)
            {
                if (!allReady)
                    Write(RoomProps.ResultStartAt, null); // 누가 나갔거나 준비를 풀었다 — 취소하고 다시 모은다
                else if (now >= startAt)
                    StartNextGame();
            }
            else if (allReady && deadline - now >= CountdownSeconds)
            {
                // 마감 직전에는 시작하지 않는다 — 카운트다운 도중에 마감이 지나 일부만 나가지 않게
                Write(RoomProps.ResultStartAt, now + CountdownSeconds);
            }
        }

        // 방은 게임을 시작할 때 닫힌 그대로다 (08-room.md 4절). 준비 · 시작 시각은 지우지 않는다 —
        // 지우면 씬이 바뀌기 전 몇 프레임 동안 「카운트다운 없음 + 마감 지남」 으로 보여 각자 방을 나갈 수 있다. 다음 게임 종료 때 새로 쓴다.
        private void StartNextGame()
        {
            _started = true;
            Debug.Log($"[{nameof(PunResultServer)}] 한 판 더 — 전원 준비 ({Members.Length}명), 다음 판으로");
            NetworkManager.Instance.LoadSceneForAll(SceneFlow.GameSceneAfterRoom);
        }

        // 값이 null · 빈 배열이면 키를 지운다
        private void Write(string key, object value)
        {
            if (value is int[] list && list.Length == 0)
                value = null;

            NetworkManager.Instance.SetRoomProps(new Hashtable { [key] = value });
            StateChanged?.Invoke();
        }

        #endregion
    }
}
