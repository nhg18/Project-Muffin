using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.Game;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 카드 한 장 (10-ui.md 2-1 「3D 층」 · 「손패 · 카드 (3D 층)」, 시안 통합 UI v3.20 카드 4번). 앞면(종이 · 틀 · 그림 · 이름 · 설명)과 뒷면은 서로 반대를 보는 면이고
    /// 셰이더가 뒷면을 컬링하므로, 루트를 Y 180° 돌리면 그대로 뒤집기다. 종류별 틀 머티리얼은 프리팹(행동 · 카운터 · 함정)이 갖고, 여기서는 CardData 의 이름 · 그림 · 설명 · 등급만 입힌다.
    /// 생김새: 흰 종이 테 + 잉크 선 한 줄 + 아래로 퍼지는 그림자 + 등급 수만큼 낙서 별 (종이 셰이더 Chapchu/Card Paper).
    /// 등급 연출 (10-ui.md 2-1 「카드 등급 연출」, 시안 「카드 반짝 · 빛 반사 v3」 1안 잔잔한 윤기): 놓인 자리(SetGradeFx)에 따라 — 보여 줄 때 · 냈을 때는 표면 빛(고급~) · 글로우(희귀~) ·
    /// 반짝이(개판) · 낙서 별을 모두, 손패에서는 글로우만 은은하게, 그 밖에는 아무것도. 앞면이 카메라를 볼 때만. 표면 빛은 종이 · 그림 칸 셰이더가, 글로우 · 반짝이는
    /// 그림자 판을 복제한 판(Chapchu/Card Soft 의 _Mode)이 그린다 — 플레이 중에만 만든다(빌더 · 확인 스크립트가 프리팹에 저장하지 않게).
    /// 손패 상태 (시안 v3.4 · v3.5): 포인터 아래 카드는 그림자가 커지고, 고른 카드는 노란 형광펜 + 더 큰 그림자, 지금 낼 수 없는 카드는 흐려진다. 크기는 HandView 가 정한다.
    /// 루트의 SortingGroup 이 카드 한 장을 묶어 뒤 카드부터 그린다 — 반투명 가장자리 · 그림자가 뒤 카드 위에 매끈하게 섞인다.
    /// 포인터 입력은 받지 않는다 — 손패 입력은 HandInput 이 한 곳에서 읽고 HandView 가 자리로 판정한다 (10-ui.md 4절). 콜라이더는 뒤에 있는 것(함정 칸 · 덱)의 탭을 가리는 데만 쓴다.
    /// 어느 장인지(InstanceId)와 무슨 카드인지(CardId · Type)만 기억한다.
    /// uGUI 판 HandCardView · HandCard_* 프리팹은 CardFxTestBench 가 아직 쓴다 — 카드 이펙트가 3D 층으로 옮겨오면 지운다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CardView : MonoBehaviour
    {
        private const float CanvasPerUnit = 100f;   // 셰이더 길이 u = 캔버스 px, 1 유닛 = 100 (10-ui.md 2-1 「3D 층 구현」)
        private const int MaxSparks = 11;            // Chapchu/Card Soft 의 MAX_SPARKS 와 같게

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int HasTexId = Shader.PropertyToID("_HasTex");
        private static readonly int ArtRectId = Shader.PropertyToID("_ArtRect");
        private static readonly int TypeColorId = Shader.PropertyToID("_TypeColor");
        private static readonly int GradeId = Shader.PropertyToID("_Grade");
        private static readonly int MarkId = Shader.PropertyToID("_Mark");
        private static readonly int FilterId = Shader.PropertyToID("_Filter");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int BlurId = Shader.PropertyToID("_Blur");
        private static readonly int StarShowId = Shader.PropertyToID("_StarShow");
        private static readonly int PaperId = Shader.PropertyToID("_Paper");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int HalfId = Shader.PropertyToID("_Half");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int Color2Id = Shader.PropertyToID("_Color2");
        private static readonly int Color3Id = Shader.PropertyToID("_Color3");
        private static readonly int GradId = Shader.PropertyToID("_Grad");
        private static readonly int SheenId = Shader.PropertyToID("_Sheen");
        private static readonly int SheenLineId = Shader.PropertyToID("_SheenLine");
        private static readonly int SheenAId = Shader.PropertyToID("_SheenA");
        private static readonly int SheenBId = Shader.PropertyToID("_SheenB");
        private static readonly int SheenCId = Shader.PropertyToID("_SheenC");
        private static readonly int SheenWhiteId = Shader.PropertyToID("_SheenWhite");
        private static readonly int SparkCountId = Shader.PropertyToID("_SparkCount");
        private static readonly int SparkAId = Shader.PropertyToID("_SparkA");
        private static readonly int SparkBId = Shader.PropertyToID("_SparkB");
        private static readonly int SparkCId = Shader.PropertyToID("_SparkC");

        // 종이 테의 네 변 — 시작 모서리 원 가운데의 부호 · 바깥 방향 (PaperEdgePoint, 위 변부터 시계 방향)
        private static readonly Vector2[] EdgeCornerSigns = { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) };
        private static readonly Vector2[] EdgeNormals = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("그림이 아직 없는 카드에만 켜는 「그림 준비 중」 글자 (시안 v3.21 도감 .art.none).")]
        [SerializeField] private TMP_Text pendingText;
        [Tooltip("앞면 종이 (Chapchu/Card Paper) — 틀 · 잉크 선 · 형광펜 · 등급 별을 그린다.")]
        [SerializeField] private MeshRenderer frameRenderer;
        [Tooltip("그림 칸 (Chapchu/Card Art). 프리팹에서의 크기가 그림 칸이고, 그림 전체가 칸 안에 들어오게(남는 쪽은 흰 바탕) 입힌다. 그림이 없는 카드는 「그림 준비 중」.")]
        [SerializeField] private MeshRenderer artRenderer;
        [Tooltip("뒷면 종이 — 흐림만 같이 받는다.")]
        [SerializeField] private MeshRenderer backRenderer;
        [Tooltip("그림자 (Chapchu/Card Soft). 쉴 때 값은 그림자 머티리얼에 있고, 포인터 아래 · 고른 카드의 값은 아래.")]
        [SerializeField] private MeshRenderer shadowRenderer;
        [Tooltip("카드 종류 색 — 「그림 준비 중」 칸의 바탕 · 점선 · 발바닥 (시안 --tc).")]
        [SerializeField] private Color typeColor = new Color32(0x00, 0x9D, 0xFF, 0xFF);

        [Header("이름 (시안 .card .nm)")]
        [Tooltip("이름이 이 글자 수보다 길면 작은 크기로 (시안 .nm.long — 4 자보다 길 때).")]
        [SerializeField] private int longNameLength = 4;
        [Tooltip("긴 이름의 글자 크기 (TMP — 시안 11.5 px, size-adjust 는 글꼴 에셋 Scale). 짧은 이름은 프리팹의 이름 글자 크기.")]
        [SerializeField] private float longNameFontSize = 2.363f;

        [Header("그림자 (시안 .card::before box-shadow) — 쉴 때는 그림자 머티리얼 값")]
        [Tooltip("포인터 아래 카드 (시안 .card.hov 0 15px 22px .36).")]
        [SerializeField] private ShadowLook hoverShadow = new ShadowLook { drop = 30.73f, blur = 45.2f, alpha = 0.36f };
        [Tooltip("고른 카드 · 놓으면 사용되는 카드 (시안 .card.sel · .armed 0 17px 24px .34).")]
        [SerializeField] private ShadowLook markedShadow = new ShadowLook { drop = 34.83f, blur = 49.31f, alpha = 0.34f };
        [Tooltip("그림자가 바뀌는 초 (시안 transition box-shadow .2s).")]
        [SerializeField] private float shadowDuration = 0.2f;

        [Header("흐림 — 지금 낼 수 없는 손패 (시안 v3.5 .card.hand.dim)")]
        [Tooltip("채도 (시안 saturate .15).")]
        [SerializeField, Range(0f, 1f)] private float dimSaturate = 0.15f;
        [Tooltip("밝기 (시안 brightness .68).")]
        [SerializeField, Range(0f, 1f)] private float dimBrightness = 0.68f;
        [Tooltip("대비 (시안 contrast .8).")]
        [SerializeField, Range(0f, 1f)] private float dimContrast = 0.8f;
        [Tooltip("흐려지는 초 (시안 transition filter .2s).")]
        [SerializeField] private float dimDuration = 0.2f;

        [Header("등급 연출 — 10-ui.md 2-1 「카드 등급 연출」 (시안 카드 반짝 · 빛 반사 v3 1안). 배열은 고급 · 희귀 · 개판 순")]
        [Tooltip("효과가 켜지는 초 (보여 주는 자리 · 낸 자리에 앞면이 선 뒤). 시안 0.3.")]
        [SerializeField] private float fxFadeIn = 0.3f;
        [Tooltip("효과가 꺼지는 초 (떠나기 시작하면). 시안 0.18 — 떠나는 카드에 효과가 끌려가지 않게 짧게.")]
        [SerializeField] private float fxFadeOut = 0.18f;
        [Tooltip("앞면이 카메라를 이만큼 마주 봐야(정면 1) 효과가 있다 — 뒤집히는 중 · 뒷면이면 없다.")]
        [SerializeField, Range(0f, 1f)] private float fxFacing = 0.5f;
        [Tooltip("흐린 손패(SetDimmed)의 효과 세기 배율 — 1 − 0.8 × 0.35 (10-ui.md 2-1).")]
        [SerializeField, Range(0f, 1f)] private float fxDimmedStrength = 0.72f;

        [Header("등급 연출 — 표면 빛 (앞면을 비스듬히 지나가는 빛 띠, 고급부터)")]
        [Tooltip("띠가 다시 지나가는 주기 초 (고급 · 희귀 · 개판). 효과가 켜진 순간부터 센다.")]
        [SerializeField] private Vector3 sheenPeriod = new Vector3(4.6f, 4.2f, 3.6f);
        [Tooltip("띠가 앞면을 한 번 건너는 초.")]
        [SerializeField] private float sheenSweep = 1.5f;
        [Tooltip("효과가 켜진 뒤 첫 띠까지 초.")]
        [SerializeField] private float sheenDelay = 0.12f;
        [Tooltip("색 띠의 반폭 u (고급 · 희귀 · 개판).")]
        [SerializeField] private Vector3 sheenHalfWidth = new Vector3(70f, 78f, 88f);
        [Tooltip("띠가 가는 방향 — 오른쪽에서 아래로 도는 각 (도). 시안 0.44 rad.")]
        [SerializeField] private float sheenAngle = 25.21f;
        [Tooltip("곱하기 색 띠 — 고급 (rgb, 가운데 알파). 양 옆 .25 · .75 마디는 알파 × 0.3.")]
        [SerializeField] private Color sheenColorHigh = new Color(1f, 0.871f, 0.549f, 0.6f);
        [Tooltip("곱하기 색 띠 — 희귀.")]
        [SerializeField] private Color sheenColorRare = new Color(0.804f, 0.706f, 1f, 0.6f);
        [Tooltip("고급 · 희귀 색 띠의 양 옆 .25 · .75 마디 알파 배율 (가운데 알파 × 이 값).")]
        [SerializeField, Range(0f, 1f)] private float sheenSideAlpha = 0.3f;
        [Tooltip("곱하기 색 띠 — 개판의 .25 · .5 · .75 마디 (금 · 분홍 · 하늘).")]
        [SerializeField] private Color[] sheenColorsTop =
        {
            new Color(1f, 0.839f, 0.431f, 0.5f), new Color(1f, 0.588f, 0.784f, 0.55f), new Color(0.471f, 0.804f, 1f, 0.5f),
        };
        [Tooltip("가운데 흰 띠 — 폭은 색 띠의 이 배, 가운데 알파.")]
        [SerializeField] private Vector2 sheenWhite = new Vector2(0.42f, 0.6f);

        [Header("등급 연출 — 글로우 (카드 뒤 흐린 빛, 희귀부터)")]
        [Tooltip("손패에서의 글로우 세기 — 보여 줄 때 · 냈을 때가 1.")]
        [SerializeField, Range(0f, 1f)] private float glowInHand = 0.5f;
        [Tooltip("글로우가 밝아지는 · 어두워지는 초.")]
        [SerializeField] private Vector2 glowFade = new Vector2(0.3f, 0.25f);
        [Tooltip("희귀 글로우 색 (시안 #9a78ff).")]
        [SerializeField] private Color glowColorRare = new Color32(0x9A, 0x78, 0xFF, 0xFF);
        [Tooltip("개판 글로우 — 천천히 도는 그라디언트 세 색 (시안 #ffcf3a · #ff78b4 · #62cfff).")]
        [SerializeField] private Color[] glowColorsTop = { new Color32(0xFF, 0xCF, 0x3A, 0xFF), new Color32(0xFF, 0x78, 0xB4, 0xFF), new Color32(0x62, 0xCF, 0xFF, 0xFF) };
        [Tooltip("개판 그라디언트가 도는 속도 (도 / 초).")]
        [SerializeField] private float glowTurnSpeed = 36f;
        [Tooltip("불투명도 — 숨이 가장 낮을 때 · 높을 때 (희귀).")]
        [SerializeField] private Vector2 glowAlphaRare = new Vector2(0.45f, 0.75f);
        [Tooltip("불투명도 — 숨이 가장 낮을 때 · 높을 때 (개판).")]
        [SerializeField] private Vector2 glowAlphaTop = new Vector2(0.7f, 0.95f);
        [Tooltip("숨 쉬는 속도 (rad / 초).")]
        [SerializeField] private float glowBreathSpeed = 1.7f;
        [Tooltip("숨이 가장 높을 때 커지는 비율.")]
        [SerializeField] private float glowBreathScale = 0.012f;
        [Tooltip("카드마다 숨 위상이 흩어지는 폭 (초) — 카드마다 0 ~ 이 값만큼 숨이 앞서 간다.")]
        [SerializeField] private float glowBreathSpread = 10f;
        [Tooltip("글로우 판을 그림자 판에서 민 로컬 z (유닛, 음수가 카메라 쪽).")]
        [SerializeField] private float glowDepth = -0.001f;
        [Tooltip("종이보다 사방으로 큰 u · 모서리 반지름 u · CSS 흐림 반지름 u (시안 inset −4 · 18 · blur(13px) = 흐림 반지름 26).")]
        [SerializeField] private Vector3 glowShape = new Vector3(4f, 18f, 26f);

        [Header("등급 연출 — 반짝이 (카드 둘레, 개판)")]
        [Tooltip("초당 새로 피는 수 · 동시에 있는 최대 수 (최대 11 — 셰이더 한도).")]
        [SerializeField] private Vector2 sparkRate = new Vector2(6f, 11f);
        [Tooltip("하나가 피었다 지는 초 (최소 · 최대).")]
        [SerializeField] private Vector2 sparkLife = new Vector2(0.9f, 1.6f);
        [Tooltip("종이 테에서 바깥으로 떨어진 u (최소 · 최대, 음수는 안쪽).")]
        [SerializeField] private Vector2 sparkDistance = new Vector2(-5f, 26f);
        [Tooltip("네 갈래 별의 반지름 u (최소 · 최대).")]
        [SerializeField] private Vector2 sparkStarRadius = new Vector2(6f, 13f);
        [Tooltip("빛 알갱이의 반지름 u (최소 · 최대).")]
        [SerializeField] private Vector2 sparkDotRadius = new Vector2(2f, 3.2f);
        [Tooltip("빛 알갱이가 나올 확률 (나머지는 네 갈래 별).")]
        [SerializeField, Range(0f, 1f)] private float sparkDotChance = 0.3f;
        [Tooltip("반짝이 색 — 하나마다 무작위 (시안 hsl 45 · 45 · 330 · 195, 명도 60 %).")]
        [SerializeField] private Color[] sparkColors =
        {
            new Color(1f, 0.8f, 0.2f), new Color(1f, 0.8f, 0.2f), new Color(1f, 0.2f, 0.6f), new Color(0.2f, 0.8f, 1f),
        };
        [Tooltip("반짝이 판의 크기 — 그림자 판의 이 배 (둘레 밖 번짐이 잘리지 않게).")]
        [SerializeField] private float sparkQuadScale = 1.25f;
        [Tooltip("반짝이 판을 그림자 판에서 민 로컬 z (유닛, 음수가 카메라 쪽).")]
        [SerializeField] private float sparkDepth = -0.03f;
        [Tooltip("별이 튀어나오며 커지는 구간 — 수명의 이 비율까지 (그 뒤 줄어든다). 가장 클 때 십자 빛줄기.")]
        [SerializeField, Range(0.01f, 0.99f)] private float sparkPop = 0.3f;
        [Tooltip("십자 빛줄기가 보이는 폭 — 가장 클 때에서 수명 비율로 이만큼 앞뒤.")]
        [SerializeField] private float sparkFlareWidth = 0.22f;
        [Tooltip("한 수명 동안 도는 각 (rad).")]
        [SerializeField] private float sparkSpin = 1.3f;

        private BoxCollider _collider;
        private MaterialPropertyBlock _block;
        private bool _hovered;
        private bool _marked;
        private bool _dimmed;
        private float _dim;           // 0 맑음 ~ 1 다 흐림 — 넘어가는 중의 값
        private ShadowLook _shadow;   // 지금 그림자 — 넘어가는 중의 값
        private Tween _shadowTween;
        private Tween _dimTween;
        private string _description; // 카드 문구 원문 — 흐림이 키워드 색을 다시 칠할 때 쓴다
        private bool _hasKeywords;

        // 등급 연출
        private int _grade;              // 0 일반 · 1 고급 · 2 희귀 · 3 개판
        private GradeFx _fxState;        // 놓인 자리
        private bool _fxFull;            // 지금 보여 주기 · 냈을 때 효과가 켜져 있다 (앞면이 서 있다)
        private float _fxSince;          // 그 효과가 켜진 시각 — 표면 빛 첫 띠의 기준
        private float _fxOn;             // 표면 빛 · 반짝이 · 별의 세기 0 ~ 1
        private float _glow;             // 글로우 세기 0 ~ 1 (손패는 glowInHand)
        private float _glowGoal;         // 지금 향하는 글로우 세기
        private float _glowStep;         // 그 전환에서 초당 움직이는 양 — 차이를 glowFade 초에 지나게
        private Camera _view;            // 앞면이 마주 보는지 재는 카메라 (Awake 에서 한 번)
        private float _fxPhase;          // 카드마다 다른 숨 · 그라디언트 출발점
        private Vector2 _paperHalf;      // 종이 반폭 · 반높이 u
        private float _paperRadius;
        private Vector4 _sheenFrame;     // 종이 셰이더의 _SheenLine
        private Vector4 _sheenArt;       // 그림 칸 셰이더의 _SheenLine (칸 가운데 기준)
        private float _sheenDiagonal;    // 띠 축 위에서 앞면 한 끝부터 다른 끝까지 u
        private MeshRenderer _glowRenderer;
        private MeshRenderer _sparkRenderer;
        private Vector2 _sparkQuadSize;
        private float _sparkSpawn;       // 쌓인 「새로 필 수」
        private bool _fxIdle;            // 다 꺼져 그릴 것이 없다 — 자리가 바뀔 때까지 매 프레임 값을 넣지 않는다
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly Vector4[] _sparkA = new Vector4[MaxSparks];
        private readonly Vector4[] _sparkB = new Vector4[MaxSparks];
        private readonly Vector4[] _sparkC = new Vector4[MaxSparks];

        // 프리팹에 저장된 값 — 흐림 · 이름 크기 · 그림자의 기준 (ReadPrefabValues)
        private bool _prefabValuesRead;
        private Color _nameColor;
        private Color _descriptionColor;
        private Color _pendingColor;
        private float _nameFontSize;
        private Color _shadowColor;
        private ShadowLook _restShadow;

        public int InstanceId { get; private set; }

        /// <summary>카드 종류 ID — 프레젠터가 덱 에셋에서 대상 타입 등을 찾는 데 쓴다.</summary>
        public int CardId { get; private set; }

        /// <summary>카드 종류 — 손패가 지금 낼 수 있는 카드를 가리는 데 쓴다 (시안 v3.5).</summary>
        public CardType Type { get; private set; }

        /// <summary>등급 연출을 얼마나 보일지 — 카드가 놓인 자리 (10-ui.md 2-1 「카드 등급 연출」).</summary>
        public enum GradeFx
        {
            /// <summary>아무것도 없다 — 버림 더미 · 함정 칸 · 떠나는 카드 등.</summary>
            Off,
            /// <summary>내 손패 — 글로우만 은은하게 (희귀부터). 표면 빛 · 반짝이 · 낙서 별은 없다.</summary>
            Hand,
            /// <summary>새 카드를 크게 보여 줄 때 · 체인에 낸 카드 — 등급의 효과 전부.</summary>
            Full,
        }

        /// <summary>카드 문구 글 — 키워드 설명 창(KeywordTipView)이 포인터 아래 키워드 링크를 찾는다.</summary>
        public TMP_Text DescriptionText => descriptionText;

        /// <summary>카드 폭 (유닛) — 콜라이더 크기(앞면 크기, 종이 테 제외). 손패 간격을 계산하는 데 쓴다.</summary>
        public float Width => Collider.size.x;

        /// <summary>카드 높이 (유닛) — 콜라이더 크기(앞면 크기, 종이 테 제외). 손패 자리를 계산하는 데 쓴다.</summary>
        public float Height => Collider.size.y;

        // 편집 모드(Awake 없음)에서도 쓸 수 있게 늦게 잡는다 — 빌더 · 확인 스크립트가 프리팹을 놓고 Setup 을 부른다.
        private BoxCollider Collider => _collider != null ? _collider : _collider = GetComponent<BoxCollider>();
        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        private void Awake()
        {
            if (nameText == null || descriptionText == null || pendingText == null || frameRenderer == null || artRenderer == null || backRenderer == null || shadowRenderer == null)
                Debug.LogError($"[{nameof(CardView)}] 글자 · 면 · 그림자 참조 중 비어 있는 것이 있습니다.", this);
            _view = Camera.main;
        }

        private void Update()
        {
            if (_grade > 0 && !_fxIdle)
                TickGradeFx(Time.deltaTime);
        }

        /// <summary>
        /// 등급 연출을 놓인 자리에 맞춘다 — 보여 주기 · 낸 자리(Full)는 앞면이 선 순간 켜지고, 손패(Hand)는 글로우만, 나머지(Off)는 꺼진다.
        /// 바뀌는 사이는 fxFadeIn · fxFadeOut · glowFade 로 넘어간다. 일반 등급은 아무것도 없다.
        /// </summary>
        public void SetGradeFx(GradeFx state)
        {
            _fxState = state;
            _fxIdle = false;
            if (state != GradeFx.Off)
                EnsureGradeFxQuads();
        }

        /// <summary>false 면 포인터를 가리지 않고 아래 오브젝트(좌석 등)로 넘긴다. 보이는 것은 그대로.</summary>
        public void SetRaycastBlocking(bool blocking) => Collider.enabled = blocking;

        /// <summary>포인터 아래 카드 — 그림자가 커진다 (시안 .card.hov). 고른 카드의 그림자가 앞선다.</summary>
        public void SetHovered(bool on)
        {
            if (_hovered == on) return;

            _hovered = on;
            RefreshShadow();
        }

        /// <summary>고른 카드 · 놓으면 사용되는 카드 — 노란 형광펜 + 그림자가 더 커진다 (시안 .card.sel · .armed).</summary>
        public void SetSelected(bool on)
        {
            if (_marked == on) return;

            _marked = on;
            SetFloat(frameRenderer, MarkId, on ? 1f : 0f);
            RefreshShadow();
        }

        /// <summary>지금 낼 수 없는 카드 — 채도 · 밝기 · 대비를 내린다 (시안 v3.5 .card.hand.dim). 그림자 · 글자까지 카드 전체.</summary>
        public void SetDimmed(bool on)
        {
            if (_dimmed == on) return;

            _dimmed = on;
            _dimTween?.Kill();
            float target = on ? 1f : 0f;
            if (!Application.isPlaying || dimDuration <= 0f)
            {
                ApplyDim(target);
                return;
            }

            _dimTween = DOTween.To(() => _dim, ApplyDim, target, dimDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        /// <summary>이 카드와 자식 전부의 레이어를 바꾼다 — 어느 카메라가 그릴지 (HUD 위 층은 TopCardLayerView).</summary>
        public void SetLayer(int layer)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        public void Setup(int instanceId, CardData data)
        {
            ReadPrefabValues();
            InstanceId = instanceId;
            CardId = data.id;
            Type = data.type;

            // 이름 — 4 자보다 길면 작은 크기 (시안 .nm.long). 그래도 칸을 넘치면 TMP 자동 크기가 줄인다
            nameText.text = data.cardName;
            float nameSize = data.cardName.Length > longNameLength ? longNameFontSize : _nameFontSize;
            nameText.fontSizeMax = nameSize;
            nameText.fontSize = nameSize;

            _description = data.description;
            _hasKeywords = CardKeywords.HasKeywords(_description, Type);
            RefreshDescription();
            SetArt(data.LoadArt());
            SetFloat(frameRenderer, GradeId, (int)data.grade); // 별 수 = 등급 (일반 0 · 고급 1 · 희귀 2 · 개판 3)
            SetupGradeFx((int)data.grade);

            gameObject.name = $"Card_{instanceId}_{data.id}";
        }

        // 프리팹에 저장된 값을 읽어 둔다 — 편집 모드(Awake 없음)에서도 쓰게 처음 쓸 때. 흐림은 이 색을 거르고, 그림자는 이 값으로 돌아간다.
        private void ReadPrefabValues()
        {
            if (_prefabValuesRead) return;

            _prefabValuesRead = true;
            _nameColor = nameText.color;
            _descriptionColor = descriptionText.color;
            _pendingColor = pendingText.color;
            _nameFontSize = nameText.fontSizeMax;
            Material shadow = shadowRenderer.sharedMaterial;
            _shadowColor = shadow.GetColor(ColorId);
            _restShadow = new ShadowLook { drop = shadow.GetFloat(OffsetId), blur = shadow.GetFloat(BlurId), alpha = _shadowColor.a };
            _shadow = _restShadow;
        }

        // 문구 — 낱말 묶기 · 키워드 색 · 아이콘 (CardKeywords, 11-card-list 5절). 키워드 색은 태그라 글자색 거르기가 닿지 않아, 흐린 만큼 거른 색으로 다시 칠한다.
        private void RefreshDescription()
        {
            if (_dim <= 0f)
            {
                descriptionText.text = CardKeywords.Format(_description, Type);
                return;
            }

            Vector4 filter = DimFilter(_dim);
            descriptionText.text = CardKeywords.Format(_description, Type, color => Filter(color, filter));
        }

        // 그림 스프라이트의 텍스처를 그림 칸에 입힌다 — 그림 전체가 칸 안에 들어오게 줄여 가운데에 두고, 남는 쪽은 흰 바탕 (10-ui.md 2-1 「그림」, PM 2026-11-28).
        // 아틀라스에 묶인 스프라이트도 되게 UV 는 textureRect 로 잡고, 그 밖(_ArtRect 밖)은 셰이더가 비운다. 그림이 아직 없는 카드는 「그림 준비 중」 칸 + 글자.
        private void SetArt(Sprite sprite)
        {
            pendingText.gameObject.SetActive(sprite == null);

            MaterialPropertyBlock block = Block;
            artRenderer.GetPropertyBlock(block);
            block.SetColor(TypeColorId, typeColor);
            if (sprite == null)
            {
                block.SetFloat(HasTexId, 0f);
                artRenderer.SetPropertyBlock(block);
                return;
            }

            Texture texture = sprite.texture;
            Rect rect = sprite.textureRect;
            Vector3 box = artRenderer.transform.localScale;
            float boxAspect = box.x / box.y, aspect = rect.width / rect.height;
            Vector2 span = Vector2.one; // 칸 폭 · 높이를 그림 크기로 잰 값 (1 이상 — 남는 쪽은 그림 밖)
            if (aspect < boxAspect)
                span.x = boxAspect / aspect;
            else
                span.y = aspect / boxAspect;
            Vector2 start = (Vector2.one - span) * 0.5f; // 가운데

            block.SetFloat(HasTexId, 1f);
            block.SetTexture(MainTexId, texture);
            block.SetVector(MainTexStId, new Vector4(
                rect.width * span.x / texture.width, rect.height * span.y / texture.height,
                (rect.x + rect.width * start.x) / texture.width, (rect.y + rect.height * start.y) / texture.height));
            block.SetVector(ArtRectId, new Vector4(
                rect.xMin / texture.width, rect.yMin / texture.height, rect.xMax / texture.width, rect.yMax / texture.height));
            artRenderer.SetPropertyBlock(block);
        }

        // 그림자 — 고른 카드 > 포인터 아래 > 쉴 때 (시안 .sel 규칙이 .hov 뒤에 있어 이긴다). 지금 값에서 목표로 넘어간다.
        private void RefreshShadow()
        {
            ReadPrefabValues();
            ShadowLook target = _marked ? markedShadow : _hovered ? hoverShadow : _restShadow;
            _shadowTween?.Kill();
            if (!Application.isPlaying || shadowDuration <= 0f)
            {
                ApplyShadow(target);
                return;
            }

            ShadowLook from = _shadow;
            _shadowTween = DOTween.To(() => 0f, t => ApplyShadow(ShadowLook.Lerp(from, target, t)), 1f, shadowDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        private void ApplyShadow(ShadowLook look)
        {
            _shadow = look;
            MaterialPropertyBlock block = Block;
            shadowRenderer.GetPropertyBlock(block);
            block.SetFloat(OffsetId, look.drop);
            block.SetFloat(BlurId, look.blur);
            block.SetColor(ColorId, new Color(_shadowColor.r, _shadowColor.g, _shadowColor.b, look.alpha));
            shadowRenderer.SetPropertyBlock(block);
        }

        // 흐림 정도(0 ~ 1)를 면 · 그림자 셰이더(_Filter)와 글자색에 넣는다 — CSS 처럼 채도 · 밝기 · 대비 값 자체가 넘어간다.
        private void ApplyDim(float amount)
        {
            ReadPrefabValues();
            _dim = amount;
            Vector4 filter = DimFilter(amount);
            SetVector(frameRenderer, FilterId, filter);
            SetVector(artRenderer, FilterId, filter);
            SetVector(backRenderer, FilterId, filter);
            SetVector(shadowRenderer, FilterId, filter);

            if (_grade > 0)
                ApplyGradeFx();

            nameText.color = Filter(_nameColor, filter);
            descriptionText.color = Filter(_descriptionColor, filter);
            pendingText.color = Filter(_pendingColor, filter);
            if (_hasKeywords)
                RefreshDescription();
        }

        private Vector4 DimFilter(float amount) =>
            new Vector4(Mathf.Lerp(1f, dimSaturate, amount), Mathf.Lerp(1f, dimBrightness, amount), Mathf.Lerp(1f, dimContrast, amount), 0f);

        // CardFilter.cginc 와 같은 식 — CSS saturate → brightness → contrast, 단계마다 0 ~ 1 로 자른다. 알파는 그대로.
        private static Color Filter(Color c, Vector4 filter)
        {
            float luma = 0.213f * c.r + 0.715f * c.g + 0.072f * c.b;
            float s = filter.x;
            var rgb = new Vector3(
                Mathf.Clamp01(luma + s * (0.787f * c.r - 0.715f * c.g - 0.072f * c.b)),
                Mathf.Clamp01(luma + s * (-0.213f * c.r + 0.285f * c.g - 0.072f * c.b)),
                Mathf.Clamp01(luma + s * (-0.213f * c.r - 0.715f * c.g + 0.928f * c.b)));
            for (int i = 0; i < 3; i++)
                rgb[i] = Mathf.Clamp01((Mathf.Clamp01(rgb[i] * filter.y) - 0.5f) * filter.z + 0.5f);
            return new Color(rgb.x, rgb.y, rgb.z, c.a);
        }

        private void SetFloat(Renderer target, int id, float value)
        {
            MaterialPropertyBlock block = Block;
            target.GetPropertyBlock(block);
            block.SetFloat(id, value);
            target.SetPropertyBlock(block);
        }

        private void SetVector(Renderer target, int id, Vector4 value)
        {
            MaterialPropertyBlock block = Block;
            target.GetPropertyBlock(block);
            block.SetVector(id, value);
            target.SetPropertyBlock(block);
        }

        // ── 등급 연출 ─────────────────────────────────────────

        // 등급이 정해질 때 — 처음은 꺼진 상태(별도 숨김). 띠 축은 이때 잡는다 (판은 EnsureGradeFxQuads).
        private void SetupGradeFx(int grade)
        {
            _grade = Mathf.Clamp(grade, 0, 3);
            _fxState = GradeFx.Off;
            _fxFull = false;
            _fxOn = 0f;
            _glow = 0f;
            _glowGoal = 0f;
            _glowStep = 0f;
            _sparks.Clear();
            _fxIdle = false;
            _fxPhase = Random.value;

            Vector4 paper = frameRenderer.sharedMaterial.GetVector(PaperId);
            _paperHalf = new Vector2(paper.x, paper.y);
            _paperRadius = paper.z;

            // 띠 축 — 시안 캔버스(왼쪽 위가 0, 아래가 +)의 (cos, sin) 을 카드 u(가운데가 0, 위가 +)로. 띠 위치 = 앞면 왼쪽 위에서 잰 거리
            float angle = sheenAngle * Mathf.Deg2Rad;
            var axis = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle));
            float fromCorner = _paperHalf.x * axis.x - _paperHalf.y * axis.y;
            _sheenDiagonal = 2f * fromCorner;
            _sheenFrame = new Vector4(axis.x, axis.y, fromCorner, sheenWhite.x);
            Transform frame = frameRenderer.transform;
            Vector2 artCenter = (frame.parent.InverseTransformPoint(artRenderer.transform.position) - frame.localPosition) * CanvasPerUnit;
            _sheenArt = new Vector4(axis.x, axis.y, fromCorner + Vector2.Dot(artCenter, axis), sheenWhite.x);

            ApplyGradeFx();
        }

        // 글로우 · 반짝이 판 — 처음으로 Hand · Full 자리에 놓일 때 만든다 (이미 있으면 그대로 쓴다). 늘 Off 인 버림 더미 · 함정 크게 보기는 만들지 않는다.
        private void EnsureGradeFxQuads()
        {
            if (!Application.isPlaying) return;

            if (_grade >= 2 && _glowRenderer == null)
                _glowRenderer = CloneShadowQuad("GradeGlow", shadowRenderer.sortingOrder, glowDepth, 1f);
            if (_grade >= 3 && _sparkRenderer == null)
            {
                int top = 0;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                    top = Mathf.Max(top, r.sortingOrder);
                _sparkRenderer = CloneShadowQuad("GradeSparks", top + 1, sparkDepth, sparkQuadScale);
                Vector4 size = shadowRenderer.sharedMaterial.GetVector(SizeId);
                _sparkQuadSize = new Vector2(size.x, size.y) * sparkQuadScale;
            }
        }

        // 그림자 판을 복제한다 — 같은 크기 · 자리(카메라 쪽으로 toCamera 유닛), 그리는 순서 order. 셰이더 값은 그릴 때 넣는다.
        private MeshRenderer CloneShadowQuad(string objectName, int order, float toCamera, float scale)
        {
            Transform source = shadowRenderer.transform;
            GameObject clone = Instantiate(source.gameObject, source.parent, false);
            clone.name = objectName;
            Transform t = clone.transform;
            t.localPosition = source.localPosition + new Vector3(0f, 0f, toCamera);
            t.localScale = Vector3.Scale(source.localScale, new Vector3(scale, scale, 1f));
            var quad = clone.GetComponent<MeshRenderer>();
            quad.sortingOrder = order;
            quad.enabled = false;
            return quad;
        }

        // 한 프레임 — 목표 세기로 넘어가고 그린다
        private void TickGradeFx(float dt)
        {
            float facing = _view != null ? Vector3.Dot(transform.forward, _view.transform.forward) : 1f;
            bool faceUp = facing >= fxFacing;
            bool full = _fxState == GradeFx.Full && faceUp;
            if (full && !_fxFull)
                _fxSince = Time.time;
            _fxFull = full;

            float onTarget = full ? 1f : 0f;
            _fxOn = Mathf.MoveTowards(_fxOn, onTarget, dt / Mathf.Max(full ? fxFadeIn : fxFadeOut, 1e-3f));
            float glowTarget = _grade < 2 ? 0f : full ? 1f : _fxState == GradeFx.Hand && faceUp ? glowInHand : 0f; // 글로우는 희귀부터
            if (glowTarget != _glowGoal)
            {
                _glowGoal = glowTarget;
                _glowStep = Mathf.Abs(glowTarget - _glow) / Mathf.Max(glowTarget > _glow ? glowFade.x : glowFade.y, 1e-3f);
            }
            _glow = Mathf.MoveTowards(_glow, glowTarget, dt * _glowStep);
            if (_grade >= 3)
                StepSparks(dt, full && _fxOn > 0.5f);

            ApplyGradeFx();
            bool glowStays = _grade >= 2 && _fxState != GradeFx.Off; // 손패의 글로우는 숨 쉬느라 계속 그린다
            _fxIdle = _fxOn <= 0f && _glow <= 0f && _sparks.Count == 0 && !glowStays && _fxState != GradeFx.Full; // Full 은 앞면이 설 때까지 기다린다
        }

        // 지금 세기로 셰이더 값을 넣는다 — 별 · 표면 빛 · 글로우 · 반짝이
        private void ApplyGradeFx()
        {
            float strength = Mathf.Lerp(1f, fxDimmedStrength, _dim);
            float on = _fxOn * strength;
            SetFloat(frameRenderer, StarShowId, _fxOn);
            ApplySheen(on);
            ApplyGlow(_glow * strength);
            ApplySparks(on);
        }

        private void ApplySheen(float on)
        {
            float amount = 0f, center = 0f, halfWidth = 1f;
            if (_grade >= 1 && on > 0.001f)
            {
                halfWidth = sheenHalfWidth[_grade - 1];
                float elapsed = Time.time - _fxSince - sheenDelay;
                float cycle = elapsed >= 0f ? elapsed % Mathf.Max(sheenPeriod[_grade - 1], 0.01f) : -1f;
                if (cycle >= 0f && cycle < sheenSweep)
                {
                    amount = on;
                    center = Mathf.Lerp(-halfWidth, _sheenDiagonal + halfWidth, Mathf.SmoothStep(0f, 1f, cycle / sheenSweep));
                }
            }

            Color a, b, c;
            if (_grade >= 3)
            {
                a = sheenColorsTop[0];
                b = sheenColorsTop[1];
                c = sheenColorsTop[2];
            }
            else
            {
                b = _grade == 2 ? sheenColorRare : sheenColorHigh;
                a = c = new Color(b.r, b.g, b.b, b.a * sheenSideAlpha);
            }

            var sheen = new Vector4(center, halfWidth, amount, 0f);
            SetSheen(frameRenderer, sheen, _sheenFrame, a, b, c);
            SetSheen(artRenderer, sheen, _sheenArt, a, b, c);
        }

        private void SetSheen(Renderer target, Vector4 sheen, Vector4 line, Color a, Color b, Color c)
        {
            MaterialPropertyBlock block = Block;
            target.GetPropertyBlock(block);
            block.SetVector(SheenId, sheen);
            block.SetVector(SheenLineId, line);
            block.SetVector(SheenAId, a);
            block.SetVector(SheenBId, b);
            block.SetVector(SheenCId, c);
            block.SetFloat(SheenWhiteId, sheenWhite.y);
            target.SetPropertyBlock(block);
        }

        private void ApplyGlow(float level)
        {
            if (_glowRenderer == null) return;

            _glowRenderer.enabled = level > 0.001f;
            if (!_glowRenderer.enabled) return;

            float breath = 0.5f + 0.5f * Mathf.Sin((Time.time + _fxPhase * glowBreathSpread) * glowBreathSpeed - Mathf.PI / 2f);
            Vector2 alphaRange = _grade >= 3 ? glowAlphaTop : glowAlphaRare;
            float scale = 1f + glowBreathScale * breath;
            Vector2 half = (_paperHalf + Vector2.one * glowShape.x) * scale;
            Color main = _grade >= 3 ? glowColorsTop[0] : glowColorRare;
            main.a = Mathf.Lerp(alphaRange.x, alphaRange.y, breath) * level;

            Vector4 grad = Vector4.zero;
            if (_grade >= 3)
            {
                // CSS linear-gradient(θ) — 화면 위가 0°, 시계 방향. 선 길이는 상자를 덮는 길이 |w sinθ| + |h cosθ|
                float theta = (Time.time * glowTurnSpeed + _fxPhase * 360f) * Mathf.Deg2Rad;
                float sin = Mathf.Sin(theta), cos = Mathf.Cos(theta);
                grad = new Vector4(sin, cos, Mathf.Abs(2f * half.x * sin) + Mathf.Abs(2f * half.y * cos), 1f);
            }

            MaterialPropertyBlock block = Block;
            _glowRenderer.GetPropertyBlock(block);
            block.SetFloat(ModeId, 1f);
            block.SetVector(FilterId, new Vector4(1f, 1f, 1f, 0f));
            block.SetVector(HalfId, half);
            block.SetFloat(RadiusId, glowShape.y * scale);
            block.SetFloat(BlurId, glowShape.z * scale);
            block.SetFloat(OffsetId, 0f);
            block.SetColor(ColorId, main);
            block.SetColor(Color2Id, _grade >= 3 ? glowColorsTop[1] : glowColorRare);
            block.SetColor(Color3Id, _grade >= 3 ? glowColorsTop[2] : glowColorRare);
            block.SetVector(GradId, grad);
            _glowRenderer.SetPropertyBlock(block);
        }

        // 반짝이 — 새로 피우고 나이를 먹인다. 효과가 꺼지면 다 걷는다
        private void StepSparks(float dt, bool spawn)
        {
            if (_fxOn <= 0f)
            {
                _sparks.Clear();
                _sparkSpawn = 0f;
                return;
            }

            if (spawn)
            {
                _sparkSpawn += dt * sparkRate.x;
                while (_sparkSpawn >= 1f)
                {
                    _sparkSpawn -= 1f;
                    if (_sparks.Count >= Mathf.Min((int)sparkRate.y, MaxSparks)) continue;

                    bool dot = Random.value < sparkDotChance;
                    Vector2 radiusRange = dot ? sparkDotRadius : sparkStarRadius;
                    _sparks.Add(new Spark
                    {
                        Position = PaperEdgePoint(Random.value, Random.Range(sparkDistance.x, sparkDistance.y)),
                        Life = Random.Range(sparkLife.x, sparkLife.y),
                        Radius = Random.Range(radiusRange.x, radiusRange.y),
                        Dot = dot,
                        Color = sparkColors[Random.Range(0, sparkColors.Length)],
                        Rotation = Random.value * Mathf.PI / 2f,
                    });
                }
            }

            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                Spark spark = _sparks[i];
                spark.Age += dt;
                if (spark.Age >= spark.Life)
                    _sparks.RemoveAt(i);
                else
                    _sparks[i] = spark;
            }
        }

        private void ApplySparks(float on)
        {
            if (_sparkRenderer == null) return;

            _sparkRenderer.enabled = on > 0.001f && _sparks.Count > 0;
            if (!_sparkRenderer.enabled) return;

            for (int i = 0; i < MaxSparks; i++)
            {
                if (i >= _sparks.Count)
                {
                    _sparkA[i] = _sparkB[i] = _sparkC[i] = Vector4.zero;
                    continue;
                }

                Spark spark = _sparks[i];
                float n = spark.Age / spark.Life;
                // 별: sparkPop 까지 튀어나오며 커지고(outBack) 그 뒤 줄어든다 · 가장 클 때 십자 빛줄기. 알갱이: 두 번 깜빡임
                float size = n < sparkPop ? OutBack(n / sparkPop) : 1f - Mathf.SmoothStep(0f, 1f, (n - sparkPop) / (1f - sparkPop));
                float flare = Mathf.Max(0f, 1f - Mathf.Abs(n - sparkPop) / Mathf.Max(sparkFlareWidth, 1e-3f));
                float blink = Mathf.Abs(Mathf.Sin(n * Mathf.PI * 2f));
                _sparkA[i] = new Vector4(spark.Position.x, spark.Position.y, spark.Radius, spark.Rotation + n * sparkSpin);
                _sparkB[i] = new Vector4(spark.Color.r, spark.Color.g, spark.Color.b, spark.Dot ? 1f : 0f);
                _sparkC[i] = new Vector4(size, flare, blink, 0f);
            }

            MaterialPropertyBlock block = Block;
            _sparkRenderer.GetPropertyBlock(block);
            block.SetFloat(ModeId, 2f);
            block.SetVector(SizeId, _sparkQuadSize);
            block.SetColor(ColorId, new Color(1f, 1f, 1f, on));
            block.SetFloat(SparkCountId, _sparks.Count);
            block.SetVectorArray(SparkAId, _sparkA);
            block.SetVectorArray(SparkBId, _sparkB);
            block.SetVectorArray(SparkCId, _sparkC);
            _sparkRenderer.SetPropertyBlock(block);
        }

        // 종이 테(둥근 사각형) 한 바퀴의 u(0 ~ 1) 자리에서 바깥으로 distance 떨어진 점 — 카드 가운데가 0, 위가 + (u). 위 변 왼쪽 끝에서 시계 방향으로
        private Vector2 PaperEdgePoint(float u, float distance)
        {
            float r = _paperRadius, hx = _paperHalf.x - r, hy = _paperHalf.y - r, arc = Mathf.PI / 2f * r;
            float s = Mathf.Repeat(u, 1f) * (4f * (hx + hy) + 4f * arc);
            // 변마다 (모서리 원의 가운데에서 가운데로 · 바깥 방향), 변 끝의 모서리는 그 바깥 방향에서 시계 방향으로 90°
            var corner = new Vector2(hx, hy);
            for (int side = 0; side < 4; side++)
            {
                Vector2 from = Vector2.Scale(EdgeCornerSigns[side], corner);
                Vector2 to = Vector2.Scale(EdgeCornerSigns[(side + 1) % 4], corner);
                Vector2 normal = EdgeNormals[side];
                float length = Vector2.Distance(from, to);
                if (s <= length)
                    return Vector2.Lerp(from, to, s / Mathf.Max(length, 1e-3f)) + normal * (r + distance);
                s -= length;

                if (s <= arc)
                {
                    float angle = Mathf.Atan2(normal.y, normal.x) - s / arc * Mathf.PI / 2f;
                    return to + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (r + distance);
                }
                s -= arc;
            }

            return new Vector2(0f, _paperHalf.y + distance);
        }

        private static float OutBack(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + 2.7f * t * t * t + 1.7f * t * t;
        }

        // 반짝이 하나 — 자리는 카드 가운데 기준 u
        private struct Spark
        {
            public Vector2 Position;
            public float Age;
            public float Life;
            public float Radius;
            public bool Dot;
            public Color Color;
            public float Rotation;
        }

        // 그림자 한 벌 — 아래로 민 거리 (u) · 흐림 반지름 (u) · 진하기 (0 ~ 1)
        [System.Serializable]
        private struct ShadowLook
        {
            public float drop;
            public float blur;
            [Range(0f, 1f)] public float alpha;

            public static ShadowLook Lerp(ShadowLook a, ShadowLook b, float t) => new ShadowLook
            {
                drop = Mathf.Lerp(a.drop, b.drop, t),
                blur = Mathf.Lerp(a.blur, b.blur, t),
                alpha = Mathf.Lerp(a.alpha, b.alpha, t),
            };
        }
    }
}
