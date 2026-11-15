using System.Collections;
using Chapchu.UI.Fx.Comic;
using UnityEngine;

namespace Chapchu.UI.Fx.Bark
{
    /// <summary>
    /// 짖기(카드 107) 만화 컷인 장면 — 한 번 짖으면 상대가 손을 못 쓴다 (시안 CI2.SC["짖기"]).
    /// 짖는 쪽(카드 그림 스티커)이 튀어 들어와 0.36 초에 「왈!!」 하고 짖는다 → 소리 물결 → 0.5 초에 맞는 쪽 강아지가 뒤로 젖혀지며 손에 든 카드 세 장이 날아간다 →
    /// 땀 → 0.88 초에 「행동 카드 금지」 도장. 4 단계 = 대상에게 「왈왈!!」 말풍선 + 판 흔들기 8 (시안 FXA 짖기 s4, 「행동 금지」 상태 글자는 게임 화면 몫이라 뺐다).
    /// </summary>
    public class BarkComicScene : ComicScene
    {
        // 시안: 짖는 때 · 맞는 때 (초)
        private const float BarkAt = 0.36f;
        private const float HitAt = 0.5f;
        // 시안: 짖는 쪽 — 0.24 초에 −200 → 400 으로 튀어 들어오고(back 1.6), 짖을 때 출렁(wob 20, 6)
        private const float EnterTime = 0.24f;
        private const float EnterOvershoot = 1.6f;
        private const float BarkerFromX = -200f;
        private const float BarkerX = 400f;
        private const float BarkerY = 740f;
        private const float RecoilFrequency = 20f;
        private const float RecoilDamping = 6f;
        private const float RecoilTilt = -0.06f;
        private const float RecoilStretchX = 0.07f;
        private const float RecoilSquashY = 0.03f;
        private const float CrouchFrom = -0.1f;
        private const float CrouchY = 0.95f;
        // 시안: 입 = 스티커 그림 (486, 345), 스티커 기준점 (300, 562), 그림 600 을 높이 560 으로
        private static readonly Vector2 MouthInArt = new Vector2(486f, 345f);
        private static readonly Vector2 StickerAnchor = new Vector2(300f, 562f);
        private const float StickerScale = 560f / 600f;
        // 시안: 소리 물결 셋 — 0.09 초 간격, 0.7 초 동안 반지름 50 → 670, 굵기 26 → 5 (먹) · 13 → 2 (흰), 진하기 1 → 0.4, 호 −0.5 ~ 0.5
        private const int WaveCount = 3;
        private const float WaveGap = 0.09f;
        private const float WaveLife = 0.7f;
        private const float WaveRadius = 50f;
        private const float WaveGrow = 620f;
        private const float WaveInk = 26f;
        private const float WaveInkMin = 5f;
        private const float WaveLight = 13f;
        private const float WaveLightMin = 2f;
        private const float WaveFade = 0.6f;
        private const float WaveHalfAngle = 0.5f;
        private static readonly Color WaveLightColor = new Color32(0xff, 0xf6, 0xe0, 0xff);
        // 시안: 맞는 쪽 — 땅 (990, 700), 맞으면 0.2 초에 24 밀리고, 젖혀짐 0.07 + 0.11·exp(−6τ)·cos(15τ), 폭 0.97, 기울이기 −0.08·exp(−4τ),
        // 다리 앞 −0.25 · 뒤 0.1 (exp(−3τ))
        private const float TargetX = 990f;
        private const float TargetY = 700f;
        private const float PushTime = 0.2f;
        private const float Push = 24f;
        private const float Lean = 0.07f;
        private const float LeanWobble = 0.11f;
        private const float LeanDamping = 6f;
        private const float LeanFrequency = 15f;
        private const float HitWidth = 0.97f;
        private const float HitSkew = -0.08f;
        private const float SkewDamping = 4f;
        private const float FrontLegKick = -0.25f;
        private const float BackLegKick = 0.1f;
        private const float LegDamping = 3f;
        // 시안: 손 = 강아지 그림 (250, 640). 카드 세 장 — 손에서 (−60 + 40i, −20 − |i − 1|·10), 기울기 (i − 1)·0.26 − 0.15,
        // 맞으면 τ 초에 ((260 + 110i)τ, −(520 − 70i)τ + 1200τ²) 날아가며 (i − 1)·0.26 + τ·(7 + 3i) 로 돈다
        private static readonly Vector2 HandInDog = new Vector2(250f, 640f);
        private const float CardSpreadX = 40f;
        private const float CardLeft = -60f;
        private const float CardRaise = -20f;
        private const float CardArch = 10f;
        private const float CardFan = 0.26f;
        private const float CardTilt = -0.15f;
        private const float FlyX = 260f;
        private const float FlyXStep = 110f;
        private const float FlyUp = 520f;
        private const float FlyUpStep = 70f;
        private const float FlyFall = 1200f;
        private const float FlySpin = 7f;
        private const float FlySpinStep = 3f;
        // 시안: 땀 — 머리 (600, 250) 옆 (30, 0) · (70, 50), 0.12 · 0.2 초부터 0.1 초에 나타난다
        private static readonly Vector2 HeadInDog = new Vector2(600f, 250f);
        private static readonly Vector2 SweatOffsetA = new Vector2(30f, 0f);
        private static readonly Vector2 SweatOffsetB = new Vector2(70f, 50f);
        private const float SweatAtA = 0.12f;
        private const float SweatAtB = 0.2f;
        private const float SweatFade = 0.1f;
        // 시안: 「왈!!」 — 짖고 0.02 초 뒤부터 0.2 초에 터지고(back 1.5), 0.5 초까지 ±4 떨린다
        private const float ShoutDelay = 0.02f;
        private const float ShoutTime = 0.2f;
        private const float ShoutOvershoot = 1.5f;
        private const float ShoutShakeUntil = 0.5f;
        private const float ShoutShake = 4f;
        // 시안: 도장 — 0.88 초부터 0.18 초에 찍힌다, 맞는 쪽 땅 x + 70
        private const float BanAt = 0.88f;
        private const float BanTime = 0.18f;
        private const float BanOffsetX = 70f;

        // 시안 FXA 짖기 s4: vbubble(t, "왈왈!!", −80) · arenaShake(8) · 300ms + (badge 자리) 300ms
        private const string BubbleText = "왈왈!!";
        private const float BubbleRise = 80f;
        private const float PostShake = 8f;

        [Header("그림 (그리는 순서)")]
        [Tooltip("집중선 — 짖기 전 반지름 260 · 진하기 0.22, 짖은 뒤 150 · 0.85.")]
        [SerializeField] private ComicFocusLines focusLines;
        [Tooltip("짖는 쪽 — 카드 그림 스티커.")]
        [SerializeField] private ComicSticker barker;
        [Tooltip("소리 물결 (장면이 그린다).")]
        [SerializeField] private ComicSketch soundWaves;
        [Tooltip("맞는 쪽 강아지.")]
        [SerializeField] private ComicDog target;
        [Tooltip("맞는 쪽 손에 든 카드 세 장 (왼쪽부터).")]
        [SerializeField] private ComicCard[] cards = new ComicCard[3];
        [Tooltip("땀 둘 (큰 것 · 작은 것).")]
        [SerializeField] private ComicSweat[] sweats = new ComicSweat[2];
        [Tooltip("「왈!!」 터짐 풍선.")]
        [SerializeField] private ComicBurst shoutBurst;
        [Tooltip("「왈!!」 글자 (찍히기).")]
        [SerializeField] private ComicStamp shoutWord;
        [Tooltip("「행동 카드 금지」 도장 (찍히기).")]
        [SerializeField] private ComicStamp ban;

        [Header("집중선")]
        [Tooltip("짖기 전 · 짖은 뒤 가운데 빈 원 반지름 (설계 px).")]
        [SerializeField] private Vector2 focusRadius = new Vector2(260f, 150f);
        [Tooltip("짖기 전 · 짖은 뒤 진하기.")]
        [SerializeField] private Vector2 focusAlpha = new Vector2(0.22f, 0.85f);

        [Header("4 단계")]
        [Tooltip("4 단계 장식 길이 (초, 시안 300 + 300ms).")]
        [SerializeField] private float postWait = 0.6f;

        private Vector2 _mouth;
        private float _barkTime;
        private Vector2 _shoutBurstRest;

        public override void Begin(ComicContext context)
        {
            target.Key = context.TargetKey;
            soundWaves.Painter = PaintWaves;
            _shoutBurstRest = ComicKit.PositionOf((RectTransform)shoutBurst.transform);
        }

        public override void Draw(float t, ComicContext context)
        {
            float bk = t - BarkAt, hitT = t - HitAt;

            focusLines.Radius = bk > 0f ? focusRadius.y : focusRadius.x;
            ComicKit.SetAlpha(focusLines, bk > 0f ? focusAlpha.y : focusAlpha.x);
            focusLines.SetTime(t);

            // 짖는 쪽 (카드 그림 스티커)
            float enter = ComicKit.Back(t / EnterTime, EnterOvershoot);
            float bx = ComicKit.Lerp(BarkerFromX, BarkerX, enter);
            float recoil = bk > 0f ? ComicKit.Wob(bk, RecoilFrequency, RecoilDamping) : 0f;
            float sx = 1f + RecoilStretchX * recoil;
            float sy = bk > CrouchFrom && bk < 0f ? CrouchY : 1f - RecoilSquashY * recoil;
            var barkerRect = (RectTransform)barker.transform;
            ComicKit.Place(barkerRect, bx, BarkerY);
            ComicKit.Turn(barkerRect, RecoilTilt * recoil);
            barkerRect.localScale = new Vector3(sx, sy, 1f);
            _mouth = new Vector2(bx + (MouthInArt.x - StickerAnchor.x) * StickerScale * sx, BarkerY + (MouthInArt.y - StickerAnchor.y) * StickerScale * sy);

            // 소리 물결
            _barkTime = bk;
            soundWaves.Redraw();

            // 맞는 쪽 — 손에 든 카드 세 장이 날아가고, 몸이 뒤로 젖혀진다
            bool hit = hitT > 0f;
            float tx = TargetX + (hit ? Push * ComicKit.Eo(hitT / PushTime) : 0f);
            target.SetTime(t);
            if (hit)
            {
                target.Rotation = Lean + LeanWobble * Mathf.Exp(-hitT * LeanDamping) * Mathf.Cos(hitT * LeanFrequency);
                target.ScaleX = HitWidth;
                target.Skew = HitSkew * Mathf.Exp(-hitT * SkewDamping);
                for (int i = 0; i < FxDog.Legs.Length; i++)
                    target.SetLeg(i, (FxDog.LegFront[i] ? FrontLegKick : BackLegKick) * Mathf.Exp(-hitT * LegDamping));
            }
            ComicKit.Place((RectTransform)target.transform, tx, TargetY);

            Vector2 hand = target.PointOf(HandInDog);
            for (int i = 0; i < cards.Length; i++)
            {
                float x = hand.x + CardLeft + i * CardSpreadX, y = hand.y + CardRaise - Mathf.Abs(i - 1) * CardArch;
                float angle = (i - 1) * CardFan + CardTilt;
                if (hit)
                {
                    float q = hitT;
                    x = hand.x + CardLeft + i * CardSpreadX + (FlyX + FlyXStep * i) * q;
                    y = hand.y + CardRaise - (FlyUp - FlyUpStep * i) * q + FlyFall * q * q;
                    angle = (i - 1) * CardFan + q * (FlySpin + i * FlySpinStep);
                }
                var cardRect = (RectTransform)cards[i].transform;
                ComicKit.Place(cardRect, x, y);
                ComicKit.Turn(cardRect, angle);
            }

            Vector2 head = target.PointOf(HeadInDog);
            PlaceSweat(sweats[0], head + SweatOffsetA, ComicKit.Cl((hitT - SweatAtA) / SweatFade));
            PlaceSweat(sweats[1], head + SweatOffsetB, ComicKit.Cl((hitT - SweatAtB) / SweatFade));

            // 효과음 「왈!!」 + 터짐 풍선
            float sk = (bk - ShoutDelay) / ShoutTime;
            shoutBurst.enabled = sk > 0f;
            Vector2 jitter = bk < ShoutShakeUntil
                ? new Vector2(Random.Range(-ShoutShake, ShoutShake), Random.Range(-ShoutShake, ShoutShake))
                : Vector2.zero;
            if (sk > 0f)
            {
                float pop = ComicKit.Back(sk, ShoutOvershoot);
                shoutBurst.SetRadius(pop, pop);
                ComicKit.Place((RectTransform)shoutBurst.transform, _shoutBurstRest.x + jitter.x, _shoutBurstRest.y + jitter.y);
            }
            shoutWord.Apply(sk, jitter);

            // 「행동 카드 금지」 도장 — 맞는 쪽을 따라 움직인다
            ban.Apply((t - BanAt) / BanTime, new Vector2(tx + BanOffsetX - ban.RestPosition.x, 0f));
        }

        public override IEnumerator PostRoutine(ComicContext context)
        {
            CardFxContext fx = context.Fx;
            if (fx.Layer != null && context.Target != null && context.Target.Anchor != null)
                fx.Layer.Bubble(context.Target.Anchor, BubbleText, BubbleRise);
            if (fx.Layer != null)
                fx.Layer.ShakeStage(PostShake, false);
            for (float time = 0f; time < postWait; time += Time.deltaTime)
                yield return null;
        }

        private static void PlaceSweat(ComicSweat sweat, Vector2 at, float alpha)
        {
            ComicKit.Place((RectTransform)sweat.transform, at.x, at.y);
            ComicKit.SetAlpha(sweat, alpha);
        }

        private void PaintWaves(ComicPen pen)
        {
            for (int i = 0; i < WaveCount; i++)
            {
                float w = _barkTime - i * WaveGap;
                if (w <= 0f || w > WaveLife)
                    continue;
                float q = w / WaveLife;
                float r = WaveRadius + ComicKit.Eo(q) * WaveGrow;
                pen.Save();
                pen.Alpha = 1f - q * WaveFade;
                pen.BeginPath();
                pen.Arc(_mouth.x, _mouth.y, r, -WaveHalfAngle, WaveHalfAngle);
                pen.Stroke(ComicKit.Ink, WaveInk * (1f - q) + WaveInkMin);
                pen.Stroke(WaveLightColor, WaveLight * (1f - q) + WaveLightMin);
                pen.Restore();
            }
        }
    }
}
