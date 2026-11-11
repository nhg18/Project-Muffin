using Chapchu.Cards;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 카드 한 장 (10-ui.md 2-1 「3D 층」). 앞면(틀 · 그림 · 이름 · 설명)과 뒷면은 서로 반대를 보는 Quad 이고 머티리얼이 뒷면을 컬링하므로,
    /// 루트를 Y 180° 돌리면 그대로 뒤집기다. 종류별 틀 머티리얼은 프리팹(행동 · 카운터 · 함정)이 갖고, 여기서는 CardData 의 이름 · 그림 · 설명만 입힌다.
    /// 포인터 입력은 받지 않는다 — 손패 입력은 HandInput 이 한 곳에서 읽고 HandView 가 자리로 판정한다 (10-ui.md 4절). 콜라이더는 뒤에 있는 것(함정 칸 · 덱)의 탭을 가리는 데만 쓴다.
    /// 어느 장인지(InstanceId)와 무슨 카드인지(CardId)만 기억한다.
    /// uGUI 판 HandCardView · HandCard_* 프리팹은 CardFxTestBench 가 아직 쓴다 — 카드 이펙트가 3D 층으로 옮겨오면 지운다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CardView : MonoBehaviour
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("그림 Quad. 프리팹에서의 크기가 그림 칸이고, 그림 비율에 맞춰 그 안에서 줄인다. 그림이 없는 카드는 끈다.")]
        [SerializeField] private MeshRenderer artRenderer;
        [Tooltip("낼 수 있을 때 켜는 테두리 빛 — 카운터 카드의 초록 링 (10-ui.md 2-1 「손패 입력 값」). 없는 프리팹은 비워 둔다.")]
        [SerializeField] private GameObject glow;
        [Tooltip("고른 카드 · 놓으면 사용되는 카드의 노란 링 (10-ui.md 2-1 「손패 입력 값」). 없는 프리팹은 비워 둔다.")]
        [SerializeField] private GameObject selectRing;
        [Tooltip("지금 낼 수 없는 카드를 어둡게 덮는 판 — 반응 시간의 나머지 카드 (10-ui.md 2-1 「손패 입력 값」). 없는 프리팹은 비워 둔다.")]
        [SerializeField] private GameObject shade;

        private BoxCollider _collider;
        private Vector3 _artBox;

        public int InstanceId { get; private set; }

        /// <summary>카드 종류 ID — 프레젠터가 덱 에셋에서 대상 타입 등을 찾는 데 쓴다.</summary>
        public int CardId { get; private set; }

        /// <summary>카드 폭 (유닛) — 콜라이더 크기. 손패 간격을 계산하는 데 쓴다.</summary>
        public float Width => Collider.size.x;

        /// <summary>카드 높이 (유닛) — 콜라이더 크기. 손패 자리를 계산하는 데 쓴다.</summary>
        public float Height => Collider.size.y;

        // 편집 모드(Awake 없음)에서도 쓸 수 있게 늦게 잡는다 — 빌더 · 확인 스크립트가 프리팹을 놓고 Setup 을 부른다.
        private BoxCollider Collider => _collider != null ? _collider : _collider = GetComponent<BoxCollider>();
        private Vector3 ArtBox => _artBox != Vector3.zero ? _artBox : _artBox = artRenderer.transform.localScale;

        private void Awake()
        {
            if (nameText == null || descriptionText == null || artRenderer == null)
                Debug.LogError($"[{nameof(CardView)}] 이름 · 설명 · 그림 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>false 면 포인터를 가리지 않고 아래 오브젝트(좌석 등)로 넘긴다. 보이는 것은 그대로.</summary>
        public void SetRaycastBlocking(bool blocking) => Collider.enabled = blocking;

        /// <summary>낼 수 있다는 테두리 빛을 켜고 끈다. 빛이 없는 종류면 아무 일 없다.</summary>
        public void SetGlow(bool on) => SetActive(glow, on);

        /// <summary>고른 카드 · 놓으면 사용되는 카드의 노란 링을 켜고 끈다.</summary>
        public void SetSelected(bool on) => SetActive(selectRing, on);

        /// <summary>지금 낼 수 없는 카드로 어둡게 한다.</summary>
        public void SetDimmed(bool on) => SetActive(shade, on);

        /// <summary>이 카드와 자식 전부의 레이어를 바꾼다 — 어느 카메라가 그릴지 (HUD 위 층은 TopCardLayerView).</summary>
        public void SetLayer(int layer)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        public void Setup(int instanceId, CardData data)
        {
            InstanceId = instanceId;
            CardId = data.id;
            nameText.text = data.cardName;
            descriptionText.text = data.description;
            SetArt(data.cardImage);

            gameObject.name = $"Card_{instanceId}_{data.id}";
        }

        private static void SetActive(GameObject target, bool on)
        {
            if (target != null && target.activeSelf != on)
                target.SetActive(on);
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
    }
}
