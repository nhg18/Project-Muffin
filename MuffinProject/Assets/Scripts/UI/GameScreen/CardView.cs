using System;
using Chapchu.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 카드 한 장 (10-ui.md 2-1 「3D 층」). 앞면(틀 · 그림 · 이름 · 설명)과 뒷면은 서로 반대를 보는 Quad 이고 머티리얼이 뒷면을 컬링하므로,
    /// 루트를 Y 180° 돌리면 그대로 뒤집기다. 종류별 틀 머티리얼은 프리팹(행동 · 카운터 · 함정)이 갖고, 여기서는 CardData 의 이름 · 그림 · 설명만 입힌다.
    /// 포인터 진입 · 이탈 · 클릭 · 드래그는 카메라의 PhysicsRaycaster 가 루트의 BoxCollider 로 맞춰 준다 — 이벤트로만 알리고, 판정은 HandView 가 한다.
    /// 어느 장인지(InstanceId)와 무슨 카드인지(CardId)만 기억한다.
    /// uGUI 판 HandCardView · HandCard_* 프리팹은 CardFxTestBench 가 아직 쓴다 — 카드 이펙트가 3D 층으로 옮겨오면 지운다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("그림 Quad. 프리팹에서의 크기가 그림 칸이고, 그림 비율에 맞춰 그 안에서 줄인다. 그림이 없는 카드는 끈다.")]
        [SerializeField] private MeshRenderer artRenderer;

        private BoxCollider _collider;
        private Vector3 _artBox;

        public int InstanceId { get; private set; }

        /// <summary>카드 종류 ID — 프레젠터가 덱 에셋에서 대상 타입 등을 찾는 데 쓴다.</summary>
        public int CardId { get; private set; }

        /// <summary>포인터가 이 카드 위에 들어왔다.</summary>
        public event Action<CardView> OnHoverEntered;
        /// <summary>포인터가 이 카드에서 나갔다.</summary>
        public event Action<CardView> OnHoverExited;
        /// <summary>이 카드를 탭(클릭)했다. 드래그가 시작되면 오지 않는다.</summary>
        public event Action<CardView> OnClicked;
        /// <summary>드래그 시작 (카드, 화면 좌표).</summary>
        public event Action<CardView, Vector2> OnDragStarted;
        /// <summary>드래그 중 (카드, 화면 좌표).</summary>
        public event Action<CardView, Vector2> OnDragged;
        /// <summary>드래그 끝 — 놓은 화면 좌표.</summary>
        public event Action<CardView, Vector2> OnDragEnded;

        /// <summary>카드 높이 (유닛) — 콜라이더 크기. 손패가 내려간 상태를 계산하는 데 쓴다.</summary>
        public float Height => Collider.size.y;

        // 편집 모드(Awake 없음)에서도 쓸 수 있게 늦게 잡는다 — 빌더 · 확인 스크립트가 프리팹을 놓고 Setup 을 부른다.
        private BoxCollider Collider => _collider != null ? _collider : _collider = GetComponent<BoxCollider>();
        private Vector3 ArtBox => _artBox != Vector3.zero ? _artBox : _artBox = artRenderer.transform.localScale;

        private void Awake()
        {
            if (nameText == null || descriptionText == null || artRenderer == null)
                Debug.LogError($"[{nameof(CardView)}] 이름 · 설명 · 그림 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>false 면 포인터 이벤트를 받지 않고 아래 오브젝트(좌석 등)로 넘긴다. 보이는 것은 그대로.</summary>
        public void SetRaycastBlocking(bool blocking) => Collider.enabled = blocking;

        public void Setup(int instanceId, CardData data)
        {
            InstanceId = instanceId;
            CardId = data.id;
            nameText.text = data.cardName;
            descriptionText.text = data.description;
            SetArt(data.cardImage);

            gameObject.name = $"Card_{instanceId}_{data.id}";
        }

        // 그림 스프라이트의 텍스처를 그림 Quad 에 입힌다. 아틀라스에 묶인 스프라이트도 되게 UV 는 textureRect 로 잡는다.
        // 그림이 아직 없는 카드가 있다 — 그림 칸만 비워 둔다.
        private void SetArt(Sprite sprite)
        {
            artRenderer.enabled = sprite != null;
            if (sprite == null) return;

            Texture texture = sprite.texture;
            Rect rect = sprite.textureRect;
            var block = new MaterialPropertyBlock();
            artRenderer.GetPropertyBlock(block);
            block.SetTexture(MainTexId, texture);
            block.SetVector(MainTexStId, new Vector4(rect.width / texture.width, rect.height / texture.height, rect.x / texture.width, rect.y / texture.height));
            artRenderer.SetPropertyBlock(block);

            // 비율 유지 — 그림 칸 안에 맞춘다 (uGUI 의 preserveAspect).
            Vector3 box = ArtBox;
            float aspect = rect.width / rect.height;
            Vector3 scale = box;
            if (aspect > box.x / box.y)
                scale.y = box.x / aspect;
            else
                scale.x = box.y * aspect;
            artRenderer.transform.localScale = scale;
        }

        public void OnPointerEnter(PointerEventData eventData) => OnHoverEntered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => OnHoverExited?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);

        public void OnBeginDrag(PointerEventData eventData) => OnDragStarted?.Invoke(this, eventData.position);

        public void OnDrag(PointerEventData eventData) => OnDragged?.Invoke(this, eventData.position);

        public void OnEndDrag(PointerEventData eventData) => OnDragEnded?.Invoke(this, eventData.position);
    }
}
