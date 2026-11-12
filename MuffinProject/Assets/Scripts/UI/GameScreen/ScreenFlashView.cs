using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 화면 전체를 한 색으로 덮었다가 서서히 걷는다 (10-ui.md 5-1 「화면 덮기」 — 113 기억 삭제의 하얀 화면, 시안 whiteout).
    /// 입력을 막지 않는다. 기다리지 않는 연출 — 덮는 동안 다음 일(새 손패)이 이어서 보인다. 기본은 비활성.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ScreenFlashView : MonoBehaviour
    {
        private Image _image;
        private Coroutine _playing;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;
        }

        /// <summary>color 로 덮고 seconds 동안 투명해진다 (ease-out). seconds 가 0 이하거나 색이 투명하면 아무 일도 없다.</summary>
        public void Play(Color color, float seconds)
        {
            if (seconds <= 0f || color.a <= 0f)
                return;

            if (_playing != null)
                StopCoroutine(_playing);
            gameObject.SetActive(true);
            _playing = StartCoroutine(FadeRoutine(color, seconds));
        }

        private IEnumerator FadeRoutine(Color color, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                _image.color = new Color(color.r, color.g, color.b, color.a * (1f - k) * (1f - k)); // ease-out — 처음에 빨리 걷힌다
                yield return null;
            }

            _image.color = Color.clear;
            _playing = null;
            gameObject.SetActive(false);
        }
    }
}
