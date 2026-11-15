using UnityEngine;
using UnityEngine.Sprites;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// <see cref="ComicDog"/> 의 그림 한 겹 — 부모 강아지의 자세 · 키를 읽어 그린다. 겹 순서 = 자식 순서 (그림자 → 꼬리 → 다리 → 몸, 시안 dog 와 같다).
    /// </summary>
    public class ComicDogPart : ComicGraphic
    {
        /// <summary>그리는 겹.</summary>
        public enum Layer
        {
            /// <summary>발밑 그림자 (기울기를 따르지 않는다).</summary>
            Shadow,
            /// <summary>꼬리 — <see cref="FxDog.TailPivot"/> 축으로 돈다.</summary>
            Tail,
            /// <summary>다리 넷 — 붙는 자리마다 돈다.</summary>
            Legs,
            /// <summary>몸.</summary>
            Body,
        }

        [Tooltip("이 오브젝트가 그리는 겹.")]
        [SerializeField] private Layer layer;

        private ComicDog _dog;

        private ComicDog Dog => _dog != null ? _dog : _dog = GetComponentInParent<ComicDog>();

        public override Texture mainTexture
        {
            get
            {
                Sprite sprite = SpriteOf(Dog != null ? Dog.CurrentSkin : null);
                return sprite != null ? sprite.texture : base.mainTexture;
            }
        }

        protected override void Paint(ComicPen pen)
        {
            ComicDog dog = Dog;
            if (dog == null)
                return;
            if (layer == Layer.Shadow)
            {
                if (!dog.HasShadow)
                    return;
                dog.ShadowShape(out Vector2 center, out Vector2 radius);
                pen.BeginPath();
                pen.Ellipse(center.x, center.y, radius.x, radius.y, 0f, 0f, ComicKit.Tau);
                pen.Fill(dog.ShadowColor);
                return;
            }

            Sprite sprite = SpriteOf(dog.CurrentSkin);
            if (sprite == null)
                return;
            Vector4 outer = DataUtility.GetOuterUV(sprite);
            var uv = new Rect(outer.x, outer.y, outer.z - outer.x, outer.w - outer.y);
            dog.ApplyBodySpace(pen);
            switch (layer)
            {
                case Layer.Tail:
                {
                    Vector4 r = FxDog.Tail;
                    Vector2 pivot = FxDog.TailPivot;
                    pen.Translate(pivot.x, pivot.y);
                    pen.Rotate(dog.TailAngle);
                    pen.Translate(-pivot.x, -pivot.y);
                    Image(pen, r.x, r.y, r.z - r.x, r.w - r.y, uv);
                    break;
                }
                case Layer.Legs:
                    for (int i = 0; i < FxDog.Legs.Length; i++)
                    {
                        Vector2 at = FxDog.Legs[i];
                        pen.Save();
                        pen.Translate(at.x, at.y);
                        pen.Rotate(dog.LegAngle(i));
                        Image(pen, -FxDog.LegPivot.x, -FxDog.LegPivot.y, FxDog.LegSize.x, FxDog.LegSize.y, uv);
                        pen.Restore();
                    }
                    break;
                case Layer.Body:
                {
                    Vector4 r = FxDog.Body;
                    Image(pen, r.x, r.y, r.z - r.x, r.w - r.y, uv);
                    break;
                }
            }
        }

        private static void Image(ComicPen pen, float x, float y, float width, float height, Rect uv)
        {
            pen.BeginPath();
            pen.Rect(x, y, width, height);
            pen.FillImage(x, y, width, height, uv);
        }

        private Sprite SpriteOf(ComicDog.Skin skin)
        {
            if (skin == null)
                return null;
            switch (layer)
            {
                case Layer.Tail:
                    return skin.tail;
                case Layer.Legs:
                    return skin.leg;
                case Layer.Body:
                    return skin.body;
                default:
                    return null;
            }
        }
    }
}
