using System.Collections;
using Chapchu.UI.Fx.Comic;
using UnityEngine;

namespace Chapchu.UI.Fx.Bark
{
    /// <summary>
    /// 짖기(카드 107) 만화 컷인 장면 — 한 번 짖으면 상대가 손을 못 쓴다 (시안 CI2.SC["짖기"]).
    /// 짖는 쪽(카드 그림 스티커)이 튀어 들어와 0.36 초에 「왈!!」 하고 짖는다 → 소리 물결 → 0.5 초에 맞는 쪽 강아지가 뒤로 젖혀지며 손에 든 카드 세 장이 날아간다 →
    /// 땀 → 0.88 초에 「행동 카드 금지」 도장. 4 단계 = 대상에게 「왈왈!!」 말풍선 + 판 흔들기 8 (시안 FXA 짖기 s4, 「행동 금지」 상태 글자는 게임 화면 몫이라 뺐다).
    /// 연출 길이 2.81초 (3 단계 2.21초 + 4 단계 0.6초) — 카드 처리 시간(resolveSeconds)은 게임 화면에 연출을 붙일 때 맞춘다 (PM 결정 2026-11-15).
    /// </summary>
    public class BarkComicScene : ComicScene
    {
        // 시안 FXA 짖기 s4: vbubble(t, "왈왈!!", −80)
        private const string BubbleText = "왈왈!!";

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

        [Header("때 (초)")]
        [Tooltip("짖는 때 (초, 시안 0.36).")]
        [SerializeField] private float barkAt = 0.36f;
        [Tooltip("맞는 때 (초, 시안 0.5).")]
        [SerializeField] private float hitAt = 0.5f;

        [Header("짖는 쪽 (시안: 0.24 초에 −200 → 400 으로 튀어 들어오고 back 1.6, 짖을 때 출렁 wob 20, 6)")]
        [Tooltip("튀어 들어오는 시간 (초, 시안 0.24).")]
        [SerializeField] private float enterTime = 0.24f;
        [Tooltip("튀어 들어올 때 지나치는 정도 (시안 back 1.6).")]
        [SerializeField] private float enterOvershoot = 1.6f;
        [Tooltip("들어오기 전 x (설계 px, 시안 −200).")]
        [SerializeField] private float barkerFromX = -200f;
        [Tooltip("자리 x (설계 px, 시안 400).")]
        [SerializeField] private float barkerX = 400f;
        [Tooltip("자리 y — 스티커 기준점 (설계 px, 시안 740).")]
        [SerializeField] private float barkerY = 740f;
        [Tooltip("짖을 때 출렁 빠르기 (시안 wob 20).")]
        [SerializeField] private float recoilFrequency = 20f;
        [Tooltip("짖을 때 출렁이 멎는 빠르기 (시안 wob 6).")]
        [SerializeField] private float recoilDamping = 6f;
        [Tooltip("출렁 기울기 (라디안, 시안 −0.06).")]
        [SerializeField] private float recoilTilt = -0.06f;
        [Tooltip("출렁 가로 늘이기 (시안 0.07).")]
        [SerializeField] private float recoilStretchX = 0.07f;
        [Tooltip("출렁 세로 줄이기 (시안 0.03).")]
        [SerializeField] private float recoilSquashY = 0.03f;
        [Tooltip("짖기 이만큼 전부터 웅크린다 (초, 음수, 시안 −0.1).")]
        [SerializeField] private float crouchFrom = -0.1f;
        [Tooltip("웅크린 세로 배율 (시안 0.95).")]
        [SerializeField] private float crouchY = 0.95f;
        [Tooltip("입 자리 — 스티커 그림 px (시안 486, 345).")]
        [SerializeField] private Vector2 mouthInArt = new Vector2(486f, 345f);
        [Tooltip("스티커 기준점 — 그림 px (시안 300, 562).")]
        [SerializeField] private Vector2 stickerAnchor = new Vector2(300f, 562f);
        [Tooltip("스티커 그림 배율 (시안 그림 600 을 높이 560 으로).")]
        [SerializeField] private float stickerScale = 560f / 600f;

        [Header("소리 물결 (시안: 반지름 50 → 670, 굵기 먹 26·(1 − q) + 5 · 흰 13·(1 − q) + 2, 진하기 1 → 0.4)")]
        [Tooltip("물결 수 (시안 3).")]
        [SerializeField] private int waveCount = 3;
        [Tooltip("물결 사이 (초, 시안 0.09).")]
        [SerializeField] private float waveGap = 0.09f;
        [Tooltip("물결 하나가 사는 시간 (초, 시안 0.7).")]
        [SerializeField] private float waveLife = 0.7f;
        [Tooltip("처음 반지름 (설계 px, 시안 50).")]
        [SerializeField] private float waveRadius = 50f;
        [Tooltip("커지는 만큼 (설계 px, 시안 620).")]
        [SerializeField] private float waveGrow = 620f;
        [Tooltip("먹 선 굵기 — 줄어드는 만큼 (설계 px, 시안 26).")]
        [SerializeField] private float waveInk = 26f;
        [Tooltip("먹 선 굵기 — 끝 (설계 px, 시안 5).")]
        [SerializeField] private float waveInkMin = 5f;
        [Tooltip("흰 선 굵기 — 줄어드는 만큼 (설계 px, 시안 13).")]
        [SerializeField] private float waveLight = 13f;
        [Tooltip("흰 선 굵기 — 끝 (설계 px, 시안 2).")]
        [SerializeField] private float waveLightMin = 2f;
        [Tooltip("끝에서 옅어지는 만큼 (시안 0.6 — 진하기 1 → 0.4).")]
        [SerializeField] private float waveFade = 0.6f;
        [Tooltip("호 반각 (라디안, 시안 ±0.5).")]
        [SerializeField] private float waveHalfAngle = 0.5f;
        [Tooltip("흰 선 색 (시안 #fff6e0).")]
        [SerializeField] private Color waveLightColor = new Color32(0xff, 0xf6, 0xe0, 0xff);

        [Header("맞는 쪽 (시안: 젖혀짐 0.07 + 0.11·exp(−6τ)·cos(15τ), 기울이기 −0.08·exp(−4τ), 다리 exp(−3τ))")]
        [Tooltip("땅 x (설계 px, 시안 990).")]
        [SerializeField] private float targetX = 990f;
        [Tooltip("땅 y (설계 px, 시안 700).")]
        [SerializeField] private float targetY = 700f;
        [Tooltip("밀리는 시간 (초, 시안 0.2).")]
        [SerializeField] private float pushTime = 0.2f;
        [Tooltip("밀리는 거리 (설계 px, 시안 24).")]
        [SerializeField] private float push = 24f;
        [Tooltip("젖혀짐 (라디안, 시안 0.07).")]
        [SerializeField] private float lean = 0.07f;
        [Tooltip("젖혀짐 출렁 (라디안, 시안 0.11).")]
        [SerializeField] private float leanWobble = 0.11f;
        [Tooltip("젖혀짐 출렁이 멎는 빠르기 (시안 6).")]
        [SerializeField] private float leanDamping = 6f;
        [Tooltip("젖혀짐 출렁 빠르기 (시안 15).")]
        [SerializeField] private float leanFrequency = 15f;
        [Tooltip("맞은 뒤 가로 배율 (시안 0.97).")]
        [SerializeField] private float hitWidth = 0.97f;
        [Tooltip("맞은 순간 기울이기 (시안 −0.08).")]
        [SerializeField] private float hitSkew = -0.08f;
        [Tooltip("기울이기가 풀리는 빠르기 (시안 4).")]
        [SerializeField] private float skewDamping = 4f;
        [Tooltip("앞다리 젖힘 (라디안, 시안 −0.25).")]
        [SerializeField] private float frontLegKick = -0.25f;
        [Tooltip("뒷다리 젖힘 (라디안, 시안 0.1).")]
        [SerializeField] private float backLegKick = 0.1f;
        [Tooltip("다리가 돌아오는 빠르기 (시안 3).")]
        [SerializeField] private float legDamping = 3f;

        [Header("손에 든 카드 (시안: 맞으면 τ 초에 ((260 + 110i)τ, −(520 − 70i)τ + 1200τ²) 날아가며 (i − 1)·0.26 + τ·(7 + 3i) 로 돈다)")]
        [Tooltip("손 자리 — 강아지 그림 px (시안 250, 640).")]
        [SerializeField] private Vector2 handInDog = new Vector2(250f, 640f);
        [Tooltip("카드 사이 x (설계 px, 시안 40).")]
        [SerializeField] private float cardSpreadX = 40f;
        [Tooltip("첫 카드 x — 손에서 (설계 px, 시안 −60).")]
        [SerializeField] private float cardLeft = -60f;
        [Tooltip("카드 y — 손에서 (설계 px, 시안 −20).")]
        [SerializeField] private float cardRaise = -20f;
        [Tooltip("가운데 카드에서 하나 멀어질 때 내려가는 만큼 (설계 px, 시안 10).")]
        [SerializeField] private float cardArch = 10f;
        [Tooltip("카드 사이 기울기 (라디안, 시안 0.26).")]
        [SerializeField] private float cardFan = 0.26f;
        [Tooltip("손에 든 카드 전체 기울기 (라디안, 시안 −0.15).")]
        [SerializeField] private float cardTilt = -0.15f;
        [Tooltip("날아가는 가로 빠르기 (설계 px/초, 시안 260).")]
        [SerializeField] private float flyX = 260f;
        [Tooltip("카드마다 더하는 가로 빠르기 (설계 px/초, 시안 110).")]
        [SerializeField] private float flyXStep = 110f;
        [Tooltip("날아오르는 빠르기 (설계 px/초, 시안 520).")]
        [SerializeField] private float flyUp = 520f;
        [Tooltip("카드마다 빼는 날아오르는 빠르기 (설계 px/초, 시안 70).")]
        [SerializeField] private float flyUpStep = 70f;
        [Tooltip("떨어지는 세기 (시안 1200τ²).")]
        [SerializeField] private float flyFall = 1200f;
        [Tooltip("도는 빠르기 (라디안/초, 시안 7).")]
        [SerializeField] private float flySpin = 7f;
        [Tooltip("카드마다 더하는 도는 빠르기 (라디안/초, 시안 3).")]
        [SerializeField] private float flySpinStep = 3f;

        [Header("땀")]
        [Tooltip("머리 자리 — 강아지 그림 px (시안 600, 250).")]
        [SerializeField] private Vector2 headInDog = new Vector2(600f, 250f);
        [Tooltip("큰 땀 — 머리에서 (설계 px, 시안 30, 0).")]
        [SerializeField] private Vector2 sweatOffsetA = new Vector2(30f, 0f);
        [Tooltip("작은 땀 — 머리에서 (설계 px, 시안 70, 50).")]
        [SerializeField] private Vector2 sweatOffsetB = new Vector2(70f, 50f);
        [Tooltip("큰 땀이 나오는 때 — 맞고 나서 (초, 시안 0.12).")]
        [SerializeField] private float sweatAtA = 0.12f;
        [Tooltip("작은 땀이 나오는 때 — 맞고 나서 (초, 시안 0.2).")]
        [SerializeField] private float sweatAtB = 0.2f;
        [Tooltip("땀이 나타나는 시간 (초, 시안 0.1).")]
        [SerializeField] private float sweatFade = 0.1f;

        [Header("「왈!!」")]
        [Tooltip("짖고 이만큼 뒤부터 터진다 (초, 시안 0.02).")]
        [SerializeField] private float shoutDelay = 0.02f;
        [Tooltip("터지는 시간 (초, 시안 0.2).")]
        [SerializeField] private float shoutTime = 0.2f;
        [Tooltip("터질 때 지나치는 정도 (시안 back 1.5).")]
        [SerializeField] private float shoutOvershoot = 1.5f;
        [Tooltip("짖고 이만큼까지 떨린다 (초, 시안 0.5).")]
        [SerializeField] private float shoutShakeUntil = 0.5f;
        [Tooltip("떨림 폭 (설계 px, 시안 ±4).")]
        [SerializeField] private float shoutShake = 4f;

        [Header("「행동 카드 금지」 도장")]
        [Tooltip("찍히기 시작하는 때 (초, 시안 0.88).")]
        [SerializeField] private float banAt = 0.88f;
        [Tooltip("찍히는 시간 (초, 시안 0.18).")]
        [SerializeField] private float banTime = 0.18f;
        [Tooltip("맞는 쪽 땅 x 에서 (설계 px, 시안 70).")]
        [SerializeField] private float banOffsetX = 70f;

        [Header("4 단계")]
        [Tooltip("4 단계 장식 길이 (초, 시안 300 + 300ms).")]
        [SerializeField] private float postWait = 0.6f;
        [Tooltip("「왈왈!!」 말풍선이 오르는 높이 (설계 px, 시안 vbubble −80).")]
        [SerializeField] private float bubbleRise = 80f;
        [Tooltip("판 흔들기 세기 (시안 arenaShake 8).")]
        [SerializeField] private float postShake = 8f;

        private Vector2 _mouth;
        private float _barkTime;
        private Vector2 _shoutBurstRest;

        private void Awake()
        {
            if (focusLines == null)
                Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(focusLines)} 가 비었다");
            if (barker == null)
                Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(barker)} 가 비었다");
            if (soundWaves == null)
                Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(soundWaves)} 가 비었다");
            if (target == null)
                Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(target)} 가 비었다");
            if (shoutBurst == null)
                Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(shoutBurst)} 가 비었다");
            if (shoutWord == null)
                Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(shoutWord)} 가 비었다");
            if (ban == null)
                Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(ban)} 가 비었다");
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                    Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(cards)}[{i}] 가 비었다");
            }
            if (sweats.Length < 2)
                Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(sweats)} 가 {sweats.Length} 개다 — 둘(큰 것 · 작은 것)이 있어야 한다");
            for (int i = 0; i < sweats.Length; i++)
            {
                if (sweats[i] == null)
                    Debug.LogError($"[{nameof(BarkComicScene)}] {name} 의 {nameof(sweats)}[{i}] 가 비었다");
            }
        }

        public override void Begin(ComicContext context)
        {
            target.Key = context.TargetKey;
            soundWaves.Painter = PaintWaves;
            _shoutBurstRest = ComicKit.PositionOf((RectTransform)shoutBurst.transform);
        }

        public override void Draw(float t, ComicContext context)
        {
            float bk = t - barkAt, hitT = t - hitAt;

            focusLines.Radius = bk > 0f ? focusRadius.y : focusRadius.x;
            ComicKit.SetAlpha(focusLines, bk > 0f ? focusAlpha.y : focusAlpha.x);
            focusLines.SetTime(t);

            // 짖는 쪽 (카드 그림 스티커)
            float enter = ComicKit.Back(t / enterTime, enterOvershoot);
            float bx = ComicKit.Lerp(barkerFromX, barkerX, enter);
            float recoil = bk > 0f ? ComicKit.Wob(bk, recoilFrequency, recoilDamping) : 0f;
            float sx = 1f + recoilStretchX * recoil;
            float sy = bk > crouchFrom && bk < 0f ? crouchY : 1f - recoilSquashY * recoil;
            var barkerRect = (RectTransform)barker.transform;
            ComicKit.Place(barkerRect, bx, barkerY);
            ComicKit.Turn(barkerRect, recoilTilt * recoil);
            barkerRect.localScale = new Vector3(sx, sy, 1f);
            _mouth = new Vector2(bx + (mouthInArt.x - stickerAnchor.x) * stickerScale * sx, barkerY + (mouthInArt.y - stickerAnchor.y) * stickerScale * sy);

            // 소리 물결
            _barkTime = bk;
            soundWaves.Redraw();

            // 맞는 쪽 — 손에 든 카드 세 장이 날아가고, 몸이 뒤로 젖혀진다
            bool hit = hitT > 0f;
            float tx = targetX + (hit ? push * ComicKit.Eo(hitT / pushTime) : 0f);
            target.SetTime(t);
            if (hit)
            {
                target.Rotation = lean + leanWobble * Mathf.Exp(-hitT * leanDamping) * Mathf.Cos(hitT * leanFrequency);
                target.ScaleX = hitWidth;
                target.Skew = hitSkew * Mathf.Exp(-hitT * skewDamping);
                for (int i = 0; i < FxDog.Legs.Count; i++)
                    target.SetLeg(i, (FxDog.LegFront[i] ? frontLegKick : backLegKick) * Mathf.Exp(-hitT * legDamping));
            }
            ComicKit.Place((RectTransform)target.transform, tx, targetY);

            Vector2 hand = target.PointOf(handInDog);
            for (int i = 0; i < cards.Length; i++)
            {
                float x = hand.x + cardLeft + i * cardSpreadX, y = hand.y + cardRaise - Mathf.Abs(i - 1) * cardArch;
                float angle = (i - 1) * cardFan + cardTilt;
                if (hit)
                {
                    float q = hitT;
                    x = hand.x + cardLeft + i * cardSpreadX + (flyX + flyXStep * i) * q;
                    y = hand.y + cardRaise - (flyUp - flyUpStep * i) * q + flyFall * q * q;
                    angle = (i - 1) * cardFan + q * (flySpin + i * flySpinStep);
                }
                var cardRect = (RectTransform)cards[i].transform;
                ComicKit.Place(cardRect, x, y);
                ComicKit.Turn(cardRect, angle);
            }

            Vector2 head = target.PointOf(headInDog);
            PlaceSweat(sweats[0], head + sweatOffsetA, ComicKit.Cl((hitT - sweatAtA) / sweatFade));
            PlaceSweat(sweats[1], head + sweatOffsetB, ComicKit.Cl((hitT - sweatAtB) / sweatFade));

            // 효과음 「왈!!」 + 터짐 풍선
            float sk = (bk - shoutDelay) / shoutTime;
            shoutBurst.enabled = sk > 0f;
            Vector2 jitter = bk < shoutShakeUntil
                ? new Vector2(Random.Range(-shoutShake, shoutShake), Random.Range(-shoutShake, shoutShake))
                : Vector2.zero;
            if (sk > 0f)
            {
                float pop = ComicKit.Back(sk, shoutOvershoot);
                shoutBurst.SetRadius(pop, pop);
                ComicKit.Place((RectTransform)shoutBurst.transform, _shoutBurstRest.x + jitter.x, _shoutBurstRest.y + jitter.y);
            }
            shoutWord.Apply(sk, jitter);

            // 「행동 카드 금지」 도장 — 맞는 쪽을 따라 움직인다
            ban.Apply((t - banAt) / banTime, new Vector2(tx + banOffsetX - ban.RestPosition.x, 0f));
        }

        public override IEnumerator PostRoutine(ComicContext context)
        {
            CardFxContext fx = context.Fx;
            if (fx.Layer != null && context.Target != null && context.Target.Anchor != null)
                fx.Layer.Bubble(context.Target.Anchor, BubbleText, bubbleRise);
            if (fx.Layer != null)
                fx.Layer.ShakeStage(postShake, false);
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
            for (int i = 0; i < waveCount; i++)
            {
                float w = _barkTime - i * waveGap;
                if (w <= 0f || w > waveLife)
                    continue;
                float q = w / waveLife;
                float r = waveRadius + ComicKit.Eo(q) * waveGrow;
                pen.Save();
                pen.Alpha = 1f - q * waveFade;
                pen.BeginPath();
                pen.Arc(_mouth.x, _mouth.y, r, -waveHalfAngle, waveHalfAngle);
                pen.Stroke(ComicKit.Ink, waveInk * (1f - q) + waveInkMin);
                pen.Stroke(waveLightColor, waveLight * (1f - q) + waveLightMin);
                pen.Restore();
            }
        }
    }
}
