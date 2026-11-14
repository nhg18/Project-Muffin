using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 함정 칸 (04-card.md 6절 · 10-ui.md 2-1 「함정 칸」). 칸 수는 전원에게 가는 장수로, 어떤 함정인지는 나에게만 오는 내용(OnMyTrapSet · OnMyTrapRemoved)으로 안다.
    /// 설치한 칸을 탭하면 그 함정을 크게 보여 주고, 다음 탭에 닫는다 (시안 「내 함정 칸」). 발동은 입력이 없다 — 조건이 맞으면 방장이 한다 (04-card.md 6절 「발동 규칙」).
    /// 언제 넣고 빼는지는 GameScreenPresenter 가 연출 큐 순서로 부른다. 늦게 열린 화면은 장수만 복구한다 — 내용은 「내 카드 다시 받기」 가 화면에 이어지면.
    /// </summary>
    [RequireComponent(typeof(TrapSlotsView))]
    public class MyTrapsPresenter : MonoBehaviour
    {
        [Tooltip("PunGameServer 와 같은 덱 에셋 — 카드 종류 ID 로 카드 내용을 찾는다.")]
        [SerializeField] private DeckData deck;
        [Tooltip("크게 보기에 쓰는 함정 카드 프리팹 (HandView 와 같은 것).")]
        [SerializeField] private CardView trapCardPrefab;
        [Tooltip("크게 보기 자리 — 체인 카드를 크게 두는 자리와 같다 (위치 · 회전 · 배율).")]
        [SerializeField] private Transform previewSpot;
        [Tooltip("크게 보기 카드를 체인 카드보다 카메라 쪽으로 띄우는 z.")]
        [SerializeField] private float previewZ = -0.6f;

        // 칸 순서대로 — 설치한 순서. 인스턴스 ID 와 카드 종류 ID
        private readonly List<int> _instanceIds = new List<int>();
        private readonly List<int> _cardIds = new List<int>();
        private TrapSlotsView _view;
        private CardView _preview;
        private bool _suppressClick; // 크게 보기를 닫은 그 탭이 칸 위였을 때 다시 열리지 않게
        private int _previewOpenedFrame = -1; // 크게 보기를 연 프레임 — 그 프레임에는 닫지 않는다

        private void Awake()
        {
            _view = GetComponent<TrapSlotsView>();

            if (deck == null || trapCardPrefab == null || previewSpot == null)
                Debug.LogError($"[{nameof(MyTrapsPresenter)}] {nameof(deck)} · {nameof(trapCardPrefab)} · {nameof(previewSpot)} 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable() => _view.OnSlotClicked += HandleSlotClicked;

        private void OnDisable() => _view.OnSlotClicked -= HandleSlotClicked;

        private void Start()
        {
            // 씬이 늦게 열렸을 때의 지금 장수 (09-network.md 6.1)
            if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerProps.TrapCount, out object count) && count is int countValue)
                _view.SetCount(countValue);
        }

        // 크게 보기는 다음 탭(어디든)에 닫는다. 터치는 마우스로 흉내 내므로 마우스 버튼만 본다.
        private void Update()
        {
            // 연 프레임은 건너뛴다 — 누름 · 뗌이 한 프레임에 들어온 탭은 클릭(EventSystem)이 먼저 열고, 같은 프레임의 이 판정이 바로 닫아 버린다
            if (_preview != null && Input.GetMouseButtonDown(0) && Time.frameCount != _previewOpenedFrame)
            {
                HidePreview();
                _suppressClick = true;
            }
        }

        private void LateUpdate()
        {
            if (_suppressClick && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
                _suppressClick = false;
        }

        /// <summary>내 함정 칸 수 (OnTrapSet 등의 trapCount).</summary>
        public void ShowCount(int trapCount) => _view.SetCount(trapCount);

        /// <summary>다음에 설치되는 함정이 들어갈 칸의 자리.</summary>
        public Transform NextSlot => _view.SlotAnchor(_instanceIds.Count);

        /// <summary>그 함정이 놓인 칸의 자리. 모르는 함정이면 마지막으로 채워진 칸.</summary>
        public Transform SlotOf(int cardInstanceId)
        {
            int index = _instanceIds.IndexOf(cardInstanceId);
            return _view.SlotAnchor(index >= 0 ? index : _view.Count - 1);
        }

        /// <summary>함정이 내 칸에 들어왔다 (설치 · 빼앗아 옴).</summary>
        public void Add(int cardInstanceId, int cardId)
        {
            _instanceIds.Add(cardInstanceId);
            _cardIds.Add(cardId);
        }

        /// <summary>함정이 내 칸에서 빠졌다 (발동 · 파괴 · 손패로 반환 · 빼앗김). 모르는 함정이면 아무 일 없다.</summary>
        public void Remove(int cardInstanceId)
        {
            int index = _instanceIds.IndexOf(cardInstanceId);
            if (index < 0) return;

            _instanceIds.RemoveAt(index);
            _cardIds.RemoveAt(index);
            HidePreview();
        }

        /// <summary>내 함정이 전부 사라졌다 (최종 사망).</summary>
        public void Clear()
        {
            _instanceIds.Clear();
            _cardIds.Clear();
            _view.SetCount(0);
            HidePreview();
        }

        private void HandleSlotClicked(int index)
        {
            if (_suppressClick) return;

            // 크게 보기가 열린 채로 온 클릭은 닫는 탭이다 — 누름 · 뗌이 한 프레임에 들어와 Update 의 닫기 판정보다 클릭이 먼저 온 경우. 닫기만 하고 다시 열지 않는다
            if (_preview != null)
            {
                HidePreview();
                return;
            }

            if (index < 0 || index >= _cardIds.Count) return;

            CardData data = deck.GetCard(_cardIds[index]);
            if (data == null) return;

            _preview = Instantiate(trapCardPrefab, transform);
            _previewOpenedFrame = Time.frameCount;
            _preview.Setup(_instanceIds[index], data);
            _preview.SetRaycastBlocking(false);
            _preview.transform.SetPositionAndRotation(previewSpot.position + previewSpot.forward * previewZ, previewSpot.rotation);
            _preview.transform.localScale = previewSpot.lossyScale;
        }

        private void HidePreview()
        {
            if (_preview == null) return;

            Destroy(_preview.gameObject);
            _preview = null;
        }
    }
}
