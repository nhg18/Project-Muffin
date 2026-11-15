using System;
using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 만화 컷인 속 강아지 — 시안 DOG2D 부품(몸 · 꼬리 · 다리 넷)을 맞춰 그린다 (시안 dog(x, y, h, t, {key, flip, rot, sx, sy, skew, legs, wag, tailA, shadow})).
    /// 이 RectTransform 의 피벗 = 땅 자리 (발바닥 가운데). 그림은 자식 <see cref="ComicDogPart"/> 넷(그림자 · 꼬리 · 다리 · 몸)이 이 값을 읽어 그린다 — 프리팹 ComicDog 를 복제해 쓴다.
    /// 장면이 매 프레임: <see cref="SetTime"/>(기본 자세 — 꼬리 흔들기 · 다리 떨림) → 바꿀 자세만 덮어쓰기(<see cref="Rotation"/> · <see cref="ScaleX"/> · <see cref="ScaleY"/> · <see cref="Skew"/> ·
    /// <see cref="TailAngle"/> · <see cref="SetLeg"/>). 바뀐 프레임 끝(LateUpdate)에 한 번 다시 그린다.
    /// 몸 위 한 점(원본 그림 좌표, 예: <see cref="FxDog.Mouth"/>)이 설계 판 어디인지는 <see cref="PointOf"/> (시안 dogPt).
    /// 누가 어느 강아지인지는 <see cref="Key"/> (나 · 초코 · 콩이 · 보리 = <see cref="FxDog.Keys"/>) — 장면이 <see cref="ComicContext.DogKeyOf"/> 로 정한다.
    /// </summary>
    public class ComicDog : MonoBehaviour
    {
        /// <summary>강아지 한 마리의 부품 그림.</summary>
        [Serializable]
        public class Skin
        {
            [Tooltip("강아지 키 (na · choco · kongi · bori).")]
            public string key;
            [Tooltip("몸 (dog_{키}_body).")]
            public Sprite body;
            [Tooltip("꼬리 (dog_{키}_tail).")]
            public Sprite tail;
            [Tooltip("다리 — 넷이 같은 그림 (dog_{키}_leg).")]
            public Sprite leg;
        }

        // 시안 dog(): 그림자 = 땅에서 h·0.01 아래, 가로 반지름 h·0.46·sx, 세로 h·0.06
        private const float ShadowDrop = 0.01f;
        private const float ShadowRadiusX = 0.46f;
        private const float ShadowRadiusY = 0.06f;

        [Tooltip("강아지 키 — 그릴 부품 그림 (na · choco · kongi · bori). 없는 키면 첫 그림.")]
        [SerializeField] private string key = "na";
        [Tooltip("땅에서 머리 끝까지 높이 (설계 px, 시안 h).")]
        [SerializeField] private float height = 410f;
        [Tooltip("왼쪽을 보게 뒤집는다 (시안 flip −1).")]
        [SerializeField] private bool faceLeft;
        [Tooltip("발밑 그림자를 그린다 (시안 shadow).")]
        [SerializeField] private bool shadow = true;
        [Tooltip("발밑 그림자 색 (시안 rgba(20, 8, 30, .32)).")]
        [SerializeField] private Color shadowColor = new Color(20f / 255f, 8f / 255f, 30f / 255f, 0.32f);
        [Tooltip("꼬리 흔드는 빠르기 (시안 wag 6).")]
        [SerializeField] private float wag = 6f;
        [Tooltip("꼬리 흔드는 폭 (라디안, 시안 0.22).")]
        [SerializeField] private float tailSwing = 0.22f;
        [Tooltip("다리 떨림 빠르기 (시안 2).")]
        [SerializeField] private float legSpeed = 2f;
        [Tooltip("다리 떨림 폭 (라디안, 시안 0.02).")]
        [SerializeField] private float legSwing = 0.02f;
        [Tooltip("강아지마다 부품 그림.")]
        [SerializeField] private Skin[] skins = Array.Empty<Skin>();

        private readonly float[] _legs = new float[4];
        private ComicDogPart[] _parts;
        private float _rotation;
        private float _scaleX = 1f;
        private float _scaleY = 1f;
        private float _skew;
        private float _tail;
        private bool _dirty;

        /// <summary>강아지 키 (<see cref="FxDog.Keys"/>). 바꾸면 그림이 바뀐다.</summary>
        public string Key
        {
            get => key;
            set
            {
                if (key == value)
                    return;
                key = value;
                Refresh(true);
            }
        }

        /// <summary>땅에서 머리 끝까지 높이 (설계 px).</summary>
        public float Height
        {
            get => height;
            set => Set(ref height, value);
        }

        /// <summary>기울기 (라디안, 시계 방향 +, 땅 자리를 축으로 — 시안 rot).</summary>
        public float Rotation
        {
            get => _rotation;
            set => Set(ref _rotation, value);
        }

        /// <summary>가로 배율 (시안 sx — 그림자 폭도 따라간다).</summary>
        public float ScaleX
        {
            get => _scaleX;
            set => Set(ref _scaleX, value);
        }

        /// <summary>세로 배율 (시안 sy).</summary>
        public float ScaleY
        {
            get => _scaleY;
            set => Set(ref _scaleY, value);
        }

        /// <summary>가로 기울이기 — x 에 skew·y 를 더한다 (y 는 땅 기준, 위가 −). + 면 위쪽이 왼쪽으로 밀린다 (시안 skew, transform(1, 0, skew, 1)).</summary>
        public float Skew
        {
            get => _skew;
            set => Set(ref _skew, value);
        }

        /// <summary>꼬리 각도 (라디안, 시안 tailA). <see cref="SetTime"/> 이 기본값으로 덮는다.</summary>
        public float TailAngle
        {
            get => _tail;
            set => Set(ref _tail, value);
        }

        /// <summary>왼쪽을 보는가 (시안 flip −1).</summary>
        public bool FaceLeft
        {
            get => faceLeft;
            set
            {
                if (faceLeft == value)
                    return;
                faceLeft = value;
                _dirty = true;
            }
        }

        /// <summary>그림자를 그리는가.</summary>
        public bool HasShadow => shadow;

        /// <summary>그림자 색.</summary>
        public Color ShadowColor => shadowColor;

        /// <summary>지금 키의 부품 그림 (없는 키면 첫 그림, 그림이 하나도 없으면 null).</summary>
        public Skin CurrentSkin
        {
            get
            {
                foreach (Skin skin in skins)
                {
                    if (skin != null && skin.key == key)
                        return skin;
                }
                return skins.Length > 0 ? skins[0] : null;
            }
        }

        /// <summary>
        /// 장면 시각 t 의 기본 자세 — 기울기 · 배율 · 기울이기는 0 · 1 · 1 · 0, 꼬리는 sin(t·wag)·0.22, 다리 i 는 sin(t·2 + i)·0.02 (시안 dog 기본값).
        /// 이것을 먼저 부르고, 장면이 바꿀 자세만 덮어쓴다.
        /// </summary>
        public void SetTime(float t)
        {
            Rotation = 0f;
            ScaleX = 1f;
            ScaleY = 1f;
            Skew = 0f;
            TailAngle = Mathf.Sin(t * wag) * tailSwing;
            for (int i = 0; i < _legs.Length; i++)
                SetLeg(i, Mathf.Sin(t * legSpeed + i) * legSwing);
        }

        /// <summary>다리 i 의 각도 (라디안, 시안 legs(i, lg)). i 는 <see cref="FxDog.Legs"/> 순서 — 앞다리인지는 <see cref="FxDog.LegFront"/>.</summary>
        public void SetLeg(int index, float angle)
        {
            Set(ref _legs[index], angle);
        }

        /// <summary>다리 i 의 각도 (라디안).</summary>
        public float LegAngle(int index) => _legs[index];

        /// <summary>원본 그림 좌표 한 점이 지금 자세에서 설계 판 어디인지 (시안 dogPt). 이 오브젝트가 장면 오브젝트처럼 설계 판 왼쪽 위 기준으로 놓였을 때.</summary>
        public Vector2 PointOf(Vector2 source)
        {
            return ComicKit.PositionOf((RectTransform)transform) + LocalPointOf(source);
        }

        /// <summary>원본 그림 좌표 한 점이 이 오브젝트(땅 자리) 기준 어디인지 (설계 px, 아래로 +).</summary>
        public Vector2 LocalPointOf(Vector2 source)
        {
            float k = height / FxDog.Height;
            float u = (source.x - FxDog.CenterX) * Flip * k * _scaleX, v = (source.y - FxDog.Ground) * k * _scaleY;
            u += _skew * v;
            float cos = Mathf.Cos(_rotation), sin = Mathf.Sin(_rotation);
            return new Vector2(u * cos - v * sin, u * sin + v * cos);
        }

        /// <summary>펜을 강아지 원본 그림 좌표로 옮긴다 (시안 dog 의 rotate · skew · scale · translate). 부품이 부른다.</summary>
        public void ApplyBodySpace(ComicPen pen)
        {
            float k = height / FxDog.Height;
            pen.Rotate(_rotation);
            pen.Transform(1f, 0f, _skew, 1f, 0f, 0f);
            pen.Scale(Flip * k * _scaleX, k * _scaleY);
            pen.Translate(-FxDog.CenterX, -FxDog.Ground);
        }

        /// <summary>그림자 타원 — 이 오브젝트 기준 가운데 · 반지름 (설계 px). 기울기를 따르지 않는다.</summary>
        public void ShadowShape(out Vector2 center, out Vector2 radius)
        {
            center = new Vector2(0f, height * ShadowDrop);
            radius = new Vector2(height * ShadowRadiusX * _scaleX, height * ShadowRadiusY);
        }

        /// <summary>부품을 모두 다시 그린다. texture 면 그림(키)도 다시 붙인다.</summary>
        public void Refresh(bool texture = false)
        {
            _dirty = false;
            if (_parts == null)
                _parts = GetComponentsInChildren<ComicDogPart>(true);
            foreach (ComicDogPart part in _parts)
            {
                part.Redraw();
                if (texture)
                    part.SetMaterialDirty();
            }
        }

        private float Flip => faceLeft ? -1f : 1f;

        private void Set(ref float field, float value)
        {
            if (Mathf.Approximately(field, value))
                return;
            field = value;
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (_dirty)
                Refresh();
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
