using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Network.Events;
using Chapchu.UI.Popups;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 방장이 내 요청을 거절하면 사유를 토스트로 띄운다 (10-ui.md 8절). 드로우 · 카드 사용 등 요청 종류와 무관하게 한 곳에서 받는다.
    /// 「내 턴이 아니에요」 는 두 줄로 — 카드를 처리하는 중이면 지금 낼 수 있는 카운터가 있는지, 아니면 누구 차례인지 · 내 차례까지 몇 명인지 (8-2).
    /// </summary>
    public class RejectToastPresenter : MonoBehaviour
    {
        // 두 줄 토스트 — 굵은 첫 줄 + 작은 흐린 둘째 줄 (시안 v2 .toast.two). 토스트 글자 30 기준
        private const string TwoLineFormat = "{0}\n<size=80%><color=#6E645C>{1}</color></size>";
        private const string ResolvingHead = "카드를 처리하는 중이에요";
        private const string ResolvingCanCounter = "초록색으로 빛나는 카운터만 낼 수 있어요";
        private const string ResolvingNoCounter = "지금 낼 수 있는 카운터가 없어요";
        private const string TurnOfFormat = "지금은 {0} 차례";
        private const string NextIsMe = "다음이 내 차례예요";
        private const string BeforeMeFormat = "내 차례까지 {0}명 더";

        [Tooltip("누구 차례인지 · 내 차례까지 몇 명인지를 묻고, 지금 턴인 줄을 톡 튀운다.")]
        [SerializeField] private TurnOrderPresenter turnOrder;
        [Tooltip("카드를 처리하는 중인지 · 내가 낼 수 있는 카운터가 있는지.")]
        [SerializeField] private ReactionTimerPresenter reactionTimer;

        private void Awake()
        {
            if (turnOrder == null || reactionTimer == null)
                Debug.LogError($"[{nameof(RejectToastPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            GameEvents.OnMyRequestRejected += HandleMyRequestRejected;
        }

        private void OnDisable()
        {
            GameEvents.OnMyRequestRejected -= HandleMyRequestRejected;
        }

        private void HandleMyRequestRejected(int code)
        {
            if (PopupManager.Instance == null)
            {
                Debug.LogWarning($"[{nameof(RejectToastPresenter)}] {nameof(PopupManager)} 가 없어 거절 코드 {code} 를 띄우지 못했습니다.", this);
                return;
            }

            SoundManager.Play(Sfx.Reject);
            PopupManager.Instance.ShowToast(code == RejectCode.NotYourTurn ? NotYourTurnText() : RejectText.Get(code));
        }

        private string NotYourTurnText()
        {
            if (reactionTimer.IsOpen)
                return string.Format(TwoLineFormat, ResolvingHead, reactionTimer.CountersOffered ? ResolvingCanCounter : ResolvingNoCounter);

            int me = PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
            if (!turnOrder.TryDescribeWait(me, out string turnName, out int before))
                return RejectText.Get(RejectCode.NotYourTurn); // 내 턴인데 시간이 끝났다 · 턴을 아직 모른다

            return string.Format(TwoLineFormat, string.Format(TurnOfFormat, turnName),
                before == 0 ? NextIsMe : string.Format(BeforeMeFormat, before));
        }
    }
}
