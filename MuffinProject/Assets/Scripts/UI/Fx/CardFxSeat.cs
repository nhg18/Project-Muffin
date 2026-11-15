﻿using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>연출이 아는 한 사람의 자리 — 화면 위치와 겉모양. 부르는 쪽(게임 화면 · 시험장)이 채운다.</summary>
    public class CardFxSeat
    {
        /// <summary>Photon ActorNumber (시험장은 아무 번호).</summary>
        public int Actor;

        /// <summary>캐릭터 그림 자리 — 피해 숫자 · 말풍선이 여기 뜬다.</summary>
        public RectTransform Anchor;

        /// <summary>이름표 글자.</summary>
        public string Name;

        /// <summary>자리 색 (PlayerLook.ColorOf).</summary>
        public Color Color;

        /// <summary>이 화면의 주인이면 true.</summary>
        public bool IsMe;
    }
}
