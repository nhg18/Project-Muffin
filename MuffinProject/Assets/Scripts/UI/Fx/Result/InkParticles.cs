using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 4 단계 공통 연출의 입자 — 별 · 동그라미 · 카드 · 하트, 모두 색 면 + 잉크 테 (시안 「4단계 공통 연출」 PF · drawP).
    /// 흐려지지 않고 남은 수명이 fade 초 아래로 내려가면 작아지며 사라진다. 입자 전부를 메시 하나로 그린다 — 입자마다 오브젝트를 만들지 않는다.
    /// 값(속도 · 크기 · 중력)은 이 화면 단위로 받는다. 위치는 이 Graphic 의 로컬 좌표.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class InkParticles : MaskableGraphic
    {
        public enum ParticleShape { Star, Circle, Card, Heart }

        // 시안 PF — 감속(drag)은 60fps 한 프레임 기준, 한 프레임 시간 상한 0.05초, 입자 수 상한 1100 (넘으면 오래된 60개를 지운다)
        private const float DragFps = 60f;
        private const float MaxDeltaTime = 0.05f;
        private const int MaxParticles = 1100;
        private const int TrimCount = 60;
        private const int CircleSegments = 14;
        private const int HeartSegments = 10; // 베지어 한 줄당

        [Tooltip("테 두께 (이 화면 단위, 시안 lineWidth 2).")]
        [SerializeField] private float borderWidth = 3f;
        [SerializeField] private Color borderColor = new Color32(0x2a, 0x24, 0x20, 0xff);

        public struct Particle
        {
            public ParticleShape Shape;
            public Color32 Color;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Gravity;   // 아래로 +
            public float Drag;
            public float Rotation;  // 라디안, 시계 방향 +
            public float Spin;
            public float Size;
            public float Life;
            public float Fade;
        }

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly List<Vector2> _points = new List<Vector2>();

        /// <summary>테 두께 · 색 — 만드는 쪽이 화면 배율에 맞춰 넣는다.</summary>
        public void SetBorder(float width, Color color)
        {
            borderWidth = width;
            borderColor = color;
        }

        public void Add(Particle particle)
        {
            if (_particles.Count >= MaxParticles)
                _particles.RemoveRange(0, TrimCount);
            _particles.Add(particle);
        }

        public void Clear()
        {
            _particles.Clear();
            SetVerticesDirty();
        }

        private void Update()
        {
            if (_particles.Count == 0)
                return;

            float dt = Mathf.Min(MaxDeltaTime, Time.deltaTime);
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                float drag = Mathf.Pow(p.Drag, dt * DragFps);
                p.Velocity.x *= drag;
                p.Velocity.y = p.Velocity.y * drag - p.Gravity * dt; // 유니티는 위로 + — 중력(아래로 +)을 뺀다
                p.Position += p.Velocity * dt;
                p.Rotation += p.Spin * dt;
                _particles[i] = p;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (Particle p in _particles)
            {
                float k = Mathf.Min(1f, p.Life / p.Fade); // 시안: 투명해지지 않고 작아지며 사라진다
                float s = p.Size * k;
                if (s <= 0.01f)
                    continue;

                Outline(p, s);
                float stroke = borderWidth * k;
                if (p.Shape == ParticleShape.Circle)
                    stroke = Mathf.Max(borderWidth * 0.5f, Mathf.Min(borderWidth, s * 0.3f)) * k; // 시안: 작은 동그라미는 테도 가늘게
                InkMesh.Fill(vh, p.Position, _points, p.Color);
                InkMesh.Stroke(vh, _points, stroke, borderColor);
            }
        }

        // 입자 하나의 윤곽 (크기 s, 회전 포함) → _points
        private void Outline(Particle p, float s)
        {
            _points.Clear();
            switch (p.Shape)
            {
                case ParticleShape.Star:
                    // 시안: 꼭짓점 10 개, 안쪽 반지름 0.45
                    for (int i = 0; i < 10; i++)
                    {
                        float r = i % 2 == 1 ? s * 0.45f : s;
                        float a = i * Mathf.PI / 5f - Mathf.PI / 2f;
                        _points.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                    }
                    Place(p.Position, p.Rotation);
                    break;
                case ParticleShape.Card:
                    RoundRect(s * 1.3f, s * 1.9f, s * 0.25f);
                    Place(p.Position, p.Rotation);
                    break;
                case ParticleShape.Heart:
                    Heart(s / 10f);
                    Place(p.Position, p.Rotation * 0.3f);
                    break;
                default:
                    InkMesh.Circle(_points, p.Position, s, CircleSegments);
                    break;
            }
        }

        // 시안 하트 — (0,4) 에서 (-10,-4) (-6,-12) (0,-6) 로, 다시 (6,-12) (10,-4) (0,4) 로 (y 는 아래로 + → 뒤집는다)
        private void Heart(float scale)
        {
            AddBezier(new Vector2(0, 4), new Vector2(-10, -4), new Vector2(-6, -12), new Vector2(0, -6), scale);
            AddBezier(new Vector2(0, -6), new Vector2(6, -12), new Vector2(10, -4), new Vector2(0, 4), scale);
        }

        private void AddBezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float scale)
        {
            for (int i = 0; i < HeartSegments; i++)
            {
                float t = i / (float)HeartSegments, u = 1f - t;
                Vector2 q = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
                _points.Add(new Vector2(q.x, -q.y) * scale);
            }
        }

        private void RoundRect(float w, float h, float radius)
        {
            float x = w / 2f - radius, y = h / 2f - radius;
            Vector2[] corners = { new Vector2(x, y), new Vector2(-x, y), new Vector2(-x, -y), new Vector2(x, -y) };
            const int steps = 3;
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i <= steps; i++)
                {
                    float a = (c + i / (float)steps) * Mathf.PI / 2f;
                    _points.Add(corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            }
        }

        // 가운데 기준 윤곽을 회전(시계 방향 +) · 이동
        private void Place(Vector2 position, float rotation)
        {
            float cos = Mathf.Cos(-rotation), sin = Mathf.Sin(-rotation);
            for (int i = 0; i < _points.Count; i++)
            {
                Vector2 q = _points[i];
                _points[i] = position + new Vector2(q.x * cos - q.y * sin, q.x * sin + q.y * cos);
            }
        }
    }
}
