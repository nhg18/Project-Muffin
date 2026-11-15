using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 그림 여러 장을 차례로 바꿔 보이는 이미지 (칼 뽑기 프레임). 재생 구간 · 빠르기는 부르는 조각이 정한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class FramePlayer : MonoBehaviour
    {
        [Tooltip("차례대로 보일 그림. 칼 뽑기: 0 = 칼집에 들어감, 마지막 = 다 뽑힘.")]
        [SerializeField] private Sprite[] frames;
        [Tooltip("재생 전에 보일 프레임 번호.")]
        [SerializeField] private int restFrame;

        private Image _image;
        private int _frame = -1;

        /// <summary>마지막 프레임 번호.</summary>
        public int LastFrame => frames.Length - 1;

        /// <summary>그림을 그리는 이미지.</summary>
        public Image Image => _image != null ? _image : _image = GetComponent<Image>();

        private void Awake()
        {
            if (frames == null || frames.Length < 2)
                Debug.LogError($"[{nameof(FramePlayer)}] 프레임 그림이 2장보다 적습니다.", this);
        }

        /// <summary>재생 전 프레임으로 돌린다.</summary>
        public void ResetState()
        {
            _frame = -1;
            SetFrame(restFrame);
        }

        /// <summary>index 번 프레임을 바로 보인다. 0 미만이면 마지막 프레임.</summary>
        public void Show(int index)
        {
            SetFrame(index < 0 ? LastFrame : index);
        }

        /// <summary>프레임 from → to. 진행은 1 - (1 - k)^easePower — 끝으로 갈수록 느려진다.</summary>
        public Tween Play(int from, int to, float durationMs, float easePower)
        {
            return DOVirtual.Float(0f, 1f, durationMs / 1000f, k =>
                SetFrame(Mathf.RoundToInt(Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - k, easePower))))).SetEase(Ease.Linear);
        }

        private void SetFrame(int index)
        {
            index = Mathf.Clamp(index, 0, LastFrame);
            if (index == _frame)
                return;
            _frame = index;
            Image.sprite = frames[index];
        }
    }
}
