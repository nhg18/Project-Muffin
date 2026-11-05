using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 일섬 — 칼선 판(이 오브젝트의 <see cref="SlashLayer"/>)을 검은 바탕으로 덮고 왼쪽 아래 밖에서 오른쪽 위 밖까지 굵은 칼선 하나 (지옥참마도: 마지막 일섬).
    /// 히트스톱 동안 빨강 · 파랑으로 어긋난 잔상, 하얗게 번쩍, 정해진 방향으로 흔들림. 끝나면 칼선 판을 걷는다.
    /// </summary>
    [RequireComponent(typeof(SlashLayer))]
    public class BigSlashFx : CardFxStep
    {
        [SerializeField] private Image flash;
        [SerializeField] private Sprite bandSprite;

        [Header("칼선")]
        [Tooltip("칼선 길이 = 화면 대각선 × 이 값.")]
        [SerializeField] private float lengthScale = 1.2f;
        [Tooltip("그림 높이 = 화면 짧은 변 × 이 값 (빛 굵기 10% × 번짐 3.4).")]
        [SerializeField] private float thicknessRatio = 0.34f;
        [Tooltip("그대로 있는 시간 (ms).")]
        [SerializeField] private float holdMs = 200f;
        [Tooltip("사라지는 시간 (ms).")]
        [SerializeField] private float fadeMs = 140f;
        [SerializeField] private Color background = Color.black;
        [Tooltip("칼선 판을 걷는 때 (ms). 다음 장면이 시작하는 시각에 맞춘다.")]
        [SerializeField] private float endMs = 260f;

        [Header("잔상 (히트스톱)")]
        [Tooltip("잔상이 좌우로 어긋난 거리 (px).")]
        [SerializeField] private float ghostOffset = 7f;
        [SerializeField] private Color leftGhost = new Color32(0xff, 0x6a, 0x8a, 0xff);
        [SerializeField] private Color rightGhost = new Color32(0x7a, 0xd8, 0xff, 0xff);
        [Tooltip("잔상이 나타나는 때 (ms).")]
        [SerializeField] private float ghostDelayMs = 60f;
        [Tooltip("잔상이 보이는 시간 (ms).")]
        [SerializeField] private float ghostMs = 140f;

        [Header("번쩍 · 흔들림")]
        [Tooltip("번쩍이는 때 (ms).")]
        [SerializeField] private float flashDelayMs = 60f;
        [Range(0f, 1f)]
        [SerializeField] private float flashAlpha = 1f;
        [Tooltip("번쩍임이 사라지는 시간 (ms).")]
        [SerializeField] private float flashMs = 180f;
        [Tooltip("번쩍인 뒤 흔들리기 시작할 때까지 (ms).")]
        [SerializeField] private float shakeDelayMs = 140f;
        [Tooltip("흔들리는 시간 (ms).")]
        [SerializeField] private float shakeMs = 380f;
        [Tooltip("흔들리며 차례로 지나는 점 (px, 시안 좌표 — y 아래 +). 마지막은 제자리.")]
        [SerializeField] private Vector2[] shakeKeys =
        {
            new Vector2(-24f, 14f), new Vector2(20f, -18f), new Vector2(-12f, 8f), new Vector2(6f, -4f), Vector2.zero,
        };

        public override float DurationMs => Mathf.Max(endMs, holdMs + fadeMs, ghostDelayMs + ghostMs, flashDelayMs + Mathf.Max(flashMs, shakeDelayMs + shakeMs));

        private SlashLayer _slashes;
        private FxShaker _shaker;

        private SlashLayer Slashes => _slashes != null ? _slashes : _slashes = GetComponent<SlashLayer>();

        private void Awake()
        {
            _shaker = GetComponentInParent<FxShaker>(true);
            if (flash == null || bandSprite == null)
                Debug.LogError($"[{nameof(BigSlashFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            Slashes.ResetState();
            SetAlpha(flash, 0f);
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            Slashes.Alpha = 1f;
            Slashes.Clear();
            Slashes.Background.color = background;
            Vector2 screen = ScreenSize;
            float length = (screen * lengthScale).magnitude;
            float angle = Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg;
            float thickness = Mathf.Min(screen.x, screen.y) * thicknessRatio;

            Image main = Slashes.Spawn(bandSprite, Vector2.zero, length, thickness, angle);
            DOTween.Sequence().SetLink(main.gameObject)
                .AppendInterval(Seconds(holdMs))
                .Append(main.DOFade(0f, Seconds(fadeMs)));

            SpawnGhost(new Vector2(-ghostOffset, 0f), leftGhost, length, thickness, angle);
            SpawnGhost(new Vector2(ghostOffset, 0f), rightGhost, length, thickness, angle);

            DOVirtual.DelayedCall(Seconds(endMs), Slashes.ResetState).SetLink(gameObject);
            DOVirtual.DelayedCall(Seconds(flashDelayMs), () =>
            {
                Flash(flash, flashAlpha, flashMs);
                DOVirtual.DelayedCall(Seconds(shakeDelayMs), () => _shaker?.ShakeKeys(shakeKeys, shakeMs)).SetLink(gameObject);
            }).SetLink(gameObject);
        }

        private void SpawnGhost(Vector2 offset, Color color, float length, float thickness, float angle)
        {
            Image ghost = Slashes.Spawn(bandSprite, offset, length, thickness, angle);
            ghost.color = new Color(color.r, color.g, color.b, 0f);
            ghost.transform.SetAsFirstSibling();
            DOTween.Sequence().SetLink(ghost.gameObject)
                .AppendInterval(Seconds(ghostDelayMs))
                .Append(ghost.DOFade(1f, 0f))
                .AppendInterval(Seconds(ghostMs))
                .Append(ghost.DOFade(0f, 0f));
        }
    }
}
