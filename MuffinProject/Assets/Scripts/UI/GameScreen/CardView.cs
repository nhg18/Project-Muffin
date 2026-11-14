using System.Text.RegularExpressions;
using Chapchu.Cards;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층의 카드 한 장 (10-ui.md 2-1 「3D 층」 · 「손패 · 카드 (3D 층)」 · 「카드 등급 연출」). 앞면(틀 · 그림 · 이름 · 설명)과 뒷면은 서로 반대를 보는 면이고
    /// 셰이더가 뒷면을 컬링하므로, 루트를 Y 180° 돌리면 그대로 뒤집기다. 종류별 틀 머티리얼은 프리팹(행동 · 카운터 · 함정)이 갖고, 여기서는 CardData 의 이름 · 그림 · 설명 · 등급만 입힌다.
    /// 등급 연출: 반짝은 앞면 셰이더(Chapchu/Card Face), 오라는 카드 둘레 빛(Chapchu/Card Glow), 개판 별은 파티클 — 셋 다 앞면이 카메라를 볼 때만 보인다.
    /// 루트의 SortingGroup 이 카드 한 장을 묶어 뒤 카드부터 그린다 — 반투명 가장자리 · 그림자 · 빛이 뒤 카드 위에 매끈하게 섞인다.
    /// 포인터 입력은 받지 않는다 — 손패 입력은 HandInput 이 한 곳에서 읽고 HandView 가 자리로 판정한다 (10-ui.md 4절). 콜라이더는 뒤에 있는 것(함정 칸 · 덱)의 탭을 가리는 데만 쓴다.
    /// 어느 장인지(InstanceId)와 무슨 카드인지(CardId)만 기억한다.
    /// uGUI 판 HandCardView · HandCard_* 프리팹은 CardFxTestBench 가 아직 쓴다 — 카드 이펙트가 3D 층으로 옮겨오면 지운다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CardView : MonoBehaviour
    {
        private const float PawAlpha = 0.7f;           // 그림 없는 카드의 발자국 — 종류 색 70% (프로토타입 v2)
        private const float DimmedGradeFade = 0.8f;    // 낼 수 없는 카드의 오라 — 회색 × 0.8 만큼 줄인다 (시안 1 − 0.8 × 회색, 반짝은 셰이더가 같은 값으로)
        private const float FacingMin = 0.15f;         // 앞면이 이만큼 이상 카메라를 볼 때만 별을 그린다 (시안 cos ry − 0.15)
        private const float BalancedWidthMin = 0.55f;  // 설명 줄을 고르게 맞출 때 칸 폭을 이만큼까지 좁혀 본다 (시안)
        private const int BalanceSteps = 6;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int HasTexId = Shader.PropertyToID("_HasTex");
        private static readonly int PawColorId = Shader.PropertyToID("_PawColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int GradeId = Shader.PropertyToID("_Grade");
        private static readonly int GradeSpeedId = Shader.PropertyToID("_GradeSpeed");
        private static readonly int GradeOffsetId = Shader.PropertyToID("_GradeOffset");
        private static readonly int GrayId = Shader.PropertyToID("_Gray");
        private static readonly int AmountId = Shader.PropertyToID("_Amount");
        private static readonly int ReachId = Shader.PropertyToID("_Reach");
        private static readonly int RainbowId = Shader.PropertyToID("_Rainbow");
        private static readonly Regex Digits = new Regex(@"\d+");
        private static readonly Regex Words = new Regex(@"\S+");

        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("앞면 틀 — 등급 반짝을 그리고, 낼 수 없을 때 채도를 뺀다. 외곽선 색(_OutlineColor)이 그림 없는 카드의 발자국 색이 된다.")]
        [SerializeField] private MeshRenderer frameRenderer;
        [Tooltip("그림 Quad. 프리팹에서의 크기가 그림 칸이고, 그림을 칸에 꽉 채워(넘치는 쪽은 잘라) 입힌다. 그림이 없는 카드는 발자국을 그린다.")]
        [SerializeField] private MeshRenderer artRenderer;
        [Tooltip("낼 수 있을 때 켜는 테두리 빛 — 카운터 카드의 초록 빛 (10-ui.md 2-1 「손패 입력 값」). 없는 프리팹은 비워 둔다.")]
        [SerializeField] private GameObject glow;
        [Tooltip("고른 카드 · 놓으면 사용되는 카드의 노란 빛 (10-ui.md 2-1 「손패 입력 값」). 없는 프리팹은 비워 둔다.")]
        [SerializeField] private GameObject selectRing;
        [Tooltip("지금 낼 수 없는 카드를 어둡게 덮는 판 — 반응 시간의 나머지 카드 (10-ui.md 2-1 「손패 입력 값」). 없는 프리팹은 비워 둔다.")]
        [SerializeField] private GameObject shade;
        [Tooltip("등급 오라 — 카드 둘레 빛. 아래 등급별 세기가 0 보다 큰 등급만 켠다. 고른 카드 · 카운터 빛이 켜지면 비킨다.")]
        [SerializeField] private MeshRenderer auraRenderer;
        [Tooltip("개판 등급의 별 파티클. 앞면이 카메라를 볼 때만 그린다.")]
        [SerializeField] private ParticleSystem sparks;
        [Tooltip("설명 글자 가운데 숫자 색 — 카드 종류 색을 조금 짙게 (프로토타입 v2).")]
        [SerializeField] private Color numberColor = new Color32(0x00, 0x77, 0xC8, 0xFF);

        [Header("등급 연출 — 일반 · 고급 · 희귀 · 개판 순")]
        [Tooltip("반짝 띠가 앞면을 훑는 횟수 (번 / 초). 0 이면 반짝 없음 (프로토타입 v2 0 · 0.31 · 0.36 · 0.45).")]
        [SerializeField] private float[] gradeSweepSpeeds = { 0f, 0.31f, 0.36f, 0.45f };
        [Tooltip("오라 세기 (0 ~ 1). 0 이면 오라 없음 (프로토타입 v2 0 · 0 · 0.8 · 0.95).")]
        [SerializeField] private float[] gradeAuraAmounts = { 0f, 0f, 0.8f, 0.95f };
        [Tooltip("오라가 퍼지는 거리 배율 (프로토타입 v2 1 · 1 · 1 · 1.15).")]
        [SerializeField] private float[] gradeAuraReaches = { 1f, 1f, 1f, 1.15f };
        [Tooltip("낼 수 없는 카드의 채도 빼기 (0 ~ 1, 시안 .card.dim 0.35). 밝기는 Shade 판이 낮춘다.")]
        [SerializeField, Range(0f, 1f)] private float dimGray = 0.35f;

        private BoxCollider _collider;
        private MaterialPropertyBlock _block;
        private Transform _camera;
        private ParticleSystemRenderer _sparksRenderer;
        private CardGrade _grade;
        private float _gradeOffset;
        private bool _gradeVisible = true;
        private bool _dimmed;

        public int InstanceId { get; private set; }

        /// <summary>카드 종류 ID — 프레젠터가 덱 에셋에서 대상 타입 등을 찾는 데 쓴다.</summary>
        public int CardId { get; private set; }

        /// <summary>카드 폭 (유닛) — 콜라이더 크기(틀 이미지 크기, 흰 여백 · 외곽선 제외). 손패 간격을 계산하는 데 쓴다.</summary>
        public float Width => Collider.size.x;

        /// <summary>카드 높이 (유닛) — 콜라이더 크기(틀 이미지 크기, 흰 여백 · 외곽선 제외). 손패 자리를 계산하는 데 쓴다.</summary>
        public float Height => Collider.size.y;

        // 편집 모드(Awake 없음)에서도 쓸 수 있게 늦게 잡는다 — 빌더 · 확인 스크립트가 프리팹을 놓고 Setup 을 부른다.
        private BoxCollider Collider => _collider != null ? _collider : _collider = GetComponent<BoxCollider>();
        private MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        private void Awake()
        {
            if (nameText == null || descriptionText == null || frameRenderer == null || artRenderer == null)
                Debug.LogError($"[{nameof(CardView)}] 이름 · 설명 · 틀 · 그림 참조 중 비어 있는 것이 있습니다.", this);

            Camera main = Camera.main;
            _camera = main != null ? main.transform : null;
            if (sparks != null)
                _sparksRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        }

        // 개판 별은 화면을 보는 빌보드라 뒷면에서도 보인다 — 앞면이 카메라를 볼 때만 그린다 (뒷면에서 등급이 새지 않게).
        private void LateUpdate()
        {
            if (_sparksRenderer == null || _camera == null || !sparks.gameObject.activeSelf) return;

            Vector3 toCamera = _camera.position - transform.position;
            _sparksRenderer.enabled = Vector3.Dot(-transform.forward, toCamera) > FacingMin * toCamera.magnitude;
        }

        /// <summary>false 면 포인터를 가리지 않고 아래 오브젝트(좌석 등)로 넘긴다. 보이는 것은 그대로.</summary>
        public void SetRaycastBlocking(bool blocking) => Collider.enabled = blocking;

        /// <summary>낼 수 있다는 테두리 빛을 켜고 끈다. 빛이 없는 종류면 아무 일 없다.</summary>
        public void SetGlow(bool on)
        {
            SetActive(glow, on);
            RefreshAura();
        }

        /// <summary>고른 카드 · 놓으면 사용되는 카드의 노란 빛을 켜고 끈다.</summary>
        public void SetSelected(bool on)
        {
            SetActive(selectRing, on);
            RefreshAura();
        }

        /// <summary>지금 낼 수 없는 카드로 어둡게 한다 — 덮는 판 + 채도 빼기, 등급 연출도 약하게.</summary>
        public void SetDimmed(bool on)
        {
            if (_dimmed == on) return;

            _dimmed = on;
            SetActive(shade, on);
            RefreshGrade();
        }

        /// <summary>등급 연출(반짝 · 오라 · 별)을 보이거나 숨긴다 — 버림 더미처럼 등급을 보이지 않는 자리 (10-ui.md 2-1 「카드 등급 연출」).</summary>
        public void SetGradeVisible(bool visible)
        {
            if (_gradeVisible == visible) return;

            _gradeVisible = visible;
            RefreshGrade();
        }

        /// <summary>이 카드와 자식 전부의 레이어를 바꾼다 — 어느 카메라가 그릴지 (HUD 위 층은 TopCardLayerView).</summary>
        public void SetLayer(int layer)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        public void Setup(int instanceId, CardData data)
        {
            InstanceId = instanceId;
            CardId = data.id;
            nameText.text = data.cardName;
            descriptionText.text = FormatDescription(data.description);
            BalanceDescriptionLines();
            SetArt(data.cardImage);

            _grade = data.grade;
            _gradeOffset = Random.value; // 카드마다 반짝이는 때가 다르게
            RefreshGrade();

            gameObject.name = $"Card_{instanceId}_{data.id}";
        }

        private static void SetActive(GameObject target, bool on)
        {
            if (target != null && target.activeSelf != on)
                target.SetActive(on);
        }

        private static bool IsActive(GameObject target) => target != null && target.activeSelf;

        private static float GradeValue(float[] values, CardGrade grade)
        {
            int index = (int)grade;
            return values != null && index >= 0 && index < values.Length ? values[index] : 0f;
        }

        // 설명 속 숫자는 카드 종류 색으로, 줄은 띄어쓰기에서만 바꾼다 (프로토타입 v2). TMP 는 한글을 글자마다 끊으므로 낱말을 nobr 로 묶는다.
        private string FormatDescription(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string hex = ColorUtility.ToHtmlStringRGB(numberColor);
            string colored = Digits.Replace(text, match => $"<color=#{hex}>{match.Value}</color>");
            return Words.Replace(colored, match => $"<nobr>{match.Value}</nobr>");
        }

        // 설명 줄 길이를 고르게 — 글자 크기는 TMP 자동 크기가 칸에 맞추고, 줄 수가 그대로인 가장 좁은 폭까지 좌우 여백을 넓힌다
        // (프로토타입 v2: 마지막 줄에 한 낱말만 남지 않게). 줄 수는 폭이 좁을수록 늘기만 하므로 반으로 나눠 찾는다.
        private void BalanceDescriptionLines()
        {
            TMP_Text text = descriptionText;
            text.margin = Vector4.zero;
            text.enableAutoSizing = true;
            text.ForceMeshUpdate();
            int lines = text.textInfo.lineCount;
            if (lines < 2) return;

            // 자동 크기가 정한 크기로 고정해 두고 폭만 바꿔 본다 — 좁혀도 글자가 줄지 않게
            float fontSize = text.fontSize;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;

            float fits = 0f, overflows = text.rectTransform.rect.width * (1f - BalancedWidthMin) / 2f;
            for (int i = 0; i < BalanceSteps; i++)
            {
                float side = (fits + overflows) / 2f;
                text.margin = new Vector4(side, 0f, side, 0f);
                text.ForceMeshUpdate();
                if (text.textInfo.lineCount == lines && !text.isTextOverflowing)
                    fits = side;
                else
                    overflows = side;
            }
            text.margin = new Vector4(fits, 0f, fits, 0f);
        }

        // 그림 스프라이트의 텍스처를 그림 Quad 에 입힌다 — 칸에 꽉 채우고 넘치는 쪽은 가운데 기준으로 자른다 (프로토타입 v2 cover).
        // 아틀라스에 묶인 스프라이트도 되게 UV 는 textureRect 로 잡는다. 그림이 아직 없는 카드는 발자국 (종류 색 = 틀의 외곽선 색).
        private void SetArt(Sprite sprite)
        {
            MaterialPropertyBlock block = Block;
            artRenderer.GetPropertyBlock(block);
            if (sprite == null)
            {
                Color paw = frameRenderer.sharedMaterial.GetColor(OutlineColorId);
                paw.a = PawAlpha;
                block.SetFloat(HasTexId, 0f);
                block.SetColor(PawColorId, paw);
                artRenderer.SetPropertyBlock(block);
                return;
            }

            Texture texture = sprite.texture;
            Rect rect = sprite.textureRect;
            Vector3 box = artRenderer.transform.localScale;
            float boxAspect = box.x / box.y, aspect = rect.width / rect.height;
            Vector2 visible = Vector2.one; // 그림 안에서 칸에 들어가는 부분 (0 ~ 1)
            if (aspect > boxAspect)
                visible.x = boxAspect / aspect;
            else
                visible.y = aspect / boxAspect;
            Vector2 start = (Vector2.one - visible) / 2f;

            block.SetFloat(HasTexId, 1f);
            block.SetTexture(MainTexId, texture);
            block.SetVector(MainTexStId, new Vector4(
                rect.width * visible.x / texture.width, rect.height * visible.y / texture.height,
                (rect.x + rect.width * start.x) / texture.width, (rect.y + rect.height * start.y) / texture.height));
            artRenderer.SetPropertyBlock(block);
        }

        // 반짝(틀 · 그림 — 같은 띠가 이어서 훑는다) · 회색 · 오라 · 별을 지금 등급 · 상태로 맞춘다.
        private void RefreshGrade()
        {
            float speed = _gradeVisible ? GradeValue(gradeSweepSpeeds, _grade) : 0f;
            float gray = _dimmed ? dimGray : 0f;
            SetFaceGrade(frameRenderer, speed, gray);
            SetFaceGrade(artRenderer, speed, gray);
            RefreshAura();
            SetActive(sparks != null ? sparks.gameObject : null, _gradeVisible && _grade == CardGrade.Gaepan);
        }

        private void SetFaceGrade(Renderer face, float speed, float gray)
        {
            if (face == null) return;

            MaterialPropertyBlock block = Block;
            face.GetPropertyBlock(block);
            block.SetFloat(GradeId, speed > 0f ? (int)_grade : 0f);
            block.SetFloat(GradeSpeedId, speed);
            block.SetFloat(GradeOffsetId, _gradeOffset);
            block.SetFloat(GrayId, gray);
            face.SetPropertyBlock(block);
        }

        // 오라는 고른 카드 · 카운터 빛이 켜지면 비킨다 (같은 자리에 두 빛이 겹치지 않게, 프로토타입 v2).
        private void RefreshAura()
        {
            if (auraRenderer == null) return;

            float amount = _gradeVisible ? GradeValue(gradeAuraAmounts, _grade) : 0f;
            if (_dimmed)
                amount *= 1f - DimmedGradeFade * dimGray;
            bool on = amount > 0f && !IsActive(glow) && !IsActive(selectRing);
            SetActive(auraRenderer.gameObject, on);
            if (!on) return;

            MaterialPropertyBlock block = Block;
            auraRenderer.GetPropertyBlock(block);
            block.SetFloat(AmountId, amount);
            block.SetFloat(ReachId, GradeValue(gradeAuraReaches, _grade));
            block.SetFloat(RainbowId, _grade == CardGrade.Gaepan ? 1f : 0f);
            auraRenderer.SetPropertyBlock(block);
        }
    }
}
