using System;
using UnityEngine;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 도시 — 시안 NUKE3D 의 CITY(씨앗 77123 으로 만든 고정 배치) 와 그 메시. 좌표는 시안 그대로 (m, 착탄점 (0, 0, 30)).
    /// 건물 · 보도 판 메시는 시안 크기(sc · 11)를 곱해 m 단위로 들어 있다 — 셰이더가 메시 좌표로 창 · 보도 줄을 그리기 때문.
    /// 그 밖의 메시는 시안 그대로 단위 크기라 그릴 때 크기를 곱한다.
    /// </summary>
    public class NukeCityData : ScriptableObject
    {
        /// <summary>건물 하나 (시안 CITY.B).</summary>
        [Serializable]
        public struct Building
        {
            [Tooltip("양식 번호 (시안 BV, 메시 = buildingMeshes[Variant]).")]
            public int Variant;
            public float X;
            public float Z;
            public float Yaw;
            [Tooltip("바닥 폭 (m) — 무너질 때 잔해 크기.")]
            public float Width;
            [Tooltip("착탄점까지 거리 (m).")]
            public float Dist;
            [Tooltip("먼지 벽이 닿는 때 (착탄 뒤 초).")]
            public float Arrive;
            [Tooltip("착탄점에서 이 건물 쪽 방향 (라디안).")]
            public float Away;
            public float R1;
            public float R2;
            [Tooltip("도심 밖 건물 — 무너지지 않고 그림자를 드리우지 않는다.")]
            public bool Far;
            [Tooltip("창 폭 · 층고 · 1층 높이 · 양식.")]
            public Vector4 MpA;
            [Tooltip("씨앗 · 불 켜진 비율 · 폭 · 깊이.")]
            public Vector4 MpB;
        }

        /// <summary>제자리에 선 물건 (나무 · 가로등 · 신호등 · 소품).</summary>
        [Serializable]
        public struct Prop
        {
            [Tooltip("메시 번호 (나무 = treeMeshes, 소품 = propMeshes, 가로등 · 신호등은 쓰지 않음).")]
            public int Mesh;
            public float X;
            public float Z;
            public float Yaw;
            [Tooltip("크기 (시안 s).")]
            public float Scale;
        }

        /// <summary>차 한 대 (서 있는 차 · 달리는 차).</summary>
        [Serializable]
        public struct Car
        {
            [Tooltip("메시 번호 (carMeshes).")]
            public int Mesh;
            [Tooltip("x — 달리는 차는 출발 x.")]
            public float X;
            public float Z;
            public float Yaw;
            [Tooltip("달리는 속도 (m/s, 서 있는 차는 0).")]
            public float Speed;
            [Tooltip("날아갈 때 도는 정도 (시안 r).")]
            public Vector3 Spin;
            [Tooltip("날아가는 높이 · 거리 · 뒤집힘 난수 (시안 u).")]
            public Vector3 Rand;
        }

        [Header("메시")]
        [Tooltip("건물 18 양식 (m 단위로 구움).")]
        public Mesh[] buildingMeshes;
        [Tooltip("땅 (시안 크기 560).")]
        public Mesh ground;
        [Tooltip("보도 판 (m 단위로 구움).")]
        public Mesh block;
        public Mesh[] treeMeshes;
        public Mesh lamp;
        public Mesh signal;
        [Tooltip("소품 — 소화전 · 쓰레기통 · 벤치 · 우체통 · 표지판 · 정류장.")]
        public Mesh[] propMeshes;
        [Tooltip("차 — 승용 0~4 · 택시 · 승합 · SUV 0 · 1 · 해치백 0 · 1 · 버스.")]
        public Mesh[] carMeshes;
        public Mesh missile;
        public Mesh flame;
        [Tooltip("불덩이 공 (시안 nkW).")]
        public Mesh fireball;
        [Tooltip("그을린 자리.")]
        public Mesh scorch;
        [Tooltip("튀는 잔해 2종.")]
        public Mesh[] chunkMeshes;
        [Tooltip("무너진 건물 자리의 잔해 더미.")]
        public Mesh rubble;

        [Header("배치")]
        public Building[] buildings;
        [Tooltip("보도 판 가운데 (x, z).")]
        public Vector2[] blocks;
        public Prop[] trees;
        public Prop[] lamps;
        public Prop[] signals;
        public Prop[] props;
        public Car[] cars;
        [Tooltip("사이렌이 울릴 때 달리다 서는 차.")]
        public Car[] movers;
    }
}
