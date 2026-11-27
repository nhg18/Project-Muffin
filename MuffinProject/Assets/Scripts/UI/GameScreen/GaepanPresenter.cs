using System.Collections;
using System.Collections.Generic;
using Chapchu.Game;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 개판 오분전 연출 (1021, 프로토타입 v2): 왼쪽 위 자명종 → 바퀴마다 자명종이 넘어감 → 판정 팻말.
    /// 시작 제목 · 「5 바퀴 동안」 도장은 카드의 3 단계 컷인(GaepanFx)이 보인다 — 판 위에 따로 띄우면 같은 글자가 두 번 나온다 (PM 2026-11-27).
    /// 값은 GameScreenPresenter 가 연출 큐 순서로 넘긴다. 늦게 열렸을 때는 Start 에서 방 속성 「gaepan」 을 한 번 읽어 자명종만 보인다 (09-network.md 6.1).
    /// </summary>
    public class GaepanPresenter : MonoBehaviour
    {
        [SerializeField] private GaepanHudView hud;
        [SerializeField] private GaepanJudgeView judge;
        [Tooltip("판정 표를 띄울 상대 캐릭터 자리.")]
        [SerializeField] private OpponentSeatsPresenter seats;
        [Tooltip("판정 표를 띄울 내 캐릭터 자리.")]
        [SerializeField] private RectTransform myCharacter;

        private void Awake()
        {
            if (hud == null || judge == null || seats == null || myCharacter == null)
                Debug.LogError($"[{nameof(GaepanPresenter)}] 참조가 비어 있습니다.", this);
        }

        private void Start()
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomProps.Gaepan, out object value) && value is int[] gaepan && gaepan.Length == 2
                && gaepan[1] >= 1 && gaepan[1] <= GameServer.GaepanRounds) // 남이 잠깐 바꾼 값은 거른다 (09-network.md 13절 #8)
                hud.Show(gaepan[1], GameServer.GaepanRounds);
        }

        /// <summary>
        /// 개판 상태가 바뀌었다 (OnGaepanChanged). 처음 · 다시 처음부터면 자명종을 새로 띄운다 (컷인 뒤), 바퀴가 줄면 자명종이 넘어간다,
        /// 0 이면 (판정 직전 · 1012 대소동으로 지워짐) 자명종을 거둔다.
        /// </summary>
        public IEnumerator ChangedRoutine(int left)
        {
            if (left <= 0)
            {
                hud.Hide(true);
                yield break;
            }

            if (left == GameServer.GaepanRounds)
            {
                hud.Hide(false);
                hud.Show(left, GameServer.GaepanRounds);
                yield break;
            }

            hud.Tick(left);
        }

        /// <summary>판정 팻말 (OnGaepanJudged) — 살아 있던 사람 머리 위에 기준 값. 끝날 때까지 기다린다.</summary>
        public IEnumerator JudgedRoutine(int loser, bool byHp, int[] actors, int[] values)
        {
            var points = new List<Vector2?>();
            int loserIndex = -1;
            for (int i = 0; i < actors.Length; i++)
            {
                if (actors[i] == loser)
                    loserIndex = i;
                points.Add(CharacterPoint(actors[i]));
            }
            yield return judge.PlayRoutine(byHp, values, points, loserIndex);
        }

        /// <summary>새 판 (1012 대소동) — 개판 표시를 모두 거둔다.</summary>
        public void ShowRestarted()
        {
            hud.Hide(false);
            judge.Hide();
        }

        private Vector2? CharacterPoint(int actor)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber == actor)
                return RectTransformUtility.WorldToScreenPoint(null, myCharacter.TransformPoint(myCharacter.rect.center));
            return seats.TryGetCharacterScreenPoint(actor, out Vector2 point) ? point : (Vector2?)null;
        }
    }
}
