using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 조준 화살표 — 한 명을 겨누는 카드를 쥔 채 사용 기준선을 넘으면 카드 위 가장자리에서 포인터까지 그린다 (10-ui.md 4절 「겨누기」).
    /// 끊어진 굵은 띠가 포인터 쪽으로 흐르고 끝에 화살촉. 면은 턴 색, 상대 위면 겨눔 색 — 잉크는 테두리만. 그림 파일 없이 메시로 그린다.
    /// 값을 받아 그리기만 한다 — 언제 · 어디로 그릴지는 HandPresenter 가 정한다. 꺼 둔 채로 저장하고, Show 가 켠다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class AimArrowView : MaskableGraphic
    {
        private const int Samples = 40; // 곡선을 나누는 수

        [Header("색")]
        [Tooltip("평소 면 색 — 턴 색 (사용 기준선이 넘었을 때와 같다).")]
        [SerializeField] private Color fillColor = new Color32(0xFF, 0xC1, 0x2E, 0xFF);
        [Tooltip("포인터가 상대 위에 있을 때 면 색 — 대상 선택 강조와 같은 빨강.")]
        [SerializeField] private Color targetColor = new Color32(0xEC, 0x3B, 0x46, 0xFF);
        [Tooltip("테두리 색 — 잉크.")]
        [SerializeField] private Color inkColor = new Color32(0x2A, 0x24, 0x20, 0xFF);

        [Header("모양 (캔버스 px)")]
        [Tooltip("띠 굵기 — 카드 쪽 끝.")]
        [SerializeField] private float startWidth = 14f;
        [Tooltip("띠 굵기 — 화살촉 쪽 끝.")]
        [SerializeField] private float endWidth = 24f;
        [Tooltip("테두리 굵기.")]
        [SerializeField] private float outline = 5f;
        [Tooltip("끊어진 띠 한 토막 길이.")]
        [SerializeField] private float dashLength = 30f;
        [Tooltip("토막 사이 틈.")]
        [SerializeField] private float gapLength = 14f;
        [Tooltip("토막이 포인터 쪽으로 흐르는 빠르기 (px/초).")]
        [SerializeField] private float flowSpeed = 90f;
        [Tooltip("화살촉 길이.")]
        [SerializeField] private float headLength = 54f;
        [Tooltip("화살촉 밑변 폭.")]
        [SerializeField] private float headWidth = 64f;
        [Tooltip("휘는 정도 — 가로로 벌어진 거리에 곱해 곡선을 위로 띄운다. 똑바로 위면 곧은 화살표.")]
        [SerializeField] private float bowRatio = 0.35f;
        [Tooltip("휘는 거리의 상한.")]
        [SerializeField] private float bowMax = 180f;

        private readonly List<Vector2> _points = new List<Vector2>(Samples + 1);
        private readonly List<float> _lengths = new List<float>(Samples + 1);
        private Vector2 _from;
        private Vector2 _to;
        private bool _onTarget;

        /// <summary>화면 좌표 from(카드) 에서 to(포인터) 까지 그린다. onTarget 이면 겨눔 색.</summary>
        public void Show(Vector2 fromScreen, Vector2 toScreen, bool onTarget)
        {
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, fromScreen, eventCamera, out _from);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, toScreen, eventCamera, out _to);
            _onTarget = onTarget;
            enabled = true;
            SetVerticesDirty();
        }

        /// <summary>지운다.</summary>
        public void Hide()
        {
            enabled = false;
        }

        // 토막이 흐르도록 매 프레임 다시 그린다
        private void Update()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Application.isPlaying) return; // 에디터에서는 그리지 않는다 — 꺼 둔 채로 저장

            BuildCurve();
            float total = _lengths[_lengths.Count - 1];
            if (total < headLength) return;

            // 띠는 화살촉 밑변 조금 안쪽까지 — 화살촉이 덮는다
            float bodyEnd = total - headLength * 0.6f;
            float period = dashLength + gapLength;
            float phase = Mathf.Repeat(Time.unscaledTime * flowSpeed, period);
            Color fill = _onTarget ? targetColor : fillColor;

            // 잉크 테두리를 먼저, 그 위에 면 — 토막 · 화살촉 모두
            for (float s = phase - period; s < bodyEnd; s += period)
                AddBand(vh, Mathf.Max(0f, s - outline), Mathf.Min(bodyEnd, s + dashLength + outline), total, outline, inkColor);
            AddHead(vh, total, outline, inkColor);

            for (float s = phase - period; s < bodyEnd; s += period)
                AddBand(vh, Mathf.Max(0f, s), Mathf.Min(bodyEnd, s + dashLength), total, 0f, fill);
            AddHead(vh, total, 0f, fill);
        }

        // 위로 휜 2차 곡선 — 가로로 벌어질수록 더 휜다. 점마다 처음부터 잰 길이를 같이 둔다.
        private void BuildCurve()
        {
            Vector2 delta = _to - _from;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            if (normal.y < 0f)
                normal = -normal;
            Vector2 control = (_from + _to) / 2f + normal * Mathf.Min(bowMax, Mathf.Abs(delta.x) * bowRatio);

            _points.Clear();
            _lengths.Clear();
            for (int i = 0; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                float u = 1f - t;
                Vector2 point = u * u * _from + 2f * u * t * control + t * t * _to;
                _lengths.Add(i == 0 ? 0f : _lengths[i - 1] + Vector2.Distance(_points[i - 1], point));
                _points.Add(point);
            }
        }

        // 곡선의 [from, to] 구간을 띠로 — 굵기는 카드 쪽에서 화살촉 쪽으로 굵어진다. grow 만큼 사방으로 두껍게(테두리).
        private void AddBand(VertexHelper vh, float from, float to, float total, float grow, Color color)
        {
            if (to - from < 0.5f) return;

            int start = vh.currentVertCount;
            AddCross(vh, from, total, grow, color);
            for (int i = 1; i < _lengths.Count - 1; i++)
            {
                if (_lengths[i] > from && _lengths[i] < to)
                    AddCross(vh, _lengths[i], total, grow, color);
            }
            AddCross(vh, to, total, grow, color);

            int crosses = (vh.currentVertCount - start) / 2;
            for (int i = 0; i < crosses - 1; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        // 길이 s 자리에서 곡선에 수직인 두 점
        private void AddCross(VertexHelper vh, float s, float total, float grow, Color color)
        {
            Vector2 point = PointAt(s, out Vector2 direction);
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float half = Mathf.Lerp(startWidth, endWidth, s / total) / 2f + grow;
            AddVertex(vh, point + normal * half, color);
            AddVertex(vh, point - normal * half, color);
        }

        // 끝의 화살촉 — 끝은 포인터. grow 만큼 사방으로 크게(테두리).
        private void AddHead(VertexHelper vh, float total, float grow, Color color)
        {
            Vector2 tip = PointAt(total, out Vector2 direction);
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 baseCenter = tip - direction * headLength;
            float half = headWidth / 2f;

            int start = vh.currentVertCount;
            AddVertex(vh, tip + direction * (grow * 2f), color);
            AddVertex(vh, baseCenter - direction * grow + normal * (half + grow * 1.6f), color);
            AddVertex(vh, baseCenter - direction * grow - normal * (half + grow * 1.6f), color);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        // 처음부터 잰 길이 s 의 점과 그 자리의 진행 방향 (단위 벡터)
        private Vector2 PointAt(float s, out Vector2 direction)
        {
            int last = _lengths.Count - 1;
            int i = 1;
            while (i < last && _lengths[i] < s)
                i++;

            Vector2 a = _points[i - 1], b = _points[i];
            direction = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector2.up;
            float span = _lengths[i] - _lengths[i - 1];
            float t = span > 1e-4f ? Mathf.Clamp01((s - _lengths[i - 1]) / span) : 0f;
            return Vector2.Lerp(a, b, t);
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vh.AddVert(vertex);
        }
    }
}
