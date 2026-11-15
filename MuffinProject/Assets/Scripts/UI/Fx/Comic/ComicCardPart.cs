using UnityEngine;
using UnityEngine.Sprites;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// <see cref="ComicCard"/> 의 그림 한 겹 — 부모 카드의 너비 · 뒷면 · 반짝을 읽어 그린다. 겹 순서 = 자식 순서 (몸 → 뒷면 → 테 → 반짝).
    /// 반짝 겹은 시안이 「lighter」 로 그리므로 머티리얼을 FxAdditive 로 둔다.
    /// </summary>
    public class ComicCardPart : ComicGraphic
    {
        /// <summary>그리는 겹.</summary>
        public enum Layer
        {
            /// <summary>그림자 + 흰 바탕.</summary>
            Body,
            /// <summary>안쪽 둥근 칸에 깐 뒷면 그림.</summary>
            Back,
            /// <summary>먹 테.</summary>
            Edge,
            /// <summary>비스듬히 지나가는 흰 빛.</summary>
            Glint,
        }

        // 시안 card(): 모서리 반지름 w·0.09, 뒷면 칸 안쪽 여백 w·0.06 · 모서리 r·0.7, 그림자 (5, 7)
        private const float CornerRatio = 0.09f;
        private const float InsetRatio = 0.06f;
        private const float InnerCornerRatio = 0.7f;
        private static readonly Vector2 ShadowOffset = new Vector2(5f, 7f);
        private static readonly Color ShadowColor = new Color(20f / 255f, 8f / 255f, 30f / 255f, 0.25f);
        private static readonly Color MissingBack = new Color32(0x6b, 0x8c, 0xff, 0xff);
        // 시안 card(): 테 두께 = max(2, w·0.045)
        private const float EdgeRatio = 0.045f;
        private const float EdgeMin = 2f;
        // 시안 card() glint: 빛 띠 가운데가 −1.2w → 1.2w, 띠 반폭 0.3w, 가운데 진하기 0.85
        private const float GlintTravel = 1.2f;
        private const float GlintHalfWidth = 0.3f;
        private const float GlintPeak = 0.85f;
        private static readonly float[] GlintStops = { 0f, 0.5f, 1f };

        [Tooltip("이 오브젝트가 그리는 겹.")]
        [SerializeField] private Layer layer;

        private ComicCard _card;

        private ComicCard Card => _card != null ? _card : _card = GetComponentInParent<ComicCard>();

        public override Texture mainTexture
        {
            get
            {
                Sprite back = Card != null ? Card.Back : null;
                return layer == Layer.Back && back != null ? back.texture : base.mainTexture;
            }
        }

        protected override void Paint(ComicPen pen)
        {
            ComicCard card = Card;
            if (card == null)
                return;
            float w = card.Width, h = w * ComicCard.Aspect, r = w * CornerRatio;
            switch (layer)
            {
                case Layer.Body:
                    if (card.Shadow)
                    {
                        pen.RoundRect(-w / 2f + ShadowOffset.x, -h / 2f + ShadowOffset.y, w, h, r);
                        pen.Fill(ShadowColor);
                    }
                    pen.RoundRect(-w / 2f, -h / 2f, w, h, r);
                    pen.Fill(Color.white);
                    break;
                case Layer.Back:
                    float inset = w * InsetRatio;
                    pen.RoundRect(-w / 2f + inset, -h / 2f + inset, w - inset * 2f, h - inset * 2f, r * InnerCornerRatio);
                    if (card.Back == null)
                    {
                        pen.Fill(MissingBack);
                        break;
                    }
                    Vector4 uv = DataUtility.GetOuterUV(card.Back);
                    pen.FillImage(-w / 2f, -h / 2f, w, h, new Rect(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y));
                    break;
                case Layer.Edge:
                    pen.RoundRect(-w / 2f, -h / 2f, w, h, r);
                    pen.Stroke(ComicKit.Ink, Mathf.Max(EdgeMin, w * EdgeRatio));
                    break;
                case Layer.Glint:
                    if (card.Glint <= 0f)
                        break;
                    float gx = ComicKit.Lerp(-w * GlintTravel, w * GlintTravel, card.Glint);
                    var clear = new Color(1f, 1f, 1f, 0f);
                    pen.RoundRect(-w / 2f, -h / 2f, w, h, r);
                    pen.FillLinear(gx - w * GlintHalfWidth, 0f, gx + w * GlintHalfWidth, 0f, GlintStops,
                        new[] { clear, new Color(1f, 1f, 1f, GlintPeak), clear });
                    break;
            }
        }
    }
}
