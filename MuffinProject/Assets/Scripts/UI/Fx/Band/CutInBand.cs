using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 띠 컷인의 띠 한 장 — 기운 평행사변형을 한 메시로 그린다 (시안 .ci-band · .ci-lines).
    /// 면은 위아래 가장자리 색 → 가운데 색 → 가장자리 색, 바깥에 검은 테 · 빛 테 · 번지는 빛, 안에 흐르는 세로 줄무늬.
    /// CSS skewY 처럼 위아래 변만 기울고 옆 변 · 줄무늬는 세로 그대로다. 밀기(<see cref="Slide"/>)는 기울이기 전에 더해 띠가 제 결을 따라 들어온다.
    /// 길이는 모두 화면 단위 (시안 px × 1.5).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다
    public class CutInBand : MaskableGraphic
    {
        // 번지는 빛을 나누는 띠 수 — 가우스 번짐을 이만큼의 곧은 띠로 근사한다
        private const int BlurSteps = 6;

        [Tooltip("기울기 (도) — 오른쪽이 올라간다 (시안 skewY(-8deg)).")]
        [SerializeField] private float skew = 8f;

        [Header("면 (시안 linear-gradient(180deg, c2, c1 22%, c1 78%, c2))")]
        [Tooltip("위아래 가장자리 색 (시안 c2).")]
        [SerializeField] private Color edgeColor = new Color32(0x5a, 0x00, 0x10, 0xff);
        [Tooltip("가운데 색 (시안 c1).")]
        [SerializeField] private Color middleColor = new Color32(0xe0, 0x11, 0x2b, 0xff);
        [Tooltip("가장자리 색이 가운데 색이 되는 곳 — 높이 대비 (위아래 같이).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float middleFrom = 0.22f;

        [Header("테 · 빛 (시안 box-shadow: 0 0 0 4px #000, 0 0 0 7px glow, 0 0 60px glow)")]
        [Tooltip("띠 바로 바깥 테 색 (시안 #000).")]
        [SerializeField] private Color inkColor = Color.black;
        [Tooltip("검은 테 두께.")]
        [SerializeField] private float inkWidth = 6f;
        [Tooltip("빛 색 (시안 glow).")]
        [SerializeField] private Color glowColor = new Color32(0xff, 0x2a, 0x3a, 0xff);
        [Tooltip("검은 테 바깥 빛 테 두께.")]
        [SerializeField] private float glowLineWidth = 4.5f;
        [Tooltip("번지는 빛 길이 (시안 blur 값 — 가우스 σ 는 그 절반).")]
        [SerializeField] private float glowBlur = 90f;

        [Header("줄무늬 (시안 .ci-lines repeating-linear-gradient)")]
        [Tooltip("무늬 한 주기 (시안 156px).")]
        [SerializeField] private float stripePeriod = 234f;
        [Tooltip("무늬가 시작하는 곳 — 띠 왼쪽 끝에서 띠 너비 대비 (시안 inset -50%).")]
        [SerializeField] private float stripeOrigin = -0.5f;
        [Tooltip("밝은 줄 — 주기 안에서 시작 · 너비 (시안 60px · 4px).")]
        [SerializeField] private Vector2 lightStripe = new Vector2(90f, 6f);
        [Tooltip("밝은 줄 색 (시안 rgba(255,255,255,.22)).")]
        [SerializeField] private Color lightStripeColor = new Color(1f, 1f, 1f, 0.22f);
        [Tooltip("어두운 줄 — 주기 안에서 시작 · 너비 (시안 150px · 6px).")]
        [SerializeField] private Vector2 darkStripe = new Vector2(225f, 9f);
        [Tooltip("어두운 줄 색 (시안 rgba(0,0,0,.25)).")]
        [SerializeField] private Color darkStripeColor = new Color(0f, 0f, 0f, 0.25f);

        // 고리 꼭짓점 버퍼 — 줄무늬가 흐르는 동안 매 프레임 메시를 다시 만들어 배열을 새로 만들지 않는다
        private readonly Vector2[] _innerCorners = new Vector2[4];
        private readonly Vector2[] _outerCorners = new Vector2[4];

        private float _slide;
        private float _stripeShift;

        /// <summary>기울이기 전 가로로 민 거리 (화면 단위, 왼쪽 −). 시안 translateX — 띠가 제 기울기를 따라 움직인다.</summary>
        public float Slide
        {
            get => _slide;
            set
            {
                _slide = value;
                SetVerticesDirty();
            }
        }

        /// <summary>줄무늬를 왼쪽으로 민 거리 (화면 단위). 한 주기마다 같은 모양.</summary>
        public float StripeShift
        {
            get => _stripeShift;
            set
            {
                _stripeShift = value;
                SetVerticesDirty();
            }
        }

        /// <summary>무늬 한 주기 (화면 단위).</summary>
        public float StripePeriod => stripePeriod;

        /// <summary>카드 색을 넣는다 — 가운데(c1) · 가장자리(c2) · 빛(glow).</summary>
        public void SetColors(Color middle, Color edge, Color glow)
        {
            middleColor = middle;
            edgeColor = edge;
            glowColor = glow;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            // 번지는 빛 → 빛 테 → 검은 테 → 면 → 줄무늬 (뒤에서 앞으로)
            float lines = inkWidth + glowLineWidth;
            float sigma = Mathf.Max(glowBlur / 2f, 0.01f);
            for (int i = 0; i < BlurSteps; i++)
            {
                float from = Mathf.Lerp(lines, glowBlur, (float)i / BlurSteps);
                float to = Mathf.Lerp(lines, glowBlur, (float)(i + 1) / BlurSteps);
                float last = i == BlurSteps - 1 ? 0f : 1f;
                Ring(vh, rect, from, to, WithAlpha(glowColor, BlurAlpha(from, sigma)), WithAlpha(glowColor, BlurAlpha(to, sigma) * last));
            }
            Ring(vh, rect, inkWidth, lines, glowColor, glowColor);
            Ring(vh, rect, 0f, inkWidth, inkColor, inkColor);

            float edge = rect.height * middleFrom;
            Quad(vh, rect.xMin, rect.xMax, rect.yMin, rect.yMin + edge, edgeColor, middleColor);
            Quad(vh, rect.xMin, rect.xMax, rect.yMin + edge, rect.yMax - edge, middleColor, middleColor);
            Quad(vh, rect.xMin, rect.xMax, rect.yMax - edge, rect.yMax, middleColor, edgeColor);

            Stripes(vh, rect, lightStripe, lightStripeColor);
            Stripes(vh, rect, darkStripe, darkStripeColor);
        }

        // 띠 안에서 주기마다 세로 줄 하나 — 띠 밖은 잘라 낸다 (시안 overflow:hidden)
        private void Stripes(VertexHelper vh, Rect rect, Vector2 stripe, Color stripeColor)
        {
            if (stripePeriod <= 0f || stripe.y <= 0f)
                return;

            float origin = rect.xMin + rect.width * stripeOrigin - _stripeShift;
            float first = Mathf.Floor((rect.xMin - origin - stripe.x - stripe.y) / stripePeriod);
            for (float k = first; origin + k * stripePeriod + stripe.x < rect.xMax; k++)
            {
                float x0 = Mathf.Max(rect.xMin, origin + k * stripePeriod + stripe.x);
                float x1 = Mathf.Min(rect.xMax, origin + k * stripePeriod + stripe.x + stripe.y);
                if (x1 > x0)
                    Quad(vh, x0, x1, rect.yMin, rect.yMax, stripeColor, stripeColor);
            }
        }

        // 띠 둘레 from ~ to 거리의 고리 (네 변 사다리꼴). 안쪽 색 → 바깥 색
        private void Ring(VertexHelper vh, Rect rect, float from, float to, Color inner, Color outer)
        {
            int start = vh.currentVertCount;
            Corners(rect, from, _innerCorners);
            Corners(rect, to, _outerCorners);
            for (int i = 0; i < 4; i++)
            {
                AddVert(vh, rect, _innerCorners[i], inner);
                AddVert(vh, rect, _outerCorners[i], outer);
            }
            for (int i = 0; i < 4; i++)
            {
                int a = start + i * 2;
                int b = start + (i + 1) % 4 * 2;
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }

        // 가로 띠 하나 — 아래 색 → 위 색
        private void Quad(VertexHelper vh, float x0, float x1, float y0, float y1, Color bottom, Color top)
        {
            int start = vh.currentVertCount;
            AddVert(vh, rectTransform.rect, new Vector2(x0, y0), bottom);
            AddVert(vh, rectTransform.rect, new Vector2(x0, y1), top);
            AddVert(vh, rectTransform.rect, new Vector2(x1, y1), top);
            AddVert(vh, rectTransform.rect, new Vector2(x1, y0), bottom);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        // 기울이기 전 좌표 → 밀고(translateX) 가운데를 기준으로 기울인다(skewY)
        private void AddVert(VertexHelper vh, Rect rect, Vector2 local, Color vertexColor)
        {
            float x = local.x + _slide;
            float y = local.y + Mathf.Tan(skew * Mathf.Deg2Rad) * (x - rect.center.x);
            vh.AddVert(new Vector3(x, y), vertexColor * color, Vector4.zero);
        }

        private static void Corners(Rect rect, float grow, Vector2[] corners)
        {
            corners[0] = new Vector2(rect.xMin - grow, rect.yMin - grow);
            corners[1] = new Vector2(rect.xMin - grow, rect.yMax + grow);
            corners[2] = new Vector2(rect.xMax + grow, rect.yMax + grow);
            corners[3] = new Vector2(rect.xMax + grow, rect.yMin - grow);
        }

        // 가장자리에서 d 만큼 떨어진 곳의 CSS 번짐 그림자 투명도 — ½·erfc(d / (σ√2))
        private static float BlurAlpha(float distance, float sigma)
        {
            return 0.5f * Erfc(distance / (sigma * Mathf.Sqrt(2f)));
        }

        // Abramowitz · Stegun 7.1.26 (x ≥ 0)
        private static float Erfc(float x)
        {
            float t = 1f / (1f + 0.3275911f * x);
            float poly = t * (0.254829592f + t * (-0.284496736f + t * (1.421413741f + t * (-1.453152027f + t * 1.061405429f))));
            return poly * Mathf.Exp(-x * x);
        }

        private static Color WithAlpha(Color c, float alpha)
        {
            c.a *= alpha;
            return c;
        }
    }
}
