using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 화면 앞을 내리는 눈송이 (시안 DOGPILE.snowfall — 앞 캔버스의 흰 점). 재생마다 점 자리를 무작위로 정하고, 시각 · 빠르기 · 카메라 방위로 매 프레임 옮긴다.
    /// 칸(시안 가로세로비 0.75 ~ 1.9 로 묶은 화면 가운데 칸) 안에 그린다. 크기는 칸 높이 800 기준.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GaepanSnowfall : MaskableGraphic
    {
        private const int Segments = 10;

        [Tooltip("눈송이 수.")]
        [SerializeField] private int count = 110;
        [Tooltip("떨어지는 빠르기 범위 (칸 높이 / 초).")]
        [SerializeField] private Vector2 fallSpeed = new Vector2(0.15f, 0.45f);
        [Tooltip("옆으로 흐르는 빠르기 범위 (칸 너비 / 초).")]
        [SerializeField] private Vector2 drift = new Vector2(-0.06f, 0.06f);
        [Tooltip("반지름 범위 (칸 높이 800 기준).")]
        [SerializeField] private Vector2 radius = new Vector2(1.2f, 3.8f);
        [Tooltip("진하기 범위.")]
        [SerializeField] private Vector2 opacity = new Vector2(0.5f, 1f);
        [Tooltip("시안이 화면을 맞추는 가로세로비 범위.")]
        [SerializeField] private Vector2 aspectRange = new Vector2(0.75f, 1.9f);
        [Tooltip("카메라가 돌면 눈송이가 이만큼 옆으로 (칸 너비 / 라디안).")]
        [SerializeField] private float yawShift = 0.15f;

        private Flake[] _flakes = new Flake[0];
        private float _time;
        private float _alpha;
        private float _speed = 1f;
        private float _yaw;

        // 시안 mk().snow 의 점 하나 (x · y = 처음 자리 비율, fall · drift = 빠르기, radius · opacity)
        private struct Flake
        {
            public float X, Y, Fall, Drift, Radius, Opacity;
        }

        /// <summary>점 자리를 무작위로 다시 정하고 감춘다 (재생마다).</summary>
        public void Scatter()
        {
            _flakes = new Flake[count];
            for (int i = 0; i < count; i++)
            {
                _flakes[i] = new Flake
                {
                    X = Random.value,
                    Y = Random.value,
                    Fall = Random.Range(fallSpeed.x, fallSpeed.y),
                    Drift = Random.Range(drift.x, drift.y),
                    Radius = Random.Range(radius.x, radius.y),
                    Opacity = Random.Range(opacity.x, opacity.y),
                };
            }
            Draw(0f, 0f, 1f, 0f);
        }

        /// <summary>t 초(장면 처음부터) 모습을 그린다. alpha = 전체 진하기 (0 이면 감춤), speed = 빠르기 배수, yaw = 카메라 방위 (라디안).</summary>
        public void Draw(float t, float alpha, float speed, float yaw)
        {
            _time = t;
            _alpha = alpha;
            _speed = speed;
            _yaw = yaw;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (_alpha <= 0f || rect.width <= 0f || rect.height <= 0f)
                return;
            float aspect = Mathf.Clamp(rect.width / rect.height, aspectRange.x, aspectRange.y);
            float boxWidth = Mathf.Min(rect.width, rect.height * aspect);
            float boxHeight = boxWidth / aspect;
            float left = rect.center.x - boxWidth / 2f, top = rect.center.y + boxHeight / 2f;
            foreach (Flake f in _flakes)
            {
                float y = GaepanWorld.Fraction(f.Y + _time * f.Fall * _speed) * boxHeight * 1.1f - boxHeight * 0.05f;
                float x = GaepanWorld.Fraction(f.X + _time * f.Drift * _speed + _yaw * yawShift) * boxWidth;
                var c = new Color(color.r, color.g, color.b, color.a * _alpha * f.Opacity);
                AddDot(vh, new Vector2(left + x, top - y), f.Radius * boxHeight / 800f, c);
            }
        }

        private static void AddDot(VertexHelper vh, Vector2 center, float r, Color32 c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, c, Vector4.zero);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, c, Vector4.zero);
            }
            for (int i = 0; i < Segments; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Segments);
        }
    }
}
