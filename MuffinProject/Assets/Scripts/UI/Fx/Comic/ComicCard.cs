using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 만화 컷인 속 카드 한 장 — 흰 테 + 카드 뒷면 + 먹 테 (+ 반짝 지나감) (시안 card(x, y, w, rot, {sc, shadow, glint})).
    /// 가운데 = 이 RectTransform 의 피벗, 기울기(rot) · 배율(sc) = 이 RectTransform. 높이 = 너비 × 1.42.
    /// 그림은 자식 <see cref="ComicCardPart"/> 넷(몸 · 뒷면 · 테 · 반짝)이 이 값을 읽어 그린다 — 프리팹 ComicCard 를 복제해 쓴다.
    /// 뒷면 그림이 비어 있으면 컷인이 시작할 때 <see cref="CardFxContext.CardBack"/> 을 넣는다 (그것도 없으면 시안처럼 파란 면).
    /// </summary>
    public class ComicCard : MonoBehaviour
    {
        /// <summary>카드 높이 / 너비 (시안 1.42).</summary>
        public const float Aspect = 1.42f;

        [Tooltip("카드 너비 (설계 px, 시안 w).")]
        [SerializeField] private float width = 78f;
        [Tooltip("그림자를 그릴지 (시안 shadow).")]
        [SerializeField] private bool shadow = true;
        [Tooltip("뒷면 그림 — 비워 두면 컷인이 카드 뒷면을 넣는다.")]
        [SerializeField] private Sprite back;

        private ComicCardPart[] _parts;
        private float _glint;

        /// <summary>카드 너비 (설계 px). 바뀌면 다시 그린다.</summary>
        public float Width
        {
            get => width;
            set
            {
                if (Mathf.Approximately(width, value))
                    return;
                width = value;
                Refresh();
            }
        }

        /// <summary>그림자를 그리는가.</summary>
        public bool Shadow => shadow;

        /// <summary>뒷면 그림. 바꾸면 다시 그린다.</summary>
        public Sprite Back
        {
            get => back;
            set
            {
                if (back == value)
                    return;
                back = value;
                Refresh(true);
            }
        }

        /// <summary>반짝이 지나간 정도 (0 = 없음, 0 ~ 1 = 왼쪽 → 오른쪽, 시안 glint). 바뀌면 다시 그린다.</summary>
        public float Glint
        {
            get => _glint;
            set
            {
                if (Mathf.Approximately(_glint, value))
                    return;
                _glint = value;
                Refresh();
            }
        }

        /// <summary>자식 그림을 모두 다시 그린다. texture 면 그림(뒷면)도 다시 붙인다.</summary>
        public void Refresh(bool texture = false)
        {
            if (_parts == null)
                _parts = GetComponentsInChildren<ComicCardPart>(true);
            foreach (ComicCardPart part in _parts)
            {
                part.Redraw();
                if (texture)
                    part.SetMaterialDirty();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _parts = null;
            Refresh(true);
        }
#endif
    }
}
