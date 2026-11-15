using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 강아지 한 마리의 그림 — 꼬리 · 다리 넷 · 몸 스프라이트를 시안 DOG2D 부품 자리(<see cref="FxDog"/>)에 붙여 두고 자세만 바꾼다.
    /// 길이 단위는 강아지 원본 그림 px (위가 +, 원점 = 발바닥 가운데). 크기(원본 1px = 월드 몇)와 방향(카메라 · 해)은 부모(<see cref="GaepanDog"/>)가 정한다.
    /// 시안 DOGPILE.dog · D3R.buildDogs 와 같다. 각도는 시안 그대로 그림 좌표(아래가 +) 라디안을 받는다.
    /// </summary>
    public class GaepanDogFigure : MonoBehaviour
    {
        // 미끄러질 때 배가 땅에 닿게 몸을 내리는 양 (원본 px) — 시안 dog 의 pose==="slide"?70
        private const float SlideDrop = 70f;

        [Tooltip("기울기 · 좌우 뒤집기 · 세로 눌림을 받는 마디 (발바닥 가운데 기준).")]
        [SerializeField] private Transform pose;
        [Tooltip("미끄러질 때 몸을 내리는 마디.")]
        [SerializeField] private Transform origin;
        [Tooltip("꼬리가 도는 축 (FxDog.TailPivot).")]
        [SerializeField] private Transform tailPivot;
        [Tooltip("다리가 도는 축 넷 — FxDog.Legs 순서 (먼 앞 · 먼 뒤 · 가까운 앞 · 가까운 뒤).")]
        [SerializeField] private Transform[] legPivots;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer tail;
        [Tooltip("다리 그림 넷 — legPivots 와 같은 순서.")]
        [SerializeField] private SpriteRenderer[] legs;

        private void Awake()
        {
            if (pose == null || origin == null || tailPivot == null || body == null || tail == null
                || legPivots == null || legPivots.Length != FxDog.Legs.Count || legs == null || legs.Length != FxDog.Legs.Count)
                Debug.LogError($"[{nameof(GaepanDogFigure)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>강아지 종류(나 · 초코 · 콩이 · 보리)의 그림으로 바꾼다.</summary>
        public void SetSprites(Sprite bodySprite, Sprite tailSprite, Sprite legSprite)
        {
            body.sprite = bodySprite;
            tail.sprite = tailSprite;
            foreach (SpriteRenderer leg in legs)
                leg.sprite = legSprite;
        }

        /// <summary>
        /// 자세를 잡는다. roll = 몸 기울기, tailAngle · legAngles = 꼬리 · 다리 각 (그림 좌표 라디안 — 시안 rot · tw · legAng),
        /// flip = 좌우 뒤집기, stretchY = 세로 늘림 (시안 sy), slide = 배 깔고 미끄러지는 자세 (몸을 내린다).
        /// </summary>
        public void Apply(float roll, bool flip, float stretchY, bool slide, float tailAngle, float[] legAngles)
        {
            pose.localRotation = Quaternion.Euler(0f, 0f, -roll * Mathf.Rad2Deg);
            pose.localScale = new Vector3(flip ? -1f : 1f, stretchY, 1f);
            origin.localPosition = new Vector3(0f, slide ? -SlideDrop : 0f, 0f);
            tailPivot.localRotation = Quaternion.Euler(0f, 0f, -tailAngle * Mathf.Rad2Deg);
            for (int i = 0; i < legPivots.Length; i++)
                legPivots[i].localRotation = Quaternion.Euler(0f, 0f, -legAngles[i] * Mathf.Rad2Deg);
        }
    }
}
