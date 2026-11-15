using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Post
{
    /// <summary>
    /// 코드로 그린 그림 한 장을 붙이는 UI 도형의 바탕 — 둥근 화살표 · 함정 카드 (10-ui 5-1 4 단계 장식).
    /// 그림자 · 테두리 · 면을 오브젝트 여럿이나 메시 여러 겹으로 그리면 CanvasGroup 으로 흐리게 할 때 아래 겹이 위 겹 너머로 비친다 (CODE_CONVENTION 10절).
    /// 텍셀마다 미리 겹쳐 그림 한 장으로 만들면 CSS opacity 처럼 통째로 흐려진다. 그림 크기는 가장 클 때 화면 1 픽셀에 텍셀 몇 개로 정한다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다 — 없으면 아무것도 그려지지 않는다
    public abstract class UIBakedGraphic : MaskableGraphic
    {
        private const int MaxTexels = 1024;

        [Header("그림")]
        [Tooltip("가장 클 때 화면 1 픽셀에 텍셀 몇 개. 그림 크기 = 그리는 곳 크기 × 가장 큰 배율 × 실제 캔버스 배율 × 이 값 — 화면이 클수록 그림도 크고 그리는 시간도 늘어난다.")]
        [SerializeField] private float texelsPerPixel = 1f;

        private Texture2D _texture;
        private Color32[] _pixels;
#if UNITY_EDITOR
        // OnValidate 가 여러 번 불려도 다음 틱에 한 번만 다시 그린다
        private bool _redrawQueued;
#endif

        public override Texture mainTexture => _texture != null ? _texture : s_WhiteTexture;

        /// <summary>화면에서 가장 클 때 이 오브젝트의 배율 (부모 기준). 그림 해상도를 정한다.</summary>
        protected float MaxScale { get; set; } = 1f;

        /// <summary>그림이 덮는 곳 (이 오브젝트 단위). 기본은 오브젝트 크기 — 그림자처럼 밖으로 나가는 겹이 있으면 넓힌다.</summary>
        protected virtual Rect DrawRect => rectTransform.rect;

        protected override void OnDestroy()
        {
            if (_texture != null)
            {
                if (Application.isPlaying)
                    Destroy(_texture);
                else
                    DestroyImmediate(_texture);
            }
            base.OnDestroy();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // 값을 읽는 중에는 그림을 만들 수 없다 — 다음 에디터 틱에 다시 그린다
            if (_redrawQueued)
                return;
            _redrawQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _redrawQueued = false;
                if (this != null && isActiveAndEnabled)
                    Redraw();
            };
        }
#endif

        /// <summary>화면에서 가장 클 때 이 오브젝트의 배율(부모 기준)을 정한다. 바뀌었으면 그 크기에 맞게 그림을 다시 그린다.</summary>
        public void SetMaxScale(float scale)
        {
            if (Mathf.Approximately(MaxScale, scale))
                return;
            MaxScale = scale;
            if (isActiveAndEnabled)
                Redraw();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            // 그림 한 장을 그리는 곳 그대로. 색은 그림에 있다 — 꼭짓점 색은 투명도만. 아직 안 그렸으면 아무것도
            vh.Clear();
            if (_texture == null)
                return;
            Rect rect = DrawRect;
            Color32 tint = new Color(1f, 1f, 1f, color.a);
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, new Vector4(0f, 0f));
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, new Vector4(0f, 1f));
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, new Vector4(1f, 1f));
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, new Vector4(1f, 0f));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        /// <summary>모양을 그림에 다시 그린다 — <see cref="BeginDraw"/> 로 텍셀 배열을 받아 채우고 <see cref="EndDraw"/>.</summary>
        protected abstract void Redraw();

        /// <summary>그리는 곳 크기 · 가장 큰 배율 · 실제 캔버스 배율로 그림 크기를 정하고 텍셀 배열을 돌려준다 (아래 줄부터, 왼쪽부터).</summary>
        protected Color32[] BeginDraw(out int width, out int height)
        {
            Rect rect = DrawRect;
            float texelsPerUnit = texelsPerPixel * MaxScale * PixelsPerParentUnit();
            width = Mathf.Clamp(Mathf.CeilToInt(rect.width * texelsPerUnit), 1, MaxTexels);
            height = Mathf.Clamp(Mathf.CeilToInt(rect.height * texelsPerUnit), 1, MaxTexels);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null)
                    DestroyImmediate(_texture);
                _texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = GetType().Name,
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                _pixels = new Color32[width * height];
            }
            return _pixels;
        }

        /// <summary>채운 텍셀을 그림에 올리고 다시 그리게 한다.</summary>
        protected void EndDraw()
        {
            _texture.SetPixels32(_pixels);
            _texture.Apply(true);
            SetMaterialDirty();
            SetVerticesDirty();
        }

        /// <summary>
        /// 텍셀 하나 — 그림자 → 테두리 → 면을 덮는 정도(0 ~ 1)만큼 겹친다 (투명도를 곱한 색으로 섞고 마지막에 나눈다).
        /// 아무것도 안 덮인 텍셀은 가까운 겹의 색에 투명도 0 — 그림을 늘려 그릴 때 가장자리가 검게 번지지 않게.
        /// </summary>
        protected static Color32 Layer(Color shadow, float shadowCover, Color stroke, float strokeCover, Color fill, float fillCover, bool shadowNearer)
        {
            float alpha = shadow.a * shadowCover;
            float red = shadow.r * alpha, green = shadow.g * alpha, blue = shadow.b * alpha;
            float top = stroke.a * strokeCover;
            red = stroke.r * top + red * (1f - top);
            green = stroke.g * top + green * (1f - top);
            blue = stroke.b * top + blue * (1f - top);
            alpha = top + alpha * (1f - top);
            top = fill.a * fillCover;
            red = fill.r * top + red * (1f - top);
            green = fill.g * top + green * (1f - top);
            blue = fill.b * top + blue * (1f - top);
            alpha = top + alpha * (1f - top);

            if (alpha > 0f)
                return new Color(red / alpha, green / alpha, blue / alpha, alpha);
            Color empty = shadowNearer ? shadow : stroke;
            empty.a = 0f;
            return empty;
        }

        /// <summary>모양까지 거리(안 −, 밖 +) → 덮는 정도. 가장자리 한 텍셀(edge)을 흐린다.</summary>
        protected static float Coverage(float distance, float edge) => Mathf.Clamp01(0.5f - distance / edge);

        // 부모 1 단위가 지금 화면 몇 픽셀인가 — 실제 캔버스 배율(CanvasScaler) × 캔버스에서 부모까지의 배율. 캔버스 밖(프리팹 편집 등)이면 1
        private float PixelsPerParentUnit()
        {
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Transform parent = rectTransform.parent;
            if (root == null || parent == null)
                return 1f;
            float rootScale = root.transform.lossyScale.x;
            return rootScale > 0f ? root.scaleFactor * parent.lossyScale.x / rootScale : 1f;
        }
    }
}
