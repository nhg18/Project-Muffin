using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 시안 AIMC 의 표적 chu() — 게임의 강아지 부품(몸 · 다리 넷 · 꼬리, <see cref="FxDog"/>)을 달밤 색으로 물들여(<see cref="AimTint"/>) 자세대로 움직인다.
    /// 맞고 쓰러진 뒤에는 몸 그림의 까만 눈 위에 털색을 덮고 감은 눈매를 그린다 (시안 closeEyes).
    /// 좌표는 시안 그대로 — 이 오브젝트의 부모가 장면 좌표(시안 px, 아래로 +)의 원점이다.
    /// </summary>
    public class AimDog : MonoBehaviour
    {
        /// <summary>시안 chu 의 자세.</summary>
        public enum Pose { Stand, Splay }

        // 시안 closeEyes — 몸 그림(가로 456px) 좌표의 두 눈: 가운데 x · y, 털색을 집은 x, 바깥쪽 방향
        private static readonly Vector4[] EyeSpots = { new Vector4(117f, 205.5f, 151f, 1f), new Vector4(251f, 200f, 217f, -1f) };
        private const float BodyPictureWidth = 456f;
        private const float EyePatchRadiusX = 27.5f;
        private const float EyePatchRadiusY = 27f;
        private const float LidWidth = 7f;
        private const float LashWidth = 4.5f;
        private const int LidSegments = 10;

        [Serializable]
        public struct Look
        {
            [Tooltip("시안 DOGPILE 키 — na · choco · kongi · bori.")]
            public string key;
            public Sprite body;
            public Sprite tail;
            public Sprite leg;
            [Tooltip("감은 눈을 덮을 털색 (왼쪽 눈 옆에서 집은 색, 시안 closeEyes).")]
            public Color eyeLeft;
            [Tooltip("감은 눈을 덮을 털색 (오른쪽 눈 옆).")]
            public Color eyeRight;
        }

        [Header("부품")]
        [Tooltip("원본 그림 좌표의 원점 — (−가운데 x, 땅 y) 에 둔다.")]
        [SerializeField] private RectTransform origin;
        [SerializeField] private Image body;
        [SerializeField] private Image tail;
        [Tooltip("다리 넷 — FxDog.Legs 순서 (먼 쪽 앞 · 뒤, 가까운 쪽 앞 · 뒤).")]
        [SerializeField] private Image[] legs;
        [Tooltip("감은 눈 (몸 그림 위).")]
        [SerializeField] private AimPainter eyes;
        [SerializeField] private Look[] looks;

        [Header("자세 (시안 chu)")]
        [Tooltip("대자로 뻗을 때 몸이 내려앉는 깊이 (원본 px, × (1 − cos(뻗음 × 각)).")]
        [SerializeField] private float splayDrop = 100f;
        [Tooltip("대자로 뻗을 때 다리 각 (라디안).")]
        [SerializeField] private float splayLegAngle = 1.38f;
        [Tooltip("대자로 뻗을 때 먼 쪽 다리가 덜 벌어지는 각 (라디안).")]
        [SerializeField] private float splayFarInset = 0.14f;
        [Tooltip("서 있을 때 다리가 살짝 흔들리는 각 (라디안).")]
        [SerializeField] private float standLegSway = 0.02f;
        [Tooltip("서 있을 때 꼬리 흔들기 — 각(라디안) · 빠르기.")]
        [SerializeField] private Vector2 standTailWag = new Vector2(0.16f, 5f);
        [Tooltip("서 있을 때 바람에 꼬리가 날리는 각 (라디안, × 바람 세기).")]
        [SerializeField] private float standTailGust = 0.22f;
        [Tooltip("감은 눈매 색.")]
        [SerializeField] private Color lidColor = new Color32(0x1c, 0x15, 0x12, 0xff);

        private readonly List<Vector2> _points = new List<Vector2>();
        private RectTransform _rect;
        private Look _look;
        private bool _hasLook;
        private bool _closed;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            if (origin == null || body == null || tail == null || legs == null || legs.Length != FxDog.Legs.Count || eyes == null || looks == null || looks.Length == 0)
                Debug.LogError($"[{nameof(AimDog)}] 참조 중 비어 있는 것이 있습니다.", this);
            if (!_hasLook && looks != null && looks.Length > 0)
                _look = looks[0];
        }

        /// <summary>어느 강아지인지 (시안 DOGPILE 키). 없는 키면 첫째.</summary>
        public void SetKey(string key)
        {
            _look = looks[0];
            _hasLook = true;
            foreach (Look look in looks)
            {
                if (look.key != key)
                    continue;
                _look = look;
                break;
            }
            body.sprite = _look.body;
            tail.sprite = _look.tail;
            foreach (Image leg in legs)
                leg.sprite = _look.leg;
            if (_closed)
                DrawClosedEyes();
        }

        /// <summary>시안 chu 의 closed — 감은 눈.</summary>
        public void SetClosedEyes(bool closed)
        {
            if (_closed == closed)
                return;
            _closed = closed;
            eyes.Clear();
            if (closed)
                DrawClosedEyes();
        }

        /// <summary>
        /// 시안 chu(x, y, h, t, o) — 땅 자리 (x, y) 에 높이 h 로 세운다. rotation 은 라디안(시계 방향 +), squash 는 세로 눌림(o.sy).
        /// Splay 는 맞고 뒤로 넘어가 다리를 뻗는 자세 — spread(o.k) 만큼 벌리고 twitch(o.tw) 만큼 파르르, tailAngle(o.tl)로 꼬리.
        /// gust 는 시안 gust(2) — 서 있을 때 꼬리가 바람에 날린다.
        /// </summary>
        public void Draw(Vector2 position, float height, float time, Pose pose, float rotation, float flip, float squash,
            float spread, float twitch, float tailAngle, float gust)
        {
            float scale = height / FxDog.Height;
            _rect.localPosition = new Vector3(position.x, -position.y, 0f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, -rotation * Mathf.Rad2Deg);
            _rect.localScale = new Vector3(flip * scale, scale * squash, 1f);

            float drop = pose == Pose.Splay ? splayDrop * (1f - Mathf.Cos(splayLegAngle * spread)) : 0f;
            origin.localPosition = new Vector3(-FxDog.CenterX, FxDog.Ground - drop, 0f);

            float tailTurn = pose == Pose.Splay
                ? tailAngle
                : Mathf.Sin(time * standTailWag.y) * standTailWag.x + gust * standTailGust * flip;
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -tailTurn * Mathf.Rad2Deg);

            for (int i = 0; i < legs.Length; i++)
            {
                float front = FxDog.LegFront[i] ? 1f : -1f;
                bool far = FxDog.LegFar[i];
                float angle = pose == Pose.Splay
                    ? (front * splayLegAngle + (far ? -front * splayFarInset : 0f)) * spread + twitch * (FxDog.LegFront[i] ? -0.6f : 1f) * (far ? 0.7f : 1f)
                    : Mathf.Sin(time * 2f + i) * standLegSway;
                legs[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            }
        }

        // 시안 closeEyes — 몸 그림 좌표(가로 456px)를 몸 칸(원본 좌표, 두 배)으로
        private void DrawClosedEyes()
        {
            eyes.Clear();
            float k = eyes.rectTransform.rect.width / BodyPictureWidth;
            eyes.Scale(k, k);
            for (int e = 0; e < EyeSpots.Length; e++)
            {
                Vector4 spot = EyeSpots[e];
                float cx = spot.x;
                float cy = spot.y;
                float side = spot.w;
                eyes.Ellipse(new Vector2(cx + side * 1.5f, cy), EyePatchRadiusX, EyePatchRadiusY, 0f, e == 0 ? _look.eyeLeft : _look.eyeRight);

                _points.Clear();
                AimPainter.AddQuadratic(_points, new Vector2(cx - 22f, cy - 1f), new Vector2(cx, cy + 19f), new Vector2(cx + 22f, cy - 1f), LidSegments);
                eyes.Stroke(_points, LidWidth, lidColor, true);
                eyes.Line(new Vector2(cx - side * 22f, cy - 1f), new Vector2(cx - side * 22f - side * 9f, cy - 1f + 7f), LashWidth, lidColor, true);
                eyes.Line(new Vector2(cx - side * 14f, cy + 8f), new Vector2(cx - side * 14f - side * 9f, cy + 8f + 7f), LashWidth, lidColor, true);
            }
        }
    }
}
