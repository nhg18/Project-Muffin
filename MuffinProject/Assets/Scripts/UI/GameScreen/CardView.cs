using Chapchu.Cards;
using Chapchu.Game;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 카드 한 장 (10-ui.md 2-1 「3D 층」 · 「손패 · 카드 (3D 층)」, 시안 통합 UI v3.20 카드 4번). 앞면(종이 · 틀 · 그림 · 이름 · 설명)과 뒷면은 서로 반대를 보는 면이고
    /// 셰이더가 뒷면을 컬링하므로, 루트를 Y 180° 돌리면 그대로 뒤집기다. 종류별 틀 머티리얼은 프리팹(행동 · 카운터 · 함정)이 갖고, 여기서는 CardData 의 이름 · 그림 · 설명 · 등급만 입힌다.
    /// 생김새: 흰 종이 테 + 잉크 선 한 줄 + 아래로 퍼지는 그림자 + 등급 수만큼 낙서 별 (종이 셰이더 Chapchu/Card Paper). 빛 · 오라 · 반짝은 없다.
    /// 손패 상태 (시안 v3.4 · v3.5): 포인터 아래 카드는 그림자가 커지고, 고른 카드는 노란 형광펜 + 더 큰 그림자, 지금 낼 수 없는 카드는 흐려진다. 크기는 HandView 가 정한다.
    /// 루트의 SortingGroup 이 카드 한 장을 묶어 뒤 카드부터 그린다 — 반투명 가장자리 · 그림자가 뒤 카드 위에 매끈하게 섞인다.
    /// 포인터 입력은 받지 않는다 — 손패 입력은 HandInput 이 한 곳에서 읽고 HandView 가 자리로 판정한다 (10-ui.md 4절). 콜라이더는 뒤에 있는 것(함정 칸 · 덱)의 탭을 가리는 데만 쓴다.
    /// 어느 장인지(InstanceId)와 무슨 카드인지(CardId · Type)만 기억한다.
    /// uGUI 판 HandCardView · HandCard_* 프리팹은 CardFxTestBench 가 아직 쓴다 — 카드 이펙트가 3D 층으로 옮겨오면 지운다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CardView : MonoBehaviour
    {
        private const float ArtFocusFromTop = 0.3f; // 그림을 자를 때 세로 기준 — 위에서 30 % (시안 object-position 50% 30%)

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int HasTexId = Shader.PropertyToID("_HasTex");
        private static readonly int TypeColorId = Shader.PropertyToID("_TypeColor");
        private static readonly int GradeId = Shader.PropertyToID("_Grade");
        private static readonly int MarkId = Shader.PropertyToID("_Mark");
        private static readonly int FilterId = Shader.PropertyToID("_Filter");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int BlurId = Shader.PropertyToID("_Blur");

        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("그림이 아직 없는 카드에만 켜는 「그림 준비 중」 글자 (시안 v3.21 도감 .art.none).")]
        [SerializeField] private TMP_Text pendingText;
        [Tooltip("앞면 종이 (Chapchu/Card Paper) — 틀 · 잉크 선 · 형광펜 · 등급 별을 그린다.")]
        [SerializeField] private MeshRenderer frameRenderer;
        [Tooltip("그림 칸 (Chapchu/Card Art). 프리팹에서의 크기가 그림 칸이고, 그림을 칸에 꽉 채워(넘치는 쪽은 잘라) 입힌다. 그림이 없는 카드는 「그림 준비 중」.")]
        [SerializeField] private MeshRenderer artRenderer;
        [Tooltip("뒷면 종이 — 흐림만 같이 받는다.")]
        [SerializeField] private MeshRenderer backRenderer;
        [Tooltip("그림자 (Chapchu/Card Soft). 쉴 때 값은 그림자 머티리얼에 있고, 포인터 아래 · 고른 카드의 값은 아래.")]
        [SerializeField] private MeshRenderer shadowRenderer;
        [Tooltip("카드 종류 색 — 「그림 준비 중」 칸의 바탕 · 점선 · 발바닥 (시안 --tc).")]
        [SerializeField] private Color typeColor = new Color32(0x00, 0x9D, 0xFF, 0xFF);

        [Header("이름 (시안 .card .nm)")]
        [Tooltip("이름이 이 글자 수보다 길면 작은 크기로 (시안 .nm.long — 4 자보다 길 때).")]
        [SerializeField] private int longNameLength = 4;
        [Tooltip("긴 이름의 글자 크기 (TMP — 시안 11.5 px, size-adjust 는 글꼴 에셋 Scale). 짧은 이름은 프리팹의 이름 글자 크기.")]
        [SerializeField] private float longNameFontSize = 2.363f;

        [Header("그림자 (시안 .card::before box-shadow) — 쉴 때는 그림자 머티리얼 값")]
        [Tooltip("포인터 아래 카드 (시안 .card.hov 0 15px 22px .36).")]
        [SerializeField] private ShadowLook hoverShadow = new ShadowLook { drop = 30.73f, blur = 45.2f, alpha = 0.36f };
        [Tooltip("고른 카드 · 놓으면 사용되는 카드 (시안 .card.sel · .armed 0 17px 24px .34).")]
        [SerializeField] private ShadowLook markedShadow = new ShadowLook { drop = 34.83f, blur = 49.31f, alpha = 0.34f };
        [Tooltip("그림자가 바뀌는 초 (시안 transition box-shadow .2s).")]
        [SerializeField] private float shadowDuration = 0.2f;

        [Header("흐림 — 지금 낼 수 없는 손패 (시안 v3.5 .card.hand.dim)")]
        [Tooltip("채도 (시안 saturate .15).")]
        [SerializeField, Range(0f, 1f)] private float dimSaturate = 0.15f;
        [Tooltip("밝기 (시안 brightness .68).")]
        [SerializeField, Range(0f, 1f)] private float dimBrightness = 0.68f;
        [Tooltip("대비 (시안 contrast .8).")]
        [SerializeField, Range(0f, 1f)] private float dimContrast = 0.8f;
        [Tooltip("흐려지는 초 (시안 transition filter .2s).")]
        [SerializeField] private float dimDuration = 0.2f;

        private BoxCollider _collider;
        private MaterialPropertyBlock _block;
        private bool _hovered;
        private bool _marked;
        private bool _dimmed;
        private float _dim;           // 0 맑음 ~ 1 다 흐림 — 넘어가는 중의 값
        private ShadowLook _shadow;   // 지금 그림자 — 넘어가는 중의 값
        private Tween _shadowTween;
        private Tween _dimTween;
        private string _description; // 카드 문구 원문 — 흐림이 키워드 색을 다시 칠할 때 쓴다
        private bool _hasKeywords;

        // 프리팹에 저장된 값 — 흐림 · 이름 크기 · 그림자의 기준 (ReadPrefabValues)
        private bool _prefabValuesRead;
        private Color _nameColor;
        private Color _descriptionColor;
        private Color _pendingColor;
        private float _nameFontSize;
        private Color _shadowColor;
        private ShadowLook _restShadow;

        public int InstanceId { get; private set; }

        /// <summary>카드 종류 ID — 프레젠터가 덱 에셋에서 대상 타입 등을 찾는 데 쓴다.</summary>
        public int CardId { get; private set; }

        /// <summary>카드 종류 — 손패가 지금 낼 수 있는 카드를 가리는 데 쓴다 (시안 v3.5).</summary>
        public CardType Type { get; private set; }

        /// <summary>카드 문구 글 — 키워드 설명 창(KeywordTipView)이 포인터 아래 키워드 링크를 찾는다.</summary>
        public TMP_Text DescriptionText => descriptionText;

        /// <summary>카드 폭 (유닛) — 콜라이더 크기(앞면 크기, 종이 테 제외). 손패 간격을 계산하는 데 쓴다.</summary>
        public float Width => Collider.size.x;

        /// <summary>카드 높이 (유닛) — 콜라이더 크기(앞면 크기, 종이 테 제외). 손패 자리를 계산하는 데 쓴다.</summary>
        public float Height => Collider.size.y;

        // 편집 모드(Awake 없음)에서도 쓸 수 있게 늦게 잡는다 — 빌더 · 확인 스크립트가 프리팹을 놓고 Setup 을 부른다.
        private BoxCollider Collider => _collider != null ? _collider : _collider = GetComponent<BoxCollider>();
        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        private void Awake()
        {
            if (nameText == null || descriptionText == null || pendingText == null || frameRenderer == null || artRenderer == null || backRenderer == null || shadowRenderer == null)
                Debug.LogError($"[{nameof(CardView)}] 글자 · 면 · 그림자 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>false 면 포인터를 가리지 않고 아래 오브젝트(좌석 등)로 넘긴다. 보이는 것은 그대로.</summary>
        public void SetRaycastBlocking(bool blocking) => Collider.enabled = blocking;

        /// <summary>포인터 아래 카드 — 그림자가 커진다 (시안 .card.hov). 고른 카드의 그림자가 앞선다.</summary>
        public void SetHovered(bool on)
        {
            if (_hovered == on) return;

            _hovered = on;
            RefreshShadow();
        }

        /// <summary>고른 카드 · 놓으면 사용되는 카드 — 노란 형광펜 + 그림자가 더 커진다 (시안 .card.sel · .armed).</summary>
        public void SetSelected(bool on)
        {
            if (_marked == on) return;

            _marked = on;
            SetFloat(frameRenderer, MarkId, on ? 1f : 0f);
            RefreshShadow();
        }

        /// <summary>지금 낼 수 없는 카드 — 채도 · 밝기 · 대비를 내린다 (시안 v3.5 .card.hand.dim). 그림자 · 글자까지 카드 전체.</summary>
        public void SetDimmed(bool on)
        {
            if (_dimmed == on) return;

            _dimmed = on;
            _dimTween?.Kill();
            float target = on ? 1f : 0f;
            if (!Application.isPlaying || dimDuration <= 0f)
            {
                ApplyDim(target);
                return;
            }

            _dimTween = DOTween.To(() => _dim, ApplyDim, target, dimDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        /// <summary>이 카드와 자식 전부의 레이어를 바꾼다 — 어느 카메라가 그릴지 (HUD 위 층은 TopCardLayerView).</summary>
        public void SetLayer(int layer)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        public void Setup(int instanceId, CardData data)
        {
            ReadPrefabValues();
            InstanceId = instanceId;
            CardId = data.id;
            Type = data.type;

            // 이름 — 4 자보다 길면 작은 크기 (시안 .nm.long). 그래도 칸을 넘치면 TMP 자동 크기가 줄인다
            nameText.text = data.cardName;
            float nameSize = data.cardName.Length > longNameLength ? longNameFontSize : _nameFontSize;
            nameText.fontSizeMax = nameSize;
            nameText.fontSize = nameSize;

            _description = data.description;
            _hasKeywords = CardKeywords.HasKeywords(_description, Type);
            RefreshDescription();
            SetArt(data.LoadArt());
            SetFloat(frameRenderer, GradeId, (int)data.grade); // 별 수 = 등급 (일반 0 · 고급 1 · 희귀 2 · 개판 3)

            gameObject.name = $"Card_{instanceId}_{data.id}";
        }

        // 프리팹에 저장된 값을 읽어 둔다 — 편집 모드(Awake 없음)에서도 쓰게 처음 쓸 때. 흐림은 이 색을 거르고, 그림자는 이 값으로 돌아간다.
        private void ReadPrefabValues()
        {
            if (_prefabValuesRead) return;

            _prefabValuesRead = true;
            _nameColor = nameText.color;
            _descriptionColor = descriptionText.color;
            _pendingColor = pendingText.color;
            _nameFontSize = nameText.fontSizeMax;
            Material shadow = shadowRenderer.sharedMaterial;
            _shadowColor = shadow.GetColor(ColorId);
            _restShadow = new ShadowLook { drop = shadow.GetFloat(OffsetId), blur = shadow.GetFloat(BlurId), alpha = _shadowColor.a };
            _shadow = _restShadow;
        }

        // 문구 — 낱말 묶기 · 키워드 색 · 아이콘 (CardKeywords, 11-card-list 5절). 키워드 색은 태그라 글자색 거르기가 닿지 않아, 흐린 만큼 거른 색으로 다시 칠한다.
        private void RefreshDescription()
        {
            if (_dim <= 0f)
            {
                descriptionText.text = CardKeywords.Format(_description, Type);
                return;
            }

            Vector4 filter = DimFilter(_dim);
            descriptionText.text = CardKeywords.Format(_description, Type, color => Filter(color, filter));
        }

        // 그림 스프라이트의 텍스처를 그림 칸에 입힌다 — 칸에 꽉 채우고 넘치는 쪽을 자른다: 가로는 가운데, 세로는 위에서 30 % (시안 cover · object-position 50% 30%).
        // 아틀라스에 묶인 스프라이트도 되게 UV 는 textureRect 로 잡는다. 그림이 아직 없는 카드는 「그림 준비 중」 칸 + 글자.
        private void SetArt(Sprite sprite)
        {
            pendingText.gameObject.SetActive(sprite == null);

            MaterialPropertyBlock block = Block;
            artRenderer.GetPropertyBlock(block);
            block.SetColor(TypeColorId, typeColor);
            if (sprite == null)
            {
                block.SetFloat(HasTexId, 0f);
                artRenderer.SetPropertyBlock(block);
                return;
            }

            Texture texture = sprite.texture;
            Rect rect = sprite.textureRect;
            Vector3 box = artRenderer.transform.localScale;
            float boxAspect = box.x / box.y, aspect = rect.width / rect.height;
            Vector2 visible = Vector2.one; // 그림 안에서 칸에 들어가는 부분 (0 ~ 1)
            if (aspect > boxAspect)
                visible.x = boxAspect / aspect;
            else
                visible.y = aspect / boxAspect;
            var start = new Vector2((1f - visible.x) * 0.5f, (1f - visible.y) * (1f - ArtFocusFromTop)); // UV 는 아래가 0 — 잘리는 몫의 70 % 가 아래

            block.SetFloat(HasTexId, 1f);
            block.SetTexture(MainTexId, texture);
            block.SetVector(MainTexStId, new Vector4(
                rect.width * visible.x / texture.width, rect.height * visible.y / texture.height,
                (rect.x + rect.width * start.x) / texture.width, (rect.y + rect.height * start.y) / texture.height));
            artRenderer.SetPropertyBlock(block);
        }

        // 그림자 — 고른 카드 > 포인터 아래 > 쉴 때 (시안 .sel 규칙이 .hov 뒤에 있어 이긴다). 지금 값에서 목표로 넘어간다.
        private void RefreshShadow()
        {
            ReadPrefabValues();
            ShadowLook target = _marked ? markedShadow : _hovered ? hoverShadow : _restShadow;
            _shadowTween?.Kill();
            if (!Application.isPlaying || shadowDuration <= 0f)
            {
                ApplyShadow(target);
                return;
            }

            ShadowLook from = _shadow;
            _shadowTween = DOTween.To(() => 0f, t => ApplyShadow(ShadowLook.Lerp(from, target, t)), 1f, shadowDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        private void ApplyShadow(ShadowLook look)
        {
            _shadow = look;
            MaterialPropertyBlock block = Block;
            shadowRenderer.GetPropertyBlock(block);
            block.SetFloat(OffsetId, look.drop);
            block.SetFloat(BlurId, look.blur);
            block.SetColor(ColorId, new Color(_shadowColor.r, _shadowColor.g, _shadowColor.b, look.alpha));
            shadowRenderer.SetPropertyBlock(block);
        }

        // 흐림 정도(0 ~ 1)를 면 · 그림자 셰이더(_Filter)와 글자색에 넣는다 — CSS 처럼 채도 · 밝기 · 대비 값 자체가 넘어간다.
        private void ApplyDim(float amount)
        {
            ReadPrefabValues();
            _dim = amount;
            Vector4 filter = DimFilter(amount);
            SetVector(frameRenderer, FilterId, filter);
            SetVector(artRenderer, FilterId, filter);
            SetVector(backRenderer, FilterId, filter);
            SetVector(shadowRenderer, FilterId, filter);

            nameText.color = Filter(_nameColor, filter);
            descriptionText.color = Filter(_descriptionColor, filter);
            pendingText.color = Filter(_pendingColor, filter);
            if (_hasKeywords)
                RefreshDescription();
        }

        private Vector4 DimFilter(float amount) =>
            new Vector4(Mathf.Lerp(1f, dimSaturate, amount), Mathf.Lerp(1f, dimBrightness, amount), Mathf.Lerp(1f, dimContrast, amount), 0f);

        // CardFilter.cginc 와 같은 식 — CSS saturate → brightness → contrast, 단계마다 0 ~ 1 로 자른다. 알파는 그대로.
        private static Color Filter(Color c, Vector4 filter)
        {
            float luma = 0.213f * c.r + 0.715f * c.g + 0.072f * c.b;
            float s = filter.x;
            var rgb = new Vector3(
                Mathf.Clamp01(luma + s * (0.787f * c.r - 0.715f * c.g - 0.072f * c.b)),
                Mathf.Clamp01(luma + s * (-0.213f * c.r + 0.285f * c.g - 0.072f * c.b)),
                Mathf.Clamp01(luma + s * (-0.213f * c.r - 0.715f * c.g + 0.928f * c.b)));
            for (int i = 0; i < 3; i++)
                rgb[i] = Mathf.Clamp01((Mathf.Clamp01(rgb[i] * filter.y) - 0.5f) * filter.z + 0.5f);
            return new Color(rgb.x, rgb.y, rgb.z, c.a);
        }

        private void SetFloat(Renderer target, int id, float value)
        {
            MaterialPropertyBlock block = Block;
            target.GetPropertyBlock(block);
            block.SetFloat(id, value);
            target.SetPropertyBlock(block);
        }

        private void SetVector(Renderer target, int id, Vector4 value)
        {
            MaterialPropertyBlock block = Block;
            target.GetPropertyBlock(block);
            block.SetVector(id, value);
            target.SetPropertyBlock(block);
        }

        // 그림자 한 벌 — 아래로 민 거리 (u) · 흐림 반지름 (u) · 진하기 (0 ~ 1)
        [System.Serializable]
        private struct ShadowLook
        {
            public float drop;
            public float blur;
            [Range(0f, 1f)] public float alpha;

            public static ShadowLook Lerp(ShadowLook a, ShadowLook b, float t) => new ShadowLook
            {
                drop = Mathf.Lerp(a.drop, b.drop, t),
                blur = Mathf.Lerp(a.blur, b.blur, t),
                alpha = Mathf.Lerp(a.alpha, b.alpha, t),
            };
        }
    }
}
