using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Post
{
    /// <summary>
    /// 둥근 화살표 (UI 도형) — 시안 「뒤로 돌아」 의 ↺ 글자 (Windows 에서 보이는 Segoe UI Symbol 모양: 열린 고리 + 꺾쇠 화살촉).
    /// 프로젝트 글꼴에 이 글자가 없어서 코드로 그린다. 글자 테두리(-webkit-text-stroke)와 아래 그림자(drop-shadow)도 같이 — 면 색 = Graphic.color.
    /// 켜질 때 모양(고리 · 화살대 · 꺾쇠 두 팔의 거리 함수)을 이 오브젝트 크기의 그림 한 장에 그린다. 메시 여러 겹으로 그리면 CanvasGroup 으로 흐리게 할 때
    /// 그림자 · 테두리가 면 너머로 비치는데, 한 장이면 CSS opacity 처럼 통째로 흐려진다.
    /// 길이 값은 이 오브젝트 단위 (시안 px 로 넣고 오브젝트 배율로 키운다). 고리 가운데 = 오브젝트 기준점. 각도는 오른쪽이 0, 반시계 방향 +.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다 — 없으면 아무것도 그려지지 않는다
    public class UICircleArrow : MaskableGraphic
    {
        private const int MaxTexels = 1024;

        [Header("고리")]
        [Tooltip("고리 가운데 반지름 (두께의 가운데).")]
        [SerializeField] private float radius = 33.55f;
        [Tooltip("고리 두께.")]
        [SerializeField] private float thickness = 8.3f;
        [Tooltip("고리가 시작하는 각도 (도) — 꼬리 끝.")]
        [SerializeField] private float startAngle = 130f;
        [Tooltip("시작에서 반시계 방향으로 도는 각도 (도) — 끝에 화살촉.")]
        [SerializeField] private float sweep = 274.3f;

        [Header("화살촉 (꺾쇠)")]
        [Tooltip("고리 끝에서 화살촉 꼭짓점까지 (진행 방향으로 곧게).")]
        [SerializeField] private float headReach = 14.2f;
        [Tooltip("꼭짓점에서 뻗는 두 팔의 길이.")]
        [SerializeField] private float headArm = 29.3f;
        [Tooltip("두 팔의 두께.")]
        [SerializeField] private float headWidth = 6.2f;
        [Tooltip("진행 방향과 두 팔 사이 각도 (도) — 135 면 두 팔이 직각.")]
        [SerializeField] private float headSpread = 135f;

        [Header("테두리 · 그림자")]
        [Tooltip("글자 바깥 테두리 두께 (시안 -webkit-text-stroke 7px 중 면 밖으로 보이는 절반).")]
        [SerializeField] private float strokeWidth = 3.5f;
        [SerializeField] private Color strokeColor = Color.white;
        [Tooltip("그림자 위치 (시안 drop-shadow 0 6px — 아래로 6).")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -6f);
        [SerializeField] private Color shadowColor = new Color32(0x2b, 0x28, 0x56, 0xff);

        [Header("그림")]
        [Tooltip("그림 해상도 — 이 오브젝트 1 단위에 텍셀 몇 개. 화면에서 가장 클 때 화면 1 픽셀에 1 텍셀 이상이면 된다. 그림 크기 = 이 오브젝트 크기 × 이 값.")]
        [SerializeField] private float resolution = 2f;

        private Texture2D _texture;
        private Color32[] _pixels;
        private Vector2[][] _polygons;

        public override Texture mainTexture => _texture != null ? _texture : s_WhiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            Redraw();
        }

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
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                    Redraw();
            };
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            // 그림 한 장을 이 오브젝트 크기 그대로. 색은 그림에 있다 — 꼭짓점 색은 투명도만
            vh.Clear();
            Rect rect = rectTransform.rect;
            Color32 tint = new Color(1f, 1f, 1f, color.a);
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, new Vector4(0f, 0f));
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, new Vector4(0f, 1f));
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, new Vector4(1f, 1f));
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, new Vector4(1f, 0f));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        // 모양을 이 오브젝트 크기의 그림에 다시 그린다 — 텍셀마다 그림자 → 테두리 → 면을 겹친다
        private void Redraw()
        {
            Rect rect = rectTransform.rect;
            int width = Mathf.Clamp(Mathf.CeilToInt(rect.width * resolution), 1, MaxTexels);
            int height = Mathf.Clamp(Mathf.CeilToInt(rect.height * resolution), 1, MaxTexels);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null)
                    DestroyImmediate(_texture);
                _texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = nameof(UICircleArrow),
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                _pixels = new Color32[width * height];
            }

            BuildPolygons();
            Vector2 texel = new Vector2(rect.width / width, rect.height / height);
            float edge = Mathf.Max(texel.x, texel.y);
            Color fill = color;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(rect.xMin + (x + 0.5f) * texel.x, rect.yMin + (y + 0.5f) * texel.y);
                    float body = Distance(point);
                    float shadow = Distance(point - shadowOffset) - strokeWidth;

                    Color result = Over(new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * Coverage(shadow, edge)), Color.clear);
                    result = Over(new Color(strokeColor.r, strokeColor.g, strokeColor.b, strokeColor.a * Coverage(body - strokeWidth, edge)), result);
                    result = Over(new Color(fill.r, fill.g, fill.b, Coverage(body, edge)), result);
                    // 빈 텍셀은 가까운 겹의 색으로 — 그림을 늘려 그릴 때 가장자리가 검게 번지지 않게
                    if (result.a <= 0f)
                    {
                        result = shadow < body - strokeWidth ? shadowColor : strokeColor;
                        result.a = 0f;
                    }
                    _pixels[y * width + x] = result;
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(true);
            SetMaterialDirty();
            SetVerticesDirty();
        }

        // 화살대 · 꺾쇠 두 팔 — 볼록 다각형 (반시계 방향 꼭짓점)
        private void BuildPolygons()
        {
            float end = startAngle + sweep;
            Vector2 tip = Polar(end, radius);
            Vector2 forward = Polar(end + 90f, 1f);
            Vector2 apex = tip + forward * headReach;
            Vector2 armA = Rotate(forward, -headSpread);
            Vector2 armB = Rotate(forward, headSpread);
            // 화살대는 고리 끝에서 꺾쇠 안쪽 모서리까지 곧게
            Vector2 shaftEnd = apex + (armA + armB) * (headWidth / 2f);
            Vector2 side = new Vector2(forward.y, -forward.x) * (thickness / 2f);

            _polygons = new[]
            {
                Ccw(new[] { tip + side, shaftEnd + side, shaftEnd - side, tip - side }),
                // 바깥 모서리가 꼭짓점, 끝은 45도로 자른다 (Segoe UI Symbol ↺)
                Ccw(new[] { apex, apex + armA * headArm, apex + armA * (headArm - headWidth) + armB * headWidth, apex + armB * headWidth }),
                Ccw(new[] { apex, apex + armB * headArm, apex + armB * (headArm - headWidth) + armA * headWidth, apex + armA * headWidth })
            };
        }

        // 모양까지의 거리 (안 −, 밖 +). 조각마다 거리의 최솟값 = 합친 모양. 볼록 조각은 변 거리의 최댓값이라 넓히면 모서리가 뾰족하다 (CSS 테두리와 같게)
        private float Distance(Vector2 point)
        {
            float distance = RingDistance(point);
            foreach (Vector2[] polygon in _polygons)
                distance = Mathf.Min(distance, PolygonDistance(polygon, point));
            return distance;
        }

        // 고리 띠 — 각도 안이면 반지름 방향 거리, 밖이면 가까운 끝을 네모나게 자른 거리
        private float RingDistance(Vector2 point)
        {
            float half = thickness / 2f;
            float relative = Mathf.Repeat(Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg - startAngle, 360f);
            if (relative <= sweep)
                return Mathf.Abs(point.magnitude - radius) - half;

            bool nearStart = relative > sweep + (360f - sweep) / 2f;
            float edgeAngle = nearStart ? startAngle : startAngle + sweep;
            Vector2 outward = Polar(edgeAngle, 1f);
            Vector2 forward = Polar(edgeAngle + 90f, 1f);
            float beyond = Vector2.Dot(point, nearStart ? -forward : forward);
            float across = Mathf.Abs(Vector2.Dot(point, outward) - radius) - half;
            return Mathf.Max(beyond, across);
        }

        private static float PolygonDistance(Vector2[] polygon, Vector2 point)
        {
            float distance = float.MinValue;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 from = polygon[i];
                Vector2 edge = (polygon[(i + 1) % polygon.Length] - from).normalized;
                var outward = new Vector2(edge.y, -edge.x);
                distance = Mathf.Max(distance, Vector2.Dot(outward, point - from));
            }
            return distance;
        }

        // 거리 → 덮는 정도 (가장자리 한 텍셀을 흐린다)
        private static float Coverage(float distance, float edge) => Mathf.Clamp01(0.5f - distance / edge);

        // 투명도가 따로인 색끼리 위에 겹치기
        private static Color Over(Color top, Color bottom)
        {
            float alpha = top.a + bottom.a * (1f - top.a);
            if (alpha <= 0f)
                return Color.clear;
            Color mixed = (top * top.a + bottom * (bottom.a * (1f - top.a))) / alpha;
            mixed.a = alpha;
            return mixed;
        }

        private static Vector2[] Ccw(Vector2[] points)
        {
            float area = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f)
                System.Array.Reverse(points);
            return points;
        }

        private static Vector2 Polar(float degrees, float length)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * length;
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }
    }
}
