using UnityEngine;

namespace Chapchu.UI.Fx.Post
{
    /// <summary>
    /// 엎어 놓은 함정 카드 (UI 도형) — 시안 .vw-trap: 둥근 모서리 · 잉크 테두리 · 아래 그림자 · 안쪽에 카드 뒷면 그림(가운데 맞춰 꽉 채움) (10-ui 5-1 4 단계 장식).
    /// 그림자 · 테두리 · 뒷면을 그림 한 장에 겹쳐 그린다 (<see cref="UIBakedGraphic"/>) — 깨지며 흐려질 때 그림자가 뒷면 너머로 비치지 않게.
    /// <see cref="Draw"/> 를 부르기 전에는 아무것도 그리지 않는다. 길이 값은 이 오브젝트 단위 (시안 px 로 넣고 오브젝트 배율로 키운다).
    /// </summary>
    public class UITrapCard : UIBakedGraphic
    {
        // 그림이 오브젝트 밖으로 그림자만큼 더 나가고, 거기에 흐린 가장자리 한 단위
        private const float EdgeMargin = 1f;

        [Header("모양 (시안 .vw-trap)")]
        [Tooltip("모서리 반지름 (시안 border-radius 8px).")]
        [SerializeField] private float radius = 8f;
        [Tooltip("테두리 두께 (시안 border 2px).")]
        [SerializeField] private float borderWidth = 2f;
        [Tooltip("테두리 색 (시안 --ink).")]
        [SerializeField] private Color borderColor = new Color32(0x2b, 0x28, 0x56, 0xff);
        [Tooltip("그림자 위치 (시안 box-shadow 0 3px 0 — 아래로 3, 번짐 없음).")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -3f);
        [Tooltip("그림자 색 (시안 --ink).")]
        [SerializeField] private Color shadowColor = new Color32(0x2b, 0x28, 0x56, 0xff);

        private Sprite _back;

        protected override Rect DrawRect
        {
            get
            {
                Rect rect = rectTransform.rect;
                float left = Mathf.Max(0f, -shadowOffset.x) + EdgeMargin;
                float right = Mathf.Max(0f, shadowOffset.x) + EdgeMargin;
                float bottom = Mathf.Max(0f, -shadowOffset.y) + EdgeMargin;
                float top = Mathf.Max(0f, shadowOffset.y) + EdgeMargin;
                return Rect.MinMaxRect(rect.xMin - left, rect.yMin - bottom, rect.xMax + right, rect.yMax + top);
            }
        }

        /// <summary>카드 뒷면 그림과 화면에서 가장 클 때 배율(부모 기준)을 받아 그린다. 뒷면이 없으면 안쪽을 Graphic.color 로 채운다.</summary>
        public void Draw(Sprite back, float maxScale)
        {
            _back = back;
            MaxScale = maxScale;
            Redraw();
        }

        // 그림자 → 테두리(바깥 둥근 네모) → 뒷면(테두리 안쪽 둥근 네모)을 텍셀마다 겹친다
        protected override void Redraw()
        {
            Rect area = DrawRect;
            Rect card = rectTransform.rect;
            Color32[] pixels = BeginDraw(out int width, out int height);
            float texelX = area.width / width;
            float texelY = area.height / height;
            float edge = Mathf.Max(texelX, texelY);

            // 뒷면은 테두리 안쪽(시안 background center/cover 가 채우는 곳)
            var inner = Rect.MinMaxRect(card.xMin + borderWidth, card.yMin + borderWidth, card.xMax - borderWidth, card.yMax - borderWidth);
            float innerRadius = Mathf.Max(0f, radius - borderWidth);
            int backWidth = Mathf.Max(1, Mathf.CeilToInt(inner.width / texelX));
            int backHeight = Mathf.Max(1, Mathf.CeilToInt(inner.height / texelY));
            Color32[] back = ReadBack(_back, backWidth, backHeight);
            var tint = new Color(color.r, color.g, color.b, 1f);

            for (int y = 0; y < height; y++)
            {
                float pointY = area.yMin + (y + 0.5f) * texelY;
                for (int x = 0; x < width; x++)
                {
                    float pointX = area.xMin + (x + 0.5f) * texelX;
                    float outer = RoundRectDistance(card, radius, pointX, pointY);
                    float shadow = RoundRectDistance(card, radius, pointX - shadowOffset.x, pointY - shadowOffset.y);
                    float face = RoundRectDistance(inner, innerRadius, pointX, pointY);

                    Color fill = tint;
                    if (back != null)
                    {
                        int u = Mathf.Clamp(Mathf.FloorToInt((pointX - inner.xMin) / inner.width * backWidth), 0, backWidth - 1);
                        int v = Mathf.Clamp(Mathf.FloorToInt((pointY - inner.yMin) / inner.height * backHeight), 0, backHeight - 1);
                        fill *= back[v * backWidth + u];
                    }
                    pixels[y * width + x] = Layer(shadowColor, Coverage(shadow, edge), borderColor, Coverage(outer, edge), fill, Coverage(face, edge), shadow < outer);
                }
            }

            EndDraw();
        }

        // 스프라이트를 width × height 에 가운데 맞춰 꽉 채운(CSS cover — 남는 쪽을 잘라 낸) 텍셀로 받는다. 그림을 읽을 수 없게 들여와도 되게 GPU 로 옮겨 그린 뒤 읽는다
        private static Color32[] ReadBack(Sprite sprite, int width, int height)
        {
            if (sprite == null)
                return null;
            Texture texture = sprite.texture;
            Rect source = sprite.textureRect;
            float spriteAspect = source.width / source.height;
            float targetAspect = (float)width / height;
            // 스프라이트 안에서 쓸 부분 (0 ~ 1) — 넓은 쪽을 가운데로 잘라 낸다
            Vector2 crop = spriteAspect > targetAspect ? new Vector2(targetAspect / spriteAspect, 1f) : new Vector2(1f, spriteAspect / targetAspect);
            var scale = new Vector2(source.width / texture.width * crop.x, source.height / texture.height * crop.y);
            var offset = new Vector2((source.x + source.width * (1f - crop.x) / 2f) / texture.width, (source.y + source.height * (1f - crop.y) / 2f) / texture.height);

            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target, scale, offset);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                DestroyImmediate(copy);
            }
        }

        // 둥근 네모까지의 거리 (안 −, 밖 +)
        private static float RoundRectDistance(Rect rect, float cornerRadius, float x, float y)
        {
            float halfX = rect.width / 2f - cornerRadius;
            float halfY = rect.height / 2f - cornerRadius;
            float dx = Mathf.Abs(x - rect.center.x) - halfX;
            float dy = Mathf.Abs(y - rect.center.y) - halfY;
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - cornerRadius;
        }
    }
}
