using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Goat
{
    /// <summary>
    /// 「GOAT」 컷인의 왕 강아지 — 문 뒤의 빛을 등지고 걸어 나오는 검은 그림자 (시안 goat_cutin.js 의 dog()).
    /// 아래부터: 바람에 펄럭이는 망토(테두리 빛 + 검붉은 천) → 강아지 그림의 테두리 빛 → 강아지 그림 → 어둡게 덮는 겹 → 눈빛 → 왕관 · 보석 · 눈.
    /// 3D 가 아니라 UI 다 — 매 프레임 <see cref="Draw"/> 가 3D 장면에서 왕이 설 자리 · 키를 받아 그린다. 이 오브젝트의 자리가 발끝이다.
    /// 왕관 · 눈 · 어깨 자리는 시안 강아지 그림의 px 로 적혀 있다 (<see cref="ArtBox"/>) — 캐릭터 그림이 바뀌면 이 자리들도 다시 잡아야 한다.
    /// </summary>
    public class GoatKing : MonoBehaviour
    {
        // 시안 CH.bb · CH.ar: 그림 원본에서 강아지가 차지하는 칸(px) · 그 칸의 가로 ÷ 세로. 키 = 1 인 좌표로 바꿀 때 쓴다
        private static readonly Rect ArtBox = Rect.MinMaxRect(394.7f, 94.6f, 1622.2f, 1836.4f);
        private const float ArtAspect = 0.7053f;
        // 그림 px: 어깨 · 왕관 꼭짓점 · 보석 · 두 눈
        private static readonly Vector2 ShoulderLeft = new Vector2(720f, 1120f), ShoulderRight = new Vector2(1300f, 1120f);
        private static readonly Vector2[] CrownPoints =
        {
            new Vector2(850f, 332f), new Vector2(818f, 168f), new Vector2(932f, 256f), new Vector2(1010f, 126f), new Vector2(1088f, 256f), new Vector2(1202f, 168f), new Vector2(1170f, 332f),
        };
        private static readonly Vector2 Jewel = new Vector2(1010f, 268f);
        private static readonly Vector2[] Eyes = { new Vector2(745f, 832f), new Vector2(1280f, 832f) };
        // 키 = 1 기준: 망토 테두리 굵기 · 왕관 테두리 굵기 · 보석 · 눈 반지름 · 걸을 때 들썩임 · 기울임(라디안)
        private const float RimWidth = 0.018f, CrownLine = 0.0125f, Bob = 0.045f, Rock = 0.028f;
        private static readonly Vector2 JewelRadius = new Vector2(0.012f, 0.017f), EyeRadius = new Vector2(0.021f, 0.03f);
        // 망토 밑단: 톱니 수 · 톱니 깊이 · 펄럭임(빠르기 · 톱니마다 어긋남 · 크기)
        private const int HemPoints = 10;
        private const float HemNotch = 0.16f, WaveSpeed = 3.1f, WaveStep = 1.7f, WaveSize = 0.028f;
        private const int EllipseSegments = 16;

        [SerializeField] private CanvasGroup group;
        [Tooltip("망토 — 테두리 빛과 천.")]
        [SerializeField] private UIQuadBatch cape;
        [Tooltip("강아지 그림의 테두리 빛 — 그림 모양을 둘레로 어긋나게 깐다 (머티리얼 Goat Silhouette).")]
        [SerializeField] private UIQuadBatch rim;
        [SerializeField] private Image body;
        [Tooltip("그림을 어둡게 덮는 겹 (머티리얼 Goat Silhouette).")]
        [SerializeField] private UIQuadBatch shade;
        [Tooltip("두 눈의 붉은 빛 (왼쪽 · 오른쪽).")]
        [SerializeField] private Graphic[] eyeGlows = new Graphic[0];
        [Tooltip("왕관 · 보석 · 눈.")]
        [SerializeField] private UIQuadBatch crown;

        [Header("색 (시안 dog)")]
        [SerializeField] private Color capeColor = new Color32(0x19, 0x0a, 0x0d, 255);
        [Tooltip("등 뒤의 빛이 그림자 둘레에 남기는 테두리.")]
        [SerializeField] private Color rimColor = new Color(206f / 255f, 224f / 255f, 1f, 0.95f);
        [SerializeField] private Color crownColor = new Color32(0x8d, 0x7a, 0x4e, 255);
        [SerializeField] private Color lineColor = new Color32(0x13, 0x13, 0x13, 255);
        [SerializeField] private Color jewelColor = new Color32(0x7c, 0x14, 0x18, 255);
        [SerializeField] private Color eyeColor = new Color32(0xff, 0x9a, 0x4c, 255);
        [Tooltip("그림을 덮는 어둠 — 머리 · 가운데 · 발 (알파 = 덮는 정도).")]
        [SerializeField] private Color shadeTop = new Color(22f / 255f, 28f / 255f, 48f / 255f, 0.42f);
        [SerializeField] private Color shadeMiddle = new Color(14f / 255f, 16f / 255f, 30f / 255f, 0.6f);
        [SerializeField] private Color shadeBottom = new Color(6f / 255f, 6f / 255f, 12f / 255f, 0.86f);

        [Header("모양")]
        [Tooltip("망토가 펄럭이는 정도 (시안 wind).")]
        [SerializeField] private float wind = 1.3f;
        [Tooltip("테두리 빛의 겹 — 번짐 폭에 곱하는 거리 · 겹마다의 알파.")]
        [SerializeField] private Vector2[] rimRings = { new Vector2(0.2f, 0.16f), new Vector2(0.47f, 0.07f), new Vector2(0.78f, 0.035f) };
        [Tooltip("눈빛이 눈 밖으로 번지는 폭 (시안 px).")]
        [SerializeField] private float eyeGlowSpread = 22f;

        private readonly List<Vector2> _outline = new List<Vector2>();
        private readonly List<float> _levels = new List<float>();
        private readonly List<Vector3> _edges = new List<Vector3>();
        private Vector4 _uv;
        private float _aspect = ArtAspect;
        private float _height;

        private void Awake()
        {
            if (group == null || cape == null || rim == null || body == null || shade == null || crown == null || eyeGlows.Length != Eyes.Length)
                Debug.LogError($"[{nameof(GoatKing)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>강아지 그림을 정한다 — 그림 · 테두리 빛 · 어둠이 같은 그림을 쓴다.</summary>
        public void SetDog(Sprite sprite)
        {
            body.sprite = sprite;
            rim.SetTexture(sprite.texture);
            shade.SetTexture(sprite.texture);
            _uv = UIQuadBatch.SpriteUv(sprite);
            _aspect = sprite.rect.width / sprite.rect.height;
        }

        /// <summary>아무것도 그리지 않는다 (왕이 카메라 뒤에 있을 때).</summary>
        public void Hide()
        {
            group.alpha = 0f;
        }

        /// <summary>
        /// 이번 프레임의 왕을 그린다. feet = 발끝 (판 왼쪽 위 기준 px, y 아래 +) · height = 키 px · walkPhase · walkAmount = 걸음(들썩임 · 기울임) ·
        /// sway = 망토가 옆으로 쏠리는 정도 · glow = 테두리 빛이 번지는 폭 px · eyes = 눈을 뜬 정도 0 ~ 1.
        /// </summary>
        public void Draw(Vector2 feet, float height, float time, float walkPhase, float walkAmount, float sway, float glow, float eyes, float alpha)
        {
            _height = height;
            group.alpha = alpha;
            float step = Mathf.Sin(walkPhase);
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(feet.x, -feet.y);
            rect.localRotation = Quaternion.Euler(0f, 0f, -step * Rock * walkAmount * Mathf.Rad2Deg);
            // 발을 디딜 때마다 몸이 들썩인다 — 망토 밑단은 땅에 남는다
            float bob = Mathf.Abs(step) * Bob * walkAmount;

            DrawCape(time, sway, glow, bob);
            DrawBody(glow, bob);
            DrawCrown(eyes, bob);
        }

        // 망토: 어깨에서 밑단으로 퍼지고, 밑단은 톱니 모양으로 펄럭인다
        private void DrawCape(float time, float sway, float glow, float bob)
        {
            float Wave(float k) => Mathf.Sin(time * WaveSpeed + k * WaveStep) * WaveSize * wind;

            Vector2 left = Art(ShoulderLeft), right = Art(ShoulderRight);
            _outline.Clear();
            _outline.Add(Place(left.x, left.y - bob));
            _outline.Add(Place(right.x, right.y - bob));
            _outline.Add(Place(right.x + 0.12f + sway * 0.4f, right.y + 0.2f));
            _outline.Add(Place(0.5f + sway + Wave(0f), -0.06f));
            for (int i = 0; i < HemPoints; i++)
            {
                float k = i / (HemPoints - 1f);
                _outline.Add(Place(Mathf.Lerp(0.5f, -0.5f, k) + sway * (1f - k * 0.6f) + Wave(i), (i % 2 == 1 ? -HemNotch : 0f) + Wave(i + 3) * 0.8f));
            }
            _outline.Add(Place(-0.5f + sway * 0.3f + Wave(5f), -0.08f));
            _outline.Add(Place(left.x - 0.12f, left.y + 0.2f));

            float line = RimWidth * _height;
            cape.Clear();
            Stroke(cape, _outline, line + glow * 1.4f, Fade(rimColor, 0.1f), false);
            Stroke(cape, _outline, line + glow * 0.7f, Fade(rimColor, 0.22f), false);
            Stroke(cape, _outline, line, rimColor, true);
            Fill(cape, _outline, capeColor);
            cape.Apply();
        }

        // 강아지 그림: 테두리 빛(그림 모양을 둘레로 어긋나게) → 그림 → 위에서 아래로 짙어지는 어둠
        private void DrawBody(float glow, float bob)
        {
            // 시안 그림은 왼쪽 끝이 −ArtAspect / 2 — 캐릭터 그림도 왼쪽 끝을 거기에 맞춘다
            float left = -ArtAspect / 2f * _height, bottom = bob * _height, width = _aspect * _height;
            float right = left + width, top = bottom + _height, middle = bottom + _height / 2f;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchoredPosition = new Vector2(left, bottom);
            bodyRect.sizeDelta = new Vector2(width, _height);

            var uv = Rect.MinMaxRect(_uv.x, _uv.y, _uv.z, _uv.w);
            var center = new Vector2(left + width / 2f, middle);
            var size = new Vector2(width, _height);
            rim.Clear();
            foreach (Vector2 ring in rimRings)
            {
                Color32 color = Fade(rimColor, ring.y);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    rim.Quad(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (glow * ring.x), size, color, uv);
                }
            }
            rim.Apply();

            float uvMiddle = (uv.yMin + uv.yMax) / 2f;
            shade.Clear();
            shade.Quad(new Vector2(left, bottom), new Vector2(uv.xMin, uv.yMin), shadeBottom, new Vector2(left, middle), new Vector2(uv.xMin, uvMiddle), shadeMiddle,
                new Vector2(right, middle), new Vector2(uv.xMax, uvMiddle), shadeMiddle, new Vector2(right, bottom), new Vector2(uv.xMax, uv.yMin), shadeBottom);
            shade.Quad(new Vector2(left, middle), new Vector2(uv.xMin, uvMiddle), shadeMiddle, new Vector2(left, top), new Vector2(uv.xMin, uv.yMax), shadeTop,
                new Vector2(right, top), new Vector2(uv.xMax, uv.yMax), shadeTop, new Vector2(right, middle), new Vector2(uv.xMax, uvMiddle), shadeMiddle);
            shade.Apply();
        }

        // 왕관 · 보석 · 붉게 뜨는 눈 — 몸과 같이 들썩인다
        private void DrawCrown(float eyes, float bob)
        {
            _outline.Clear();
            foreach (Vector2 point in CrownPoints)
            {
                Vector2 p = Art(point);
                _outline.Add(Place(p.x, p.y - bob));
            }

            crown.Clear();
            Fill(crown, _outline, crownColor);
            Stroke(crown, _outline, CrownLine * _height, lineColor, true);
            Vector2 jewel = Art(Jewel);
            Ellipse(crown, Place(jewel.x, jewel.y - bob), JewelRadius * _height, jewelColor);
            for (int i = 0; i < Eyes.Length; i++)
            {
                Vector2 eye = Art(Eyes[i]);
                Vector2 center = Place(eye.x, eye.y - bob);
                var radius = new Vector2(EyeRadius.x, EyeRadius.y * eyes) * _height;
                eyeGlows[i].canvasRenderer.SetAlpha(eyes);
                eyeGlows[i].rectTransform.anchoredPosition = center;
                eyeGlows[i].rectTransform.sizeDelta = (radius + Vector2.one * eyeGlowSpread) * 2f;
                if (eyes > 0f)
                    Ellipse(crown, center, radius, eyeColor);
            }
            crown.Apply();
        }

        // 그림 px → 키 = 1 인 좌표 (가운데 x 0, 발끝 y 0, 위가 −)
        private static Vector2 Art(Vector2 pixel)
        {
            return new Vector2(((pixel.x - ArtBox.xMin) / ArtBox.width - 0.5f) * ArtAspect, (pixel.y - ArtBox.yMax) / ArtBox.height);
        }

        // 키 = 1 인 좌표 → 이 오브젝트 안의 px (위가 +)
        private Vector2 Place(float x, float y) => new Vector2(x, -y) * _height;

        private static Color32 Fade(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);

        // 닫힌 둘레선. joints 면 꺾이는 자리를 둥글게 메운다 (반투명한 선은 겹친 자리가 진해져서 메우지 않는다)
        private static void Stroke(UIQuadBatch batch, List<Vector2> points, float width, Color32 color, bool joints)
        {
            for (int i = 0; i < points.Count; i++)
            {
                batch.Line(points[i], points[(i + 1) % points.Count], width, color);
                if (joints)
                    Ellipse(batch, points[i], new Vector2(width, width) / 2f, color);
            }
        }

        private static void Ellipse(UIQuadBatch batch, Vector2 center, Vector2 radius, Color32 color)
        {
            Vector2 previous = center + new Vector2(radius.x, 0f);
            for (int i = 1; i <= EllipseSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / EllipseSegments;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                batch.Triangle(center, color, previous, color, next, color);
                previous = next;
            }
        }

        // 닫힌 꼴을 채운다 — 오목해도, 선이 서로 엇갈려도 된다 (망토 밑단은 펄럭이며 엇갈린다. 시안 canvas fill 과 같은 규칙).
        // 꼭짓점 · 엇갈린 점의 높이마다 가로로 자르면, 띠 안에서는 선이 엇갈리지 않는다 — 띠마다 꼴 안쪽 구간을 사다리꼴로 그린다
        private void Fill(UIQuadBatch batch, List<Vector2> points, Color32 color)
        {
            int count = points.Count;
            _levels.Clear();
            for (int i = 0; i < count; i++)
            {
                _levels.Add(points[i].y);
                for (int j = i + 2; j < count; j++)
                {
                    // 이웃한 변끼리는 꼭짓점에서만 만난다
                    if (i == 0 && j == count - 1)
                        continue;
                    if (Crossing(points[i], points[i + 1], points[j], points[(j + 1) % count], out float y))
                        _levels.Add(y);
                }
            }
            _levels.Sort();

            for (int k = 0; k + 1 < _levels.Count; k++)
            {
                float low = _levels[k], high = _levels[k + 1], middle = (low + high) / 2f;
                if (high - low < 0.01f)
                    continue;

                // 이 띠를 지나는 변: (아래 끝 x, 위 끝 x, 올라가면 +1 · 내려가면 −1)
                _edges.Clear();
                for (int i = 0; i < count; i++)
                {
                    Vector2 a = points[i], b = points[(i + 1) % count];
                    if ((a.y < middle) == (b.y < middle))
                        continue;
                    float slope = (b.x - a.x) / (b.y - a.y);
                    _edges.Add(new Vector3(a.x + (low - a.y) * slope, a.x + (high - a.y) * slope, b.y > a.y ? 1f : -1f));
                }
                _edges.Sort((p, q) => (p.x + p.y).CompareTo(q.x + q.y));

                // 왼쪽부터 변을 넘을 때마다 감긴 수를 더한다 — 0 이 아닌 구간이 꼴 안쪽
                float winding = 0f;
                Vector3 open = default;
                foreach (Vector3 edge in _edges)
                {
                    if (winding == 0f)
                        open = edge;
                    winding += edge.z;
                    if (winding == 0f)
                        batch.Quad(new Vector2(open.x, low), color, new Vector2(open.y, high), color, new Vector2(edge.y, high), color, new Vector2(edge.x, low), color);
                }
            }
        }

        // 두 선분 a–b · c–d 가 (끝점이 아닌 곳에서) 엇갈리면 그 점의 높이
        private static bool Crossing(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float y)
        {
            y = 0f;
            Vector2 first = b - a, second = d - c;
            float denominator = Cross(first, second);
            if (Mathf.Approximately(denominator, 0f))
                return false;

            float alongFirst = Cross(c - a, second) / denominator, alongSecond = Cross(c - a, first) / denominator;
            if (alongFirst <= 0f || alongFirst >= 1f || alongSecond <= 0f || alongSecond >= 1f)
                return false;

            y = a.y + first.y * alongFirst;
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
