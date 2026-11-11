using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Game;
using DG.Tweening;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 체인에 올라간 카드를 모든 화면에 보여 준다 (10-ui.md 5절 「현재 처리 카드」 · 5-1 단계 3 · 2-1 「카드 연출 자리」, 프로토타입 v2 FXV.show → settle).
    /// 내 카드는 손패에서 넘겨받고, 남의 카드는 그 좌석의 손 자리(발동한 함정은 그 칸)에서 앞면으로 만들어 바로 가운데 큰 자리로 보낸다 — 테이블에 눕히는 1 단계는 없다 (사용자 결정 2026-11-11).
    /// 3 단계: 가운데로 세워 크게 — 3D 층(테이블 · 손패)만 어둡게 하고 카드 아래에 주인 태그. 끝나도 줄이지 않고 그 크기로 둔다 (반응 시간 · 체인 처리 동안).
    /// 처리 결과가 오면 버림 더미로 보내 지운다 — 「손패로 반환」 이면 주인에게 돌려준다. 2 · 4 · 5 단계 · 무효 도장은 아직 없다.
    /// 카운터가 끼어들면 새 카드가 같은 자리 앞에 겹친다 — 체인 배치(붙이기 · 비키기)는 수치가 정해진 뒤 (10-ui.md 11절 #14).
    /// 언제 무엇을 하는지는 GameScreenPresenter 가 연출 큐 순서로 부른다. 판정은 하지 않는다.
    /// </summary>
    public class ChainCardsPresenter : MonoBehaviour
    {
        private const string MeName = "나";
        private const string OwnerOnlyFormat = "{0}";
        private const string OwnerToTargetFormat = "{0} → {1}";
        private const string TargetSeparator = " · ";
        private const string NameFormat = "<color=#{0}><noparse>{1}</noparse></color>";

        [Tooltip("PunGameServer 와 같은 덱 에셋 — 카드 종류 ID 로 카드 내용을 찾는다 (05-deck.md 8절).")]
        [SerializeField] private DeckData deck;

        [Header("종류별 카드 프리팹 (HandView 와 같은 것) — 남이 낸 카드를 만든다")]
        [SerializeField] private CardView actionCardPrefab;
        [SerializeField] private CardView counterCardPrefab;
        [SerializeField] private CardView trapCardPrefab;

        [Tooltip("남이 낸 카드가 출발하는 좌석의 손 자리를 묻는다.")]
        [SerializeField] private OpponentSeatsPresenter seats;
        [Tooltip("3 단계 동안 카드 아래에 뜨는 주인 태그.")]
        [SerializeField] private CardOwnerTagView ownerTag;
        [Tooltip("3 단계 동안 3D 층(테이블 · 덱 · 버림 더미 · 내 손패)을 어둡게 덮는 판 — 손패보다 앞, 크게 둔 카드보다 뒤에 있다. HUD 는 덮지 않는다.")]
        [SerializeField] private MeshRenderer dimmer;

        [Header("자리")]
        [Tooltip("3 단계 · 홀드 — 크게 둔 카드의 자리. 카드 기준점(아래 가운데)의 위치 · 회전 · 배율.")]
        [SerializeField] private Transform showSpot;
        [Tooltip("버림 더미의 맨 위 카드 자리 (DiscardPileView.TopAnchor). 위치 · 회전 · 배율 그대로 닿는다.")]
        [SerializeField] private Transform discardSpot;

        [Header("연출 (10-ui.md 5-1 — 3 단계 1.0 = 카드 데이터의 등장 시간 1.0초. 1 단계는 없다)")]
        [Tooltip("3 단계 — 손(좌석 · 함정 칸)에서 가운데로 커지며 가는 초. 옛 1 단계의 이동 시간과 같다.")]
        [SerializeField] private float zoomMoveDuration = 0.46f;
        [Tooltip("3 단계 전체 초 — 커지는 시간 포함. 이 동안 어둡게 + 주인 태그.")]
        [SerializeField] private float zoomDuration = 1f;
        [Tooltip("버림 더미로 가는 초.")]
        [SerializeField] private float discardDuration = 0.38f;
        [Tooltip("남의 카드가 좌석의 손 자리에서 나올 때의 배율 — 뒷면 부채꼴 카드(36 × 54) 크기.")]
        [SerializeField] private float seatCardScale = 0.16f;
        [Tooltip("체인에 카드가 여러 장일 때 뒤에 낸 카드를 카메라 쪽으로 띄우는 z — 같은 자리라 면이 겹치지 않게.")]
        [SerializeField] private float stackZ = -0.05f;
        [Tooltip("3 단계 동안 3D 층을 덮는 어둠의 알파 (시안 C3D.dim .42).")]
        [SerializeField] private float dimAlpha = 0.42f;
        [SerializeField] private float dimFadeDuration = 0.2f;

        [Header("손패를 올렸을 때 (10-ui.md 2-1 「카드 연출 자리」 — 프로토타입 v2 HANDUP_LAY)")]
        [Tooltip("크게 둔 카드가 줄어드는 비율 — 1.85 배 → 1.15 배.")]
        [SerializeField] private float handUpShrink = 1.15f / 1.85f;
        [Tooltip("카드 가운데가 올라가는 높이 (테이블 면 기준 유닛) — 시안 40 × 2 = 캔버스 80.")]
        [SerializeField] private float handUpRaise = 0.8f;
        [Tooltip("비키고 돌아오는 초 — 시안 0.2.")]
        [SerializeField] private float handUpDuration = 0.2f;

        // 체인에 올라가 처리 결과를 기다리는 카드 (인스턴스 ID → 카드). 함정 칸에서 나온 카드는 아직 그리지 않아 여기 없다.
        private readonly Dictionary<int, CardView> _cards = new Dictionary<int, CardView>();
        private readonly Dictionary<int, float> _lifts = new Dictionary<int, float>(); // 겹쳐 둔 카드마다 카메라 쪽으로 띄운 거리
        private readonly List<int> _order = new List<int>();                            // 체인에 오른 순서 — 맨 뒤가 맨 위 카드
        private bool _handRaised;
        private Camera _camera;
        private Tween _dimTween;

        private void Awake()
        {
            _camera = Camera.main;

            if (deck == null || actionCardPrefab == null || counterCardPrefab == null || trapCardPrefab == null || seats == null || ownerTag == null || dimmer == null
                || showSpot == null || discardSpot == null || _camera == null)
            {
                Debug.LogError($"[{nameof(ChainCardsPresenter)}] 참조 중 비어 있는 것이 있습니다 (메인 카메라 포함).", this);
                return;
            }

            SetDimAlpha(0f);
            dimmer.gameObject.SetActive(false);
        }

        /// <summary>체인 맨 위 카드(반응을 기다리는 카드)의 이름. 체인이 비었으면 null.</summary>
        public string TopCardName => _order.Count > 0 && _cards.TryGetValue(_order[_order.Count - 1], out CardView top) ? deck.GetCard(top.CardId)?.cardName : null;

        /// <summary>
        /// 내 손패가 올라왔다 · 내려갔다 — 올라와 있는 동안 체인의 카드를 작게 · 위로 비켜 손패와 겹치지 않게 한다. 판정과 상관없는 연출.
        /// 3 단계(크게 보여 주는 중)에도 바로 따른다.
        /// </summary>
        public void SetHandRaised(bool raised)
        {
            if (_handRaised == raised) return;

            _handRaised = raised;
            foreach (KeyValuePair<int, CardView> pair in _cards)
            {
                if (pair.Value == null) continue;

                GetSpot(pair.Value, _lifts[pair.Key], out Vector3 position, out Vector3 scale);
                MoveTo(pair.Value, position, showSpot.rotation, scale, handUpDuration, Ease.OutQuart);
            }
        }

        /// <summary>
        /// 카드가 체인에 올라갔다 (OnCardPlayed) — 3 단계(가운데에서 크게) 뒤 그 자리에 둔다. 연출 큐가 기다릴 코루틴.
        /// fromHand = 내 손패에서 떼어 온 카드. null 이면 새로 만든다 — fromSlot(발동한 함정이 있던 칸)이 있으면 그 자리에서, 없으면(남의 카드) 낸 사람 좌석의 손 자리에서.
        /// </summary>
        public IEnumerator PlayRoutine(int actorNumber, int cardInstanceId, int cardId, int[] targets, CardView fromHand, Transform fromSlot = null)
        {
            CardData data = deck.GetCard(cardId);
            CardView card = fromHand != null ? fromHand : Spawn(actorNumber, cardInstanceId, data, fromSlot);
            if (card == null || _cards.ContainsKey(cardInstanceId))
            {
                Debug.LogWarning($"[{nameof(ChainCardsPresenter)}] 카드를 보여 주지 못했다. instanceId={cardInstanceId} cardId={cardId}", this);
                yield break;
            }

            Transform t = card.transform;
            t.SetParent(transform, true); // 손패 올리기 · 내리기에 끌려가지 않게
            card.SetRaycastBlocking(false);

            float lift = stackZ * _cards.Count;
            _cards[cardInstanceId] = card;
            _lifts[cardInstanceId] = lift;
            _order.Add(cardInstanceId);

            // 3 단계 — 손에서 바로 가운데로 크게. 모두가 카드를 읽는 구간
            ownerTag.Show(data != null ? data.type : CardType.Action, OwnerText(actorNumber, targets));
            FadeDim(true);
            GetSpot(card, lift, out Vector3 spotPosition, out Vector3 spotScale);
            MoveTo(card, spotPosition, showSpot.rotation, spotScale, zoomMoveDuration, Ease.OutQuart);
            yield return new WaitForSeconds(zoomDuration);

            // 줄이지 않고 그대로 둔다 — 어둠과 주인 태그만 걷는다
            ownerTag.Hide();
            FadeDim(false);
        }

        /// <summary>체인의 카드가 처리됐다 (효과 적용 · 무효화) — 버림 더미로 보내 지운다. 연출 큐가 기다릴 코루틴. 여기 없는 카드(함정 칸에서 나온 카드)면 바로 끝난다.</summary>
        public IEnumerator DiscardRoutine(int cardInstanceId)
        {
            CardView card = Take(cardInstanceId);
            if (card == null)
                yield break;

            MoveTo(card, discardSpot.position, discardSpot.rotation, discardSpot.lossyScale, discardDuration, Ease.InQuad);
            yield return new WaitForSeconds(discardDuration);
            Destroy(card.gameObject);
        }

        /// <summary>체인에서 카드를 떼어 넘긴다 — 내 카드가 손패로 돌아올 때 (04-card.md 7절 「손패로 반환」). 여기 없으면 null.</summary>
        public CardView Take(int cardInstanceId)
        {
            if (!_cards.TryGetValue(cardInstanceId, out CardView card))
                return null;

            _cards.Remove(cardInstanceId);
            _lifts.Remove(cardInstanceId);
            _order.Remove(cardInstanceId);
            return card;
        }

        // 체인의 카드가 놓일 자리 · 배율 — 평소는 가운데 큰 자리(showSpot), 손패가 올라와 있으면 작게 · 위로.
        // 카드 기준점이 아래 가운데라, 줄어든 카드의 가운데를 handUpRaise 만큼 올린 뒤 그 아래 끝을 구한다. 올리는 높이는 카메라에 가까운 만큼 줄인다 (화면에서 캔버스 80).
        private void GetSpot(CardView card, float lift, out Vector3 position, out Vector3 scale)
        {
            position = showSpot.position + showSpot.forward * lift;
            scale = showSpot.lossyScale;
            if (!_handRaised) return;

            float cameraZ = _camera.transform.position.z;
            float near = (showSpot.position.z - cameraZ) / -cameraZ;
            float halfHeight = card.Height * scale.y / 2f;
            Vector3 center = position + showSpot.up * halfHeight + Vector3.up * (handUpRaise * near);
            scale *= handUpShrink;
            position = center - showSpot.up * (halfHeight * handUpShrink);
        }

        /// <summary>남의 카드가 그 사람 손패로 돌아갔다 (「손패로 반환」) — 좌석의 손 자리로 보내 지운다. 연출 큐가 기다릴 코루틴.</summary>
        public IEnumerator ReturnToSeatRoutine(int actorNumber, int cardInstanceId)
        {
            CardView card = Take(cardInstanceId);
            if (card == null)
                yield break;

            MoveTo(card, SeatHandPoint(actorNumber), Quaternion.identity, Vector3.one * seatCardScale, discardDuration, Ease.InQuad);
            yield return new WaitForSeconds(discardDuration);
            Destroy(card.gameObject);
        }

        // 남이 낸 카드 — 그 좌석의 손 자리에 작게 만든다. 손패 내용은 비공개라 낸 순간에 앞면이 처음 보인다.
        private CardView Spawn(int actorNumber, int cardInstanceId, CardData data, Transform fromSlot)
        {
            if (data == null)
                return null;

            CardView card = Instantiate(PrefabOf(data.type), transform);
            card.Setup(cardInstanceId, data);
            if (fromSlot != null)
            {
                // 함정 칸에 뒷면으로 누워 있던 카드가 공개되며 나온다
                card.transform.SetPositionAndRotation(fromSlot.position, fromSlot.rotation * Quaternion.Euler(0f, 180f, 0f));
                card.transform.localScale = fromSlot.lossyScale;
            }
            else
            {
                card.transform.SetPositionAndRotation(SeatHandPoint(actorNumber), Quaternion.identity);
                card.transform.localScale = Vector3.one * seatCardScale;
            }

            return card;
        }

        // 좌석의 손 자리(HUD 화면 좌표)를 테이블 면(z 0)의 월드 좌표로. 좌석이 없으면(나 · 나간 사람) 테이블 가운데.
        private Vector3 SeatHandPoint(int actorNumber)
        {
            Vector3 tableCenter = new Vector3(transform.position.x, transform.position.y, 0f);
            if (!seats.TryGetHandScreenPoint(actorNumber, out Vector2 screenPoint))
                return tableCenter;

            Ray ray = _camera.ScreenPointToRay(screenPoint);
            return Mathf.Approximately(ray.direction.z, 0f) ? tableCenter : ray.GetPoint(-ray.origin.z / ray.direction.z);
        }

        private static void MoveTo(CardView card, Vector3 position, Quaternion rotation, Vector3 scale, float duration, Ease ease)
        {
            Transform t = card.transform;
            t.DOKill();
            t.DOMove(position, duration).SetEase(ease).SetLink(card.gameObject);
            t.DORotateQuaternion(rotation, duration).SetEase(ease).SetLink(card.gameObject);
            t.DOScale(scale, duration).SetEase(ease).SetLink(card.gameObject);
        }

        private void FadeDim(bool on)
        {
            _dimTween?.Kill();
            dimmer.gameObject.SetActive(true);
            _dimTween = DOVirtual.Float(dimmer.material.color.a, on ? dimAlpha : 0f, dimFadeDuration, SetDimAlpha).SetLink(dimmer.gameObject)
                .OnComplete(() => dimmer.gameObject.SetActive(on));
        }

        private void SetDimAlpha(float alpha)
        {
            Color color = dimmer.material.color;
            color.a = alpha;
            dimmer.material.color = color;
        }

        // 「낸 사람 → 대상」, 이름은 플레이어 색 (10-ui.md 2-1 「카드 연출 자리」). 대상이 없으면 낸 사람만.
        private static string OwnerText(int actorNumber, int[] targets)
        {
            if (targets == null || targets.Length == 0)
                return string.Format(OwnerOnlyFormat, NameOf(actorNumber));

            return string.Format(OwnerToTargetFormat, NameOf(actorNumber), string.Join(TargetSeparator, targets.Select(NameOf)));
        }

        private static string NameOf(int actorNumber)
        {
            bool isMe = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber;
            return string.Format(NameFormat, ColorUtility.ToHtmlStringRGB(PlayerLook.InkOf(actorNumber)), isMe ? MeName : PlayerLook.NameOf(actorNumber));
        }

        private CardView PrefabOf(CardType type)
        {
            switch (type)
            {
                case CardType.Counter: return counterCardPrefab;
                case CardType.Trap:    return trapCardPrefab;
                default:               return actionCardPrefab;
            }
        }
    }
}
