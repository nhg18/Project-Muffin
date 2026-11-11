using UnityEngine;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 「찹츄!」 로고 움직임 — 찹 · 츄는 통통 뛰고(츄는 0.18초 늦게) ! 는 아래를 축으로 흔든다.
    /// 기준: 「통합 화면 UI v2」 타이틀 .logo (chHop 1.4초 · chWag 0.6초)
    /// </summary>
    public class TitleLogoView : MonoBehaviour
    {
        [SerializeField] private RectTransform chap;
        [SerializeField] private RectTransform chu;
        [SerializeField] private RectTransform bang;

        [Tooltip("뛰기 한 번 (초)")]
        [SerializeField] private float hopSeconds = 1.4f;
        [Tooltip("츄가 찹보다 늦게 뛰는 시간 (초)")]
        [SerializeField] private float hopDelay = 0.18f;
        [Tooltip("뛰는 높이 · 내려앉는 깊이 — 글자 높이 비율 (시안 −8% · +2%)")]
        [SerializeField] private Vector2 hopUpAndDown = new Vector2(0.08f, 0.02f);
        [Tooltip("! 흔들기 한 번 (초)")]
        [SerializeField] private float wagSeconds = 0.6f;
        [Tooltip("! 기울기 범위 (도, 시계 방향) — 6 ↔ 16")]
        [SerializeField] private Vector2 wagAngles = new Vector2(6f, 16f);

        private Vector2 _chapOrigin;
        private Vector2 _chuOrigin;

        private void Awake()
        {
            if (chap == null || chu == null || bang == null)
                Debug.LogError($"[{nameof(TitleLogoView)}] 글자 참조가 비어 있습니다.", this);

            _chapOrigin = chap.anchoredPosition;
            _chuOrigin = chu.anchoredPosition;
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            chap.anchoredPosition = _chapOrigin + new Vector2(0f, Hop(time) * chap.rect.height);
            chu.anchoredPosition = _chuOrigin + new Vector2(0f, Hop(time - hopDelay) * chu.rect.height);

            float wag = 0.5f - 0.5f * Mathf.Cos(time / wagSeconds * Mathf.PI * 2f);
            bang.localEulerAngles = new Vector3(0f, 0f, -Mathf.Lerp(wagAngles.x, wagAngles.y, wag));
        }

        // 0 → 40% 위로 → 60% 아래로 → 100% 제자리. 구간마다 ease-in-out (CSS keyframes 와 같다). 위쪽이 +.
        private float Hop(float time)
        {
            float t = Mathf.Repeat(time, hopSeconds) / hopSeconds;
            if (t < 0.4f)
                return Mathf.Lerp(0f, hopUpAndDown.x, Mathf.SmoothStep(0f, 1f, t / 0.4f));

            if (t < 0.6f)
                return Mathf.Lerp(hopUpAndDown.x, -hopUpAndDown.y, Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 0.2f));

            return Mathf.Lerp(-hopUpAndDown.y, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
        }
    }
}
