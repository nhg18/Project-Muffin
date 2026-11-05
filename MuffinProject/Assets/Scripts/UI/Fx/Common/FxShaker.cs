using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 판 흔들기 · 튕기기. 흔들릴 판에 붙인다 — 판은 화면보다 넉넉하게 커야 흔들려도 가장자리가 비지 않는다.
    /// 이 판 안의 이펙트(<see cref="ShakeFx"/> · 난도질 · 일섬)가 부모에서 찾아 쓴다. 세기 · 시간은 부르는 이펙트가 정한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FxShaker : MonoBehaviour
    {
        private readonly object _shakeId = new object();
        private readonly object _punchId = new object();
        private RectTransform _rect;

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        /// <summary>시안 shakeC — 무작위 8곳을 거쳐 제자리로 (선형).</summary>
        public void Shake(float amount, float durationMs)
        {
            var keys = new Vector2[9];
            for (int i = 0; i < 8; i++)
                keys[i] = new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount));
            keys[8] = Vector2.zero;
            ShakeKeys(keys, durationMs);
        }

        /// <summary>정해진 점들을 차례로 지나 흔든다. 점은 시안 좌표 (y 아래 +).</summary>
        public void ShakeKeys(Vector2[] keys, float durationMs)
        {
            DOTween.Kill(_shakeId);
            Rect.anchoredPosition = Vector2.zero;
            DOVirtual.Float(0f, 1f, durationMs / 1000f, t =>
            {
                // 시안은 0 에서 시작해 keys 를 차례로 지난다 (y 아래 + → 유니티 위 +)
                float k = t * keys.Length;
                int i = Mathf.Min(keys.Length - 1, Mathf.FloorToInt(k));
                Vector2 from = i == 0 ? Vector2.zero : keys[i - 1];
                Vector2 offset = Vector2.Lerp(from, keys[i], k - i);
                Rect.anchoredPosition = new Vector2(offset.x, -offset.y);
            }).SetEase(Ease.Linear).SetId(_shakeId).SetLink(gameObject);
        }

        /// <summary>판이 잠깐 커졌다 돌아온다.</summary>
        public void Punch(float amount, float durationMs)
        {
            // 흔들기와 따로 끈다 — 같이 끄면 튕기다 만 크기가 남아 판이 점점 커진다
            DOTween.Kill(_punchId, true);
            Rect.localScale = Vector3.one;
            Rect.DOPunchScale(Vector3.one * amount, durationMs / 1000f, 1, 0f).SetId(_punchId).SetLink(gameObject);
        }

        /// <summary>흔들림 · 튕김을 멈추고 제자리로.</summary>
        public void ResetState()
        {
            DOTween.Kill(_shakeId);
            DOTween.Kill(_punchId);
            Rect.anchoredPosition = Vector2.zero;
            Rect.localScale = Vector3.one;
        }
    }
}
