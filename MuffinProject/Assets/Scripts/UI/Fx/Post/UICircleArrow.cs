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
        // 화살대 · 꺾쇠 두 팔 — 꼭짓점 넷인 볼록 다각형 셋
        private const int PolygonCount = 3;
        private const int PolygonSides = 4;
        // 화살촉을 감싸는 원 밖으로 흐린 가장자리까지 넉넉히 (이 오브젝트 단위)
        private const float HeadMargin = 2f;

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
        [Tooltip("글자 바깥 테두리 색.")]
        [SerializeField] private Color strokeColor = Color.white;
        [Tooltip("그림자 위치 (시안 drop-shadow 0 6px — 아래로 6).")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -6f);
        [Tooltip("아래 그림자 색.")]
        [SerializeField] private Color shadowColor = new Color32(0x2b, 0x28, 0x56, 0xff);

        [Header("그림")]
        [Tooltip("그림 해상도 — 이 오브젝트 1 단위에 텍셀 몇 개. 화면에서 가장 클 때 화면 1 픽셀에 1 텍셀 이상이면 된다. 그림 크기 = 이 오브젝트 크기 × 이 값.")]
        [SerializeField] private float resolution = 1.5f;

        // 화살대 · 꺾쇠 두 팔의 변 — (바깥 법선 x, y, 법선 방향 위치). 점까지 거리 = 법선 · 점 − 위치
        private readonly Vector3[] _planes = new Vector3[PolygonCount * PolygonSides];
        private Texture2D _texture;
        private Color32[] _pixels;
        private Vector2 _startOut;
        private Vector2 _endOut;
        private Vector2 _gapMiddle;
        private Vector2 _headCenter;
        private float _headRadius;

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

            BuildShape();
            float texelX = rect.width / width;
            float texelY = rect.height / height;
            float edge = Mathf.Max(texelX, texelY);
            Color fill = color;
            for (int y = 0; y < height; y++)
            {
                float pointY = rect.yMin + (y + 0.5f) * texelY;
                for (int x = 0; x < width; x++)
                {
                    float pointX = rect.xMin + (x + 0.5f) * texelX;
                    float body = Distance(pointX, pointY);
                    float shadow = Distance(pointX - shadowOffset.x, pointY - shadowOffset.y) - strokeWidth;
                    float stroke = body - strokeWidth;

                    // 투명도를 곱해 둔 색으로 겹친다: 그림자 → 테두리 → 면
                    float alpha = shadowColor.a * Coverage(shadow, edge);
                    float red = shadowColor.r * alpha, green = shadowColor.g * alpha, blue = shadowColor.b * alpha;
                    float top = strokeColor.a * Coverage(stroke, edge);
                    red = strokeColor.r * top + red * (1f - top);
                    green = strokeColor.g * top + green * (1f - top);
                    blue = strokeColor.b * top + blue * (1f - top);
                    alpha = top + alpha * (1f - top);
                    top = Coverage(body, edge);
                    red = fill.r * top + red * (1f - top);
                    green = fill.g * top + green * (1f - top);
                    blue = fill.b * top + blue * (1f - top);
                    alpha = top + alpha * (1f - top);

                    Color result;
                    if (alpha > 0f)
                    {
                        result = new Color(red / alpha, green / alpha, blue / alpha, alpha);
                    }
                    else
                    {
                        // 빈 텍셀은 가까운 겹의 색으로 — 그림을 늘려 그릴 때 가장자리가 검게 번지지 않게
                        result = shadow < stroke ? shadowColor : strokeColor;
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

        // 고리 끝 방향과, 화살대 · 꺾쇠 두 팔(볼록 사각형)의 변을 미리 구한다
        private void BuildShape()
        {
            float end = startAngle + sweep;
            _startOut = Polar(startAngle, 1f);
            _endOut = Polar(end, 1f);
            _gapMiddle = Polar(end + (360f - sweep) / 2f, 1f);

            Vector2 tip = _endOut * radius;
            Vector2 forward = Polar(end + 90f, 1f);
            Vector2 apex = tip + forward * headReach;
            Vector2 armA = Rotate(forward, -headSpread);
            Vector2 armB = Rotate(forward, headSpread);
            // 화살대는 고리 끝에서 꺾쇠 안쪽 모서리까지 곧게
            Vector2 shaftEnd = apex + (armA + armB) * (headWidth / 2f);
            Vector2 side = new Vector2(forward.y, -forward.x) * (thickness / 2f);

            Vector2[][] polygons =
            {
                new[] { tip + side, shaftEnd + side, shaftEnd - side, tip - side },
                // 바깥 모서리가 꼭짓점, 끝은 45도로 자른다 (Segoe UI Symbol ↺)
                new[] { apex, apex + armA * headArm, apex + armA * (headArm - headWidth) + armB * headWidth, apex + armB * headWidth },
                new[] { apex, apex + armB * headArm, apex + armB * (headArm - headWidth) + armA * headWidth, apex + armA * headWidth }
            };

            // 텍셀마다 화살촉까지 재지 않게 — 화살촉을 감싸는 원
            _headCenter = apex;
            _headRadius = 0f;
            for (int i = 0; i < PolygonCount; i++)
            {
                Vector2[] polygon = Ccw(polygons[i]);
                for (int k = 0; k < PolygonSides; k++)
                {
                    Vector2 from = polygon[k];
                    Vector2 along = (polygon[(k + 1) % PolygonSides] - from).normalized;
                    var outward = new Vector2(along.y, -along.x);
                    _planes[i * PolygonSides + k] = new Vector3(outward.x, outward.y, Vector2.Dot(outward, from));
                    _headRadius = Mathf.Max(_headRadius, (from - apex).magnitude);
                }
            }
        }

        // 모양까지의 거리 (안 −, 밖 +). 조각마다 거리의 최솟값 = 합친 모양. 볼록 조각은 변 거리의 최댓값이라 넓히면 모서리가 뾰족하다 (CSS 테두리와 같게)
        private float Distance(float x, float y)
        {
            float distance = RingDistance(x, y);
            // 화살촉에서 테두리 + 흐린 가장자리보다 멀면 덮는 정도가 0 이라 재지 않아도 결과가 같다
            float reach = _headRadius + strokeWidth + HeadMargin;
            float dx = x - _headCenter.x;
            float dy = y - _headCenter.y;
            if (dx * dx + dy * dy > reach * reach)
                return distance;

            for (int i = 0; i < PolygonCount; i++)
            {
                float inside = float.MinValue;
                for (int k = 0; k < PolygonSides; k++)
                {
                    Vector3 plane = _planes[i * PolygonSides + k];
                    inside = Mathf.Max(inside, plane.x * x + plane.y * y - plane.z);
                }
                distance = Mathf.Min(distance, inside);
            }
            return distance;
        }

        // 고리 띠 — 각도 안이면 반지름 방향 거리, 밖(끝 → 시작 사이)이면 가까운 끝을 네모나게 자른 거리
        private float RingDistance(float x, float y)
        {
            float half = thickness / 2f;
            bool inGap = sweep >= 180f
                ? Cross(_endOut.x, _endOut.y, x, y) > 0f && Cross(x, y, _startOut.x, _startOut.y) > 0f
                : !(Cross(_startOut.x, _startOut.y, x, y) >= 0f && Cross(x, y, _endOut.x, _endOut.y) >= 0f);
            if (!inGap)
                return Mathf.Abs(Mathf.Sqrt(x * x + y * y) - radius) - half;

            // 빈 각도의 가운데보다 반시계 방향이면 시작 끝 쪽
            bool nearStart = Cross(_gapMiddle.x, _gapMiddle.y, x, y) > 0f;
            Vector2 outward = nearStart ? _startOut : _endOut;
            // 끝 선 너머로 간 거리 — 시작 끝은 시계 방향, 고리 끝은 반시계 방향이 바깥
            float beyond = Cross(outward.x, outward.y, x, y) * (nearStart ? -1f : 1f);
            float across = Mathf.Abs(outward.x * x + outward.y * y - radius) - half;
            return Mathf.Max(beyond, across);
        }

        private static float Cross(float ax, float ay, float bx, float by) => ax * by - ay * bx;

        // 거리 → 덮는 정도 (가장자리 한 텍셀을 흐린다)
        private static float Coverage(float distance, float edge) => Mathf.Clamp01(0.5f - distance / edge);

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
