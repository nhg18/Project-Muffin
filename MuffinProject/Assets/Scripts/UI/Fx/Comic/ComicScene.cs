using System;
using System.Collections;
using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 만화 컷인의 카드 한 장 장면 — 시안 CI2.SC[카드 이름] = {len, accent, deep, light, bgCol, title, tag, titleAt, hits, draw(t, P)} 하나.
    /// 값(len 등)은 이 컴포넌트의 Inspector, 그림은 이 오브젝트 아래 자식들(도구 컴포넌트), 움직임은 <see cref="Draw"/>.
    ///
    /// <para><b>장면 하나 더하기</b> (틀 · 도구 코드는 고치지 않는다)</para>
    /// <list type="number">
    /// <item>그림: 시안 SPA 의 진짜 그림만 PNG 로 <c>Sprites/CardFx/&lt;기능&gt;/</c> 에 (스프라이트, 밉맵 끔). 빛 · 선 · 상자 · 글자는 도구로 그린다.
    /// 크기는 시안 그림 그대로 둔다 — <see cref="ComicSticker"/> 의 기준점(시안 stk 의 ax · ay)이 그 px 이다.</item>
    /// <item>코드: <c>Scripts/UI/Fx/&lt;기능&gt;/&lt;이름&gt;ComicScene.cs</c> — 이 클래스를 상속하고, 자식 도구를 [SerializeField] 로 들고,
    /// <see cref="Draw"/> 에 시안 draw(t, P) 를 줄 단위로 옮긴다 (시안 c.translate(x, y) → <see cref="ComicKit.Place"/>, c.rotate → <see cref="ComicKit.Turn"/>,
    /// stamp(…, k) → <see cref="ComicStamp.Apply"/>, 도구 값 → 도구의 속성 · SetTime). 장면에만 있는 자유 모양(c.arc · c.fill 등)은 <see cref="ComicSketch"/>.
    /// 시안 값(시각 · 자리 · 크기 · 색)은 [SerializeField] + [Tooltip](시안 값 표기)으로 이름을 붙인다 — 연출 시험장 Inspector 에서 바꿀 수 있게.</item>
    /// <item>프리팹: <c>Prefab/UI/CardFx/Comic/ComicCutIn.prefab</c> 의 프리팹 변형을 <c>Resources/CardFx/Card_&lt;카드 ID&gt;.prefab</c> 으로 만들고,
    /// Stage 아래에 장면 오브젝트(화면 전체로 늘인 RectTransform + 이 컴포넌트)를 하나 둔다. 장면 자식은 앵커 · 피벗을 왼쪽 위(0, 1)에 둬서
    /// anchoredPosition = (시안 x, −시안 y) 가 되게 한다. 시안 그리는 순서 = 자식 순서. 되풀이되는 것은 프리팹(ComicCard · ComicDog · ComicWord)을 복제한다.
    /// 뼈대의 UIRing 은 CanvasRenderer 를 스스로 붙이지 않는다 — 같이 붙여야 보인다.
    /// 프리팹은 다 찍힌 모습(찍히기 도구의 자리 · 크기 · 기울기)을 저장해 둔다 — <see cref="ComicStamp"/> 가 그 모습에서 출발한다.</item>
    /// <item>4 단계 장식(시안 FXA s4 — 말풍선 · 판 흔들기)은 <see cref="PostRoutine"/> 에서 <see cref="CardFxContext.Layer"/> 로.</item>
    /// </list>
    ///
    /// <para><b>시안 → Unity</b>: P.from · tg · tk → <see cref="ComicContext"/> 의 From · Target · TargetKey (others · avs · hit 는 쓰는 장면이 생길 때 더한다).
    /// focus → <see cref="ComicFocusLines"/>, speed → <see cref="ComicSpeedLines"/>, tone/halftone → <see cref="ComicHalftone"/>, bgGrad → <see cref="ComicGradient"/>,
    /// burst → <see cref="ComicBurst"/>, bubble → <see cref="ComicBubble"/>, glow → <see cref="ComicGlow"/>, sparkle → <see cref="ComicSparkle"/>, sweat → <see cref="ComicSweat"/>,
    /// word + stamp → <see cref="ComicWord"/> + <see cref="ComicStamp"/>, stk → <see cref="ComicSticker"/>, dog · dogPt → <see cref="ComicDog"/> · <see cref="ComicDog.PointOf"/>,
    /// card → <see cref="ComicCard"/>, rr → <see cref="ComicPen.RoundRect"/>. 「lighter」 로 그린 것은 머티리얼 FxAdditive. 소리(sfx)는 넣지 않는다.
    /// 제목 · 한마디 · 레터박스 · 종이결 · 들어오기/나가기 · 확대 · 흔들림은 틀(<see cref="ComicCutInFx"/>)이 한다.</para>
    /// </summary>
    public abstract class ComicScene : MonoBehaviour
    {
        [Header("장면 값 (시안 SC)")]
        [Tooltip("장면 길이 (초, 시안 len). 3 단계 길이 = 이것 + 0.16 초 (시안 SPECIAL_MS + 80ms 기다림).")]
        [SerializeField] private float length = 2f;
        [Tooltip("장면 색 — 제목 안쪽 테 · 띠 · 한마디 색 줄 (시안 accent).")]
        [SerializeField] private Color accent = Color.white;
        [Tooltip("진한 색 — 제목 입체 · 띠 그림자 (시안 deep).")]
        [SerializeField] private Color deep = Color.black;
        [Tooltip("밝은 색 (시안 light — 제목 면 아래 색이지만 시안 캔버스에서 보이지 않는다).")]
        [SerializeField] private Color lightColor = Color.white;
        [Tooltip("장면 밑바탕 색 — 장면 그림 뒤 화면 전체 (시안 bgCol).")]
        [SerializeField] private Color background = Color.black;
        [Tooltip("제목 (시안 title). 비우면 카드 이름.")]
        [SerializeField] private string title;
        [Tooltip("제목 위 한마디 (시안 tag).")]
        [SerializeField] private string tagline;
        [Tooltip("제목이 나오는 때 (초, 시안 titleAt).")]
        [SerializeField] private float titleAt = 1f;
        [Tooltip("판 흔들림 (x = 시각 초, y = 세기 — 시안 hits).")]
        [SerializeField] private Vector2[] hits = Array.Empty<Vector2>();

        /// <summary>장면 길이 (초, 시안 len).</summary>
        public float Length => length;

        /// <summary>장면 색 (시안 accent).</summary>
        public Color Accent => accent;

        /// <summary>진한 색 (시안 deep).</summary>
        public Color Deep => deep;

        /// <summary>밝은 색 (시안 light).</summary>
        public Color Light => lightColor;

        /// <summary>장면 밑바탕 색 (시안 bgCol).</summary>
        public Color Background => background;

        /// <summary>제목 (시안 title). 비어 있으면 컷인이 카드 이름을 쓴다.</summary>
        public string Title => title;

        /// <summary>제목 위 한마디 (시안 tag).</summary>
        public string Tagline => tagline;

        /// <summary>제목이 나오는 때 (초, 시안 titleAt).</summary>
        public float TitleAt => titleAt;

        /// <summary>판 흔들림 (시각, 세기) (시안 hits).</summary>
        public Vector2[] Hits => hits;

        /// <summary>장면을 시작하기 전에 한 번 — 누가 누구인지(강아지 키 · 얼굴)를 그림에 넣는다. 그림 만들기(스티커)는 틀이 기다려 준다.</summary>
        public virtual void Begin(ComicContext context)
        {
        }

        /// <summary>장면 시각 t (초, 0 ~ <see cref="Length"/>) 의 모습으로 둔다 (시안 draw(t, P)). 매 프레임 부른다.</summary>
        public abstract void Draw(float t, ComicContext context);

        /// <summary>4 단계의 카드만의 장식 (시안 FXA s4). 컷인은 이미 사라졌다. 기본은 바로 끝난다.</summary>
        public virtual IEnumerator PostRoutine(ComicContext context)
        {
            yield break;
        }
    }
}
