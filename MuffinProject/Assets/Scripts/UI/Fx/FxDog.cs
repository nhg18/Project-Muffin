using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 시안 DOG2D 의 강아지 부품을 맞추는 값 — 카드 연출(만화 컷인 · 개판 · 대폭발)이 강아지를 몸 · 꼬리 · 다리 넷으로 나눠 움직일 때 쓴다.
    /// 그림은 Sprites/CardFx/Common/Dog/dog_{키}_{부품}.png (키 = na · choco · kongi · bori, 부품 = body · tail · leg · snow(얼어붙은 몸)),
    /// 다리 · 꼬리만 있는 dog_snow_leg · dog_snow_tail 은 얼어붙은 강아지용.
    /// 좌표는 시안 그대로 — 강아지 원본 그림 좌표(왼쪽 위 0, 아래로 +, 단위 원본 px). 부품 그림은 원본의 <see cref="Scale"/> 배로 들어 있다.
    /// 시안 CI2.dog: 땅 자리 (x, y) 에 높이 h 로 그리면 원본 1px = h / <see cref="Height"/>, 원본 점 (px, py) 는 (x + (px − <see cref="CenterX"/>)·k, y + (py − <see cref="Ground"/>)·k).
    /// </summary>
    public static class FxDog
    {
        /// <summary>강아지 키 — 시안 DOGPILE.KEYS (나 · 초코 · 콩이 · 보리).</summary>
        public static readonly IReadOnlyList<string> Keys = Array.AsReadOnly(new[] { "na", "choco", "kongi", "bori" });

        /// <summary>
        /// 자리의 강아지 키 (시안 DOGPILE.keyOf) — 이 화면의 주인 = 나(na), 나머지는 seats 순서대로 초코 · 콩이 · 보리.
        /// 자리마다 정해진 캐릭터 값이 생기면 그것으로 바꾼다.
        /// </summary>
        public static string KeyOf(CardFxSeat seat, IReadOnlyList<CardFxSeat> seats)
        {
            if (seat == null || seat.IsMe)
                return Keys[0];
            int order = 0;
            foreach (CardFxSeat other in seats)
            {
                if (other == null || other.IsMe)
                    continue;
                order++;
                if (other == seat)
                    break;
            }
            return Keys[Mathf.Clamp(order, 1, Keys.Count - 1)];
        }

        /// <summary>부품 그림 크기 / 원본 좌표 크기.</summary>
        public const float Scale = 0.5f;

        /// <summary>원본 그림에서 강아지 높이 (땅에서 머리 끝까지).</summary>
        public const float Height = 752f;

        /// <summary>원본 그림에서 땅(발바닥) y.</summary>
        public const float Ground = 812f;

        /// <summary>원본 그림에서 강아지 가운데 x.</summary>
        public const float CenterX = 590f;

        /// <summary>몸 그림이 차지하는 칸 (xMin, yMin, xMax, yMax).</summary>
        public static readonly Vector4 Body = new Vector4(37f, 0f, 949f, 737f);

        /// <summary>꼬리 그림이 차지하는 칸 (xMin, yMin, xMax, yMax).</summary>
        public static readonly Vector4 Tail = new Vector4(875f, 325f, 992f, 586f);

        /// <summary>꼬리가 도는 축.</summary>
        public static readonly Vector2 TailPivot = new Vector2(900f, 548f);

        /// <summary>다리 그림 크기 (원본 좌표).</summary>
        public static readonly Vector2 LegSize = new Vector2(112f, 150f);

        /// <summary>다리 그림 안의 도는 축 (다리 그림 왼쪽 위 기준).</summary>
        public static readonly Vector2 LegPivot = new Vector2(52f, 38f);

        /// <summary>다리 넷이 붙는 자리 (그리는 순서 — 먼 쪽 앞 · 뒤, 가까운 쪽 앞 · 뒤). 다리는 몸보다 먼저(뒤에) 그린다.</summary>
        public static readonly IReadOnlyList<Vector2> Legs = Array.AsReadOnly(new[]
        {
            new Vector2(478f, 712f),
            new Vector2(760f, 708f),
            new Vector2(340f, 716f),
            new Vector2(846f, 712f),
        });

        /// <summary>다리가 먼 쪽(화면 안쪽)인가 — Legs 와 같은 순서.</summary>
        public static readonly IReadOnlyList<bool> LegFar = Array.AsReadOnly(new[] { true, true, false, false });

        /// <summary>앞다리인가 — Legs 와 같은 순서.</summary>
        public static readonly IReadOnlyList<bool> LegFront = Array.AsReadOnly(new[] { true, false, true, false });

        /// <summary>얼굴 부위 자리 (원본 좌표) — 시안 CI2.DOGF. 땀 · 말풍선 꼬리 · 반짝임을 얼굴에 맞춰 붙일 때.</summary>
        public static readonly Vector2 EyeLeft = new Vector2(290f, 420f), EyeRight = new Vector2(560f, 420f), Nose = new Vector2(425f, 485f),
            Mouth = new Vector2(425f, 528f), MouthRight = new Vector2(540f, 515f), MouthLeft = new Vector2(315f, 520f),
            HeadTop = new Vector2(420f, 175f), Chest = new Vector2(440f, 650f), EarLeft = new Vector2(110f, 140f), EarRight = new Vector2(690f, 120f),
            CheekRight = new Vector2(575f, 480f), CheekLeft = new Vector2(285f, 485f);

        /// <summary>부품 그림 경로 (Assets 기준) — 에디터 · 빌더용. 키는 <see cref="Keys"/> 또는 "snow", 부품은 body · tail · leg · snow.</summary>
        public static string SpritePath(string key, string part) => $"Assets/Sprites/CardFx/Common/Dog/dog_{key}_{part}.png";
    }
}
