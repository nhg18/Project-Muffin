using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.Fx.Post
{
    /// <summary>
    /// 「뒤로 돌아」 4 단계 장식 — 판 가운데에 둥근 화살표(↺)가 커지며 반시계 방향으로 한 바퀴 돌고 사라진다 (시안 spinArrow, 1.1초). 3 단계는 없다.
    /// 화살표 모양은 <see cref="UICircleArrow"/> (시안 px 크기) — 연출 레이어 배율(PixelScale)을 곱해 키운다. 플레이어 판의 「방향 반대」 글자는 게임 화면 몫이다.
    /// 시안: 아티팩트 「찹츄 카드 이펙트 v2」 FXA 뒤로 돌아 · spinArrow · .vw-spin.
    /// </summary>
    public class SpinArrowFx : CardFx
    {
        [Tooltip("돌아가는 화살표 — 판 가운데. 시작은 투명.")]
        [SerializeField] private CanvasGroup arrow;
        [Tooltip("커져서 돌고 사라질 때까지 (ms, 시안 spinArrow 1100ms · ease-out).")]
        [SerializeField] private float durationMs = 1100f;
        [Tooltip("키프레임 시점 (0 ~ 1). 아래 배율 · 각도 · 투명도와 개수가 같아야 한다.")]
        [SerializeField] private float[] keys = { 0f, 0.4f, 0.8f, 1f };
        [Tooltip("키프레임마다 배율 (시안 px 크기 대비).")]
        [SerializeField] private float[] scales = { 0.3f, 1.1f, 1f, 1f };
        [Tooltip("키프레임마다 각도 (도, 시안 rotate — − 가 반시계 방향).")]
        [SerializeField] private float[] angles = { 0f, -200f, -360f, -360f };
        [Tooltip("키프레임마다 투명도.")]
        [SerializeField] private float[] alphas = { 0f, 1f, 1f, 0f };

        private void Awake()
        {
            if (arrow == null)
                Debug.LogError($"[{nameof(SpinArrowFx)}] 화살표 참조가 비어 있습니다.", this);
            if (keys.Length < 2 || scales.Length != keys.Length || angles.Length != keys.Length || alphas.Length != keys.Length)
                Debug.LogError($"[{nameof(SpinArrowFx)}] 키프레임 배열의 개수가 맞지 않습니다.", this);
        }

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            float pixelScale = context.Layer.PixelScale;
            var rect = (RectTransform)arrow.transform;
            Tween spin = DOVirtual.Float(0f, 1f, durationMs / 1000f, t =>
            {
                int i = 0;
                while (i < keys.Length - 2 && t > keys[i + 1])
                    i++;
                float f = Mathf.InverseLerp(keys[i], keys[i + 1], t);
                rect.localScale = Vector3.one * (pixelScale * Mathf.Lerp(scales[i], scales[i + 1], f));
                // 시안은 시계 방향 + — 유니티는 반시계 방향 +
                rect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(angles[i], angles[i + 1], f));
                arrow.alpha = Mathf.Lerp(alphas[i], alphas[i + 1], f);
            }).SetEase(Ease.OutQuad).SetLink(arrow.gameObject);

            yield return spin.WaitForCompletion();
        }
    }
}
