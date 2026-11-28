using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Domain
{
    /// <summary>
    /// 「영역전개」 한자 한 글자 — 같은 그림을 뒤로 조금씩 물려 여러 겹 쌓아 두께가 있는 글자로 보이게 하고, 원근을 넣어 그린다 (시안 .dom-ch 의 img 11장 + perspective).
    /// 글자가 멀리서 날아와 박히는 자세와, 네 글자를 함께 돌려 보는 카메라 각을 <see cref="Set"/> 으로 받는다. 각 · 깊이의 뜻은 CSS 와 같다 (rotateX · rotateY 도, translateZ 는 앞이 +).
    /// 이 RectTransform 의 칸 = 글자가 다 박혔을 때의 자리.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class DomainGlyph : MaskableGraphic
    {
        // 한 겹을 이만큼 잘게 나눈다 — 기울어진 네모를 세모 둘로만 그리면 그림이 대각선에서 꺾인다
        private const int Grid = 6;
        private const float MinDepth = 0.05f;

        [SerializeField] private Sprite sprite;
        [Tooltip("겹 수 (시안 11) — 맨 위 한 장과 그 뒤의 어두운 장들.")]
        [SerializeField] private int layers = 11;
        [Tooltip("겹 사이 깊이 (시안 6px — 화면 짧은 변 800 기준).")]
        [SerializeField] private float layerGap = 6f;
        [Tooltip("가장 깊은 겹의 밝기 · 한 겹 앞으로 올 때마다 더해지는 밝기 (시안 0.12 + j × 0.035).")]
        [SerializeField] private Vector2 shade = new Vector2(0.12f, 0.035f);

        private Vector3 _pose;
        private Vector2 _camera;
        private Vector2 _fromCamera;
        private float _perspective = 1100f;
        private float _unit = 1f;

        public override Texture mainTexture => sprite != null ? sprite.texture : s_WhiteTexture;

        /// <summary>
        /// 자세를 바꾸고 다시 그린다. pose = 글자 (rotateX 도, rotateY 도, translateZ), cameraAngles = 네 글자를 함께 돌리는 (rotateX 도, rotateY 도),
        /// fromCamera = 카메라가 도는 중심에서 본 이 글자의 가운데 (y 위 +), perspective = 원근 거리, unit = 시안 1px 의 크기.
        /// </summary>
        public void Set(Vector3 pose, Vector2 cameraAngles, Vector2 fromCamera, float perspective, float unit)
        {
            _pose = pose;
            _camera = cameraAngles;
            _fromCamera = fromCamera;
            _perspective = perspective;
            _unit = unit;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (sprite == null || layers < 1)
                return;

            Rect rect = GetPixelAdjustedRect();
            Vector4 uv = UIQuadBatch.SpriteUv(sprite);
            int side = Grid + 1;
            for (int layer = 0; layer < layers; layer++)
            {
                bool top = layer == layers - 1;
                float light = top ? 1f : shade.x + layer * shade.y;
                var tint = new Color(color.r * light, color.g * light, color.b * light, color.a);
                float depth = (layer - (layers - 1)) * layerGap * _unit;
                int start = vh.currentVertCount;
                for (int y = 0; y <= Grid; y++)
                {
                    for (int x = 0; x <= Grid; x++)
                    {
                        float u = (float)x / Grid, v = (float)y / Grid;
                        // CSS 좌표 (y 아래 +): 글자 가운데 기준
                        var point = new Vector3((u - 0.5f) * rect.width, (v - 0.5f) * rect.height, depth);
                        Vector2 screen = Project(point);
                        vh.AddVert(rect.center + new Vector2(screen.x, -screen.y), tint, new Vector2(Mathf.Lerp(uv.x, uv.z, u), Mathf.Lerp(uv.w, uv.y, v)));
                    }
                }

                for (int y = 0; y < Grid; y++)
                {
                    for (int x = 0; x < Grid; x++)
                    {
                        int a = start + y * side + x;
                        vh.AddTriangle(a, a + side, a + side + 1);
                        vh.AddTriangle(a, a + side + 1, a + 1);
                    }
                }
            }
        }

        // 글자 가운데 기준 점(CSS 좌표)이 화면에서 글자 가운데 기준 어디에 보이는지 — 글자 자세 → 카메라 회전 → 원근
        private Vector2 Project(Vector3 point)
        {
            point = RotateX(RotateY(point, _pose.y), _pose.x);
            point.z += _pose.z * _unit;

            var center = new Vector3(_fromCamera.x, -_fromCamera.y, 0f);
            point = RotateX(RotateY(point + center, _camera.y), _camera.x);

            float distance = _perspective * _unit;
            float scale = distance / Mathf.Max(distance * MinDepth, distance - point.z);
            return new Vector2(point.x * scale - center.x, point.y * scale - center.y);
        }

        // CSS rotateY — x 오른쪽, y 아래, z 앞이 +
        private static Vector3 RotateY(Vector3 point, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            return new Vector3(point.x * cos + point.z * sin, point.y, -point.x * sin + point.z * cos);
        }

        // CSS rotateX
        private static Vector3 RotateX(Vector3 point, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            return new Vector3(point.x, point.y * cos - point.z * sin, point.y * sin + point.z * cos);
        }
    }
}
