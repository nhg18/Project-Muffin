using System.Collections.Generic;
using Chapchu.UI.Fx.Comic;
using UnityEngine;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면의 색종이 (시안 「결과화면 UI v3」 burst · step) — 승자 칸 위에서 두 번 터져 떨어진다. 조각 전부를 메시 하나로 그린다.
    /// 좌표는 시안 그대로 — 이 RectTransform 의 피벗(1920 무대의 왼쪽 위)이 원점, y 는 아래로 +.
    /// 움직임 값은 시안이 프레임(1/60초)마다 더하는 값이라, 여기서도 1/60초씩 끊어서 계산한다.
    /// </summary>
    public class ResultConfettiView : ComicGraphic
    {
        private struct Piece
        {
            public float X, Y, Vx, Vy, Width, Height, Angle, Spin;
            public int Life;
            public Color Color;
        }

        private const float Step = 1f / 60f;

        [Tooltip("터지는 때 (초, 시안 1.9 · 2.3) — Play 를 부른 뒤부터.")]
        [SerializeField] private float[] burstAt = { 1.9f, 2.3f };
        [Tooltip("터질 때마다의 조각 수 (시안 90 · 60) — burstAt 과 같은 자리.")]
        [SerializeField] private int[] burstCount = { 90, 60 };
        [Tooltip("터지는 자리 — 가운데에서 좌우로 퍼지는 폭 · 위에서부터의 y · y 가 퍼지는 폭 (시안 300 · 420 · 60).")]
        [SerializeField] private Vector3 origin = new Vector3(300f, 420f, 60f);
        [Tooltip("처음 빠르기 — 옆으로 퍼지는 폭 · 위로 최소 · 위로 더하는 폭 (px/프레임, 시안 26 · 14 · 16).")]
        [SerializeField] private Vector3 launch = new Vector3(26f, 14f, 16f);
        [Tooltip("프레임마다 — 아래로 더해지는 빠르기 · 옆 빠르기에 곱하는 값 · 떨어지는 빠르기 상한 (시안 0.45 · 0.985 · 7).")]
        [SerializeField] private Vector3 fall = new Vector3(0.45f, 0.985f, 7f);
        [Tooltip("조각 크기 — 폭 최소 · 폭 더하는 폭 · 높이 최소 · 높이 더하는 폭 (시안 10 · 10 · 16 · 14).")]
        [SerializeField] private Vector4 pieceSize = new Vector4(10f, 10f, 16f, 14f);
        [Tooltip("조각의 회전 — 처음 각도의 폭 · 프레임마다 도는 빠르기의 폭 (시안 6 · 0.3).")]
        [SerializeField] private Vector2 spin = new Vector2(6f, 0.3f);
        [Tooltip("좌우로 흔들리며 떨어진다 — 흔들림 주기를 나누는 값(프레임) · 프레임마다 흔들리는 폭 (시안 14 · 1.4).")]
        [SerializeField] private Vector2 sway = new Vector2(14f, 1.4f);
        [Tooltip("뒤집히듯 높이가 줄었다 느는 주기를 나누는 값 (프레임, 시안 9).")]
        [SerializeField] private float flipPeriod = 9f;
        [Tooltip("이 y 를 넘으면 사라진다 (시안 1120).")]
        [SerializeField] private float floorY = 1120f;
        [Tooltip("조각 색 (시안 CONF_COLORS).")]
        [SerializeField] private Color[] colors =
        {
            new Color32(0xFF, 0xD3, 0x5A, 0xFF), new Color32(0x5D, 0xBB, 0x85, 0xFF), new Color32(0x6E, 0x9B, 0xFF, 0xFF),
            new Color32(0xA5, 0x74, 0xF0, 0xFF), new Color32(0xFF, 0x8F, 0xAE, 0xFF), new Color32(0x7F, 0xD6, 0xE8, 0xFF),
        };

        private readonly List<Piece> _pieces = new List<Piece>();
        private float _centerX;
        private float _time = -1f; // Play 뒤로 흐른 시간. 음수면 시작 전
        private float _stepDebt;
        private int _burstsDone;

        /// <summary>색종이를 시작한다 — centerX = 터지는 자리의 가운데 x (무대 왼쪽 끝 기준).</summary>
        public void Play(float centerX)
        {
            _centerX = centerX;
            _time = 0f;
            _stepDebt = 0f;
            _burstsDone = 0;
            _pieces.Clear();
        }

        private void Update()
        {
            if (_time < 0f)
                return;

            _time += Time.deltaTime;
            while (_burstsDone < burstAt.Length && _time >= burstAt[_burstsDone])
                Burst(burstCount[_burstsDone++]);

            if (_pieces.Count == 0)
            {
                if (_burstsDone >= burstAt.Length)
                {
                    _time = -1f; // 다 떨어졌다
                    Redraw();
                }
                return;
            }

            _stepDebt += Time.deltaTime;
            while (_stepDebt >= Step)
            {
                _stepDebt -= Step;
                Advance();
            }
            Redraw();
        }

        private void Burst(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _pieces.Add(new Piece
                {
                    X = _centerX + (Random.value - 0.5f) * origin.x,
                    Y = origin.y + Random.value * origin.z,
                    Vx = (Random.value - 0.5f) * launch.x,
                    Vy = -launch.y - Random.value * launch.z,
                    Width = pieceSize.x + Random.value * pieceSize.y,
                    Height = pieceSize.z + Random.value * pieceSize.w,
                    Angle = Random.value * spin.x,
                    Spin = (Random.value - 0.5f) * spin.y,
                    Color = colors[Random.Range(0, colors.Length)],
                });
            }
        }

        // 시안 step 의 한 프레임
        private void Advance()
        {
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                Piece p = _pieces[i];
                p.Vy = Mathf.Min(p.Vy + fall.x, fall.z);
                p.Vx *= fall.y;
                p.X += p.Vx + Mathf.Sin((p.Life + p.Angle * 10f) / sway.x) * sway.y;
                p.Y += p.Vy;
                p.Angle += p.Spin;
                p.Life++;
                if (p.Y >= floorY)
                    _pieces.RemoveAt(i);
                else
                    _pieces[i] = p;
            }
        }

        protected override void Paint(ComicPen pen)
        {
            foreach (Piece p in _pieces)
            {
                pen.Save();
                pen.Translate(p.X, p.Y);
                pen.Rotate(p.Angle);
                pen.BeginPath();
                pen.Rect(-p.Width / 2f, -p.Height / 2f, p.Width, p.Height * Mathf.Abs(Mathf.Cos(p.Life / flipPeriod)) + 2f); // 뒤집히듯 높이가 줄었다 는다
                pen.Fill(p.Color);
                pen.Restore();
            }
        }
    }
}
