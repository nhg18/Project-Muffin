using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 찍히기 — 크게 튀어나왔다가 제자리 크기로 쾅 내려앉는 등장 (시안 stamp(sp, x, y, sc, rot, k, {from, back, spin, fade, a})).
    /// 프리팹에 저장한 자리 · 기울기 · 크기가 다 찍힌 모습(시안 x · y · rot · sc)이다. 장면이 매 프레임 <see cref="Apply"/>(k) 를 부른다 — k = 등장 진행 (0 이하 = 안 보임, 1 넘어도 그대로).
    /// 크기 = sc · lerp(from, 1, back(k)), 기울기 = rot + (1 − back(k))·spin, 투명도 = cl(k·fade)·a.
    /// 투명도는 같은 오브젝트의 CanvasGroup 에 (여러 그림을 같이 찍을 때 — 예: 도장), 없으면 Graphic.color.a 에 준다.
    /// </summary>
    public class ComicStamp : MonoBehaviour
    {
        [Tooltip("처음 크기 배율 — 여기서 1 로 줄어든다 (시안 from 2.2).")]
        [SerializeField] private float from = 2.2f;
        [Tooltip("내려앉을 때 지나쳤다 돌아오는 정도 (시안 back 2.4).")]
        [SerializeField] private float overshoot = 2.4f;
        [Tooltip("처음에 더 돌아가 있는 각도 (라디안, 시안 spin 0.12).")]
        [SerializeField] private float spin = 0.12f;
        [Tooltip("나타나는 빠르기 — k 가 1/fade 일 때 다 보인다 (시안 fade 4).")]
        [SerializeField] private float fade = 4f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Graphic _graphic;
        private Vector2 _position;
        private Vector3 _scale;
        private float _angle;
        private bool _captured;

        /// <summary>찍힌 자리 (설계 좌표, 아래로 +) — 프리팹에 저장한 자리. 장면이 옮길 때 바꾼다.</summary>
        public Vector2 RestPosition
        {
            get
            {
                Capture();
                return new Vector2(_position.x, -_position.y);
            }
            set
            {
                Capture();
                _position = new Vector2(value.x, -value.y);
            }
        }

        /// <summary>
        /// 등장 진행 k 의 모습으로 둔다. offset = 찍힌 자리에서 더 옮길 만큼 (설계 px, 아래로 + — 시안의 떨림 j 등), alpha = 시안 a (사라지게 할 때).
        /// </summary>
        public void Apply(float k, Vector2 offset = default, float alpha = 1f)
        {
            Capture();
            if (k <= 0f)
            {
                SetAlpha(0f);
                return;
            }
            float e = ComicKit.Back(k, overshoot);
            _rect.anchoredPosition = _position + new Vector2(offset.x, -offset.y);
            _rect.localScale = _scale * ComicKit.Lerp(from, 1f, e);
            _rect.localRotation = Quaternion.Euler(0f, 0f, _angle - (1f - e) * spin * Mathf.Rad2Deg);
            SetAlpha(ComicKit.Cl(k * fade) * alpha);
        }

        private void Awake()
        {
            Capture();
        }

        private void Capture()
        {
            if (_captured)
                return;
            _captured = true;
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _graphic = GetComponent<Graphic>();
            _position = _rect.anchoredPosition;
            _scale = _rect.localScale;
            _angle = _rect.localEulerAngles.z;
        }

        private void SetAlpha(float alpha)
        {
            if (_group != null)
                _group.alpha = alpha;
            else if (_graphic != null)
                ComicKit.SetAlpha(_graphic, alpha);
        }
    }
}
