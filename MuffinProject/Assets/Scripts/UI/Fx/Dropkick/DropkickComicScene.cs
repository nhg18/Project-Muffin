using System.Collections;
using Chapchu.Cards;
using Chapchu.Game.Effects;
using Chapchu.UI.Fx.Comic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Dropkick
{
    /// <summary>
    /// 드롭킥(카드 1005) 만화 컷인 장면 — 도움닫기 → 날아차기 → 퍽!! (시안 CI2.SC["드롭킥"]).
    /// 0 ~ 0.44 초 옆에서 본 컷: 맞는 쪽이 「!?」 하고 땀을 흘리는 사이, 쓴 쪽이 오른쪽에서 달려와 0.26 초에 뛰어올라 잔상을 남기며 두 발부터 날아든다.
    /// 0.44 초 맞는 순간: 햇살 무늬 · 집중선 위로 카드 그림(스티커)이 크게 들이받고 「퍽!!」 → 0.6 초 「-N」 (N = 카드 데이터 DamageEffect 양, 시안은 -25).
    /// 맞는 순간 마무리(시안 FINISH 드롭킥 — 맞은 쪽 자리에서 주황 · 흰 줄 빛 14)는 3 단계가 끝난 그 순간(시안 s3 의 fin)이라 4 단계 첫 줄에서 튼다.
    /// 4 단계 = 맞은 쪽이 쓰러지지 않았으면 0.55 초 뒤 쓴 쪽에 「으악!」 말풍선 (시안 FXA 드롭킥 s4 — 피해 숫자 · 「쓰러짐」 상태 글자는 부르는 쪽 몫)과
    /// 「반동 -N」 표 + 땀 (N = 카드 데이터 RecoilDamageEffect 양). 시안은 이 표를 3 단계 0.86 초에 늘 보였지만, 반동은 못 쓰러뜨렸을 때만이라
    /// 결과를 아는 4 단계로 옮겼다 (PM 결정 2026-11-15) — 모양 · 튀어나오기는 시안 그대로, 자리는 쓴 쪽 자리 위.
    /// 쓰러졌는지는 4 단계 결과 알림(<see cref="CardFxContext.OnHpChanged"/>)으로 안다 — 알림이 없으면 못 쓰러뜨린 것으로 본다 (시안 기본값).
    /// 연출 길이 3.26초 (3 단계 2.36초 + 4 단계 0.9초) — 카드 처리 시간(resolveSeconds)은 게임 화면에 연출을 붙일 때 맞춘다 (PM 결정 2026-11-15).
    /// </summary>
    public class DropkickComicScene : ComicScene
    {
        // 시안 FXA 드롭킥 s4: vbubble(u, "으악!")
        private const string OuchText = "으악!";
        // 시안 「-25」 · 「반동 -10」 꼴 — 숫자는 카드 데이터
        private const string DamageFormat = "-{0}";
        private const string RecoilFormat = "반동 -{0}";

        [Header("카드")]
        [Tooltip("이 카드 데이터 — 「-N」 은 DamageEffect 양, 「반동 -N」 은 RecoilDamageEffect 양.")]
        [SerializeField] private CardData card;

        [Header("그림 — 달려오기 (맞기 전, 그리는 순서)")]
        [Tooltip("달려오기 묶음 — 맞는 순간에 감춘다 (바탕 · 속도선 · 망점 · 두 강아지).")]
        [SerializeField] private CanvasGroup runUp;
        [Tooltip("속도선 (장면이 그린다, 색 · 진하기 = Graphic.color — 시안 흰색 0.6).")]
        [SerializeField] private ComicSketch speedLines;
        [Tooltip("맞는 쪽 강아지.")]
        [SerializeField] private ComicDog target;
        [Tooltip("맞는 쪽 땀 둘 (큰 것 · 작은 것).")]
        [SerializeField] private ComicSweat[] sweats = new ComicSweat[2];
        [Tooltip("「!?」 글자 (찍히기).")]
        [SerializeField] private ComicStamp surprise;
        [Tooltip("잔상 넷 — 가장 먼 것부터 (그리는 순서, 시안 g = 4 → 1). 진하기는 각자의 CanvasGroup.")]
        [SerializeField] private ComicDog[] afterimages = new ComicDog[4];
        [Tooltip("차는 쪽 강아지.")]
        [SerializeField] private ComicDog kicker;
        [Tooltip("차는 쪽 발밑 그림자 — 뛰어오르면 끈다 (시안 shadow: jump < 0.2).")]
        [SerializeField] private Graphic kickerShadow;

        [Header("그림 — 맞는 순간 (그리는 순서)")]
        [Tooltip("맞는 순간 묶음 — 그 전에는 투명 (스티커 테를 미리 만들게 켜 둔다).")]
        [SerializeField] private CanvasGroup impact;
        [Tooltip("햇살 무늬 (장면이 그린다, 맞닿은 곳이 가운데).")]
        [SerializeField] private ComicSketch sunburst;
        [Tooltip("집중선 (맞닿은 곳).")]
        [SerializeField] private ComicFocusLines focusLines;
        [Tooltip("카드 그림 스티커.")]
        [SerializeField] private ComicSticker sticker;
        [Tooltip("맞닿은 곳의 터짐 별 (흰색).")]
        [SerializeField] private ComicBurst burst;
        [Tooltip("「퍽!!」 글자 (찍히기).")]
        [SerializeField] private ComicStamp pow;
        [Tooltip("「-N」 찍히기.")]
        [SerializeField] private ComicStamp damage;
        [Tooltip("「-N」 글자 — 카드 데이터 피해 양을 넣는다.")]
        [SerializeField] private ComicWord damageText;
        [Tooltip("「반동 -N」 표 본 — 장면 안에서는 늘 꺼 두고, 4 단계에 게임 화면 위로 복제한다 (기울기 · 상자 · 글자, 시안 label). 상자는 자식 ComicSketch 하나 (장면이 그린다).")]
        [SerializeField] private RectTransform recoilLabel;
        [Tooltip("「반동 -N」 표 글자 — 상자 너비가 이 글자 폭을 따른다.")]
        [SerializeField] private TMP_Text recoilText;
        [Tooltip("「반동 -N」 옆 땀 본 — 표와 함께 4 단계에 복제한다 (표와의 거리는 장면 안 자리 그대로).")]
        [SerializeField] private ComicSweat recoilSweat;

        [Header("때 (초)")]
        [Tooltip("맞는 때 — 그림이 맞는 순간으로 바뀐다 (초, 시안 0.44).")]
        [SerializeField] private float impactAt = 0.44f;

        [Header("속도선 (시안 speed(t, 12, {n 46, dir 1, y0 150, y1 650}))")]
        [Tooltip("씨앗 (시안 12).")]
        [SerializeField] private int speedSeed = 12;
        [Tooltip("줄 수 (시안 46).")]
        [SerializeField] private int speedCount = 46;
        [Tooltip("줄이 놓이는 높이 범위 — 위 · 아래 (설계 px, 시안 150 · 650).")]
        [SerializeField] private Vector2 speedBand = new Vector2(150f, 650f);
        [Tooltip("줄 길이 — 최소 · 더하는 범위 (설계 px, 시안 120 + 0 ~ 420).")]
        [SerializeField] private Vector2 speedLength = new Vector2(120f, 420f);
        [Tooltip("줄 굵기 — 최소 · 더하는 범위 (설계 px, 시안 1 + R·R·7).")]
        [SerializeField] private Vector2 speedThickness = new Vector2(1f, 7f);
        [Tooltip("흐르는 빠르기 — 최소 · 더하는 범위 (설계 px/초, 시안 1400 + 0 ~ 1600).")]
        [SerializeField] private Vector2 speedVelocity = new Vector2(1400f, 1600f);
        [Tooltip("처음 자리 흩어짐 (설계 px, 시안 0 ~ 3000).")]
        [SerializeField] private float speedPhase = 3000f;
        [Tooltip("화면 밖으로 돌아 나가는 여유 (설계 px, 시안 900 — 왼쪽 밖 450 에서 시작).")]
        [SerializeField] private float speedWrap = 900f;
        [Tooltip("흐르는 쪽 (1 = 오른쪽으로, −1 = 왼쪽으로, 시안 dir 1).")]
        [SerializeField] private float speedDirection = 1f;

        [Header("맞는 쪽 (시안: (290, 700) 높이 400 왼쪽을 봄, 다리 앞 0.12 · 뒤 −0.08 × sm(t / 0.3))")]
        [Tooltip("다리가 벌어지는 시간 (초, 시안 0.3).")]
        [SerializeField] private float braceTime = 0.3f;
        [Tooltip("앞다리 벌림 (라디안, 시안 0.12).")]
        [SerializeField] private float braceFront = 0.12f;
        [Tooltip("뒷다리 벌림 (라디안, 시안 −0.08).")]
        [SerializeField] private float braceBack = -0.08f;
        [Tooltip("땀이 나타나기 시작하는 때 (초, 시안 0.18 · 0.26).")]
        [SerializeField] private Vector2 sweatAt = new Vector2(0.18f, 0.26f);
        [Tooltip("땀이 나타나는 시간 (초, 시안 0.1).")]
        [SerializeField] private float sweatFade = 0.1f;
        [Tooltip("「!?」 이 찍히기 시작하는 때 (초, 시안 0.16 — 큰 땀이 보일 때부터 그린다).")]
        [SerializeField] private float surpriseAt = 0.16f;
        [Tooltip("「!?」 이 찍히는 시간 (초, 시안 0.12).")]
        [SerializeField] private float surpriseTime = 0.12f;

        [Header("차는 쪽 (시안: 0.26 초 동안 1180 → 800 달리고, 0.18 초 동안 뛰어올라 330 날아든다)")]
        [Tooltip("달리는 시간 (초, 시안 0.26).")]
        [SerializeField] private float runTime = 0.26f;
        [Tooltip("뛰어오르는 시간 (초, 시안 0.18).")]
        [SerializeField] private float jumpTime = 0.18f;
        [Tooltip("달리기 시작 · 끝 x (설계 px, 시안 1180 · 800).")]
        [SerializeField] private Vector2 runX = new Vector2(1180f, 800f);
        [Tooltip("뛰어서 날아드는 거리 (설계 px, 시안 330).")]
        [SerializeField] private float jumpReach = 330f;
        [Tooltip("땅 y (설계 px, 시안 700).")]
        [SerializeField] private float groundY = 700f;
        [Tooltip("뛰는 높이 (설계 px, 시안 170 — sin(jump·π·0.75)).")]
        [SerializeField] private float jumpHeight = 170f;
        [Tooltip("뛰는 높이 곡선 — sin 에 곱하는 반 바퀴 비율 (시안 0.75).")]
        [SerializeField] private float jumpArc = 0.75f;
        [Tooltip("뛰며 뒤로 젖히는 기울기 (라디안, 시안 0.62).")]
        [SerializeField] private float jumpLean = 0.62f;
        [Tooltip("뛸 때 다리 뻗기 (라디안, 시안 1.05).")]
        [SerializeField] private float jumpLegs = 1.05f;
        [Tooltip("달릴 때 다리 빠르기 · 다리마다 어긋남 (시안 sin(t·34 + i·1.6)).")]
        [SerializeField] private Vector2 runLegSpeed = new Vector2(34f, 1.6f);
        [Tooltip("달릴 때 다리 폭 (라디안, 시안 0.55).")]
        [SerializeField] private float runLegSwing = 0.55f;
        [Tooltip("꼬리 각도 (라디안, 시안 tailA −0.4).")]
        [SerializeField] private float kickerTail = -0.4f;
        [Tooltip("이만큼 뛰어오르면 그림자를 끈다 (0 ~ 1, 시안 0.2).")]
        [SerializeField] private float shadowUntil = 0.2f;
        [Tooltip("잔상 사이 — 뒤로 · 아래로 (설계 px, 시안 70 · −10).")]
        [SerializeField] private Vector2 afterimageStep = new Vector2(70f, -10f);

        [Header("맞는 순간 (시안: 카드 그림 690 을 (640, 735) 에, 맞닿은 곳으로 1 + 0.2·exp(−9τ) 배 들이받음)")]
        [Tooltip("스티커 자리 — 그림 기준점이 놓일 곳 (설계 px, 시안 640, 735).")]
        [SerializeField] private Vector2 stickerAt = new Vector2(640f, 735f);
        [Tooltip("스티커 기준점 — 그림 px (시안 ax 300, ay 530).")]
        [SerializeField] private Vector2 stickerAnchor = new Vector2(300f, 530f);
        [Tooltip("스티커 그림 배율 (시안 그림 600 을 높이 690 으로).")]
        [SerializeField] private float stickerScale = 690f / 600f;
        [Tooltip("맞닿은 곳 — 그림 px (시안 기준점에서 −85, −110).")]
        [SerializeField] private Vector2 contactInArt = new Vector2(215f, 420f);
        [Tooltip("들이받을 때 더 커지는 만큼 (시안 0.2).")]
        [SerializeField] private float punch = 0.2f;
        [Tooltip("들이받은 크기가 돌아오는 빠르기 (시안 9).")]
        [SerializeField] private float punchDamping = 9f;
        [Tooltip("햇살 무늬가 도는 빠르기 (라디안/초, 시안 0.25).")]
        [SerializeField] private float sunburstSpin = 0.25f;
        [Tooltip("햇살 무늬 조각 수 (시안 22).")]
        [SerializeField] private int sunburstRays = 22;
        [Tooltip("햇살 무늬 반지름 (설계 px, 시안 2400).")]
        [SerializeField] private float sunburstRadius = 2400f;
        [Tooltip("햇살 무늬 색 — 홀수 조각 (시안 #ffd23a).")]
        [SerializeField] private Color sunburstOdd = new Color32(0xff, 0xd2, 0x3a, 0xff);
        [Tooltip("햇살 무늬 색 — 짝수 조각 (시안 #ffb02e).")]
        [SerializeField] private Color sunburstEven = new Color32(0xff, 0xb0, 0x2e, 0xff);
        [Tooltip("터짐 별이 퍼지는 시간 (초, 시안 0.3).")]
        [SerializeField] private float burstTime = 0.3f;
        [Tooltip("터짐 별 처음 반지름 (설계 px, 시안 60 · 45).")]
        [SerializeField] private Vector2 burstFrom = new Vector2(60f, 45f);
        [Tooltip("터짐 별 끝 반지름 — 터짐 별 오브젝트의 반지름과 같게 둔다 (설계 px, 시안 190 · 140).")]
        [SerializeField] private Vector2 burstTo = new Vector2(190f, 140f);
        [Tooltip("「퍽!!」 이 찍히는 시간 (초, 시안 0.16).")]
        [SerializeField] private float powTime = 0.16f;
        [Tooltip("「-N」 이 찍히기 시작하는 때 · 찍히는 시간 (초, 시안 0.6 · 0.16).")]
        [SerializeField] private Vector2 damageAt = new Vector2(0.6f, 0.16f);

        [Header("「반동 -N」 표 (시안 label(1070, 440, {size 44, rot 0.08, sc back(rk, 2.2)}) + 땀 — 4 단계에 쓴 쪽 자리 위)")]
        [Tooltip("표가 튀어나오는 시간 — 땀도 이 시간에 진해진다 (초, 시안 rk = (t − 0.86) / 0.16 의 0.16).")]
        [SerializeField] private float recoilPop = 0.16f;
        [Tooltip("튀어나올 때 지나치는 정도 (시안 back 2.2).")]
        [SerializeField] private float recoilOvershoot = 2.2f;
        [Tooltip("상자 너비 = 글자 폭 + 이만큼 (설계 px, 시안 34).")]
        [SerializeField] private float labelPadding = 34f;
        [Tooltip("상자 높이 / 글자 크기 (시안 1.35).")]
        [SerializeField] private float labelHeightRatio = 1.35f;
        [Tooltip("상자 그림자 어긋남 (설계 px, 시안 5 · 6).")]
        [SerializeField] private Vector2 labelShadowOffset = new Vector2(5f, 6f);
        [Tooltip("상자 그림자 색 (시안 rgba(0, 0, 0, .3)).")]
        [SerializeField] private Color labelShadow = new Color(0f, 0f, 0f, 0.3f);
        [Tooltip("상자 테 두께 (설계 px, 시안 5).")]
        [SerializeField] private float labelLine = 5f;
        [Tooltip("쓴 쪽 자리 가운데에서 표 가운데까지 (시안 px, 위로 + — 시안 4 단계엔 표가 없어 「으악!」 말풍선(70) 위에 둔다).")]
        [SerializeField] private Vector2 recoilOffset = new Vector2(0f, 140f);

        [Header("4 단계 (시안 FXA 드롭킥 s4 · FINISH 드롭킥)")]
        [Tooltip("맞은 쪽 줄 빛 수 (시안 streaks 14).")]
        [SerializeField] private int finishStreaks = 14;
        [Tooltip("맞은 쪽 줄 빛 색 (시안 #ff8a1f · #ffffff).")]
        [SerializeField] private Color[] finishColors = { new Color32(0xff, 0x8a, 0x1f, 0xff), Color.white };
        [Tooltip("「으악!」 말풍선까지 (초, 시안 550ms).")]
        [SerializeField] private float ouchDelay = 0.55f;
        [Tooltip("말풍선 뒤 기다림 (초, 시안 350ms).")]
        [SerializeField] private float postTail = 0.35f;

        private float _time;
        private float _labelWidth;
        private int _recoilAmount;
        private CardFxSeat _hitSeat;
        private bool _knockedOut;

        private void Awake()
        {
            if (runUp == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(runUp)} 가 비었다");
            if (speedLines == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(speedLines)} 가 비었다");
            if (target == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(target)} 가 비었다");
            if (surprise == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(surprise)} 가 비었다");
            if (kicker == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(kicker)} 가 비었다");
            if (kickerShadow == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(kickerShadow)} 가 비었다");
            if (impact == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(impact)} 가 비었다");
            if (sunburst == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(sunburst)} 가 비었다");
            if (focusLines == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(focusLines)} 가 비었다");
            if (sticker == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(sticker)} 가 비었다");
            if (burst == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(burst)} 가 비었다");
            if (pow == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(pow)} 가 비었다");
            if (card == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(card)} 가 비었다");
            if (damage == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(damage)} 가 비었다");
            if (damageText == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(damageText)} 가 비었다");
            if (recoilLabel == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(recoilLabel)} 가 비었다");
            else if (recoilLabel.GetComponentInChildren<ComicSketch>(true) == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(recoilLabel)} 아래에 상자(ComicSketch)가 없다");
            if (recoilText == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(recoilText)} 가 비었다");
            if (recoilSweat == null)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(recoilSweat)} 가 비었다");
            if (sweats.Length < 2)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(sweats)} 가 {sweats.Length} 개다 — 둘(큰 것 · 작은 것)이 있어야 한다");
            for (int i = 0; i < sweats.Length; i++)
            {
                if (sweats[i] == null)
                    Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(sweats)}[{i}] 가 비었다");
            }
            for (int i = 0; i < afterimages.Length; i++)
            {
                if (afterimages[i] == null)
                    Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(afterimages)}[{i}] 가 비었다");
            }
        }

        public override void Begin(ComicContext context)
        {
            string kickerKey = context.DogKeyOf(context.From);
            kicker.Key = kickerKey;
            foreach (ComicDog afterimage in afterimages)
                afterimage.Key = kickerKey;
            target.Key = context.TargetKey;
            speedLines.Painter = PaintSpeedLines;
            sunburst.Painter = PaintSunburst;

            int damageAmount = 0;
            _recoilAmount = 0;
            foreach (CardEffect effect in card.effects)
            {
                if (effect is DamageEffect hit)
                    damageAmount = hit.amount;
                else if (effect is RecoilDamageEffect recoil)
                    _recoilAmount = recoil.amount;
            }
            if (card.id != context.Fx.CardId)
                Debug.LogError($"[{nameof(DropkickComicScene)}] {name} 의 {nameof(card)} 는 카드 {card.id} 인데 낸 카드는 {context.Fx.CardId} 다");
            damageText.Text = string.Format(DamageFormat, damageAmount);
            recoilText.text = string.Format(RecoilFormat, _recoilAmount);
            _labelWidth = recoilText.GetPreferredValues(recoilText.text).x;
            recoilLabel.gameObject.SetActive(false);
            ComicKit.SetAlpha(recoilSweat, 0f);
            sunburst.Redraw();
        }

        public override void Draw(float t, ComicContext context)
        {
            _time = t;
            float imp = t - impactAt;
            bool hit = imp >= 0f;
            runUp.alpha = hit ? 0f : 1f;
            impact.alpha = hit ? 1f : 0f;
            if (hit)
                DrawImpact(t, imp);
            else
                DrawRunUp(t);
        }

        public override IEnumerator PostRoutine(ComicContext context)
        {
            CardFxContext fx = context.Fx;
            _hitSeat = context.Target;
            _knockedOut = false;
            fx.OnHpChanged += HandleHpChanged;
            try
            {
                // 시안 FINISH 드롭킥: 쓴 사람이 맞는 쪽이면 하지 않는다
                if (fx.Layer != null && _hitSeat != null && _hitSeat != context.From && _hitSeat.Anchor != null)
                    fx.Layer.Streaks(fx.Layer.CenterOf(_hitSeat.Anchor), finishStreaks, finishColors);
                for (float time = 0f; time < ouchDelay; time += Time.deltaTime)
                    yield return null;
                if (!_knockedOut && fx.Layer != null && context.From != null && context.From.Anchor != null)
                {
                    fx.Layer.Bubble(context.From.Anchor, OuchText);
                    if (_recoilAmount > 0)
                        ShowRecoil(fx.Layer, context.From.Anchor);
                }
                for (float time = 0f; time < postTail; time += Time.deltaTime)
                    yield return null;
            }
            finally
            {
                fx.OnHpChanged -= HandleHpChanged;
            }
        }

        private void HandleHpChanged(CardFxSeat seat, int before, int after)
        {
            if (seat == _hitSeat && after <= 0)
                _knockedOut = true;
        }

        // 옆에서 본 컷: 오른쪽에서 달려와 뒤로 젖히며 두 발부터 날아든다
        private void DrawRunUp(float t)
        {
            speedLines.Redraw();

            target.SetTime(t);
            float brace = ComicKit.Sm(t / braceTime);
            for (int i = 0; i < FxDog.Legs.Count; i++)
                target.SetLeg(i, (FxDog.LegFront[i] ? braceFront : braceBack) * brace);

            ComicKit.SetAlpha(sweats[0], ComicKit.Cl((t - sweatAt.x) / sweatFade));
            ComicKit.SetAlpha(sweats[1], ComicKit.Cl((t - sweatAt.y) / sweatFade));
            surprise.Apply(t > sweatAt.x ? (t - surpriseAt) / surpriseTime : 0f);

            float run = ComicKit.Cl(t / runTime), jump = ComicKit.Cl((t - runTime) / jumpTime);
            float x = ComicKit.Lerp(runX.x, runX.y, ComicKit.Eo(run)) - ComicKit.Eo(jump) * jumpReach;
            float y = groundY - Mathf.Sin(jump * Mathf.PI * jumpArc) * jumpHeight;
            float lean = jumpLean * ComicKit.Eo(jump);
            PoseKicker(kicker, t, jump, lean);
            ComicKit.Place((RectTransform)kicker.transform, x, y);
            kickerShadow.enabled = jump < shadowUntil;

            for (int i = 0; i < afterimages.Length; i++)
            {
                ComicDog afterimage = afterimages[i];
                afterimage.gameObject.SetActive(jump > 0f);
                if (jump <= 0f)
                    continue;
                // afterimages[0] = 가장 먼 것 (시안 g = 4)
                int g = afterimages.Length - i;
                PoseKicker(afterimage, t, jump, lean);
                ComicKit.Place((RectTransform)afterimage.transform, x + g * afterimageStep.x, y + g * afterimageStep.y);
            }
        }

        // 맞는 순간: 카드 그림을 크게, 맞닿은 곳으로 모이는 선
        private void DrawImpact(float t, float imp)
        {
            Vector2 contact = stickerAt + (contactInArt - stickerAnchor) * stickerScale;
            ComicKit.Turn((RectTransform)sunburst.transform, imp * sunburstSpin);
            focusLines.SetTime(t);

            float z = 1f + punch * Mathf.Exp(-imp * punchDamping);
            var stickerRect = (RectTransform)sticker.transform;
            Vector2 at = contact + (stickerAt - contact) * z;
            ComicKit.Place(stickerRect, at.x, at.y);
            stickerRect.localScale = new Vector3(z, z, 1f);

            bool bursting = imp < burstTime;
            burst.enabled = bursting;
            if (bursting)
            {
                float e = imp / burstTime;
                float grow = ComicKit.Eo(e);
                burst.SetRadius(ComicKit.Lerp(burstFrom.x, burstTo.x, grow) / burstTo.x, ComicKit.Lerp(burstFrom.y, burstTo.y, grow) / burstTo.y);
                ComicKit.SetAlpha(burst, 1f - e * e);
            }

            pow.Apply(imp / powTime);
            damage.Apply((t - damageAt.x) / damageAt.y);
        }

        // 「반동 -N」 표 + 땀을 게임 화면(FxLayer) 쓴 쪽 자리 위에 복제해 시안 label 처럼 튀어나오게 — 「으악!」 말풍선과 같이 떠 있다 사라진다
        private void ShowRecoil(FxLayer layer, RectTransform seat)
        {
            float scale = layer.PixelScale;
            Vector2 at = layer.CenterOf(seat) + recoilOffset * scale;
            Vector2 labelAt = ComicKit.PositionOf(recoilLabel), sweatAt = ComicKit.PositionOf(recoilSweat.rectTransform);
            RectTransform label = Spawn(recoilLabel, layer, at);
            RectTransform sweat = Spawn(recoilSweat.rectTransform, layer, at + new Vector2(sweatAt.x - labelAt.x, labelAt.y - sweatAt.y) * scale);
            sweat.localScale = recoilSweat.rectTransform.localScale * scale;
            ComicSketch box = label.GetComponentInChildren<ComicSketch>();
            box.Painter = PaintRecoilBox;
            box.Redraw();
            ComicSweat drop = sweat.GetComponent<ComicSweat>();
            ComicKit.SetAlpha(drop, 0f);
            label.localScale = Vector3.zero;
            CanvasGroup group = label.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            // 수명 · 흐려지는 시간은 「으악!」 말풍선(FxLayer)을 따른다
            float recoilLife = layer.BubbleSeconds, recoilFade = recoilLife * (1f - layer.BubbleFadeFrom);
            DOVirtual.Float(0f, recoilLife, recoilLife, time =>
            {
                float k = time / recoilPop, fade = 1f - ComicKit.Cl((time - (recoilLife - recoilFade)) / recoilFade);
                float pop = ComicKit.Back(k, recoilOvershoot) * scale;
                label.localScale = new Vector3(pop, pop, 1f);
                group.alpha = fade;
                ComicKit.SetAlpha(drop, ComicKit.Cl(k) * fade);
            }).SetEase(Ease.Linear).SetLink(label.gameObject).OnComplete(() =>
            {
                Destroy(label.gameObject);
                Destroy(sweat.gameObject);
            });
        }

        // 장면 안의 본을 게임 화면 위에 복제한다 — 자리 = 레이어 좌표 (가운데 기준)
        private static RectTransform Spawn(RectTransform source, FxLayer layer, Vector2 position)
        {
            RectTransform copy = Instantiate(source, layer.transform, false);
            copy.anchorMin = copy.anchorMax = new Vector2(0.5f, 0.5f);
            copy.anchoredPosition = position;
            copy.gameObject.SetActive(true);
            return copy;
        }

        private void PoseKicker(ComicDog dog, float t, float jump, float lean)
        {
            dog.SetTime(t);
            dog.Rotation = lean;
            dog.TailAngle = kickerTail;
            for (int i = 0; i < FxDog.Legs.Count; i++)
                dog.SetLeg(i, jump > 0f ? jumpLegs * ComicKit.Eo(jump) : Mathf.Sin(t * runLegSpeed.x + i * runLegSpeed.y) * runLegSwing);
        }

        // 시안 speed(t, seed, {n, dir, y0, y1}): 가로로 흐르는 가는 쐐기
        private void PaintSpeedLines(ComicPen pen)
        {
            var random = new ComicRandom(speedSeed);
            float span = ComicKit.Width + speedWrap;
            pen.BeginPath();
            for (int i = 0; i < speedCount; i++)
            {
                float y = ComicKit.Lerp(speedBand.x, speedBand.y, random.Next());
                float length = speedLength.x + random.Next() * speedLength.y;
                float thickness = speedThickness.x + random.Next() * random.Next() * speedThickness.y;
                float velocity = (speedVelocity.x + random.Next() * speedVelocity.y) * speedDirection;
                float phase = random.Next() * speedPhase;
                float x = ((phase + _time * velocity) % span + span) % span - speedWrap / 2f;
                pen.MoveTo(x, y - thickness / 2f);
                pen.LineTo(x - length * speedDirection, y);
                pen.LineTo(x, y + thickness / 2f);
                pen.ClosePath();
            }
            pen.Fill(Color.white);
        }

        // 시안 sunburst(x, y, n, a0, c1, c2): 가운데에서 번갈아 칠한 부채꼴 (도는 것은 RectTransform)
        private void PaintSunburst(ComicPen pen)
        {
            for (int i = 0; i < sunburstRays; i++)
            {
                pen.BeginPath();
                pen.MoveTo(0f, 0f);
                pen.Arc(0f, 0f, sunburstRadius, (float)i / sunburstRays * ComicKit.Tau, (float)(i + 1) / sunburstRays * ComicKit.Tau);
                pen.ClosePath();
                pen.Fill(i % 2 == 1 ? sunburstOdd : sunburstEven);
            }
        }

        // 시안 label: 그림자 판 + 흰 판 + 먹 테 (글자는 TMP)
        private void PaintRecoilBox(ComicPen pen)
        {
            float w = _labelWidth + labelPadding, h = recoilText.fontSize * labelHeightRatio;
            pen.BeginPath();
            pen.Rect(-w / 2f + labelShadowOffset.x, -h / 2f + labelShadowOffset.y, w, h);
            pen.Fill(labelShadow);
            pen.BeginPath();
            pen.Rect(-w / 2f, -h / 2f, w, h);
            pen.Fill(Color.white);
            pen.Stroke(ComicKit.Ink, labelLine);
        }
    }
}
