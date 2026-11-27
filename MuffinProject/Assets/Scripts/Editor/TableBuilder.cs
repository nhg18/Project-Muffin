using System.IO;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Network;
using Chapchu.UI.GameScreen;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 인게임 3D 층(카드 · 덱 · 버림 더미 · 손패 · 카드 내는 자리)을 조립한다 — 10-ui.md 2-1 「3D 층」. GameScreenBuilder.Build 가 부른다.
    /// 단위: 캔버스 100px = 1 유닛, 테이블 면 z = 0, 카메라는 원근 · 수직 FOV 40° 로 1080px 높이가 화면을 채우는 거리에. 좌석 이름표는 2D 층(HUD v2).
    /// 덱 · 함정 칸 머티리얼(Unlit · 컷아웃 · 뒷면 컬링)은 없으면 만들고, 있으면 덮어쓰지 않는다.
    /// 카드 프리팹 3개는 없으면 만들고, 있으면 「Upgrade Card Prefabs」 메뉴로 그 자리에서 지금 생김새에 맞춘다 (UpgradeCard — 카드 셰이더 Chapchu/Card *).
    /// 내 카드(손패 · 내는 카드)는 HandCards 레이어 — 메인 카메라 자식인 손패 카메라가 깊이만 지우고 그 위에 그린다. 기울인 카드(4° · 40°)의 윗부분이 덱 뒤로 들어가 가려지지 않게 (10-ui.md 2-1 「3D 층 구현」 손패 층).
    /// HUD 를 덮어야 하는 카드(올린 손패 · 끄는 카드 · 대상 고르는 카드)는 TopCards 레이어 — 앞 층 카메라가 렌더 텍스처에 그리고 HUD 의 RawImage 가 얹는다 (TopCardLayerView).
    /// </summary>
    public static class TableBuilder
    {
        public const float UnitsPerPixel = 0.01f;
        // 시안 perspective 1500px 을 캔버스 높이 1080 에 대면 수직 FOV 2·atan(540 / 1500) ≈ 40°.
        public const float CameraFieldOfView = 40f;
        // 1080px(10.8 유닛)이 화면을 채우는 거리 — 5.4 / tan 20° ≈ 14.85.
        public static readonly float CameraDistance = 5.4f / Mathf.Tan(CameraFieldOfView * 0.5f * Mathf.Deg2Rad);

        internal const string TableName = "Table";
        private const string HandCameraName = "HandCamera";
        private const string TopCameraName = "TopCardCamera";
        private const string TopChainCameraName = "TopChainCamera";
        public const string HandLayerName = "HandCards"; // 내 카드 전용 레이어 — TagManager 에 없으면 빈 사용자 칸(8~)에 만든다
        private const string PrefabFolder = "Assets/Prefab/Table";
        private const string MaterialFolder = "Assets/Materials/Cards";
        private const string CardShaderName = "Unlit/Transparent Cutout"; // 덱 · 함정 칸 · 받침 카드 — 알파 컷아웃 + ZWrite, 뒷면 컬링 (카드 프리팹은 Chapchu/Card *)
        private const string BackTexturePath = GameScreenBuilder.CardBackSpritePath;
        private const float CardThickness = 0.02f; // 더미 장당 두께
        private const float LayerGap = 0.005f;     // 그림 · 글자를 틀 앞에 띄우는 간격 — 같은 면이면 z 싸움

        // 카드 226 × 336 px → 2.26 × 3.36 유닛. 기준점 아래 가운데 (10-ui.md 2-1 「손패」). 앞면(틀 이미지) 크기 — 콜라이더 · 손패 간격의 기준.
        // 생김새는 시안 통합 UI v3.20 카드 4번 (.card 앞면 110 × 164 px) — 시안 px 는 가로 226/110 · 세로 336/164 배 해서 캔버스 px 로 적는다.
        private static readonly Vector2 CardSize = new Vector2(2.26f, 3.36f);
        // 그림 칸 · 이름 띠 · 설명 칸 (앞면 대비 비율, 왼쪽 아래 기준) — 시안 .card .art · .nm · .tx.
        private static readonly Rect ArtBox = Rect.MinMaxRect(0.056f, 0.399f, 0.944f, 0.844f);
        private static readonly Rect NameBox = Rect.MinMaxRect(0.295f, 0.87f, 0.89f, 0.956f);
        private static readonly Rect DescriptionBox = Rect.MinMaxRect(0.08f, 0.055f, 0.92f, 0.365f);
        // 틀의 발바닥 배지 — 틀 그림(820 × 1222) 위의 원 (121, 117) r 109 (시안 .card .bdg). 그림 칸이 이 자리를 뚫어 배지가 그림 위로 온다.
        private static readonly Vector3 BadgeCircle = new Vector3(121f / 820f, 117f / 1222f, 109f / 820f); // x · 위에서 y · 반지름 (틀 폭 · 높이 대비)

        // 글자 (캔버스 px) — 시안 CSS 크기 그대로. 이름 14 px · 긴 이름 11.5 px · 설명 10.5 px · 그림 준비 중 6.6 cqw.
        //   시안 @font-face 의 size-adjust (태백체 .85 · Pretendard .96)는 글꼴 에셋의 Scale 이 맡는다 — 자간 · 줄 높이(TMP em)는 글자 크기 기준이라 Scale 과 상관없다.
        private const string NameFontPath = "Assets/Fonts/Taebaek/Taebaek SDF.asset";                  // 이름 · 그림 준비 중 = 태백체 (시안 --f-d)
        private const string DescriptionFontPath = "Assets/Fonts/Pretendard/Pretendard Bold SDF.asset"; // 설명 = Pretendard 700 (시안 --f-b)
        private const float NameFontPx = 28.76f, NameFontMinPx = 13f, DescriptionFontPx = 21.57f, DescriptionFontMinPx = 11.5f; // 긴 이름(11.5 px) 크기는 CardView
        private const float NameCharacterSpacing = -1f;      // 자간 −.01 em (TMP 단위 em ÷ 100)
        private const float DescriptionLineHeightPx = 28.04f; // 줄 높이 1.3 × 10.5 px
        private const string PendingLabel = "그림 준비 중";
        private const float PendingFontPx = 14.92f;          // 6.6 cqw (카드 폭 1 % = 2.26 px)
        private const float PendingTextYPx = -23.55f;         // 그림 칸 가운데에서 — 발바닥 21 % · 틈 2.2 cqw · 글자 줄을 세로 가운데로 쌓은 자리
        private static readonly Color PendingGray = new Color32(0x8A, 0x86, 0xA6, 0xFF); // 그림 준비 중 글자 — 회보라에 종류 색 45 % (시안)

        // 종이 · 잉크 선 · 그림자 (캔버스 px) — 시안 .card::before (inset −6.8 · 모서리 6.7) · .tpl (모서리 1.5) · .ink (형광펜 12)
        private const float PaperMarginPx = 13.97f;  // 흰 종이 테 — 앞면보다 사방으로
        private const float PaperCornerPx = 13.77f;
        private const float FaceCornerPx = 3.08f;    // 틀 그림 모서리
        private const float InkWidthPx = 3f;         // 잉크 선 — 종이 테에 가운데를 맞춘다
        private const float MarkReachPx = 14f;       // 형광펜이 종이 테 바깥으로 가는 거리 — 띠 12 + 흔들림
        // 등급 별 (시안 cardStars) — 종이 상자 안 자리(왼쪽 · 위 %) · 배율. 별 상자 14.6 px = 30 u, viewBox 24 칸, 별 끝 10 칸 + 잉크 테 0.9 칸.
        private static readonly Vector3[] StarPlacements = { new Vector3(0.84f, -0.07f, 1f), new Vector3(0.97f, 0.09f, 0.6f), new Vector3(-0.09f, 0.09f, 0.75f) };
        private const float StarBoxPx = 30f, StarViewBox = 24f, StarReach = 10.9f;
        private const float CardDepth = 0.018f;  // 앞뒤 면 사이 두께 (시안 THICK 3) — 손패 장당 간격 0.02 보다 얇게 해 옆 카드와 겹치지 않는다
        private const float FaceGap = 0.003f;    // 그림 · 글자를 틀 앞에 띄우는 간격
        // 그림자 — 쉴 때 0 8px 12px rgba(70,45,30,.3). 포인터 아래 · 고른 카드 값은 CardView 가 갖고, 판은 가장 큰 것(고른 카드 0 17px 24px)이 들어갈 만큼
        private const float ShadowDropPx = 16.39f, ShadowBlurPx = 24.65f;
        private const float ShadowReachPx = 54f, ShadowDropMaxPx = 36f;
        private const float ShadowBehind = 0.012f;
        private static readonly Color CardInk = new Color32(0x2B, 0x28, 0x56, 0xFF);            // 시안 카드 잉크 — 옆면 · 설명 글자
        private static readonly Color CardShadowColor = new Color(70f / 255f, 45f / 255f, 30f / 255f, 0.3f);
        private const string MeshFolder = "Assets/Prefab/Table/Meshes";
        private const string CardPaperShaderName = "Chapchu/Card Paper";
        private const string CardArtShaderName = "Chapchu/Card Art";
        private const string CardSoftShaderName = "Chapchu/Card Soft";
        private const string CardEdgeShaderName = "Chapchu/Card Edge";

        // 카드 한 장 안의 그리는 순서 — 루트의 SortingGroup 이 카드를 묶고, 그 안에서 이 순서로 그린다.
        private enum CardPart { Shadow, Edge, Back, Frame, Art, Text }

        // 종류 색 (시안 --act · --ctr · --trp) — 그림 준비 중 칸 · 글자
        private static Color TypeColor(CardType type) => type switch
        {
            CardType.Counter => new Color32(0x6F, 0xA4, 0x34, 0xFF),
            CardType.Trap => new Color32(0xF6, 0x46, 0x12, 0xFF),
            _ => new Color32(0x00, 0x9D, 0xFF, 0xFF),
        };

        private static Vector2 FaceHalfPx => CardSize / UnitsPerPixel / 2f;
        private static Vector2 PaperHalfPx => FaceHalfPx + Vector2.one * PaperMarginPx;

        // 별 가운데 (카드 가운데 기준, 위가 + px) · 배율
        private static Vector3 StarPx(int index)
        {
            Vector3 placement = StarPlacements[index];
            Vector2 paper = PaperHalfPx * 2f;
            return new Vector3(placement.x * paper.x + StarBoxPx / 2f - paper.x / 2f, paper.y / 2f - (placement.y * paper.y + StarBoxPx / 2f), placement.z);
        }

        // 종이 면 Quad — 종이 + 형광펜, 별이 들어가는 크기 (px)
        private static Vector2 PaperQuadPx
        {
            get
            {
                Vector2 half = PaperHalfPx + Vector2.one * MarkReachPx;
                for (int i = 0; i < StarPlacements.Length; i++)
                {
                    Vector3 star = StarPx(i);
                    float reach = StarReach * StarBoxPx / StarViewBox * star.z;
                    half = Vector2.Max(half, new Vector2(Mathf.Abs(star.x) + reach, Mathf.Abs(star.y) + reach));
                }
                return (half + Vector2.one) * 2f; // 가장자리를 매끈하게 할 1 px 씩
            }
        }

        private static Vector2 ShadowQuadPx => (PaperHalfPx + new Vector2(ShadowReachPx, ShadowReachPx + ShadowDropMaxPx)) * 2f;

        // 테이블 방석 — 시안 TABLE (200, 170) 560 × 280 × 2 = 중심 (960, 620) 1120 × 560. 카드 뒤(z +0.01)에 눕힌 그림 한 장.

        // 테이블 면 기울기 — 프로토타입 v2 `.plane` rotateX(40deg): 방석 · 덱 · 버림 더미가 윗변이 멀어지게 눕는다 (10-ui.md 2-1 「테이블 면」, 2026-11-10).
        // 더미마다 자기 가운데 줄을 축으로 돌린다 — 시안의 회전축(y 310)이 덱 · 버림 · 방석 가운데와 같은 줄이라 결과가 같다.
        public const float TableTilt = 40f;
        private static readonly Quaternion TableRotation = Quaternion.Euler(TableTilt, 0f, 0f);

        // 덱 132 × 196 · 중심 (810, 620) (10-ui.md 2-1 「덱」, 프로토타입 v2 `.deck` 66 × 98 · DECK (405, 310) × 2).
        // 맨 위 = 흰 받침(잉크 테) 위에 뒷면 그림. 그 밑에 받침 카드를 판 위에서 앞쪽(화면 아래)으로 밀어 깔아 겹친 두께를 낸다 — 시안 box-shadow 의 흰 · 잉크 띠.
        private static readonly Vector2 DeckCardSize = new Vector2(1.32f, 1.96f);
        private static readonly Vector3 DeckPosition = CanvasToWorld(810f, 620f);
        private const float DeckBackInsetPx = 8f;                         // 뒷면 그림을 흰 받침 안쪽으로 — 잉크 테가 보이게 (시안 외곽선 2.5 × 2)
        private static readonly float[] DeckUnderOffsetsPx = { 6f, 16f }; // 받침 카드, 위부터 — 판 위에서 앞쪽으로 민 거리
        private const float DeckCountFontPx = 30f; // 시안 .lab 15 × 2
        private static readonly Vector2 DeckCountSize = new Vector2(1.6f, 0.3f);
        // 「덱 n」 · 「버림 n」 윗변 = 판 위에서 더미 가운데보다 116px 앞 (시안 .lab top DECK.y + 58). 글자는 눕히지 않고 선다.
        private const float PileLabelTopPx = 116f;

        // 손패 루트 — 카드 자리는 HandView 가 화면 아래 끝을 기준으로 계산하므로(10-ui.md 2-1 「부채꼴」) 루트는 손패 평면(z 0)만 정한다. 화면 아래 끝 가운데에 둔다.
        private static readonly Vector3 HandPosition = CanvasToWorld(960f, 1080f);

        // 버림 자리 — 시안 DISC (555, 310) × 2: 방석 오른쪽 (HUD v2). 내는 자리(960, 620)는 1 단계와 함께 없앴다 (2026-11-11).
        private static readonly Vector3 DiscardSpotPosition = CanvasToWorld(1110f, 620f);

        // 3 단계 확대 · 홀드 자리 — 프로토타입 v2 `TIME.big 1.85` · `BIGY 262` × 2: 카드 가운데가 캔버스 (960, 524), 1.85배 (10-ui.md 2-1 「카드 연출 자리」).
        // 내 손패(호버 z −0.5)보다 카메라 쪽(z −1.5)에 두고, 가까워진 만큼 위치 · 배율을 줄여 화면에서는 그 자리 · 그 크기로 보이게 한다.
        private const float ShowCenterYPx = 524f;
        private const float ShowScale = 1.85f;
        private const float ShowZ = -1.5f;
        // 3 단계 동안 3D 층을 덮는 어두운 판 — 손패(호버 포함)보다 앞, 크게 둔 카드보다 뒤. 손패 층이라 테이블 · 덱 위에도 그려진다.
        private const float DimZ = -0.8f;
        private static readonly Vector2 DimSize = new Vector2(80f, 40f);
        private const string DimShaderName = "Sprites/Default"; // 색 · 알파만 있는 반투명 Unlit

        // 버림 더미 (10-ui.md 2-1 「버림 더미」, 시안 DISC) — 카드 0.6배. 받침 흰 카드 2장(아래부터: 캔버스 px 오프셋 · 반시계 각도) 위에 맨 위 카드 −3°.
        private const float DiscardCardScale = 0.6f;
        private const float DiscardTopAngle = -3f;
        private static readonly (Vector2 offsetPx, float angle)[] DiscardUnderCards = { (new Vector2(-20f, -8f), 7f), (new Vector2(-10f, -4f), 3f) };
        // 받침 흰 카드 그림 — 없으면 만든다. 카드 0.6배(136 × 202)의 2배 크기, 흰 바탕 · 잉크 테 4 · 반지름 14 (캔버스 px).
        private const string BlankTexturePath = "Assets/Sprites/Cards/Frames/CardBlank.png";
        private const int BlankTexturePixelsPerPx = 2;
        private const float BlankBorderPx = 4f, BlankRadiusPx = 14f;

        private static readonly Color Ink = new Color32(0x2a, 0x24, 0x20, 0xFF);

        // 함정 칸 (10-ui.md 2-1 「함정 칸」) — 프로토타입 v2 `MYTRAP` · `GEO.trap` · `.slot` × 2, 테이블 면 위 좌표. 칸 가운데 (캔버스 px) · 칸 사이 간격 · 폭 · 눕힌 방향(Z 각도).
        // 칸은 카드 비율로 그린다 (폭에 맞춤) — 시안의 칸(52 × 72 · 40 × 56)보다 높이가 조금 길다.
        private static readonly (Vector2 first, Vector2 step, float widthPx, float angle) MyTrapSlots = (new Vector2(900f, 840f), new Vector2(60f, 0f), 52f, 0f);
        private static readonly (Vector2 first, Vector2 step, float widthPx, float angle)[] SeatTrapSlots =
        {
            (new Vector2(472f, 564f), new Vector2(0f, 52f), 40f, -90f),  // 좌석 1 (왼쪽) — 옆으로 눕힘
            (new Vector2(914f, 424f), new Vector2(48f, 0f), 40f, 0f),    // 좌석 2 (정면)
            (new Vector2(1448f, 564f), new Vector2(0f, 52f), 40f, 90f),  // 좌석 3 (오른쪽) — 옆으로 눕힘
        };
        private const int TrapSlotCount = 3;
        private const float TableAxisYPx = 620f; // 테이블 면이 도는 축 (시안 y 310)
        private const float TrapSlotZ = -0.01f;   // 방석보다 조금 앞
        private const string TrapSlotTextureName = "TrapSlotEmpty"; // 빈 칸 — 흰 45% · 잉크 30% 점선 테 (프로토타입 v2 .slot 2px dashed × 2). 줄 너비(px)를 붙여 줄마다 따로 — 배율이 달라 테 · 점선 값이 다르다
        private static readonly Color TrapSlotFill = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color TrapSlotEdge = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.3f);
        private const float TrapSlotDashPx = 12f; // 점선 칸 · 틈 (캔버스 px) — 브라우저 dashed 는 두께 2px 에 칸 · 틈 6px (× 2)

        /// <summary>캔버스 좌표(왼쪽 위 원점 · px) → 테이블 면 월드 좌표.</summary>
        public static Vector3 CanvasToWorld(float x, float y) => new Vector3((x - 960f) * UnitsPerPixel, (540f - y) * UnitsPerPixel, 0f);

        /// <summary>메인 카메라를 원근 · FOV 40 · 테이블 정면 거리로 놓고, 3D 층 포인터용 PhysicsRaycaster 를 붙인다. 없으면 null.</summary>
        public static Camera SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError($"[{nameof(TableBuilder)}] 씬에 MainCamera 태그의 카메라가 없다.");
                return null;
            }

            camera.orthographic = false;
            camera.fieldOfView = CameraFieldOfView;
            // 반투명은 카메라 앞뒤(z)로 줄 세운다 — 원근 기본값(카메라와의 거리)이면 손패 오른쪽 카드가 옆 카드보다 멀다고 보고 먼저 그려
            // 겹친 가장자리가 뒤 카드 위에 섞이지 않는다. 프로젝트 설정(Graphics)이라 모든 카메라에 같다.
            GraphicsSettings.transparencySortMode = TransparencySortMode.Orthographic;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -CameraDistance), Quaternion.identity);
            if (camera.GetComponent<PhysicsRaycaster>() == null)
                camera.gameObject.AddComponent<PhysicsRaycaster>();
            SetupHandCamera(camera);
            SetupTopCamera(camera);
            return camera;
        }

        /// <summary>
        /// 앞 층 그림을 HUD 에 얹는 재질 — 렌더 텍스처의 색은 이미 알파가 곱해져 있으므로(카드 셰이더의 알파 따로 쌓기) One · OneMinusSrcAlpha 로 섞는다.
        /// 기본 UI 재질로 얹으면 반투명 가장자리 · 빛 · 그림자가 알파를 두 번 곱해 어둡게 보인다. 있으면 그대로 쓴다.
        /// </summary>
        public static Material TopLayerCompositeMaterial() =>
            CardMaterial("TopCardsComposite", "Chapchu/UI Fx Blend", material =>
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            });

        /// <summary>
        /// 앞 층의 체인 카메라 — 메인 카메라의 자식(같은 자리 · 같은 화각)으로 TopChainCards 레이어(낸 카드)만 투명 바탕에 그린다.
        /// 그림은 자기 렌더 텍스처에 그리고 HUD 의 체인 층 RawImage 가 얹는다 — 상대 좌석 위 · 반응 타이머 · 손패 층 아래 (사용자 2026-11-17). 평소에는 꺼 두고 뷰가 켠다.
        /// 낸 카드는 누르기를 받지 않으므로 PhysicsRaycaster 는 없다. 이미 있으면 값만 맞춘다.
        /// </summary>
        public static Camera SetupTopChainCamera(Camera main) =>
            SetupTopLayerCamera(main, TopChainCameraName, TopCardLayerView.ChainLayerName, CameraClearFlags.SolidColor, 2, false);

        /// <summary>
        /// 앞 층 카메라 — 메인 카메라의 자식(같은 자리 · 같은 화각)으로 TopCards 레이어(손패)만 그린다. 그림은 TopCardLayerView 가 렌더 텍스처로 받아 HUD 위에 얹는다.
        /// 투명 바탕. 이 층의 RawImage 가 체인 층 · 반응 타이머보다 뒤 형제라 손패가 언제나 앞이다 (사용자 2026-11-17).
        /// 평소에는 꺼 두고 뷰가 켠다. PhysicsRaycaster 는 앞 층 카드가 뒤에 있는 것(내 함정 칸 · 덱)의 탭을 가리게 하려고 붙인다. 이미 있으면 값만 맞춘다.
        /// </summary>
        public static Camera SetupTopCamera(Camera main) =>
            SetupTopLayerCamera(main, TopCameraName, TopCardLayerView.TopLayerName, CameraClearFlags.SolidColor, 3, true);

        /// <summary>앞 층 카메라 공통 — 메인 카메라의 자식으로 한 레이어만 그리고, 메인 카메라 컬링에서는 그 레이어를 뺀다. 평소에는 꺼 둔다. 이미 있으면 값만 맞춘다.</summary>
        private static Camera SetupTopLayerCamera(Camera main, string cameraName, string layerName, CameraClearFlags clearFlags, int depthOffset, bool raycaster)
        {
            int layer = EnsureLayer(layerName);
            main.cullingMask &= ~(1 << layer);

            Transform existing = main.transform.Find(cameraName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(cameraName, typeof(Camera));
            go.transform.SetParent(main.transform, false);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var camera = go.GetComponent<Camera>();
            camera.CopyFrom(main);
            camera.clearFlags = clearFlags;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << layer;
            camera.depth = main.depth + depthOffset;
            camera.enabled = false;
            if (raycaster && go.GetComponent<PhysicsRaycaster>() == null)
                go.AddComponent<PhysicsRaycaster>();
            return camera;
        }

        /// <summary>
        /// 손패 카메라 — 메인 카메라의 자식(같은 자리 · 같은 FOV)으로 HandCards 레이어만, 깊이를 지우고 뒤에 그린다. 메인 카메라는 그 레이어를 빼서 카드가 두 번 그려지지 않게 한다.
        /// 포인터는 메인 카메라의 PhysicsRaycaster 가 컬링 마스크 밖 레이어를 맞추지 않으므로 손패 카메라에도 하나 붙인다 — 카메라 depth 가 높아 덱보다 카드가 먼저 맞는다.
        /// 이미 있으면 값만 맞춘다. 사람이 손패 · 덱 자리를 바꿔도 겹침이 가려지지 않는다.
        /// </summary>
        public static Camera SetupHandCamera(Camera main)
        {
            int layer = EnsureLayer(HandLayerName);
            main.cullingMask &= ~(1 << layer);

            Transform existing = main.transform.Find(HandCameraName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(HandCameraName, typeof(Camera), typeof(PhysicsRaycaster));
            go.transform.SetParent(main.transform, false);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var camera = go.GetComponent<Camera>();
            camera.CopyFrom(main);
            camera.clearFlags = CameraClearFlags.Depth;
            camera.cullingMask = 1 << layer;
            camera.depth = main.depth + 1;
            if (go.GetComponent<PhysicsRaycaster>() == null)
                go.AddComponent<PhysicsRaycaster>();
            return camera;
        }

        /// <summary>이름의 레이어 번호. TagManager 에 없으면 빈 사용자 칸(8 이상)에 만든다. 빈 칸이 없으면 LogError 와 Default(0).</summary>
        public static int EnsureLayer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0)
                return layer;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }

            Debug.LogError($"[{nameof(TableBuilder)}] 빈 레이어 칸이 없어 {name} 레이어를 만들지 못했다 — 내 카드가 덱 뒤로 가려진다.");
            return 0;
        }

        /// <summary>오브젝트와 자식 전부를 HandCards 레이어로 — 손패 카메라가 그리게. 카드 프리팹을 만들 때와 기존 프리팹을 고칠 때 쓴다.</summary>
        public static void SetHandLayer(GameObject root)
        {
            int layer = EnsureLayer(HandLayerName);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        /// <summary>3D 층 루트. 이미 있으면 덮어쓰지 않고 null.</summary>
        public static Transform BuildTable()
        {
            if (GameObject.Find(TableName) != null)
            {
                Debug.LogError($"[{nameof(TableBuilder)}] 씬에 {TableName} 이 이미 있다. 덮어쓰지 않는다 — 지우고 다시 실행할 것.");
                return null;
            }

            return new GameObject(TableName).transform;
        }

        public static DeckView BuildDeck(Transform table)
        {
            var root = new GameObject("Deck");
            root.transform.SetParent(table, false);
            root.transform.localPosition = DeckPosition;

            var view = root.AddComponent<DeckView>();
            BuildDeckParts(view);

            var presenter = root.AddComponent<DeckPresenter>();
            GameScreenBuilder.Wire(presenter, ("server", FindServer(nameof(DeckPresenter))));
            return view;
        }

        /// <summary>
        /// 덱 뷰 아래를 지우고 다시 만든다 — 판에 눕힌 `Stack`(받침 카드 · 맨 위 카드 · 탭 콜라이더) + 선 채로 둔 `덱 n`. 뷰의 참조를 다시 잇는다.
        /// 루트(뷰 · 프레젠터)는 그대로라 다른 곳의 참조(손패의 뽑기 자리 등)는 끊기지 않는다.
        /// </summary>
        public static void BuildDeckParts(DeckView view)
        {
            Transform root = view.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);

            var stackGo = new GameObject("Stack", typeof(BoxCollider));
            Transform stack = stackGo.transform;
            stack.SetParent(root, false);
            stack.localRotation = TableRotation;

            // 받침 카드: 아래(테이블 쪽)부터 깔아 맨 위 카드가 카메라 쪽에 오게 한다. 뷰에는 위부터 넘긴다.
            Material blank = EnsureMaterial("CardBlank", EnsureBlankTexture(DeckCardSize));
            int underCount = DeckUnderOffsetsPx.Length;
            var underCards = new Object[underCount];
            for (int i = underCount - 1; i >= 0; i--)
            {
                float z = (underCount - 1 - i) * -CardThickness;
                underCards[i] = Quad($"Under{i}", stack, blank, DeckCardSize, new Vector3(0f, -DeckUnderOffsetsPx[i] * UnitsPerPixel, z), Quaternion.identity).gameObject;
            }

            // 맨 위 카드 = 흰 받침 + 뒷면 그림. 0장이면 뷰가 숨긴다 — 콜라이더는 Stack 에 있어 탭 영역은 남는다.
            float topZ = -underCount * CardThickness;
            Transform top = Child("Top", stack, new Vector3(0f, 0f, topZ), Quaternion.identity);
            Quad("Base", top, blank, DeckCardSize, Vector3.zero, Quaternion.identity);
            Vector2 backSize = DeckCardSize - Vector2.one * (DeckBackInsetPx * 2f * UnitsPerPixel);
            Quad("Back", top, EnsureMaterial("CardBack", BackTexturePath), backSize, new Vector3(0f, 0f, -LayerGap), Quaternion.identity);

            // 탭 영역 = 맨 위 카드 + 앞으로 밀린 받침 카드까지
            float reach = DeckUnderOffsetsPx[underCount - 1] * UnitsPerPixel;
            var collider = stackGo.GetComponent<BoxCollider>();
            collider.size = new Vector3(DeckCardSize.x, DeckCardSize.y + reach, (underCount + 1) * CardThickness);
            collider.center = new Vector3(0f, -reach / 2f, topZ / 2f);

            TMP_Text count = NewText3D("CountText", root, "덱 -", DeckCountFontPx, Ink, true, TextAlignmentOptions.Center, DeckCountSize, PileLabelPosition());

            GameScreenBuilder.Wire(view, ("countText", count), ("topCard", top.gameObject));
            GameScreenBuilder.WireArray(view, "underCards", underCards);
        }

        // 「덱 n」 · 「버림 n」 자리 — 판 위 라벨 윗변 점을 판과 같이 돌린 곳에서 글자 높이 반만큼 아래. 글자는 서 있으므로 시안처럼 판 위 점에 붙어 보인다.
        private static Vector3 PileLabelPosition()
        {
            Vector3 top = TableRotation * new Vector3(0f, -PileLabelTopPx * UnitsPerPixel, 0f);
            return top + new Vector3(0f, -DeckCountSize.y / 2f, -LayerGap);
        }

        /// <summary>
        /// 버림 더미 — 받침 흰 카드 + 맨 위 카드 자리 + `버림 n` (10-ui.md 2-1 「버림 더미」). 덱과 같은 기본 층이라 내 카드(손패 층)가 항상 위에 그려진다.
        /// 맨 위 카드 자리는 가운데를 축으로 돌린 `Top` 아래, 카드 기준점(아래 가운데)만큼 내린 `TopAnchor` — 날아오는 카드가 이 자세 · 배율로 닿는다.
        /// </summary>
        public static DiscardPileView BuildDiscardPile(Transform table, DeckData gameDeck)
        {
            var root = new GameObject("DiscardPile");
            root.transform.SetParent(table, false);
            root.transform.localPosition = DiscardSpotPosition;

            // 카드는 판에 눕힌 `Pile` 아래 — 날아오는 카드도 TopAnchor 의 누운 자세로 닿는다. 「버림 n」 은 서 있게 루트에.
            Transform pile = Child("Pile", root.transform, Vector3.zero, TableRotation);

            Vector2 cardSize = CardSize * DiscardCardScale;
            Material blank = EnsureMaterial("CardBlank", EnsureBlankTexture(cardSize));
            var underCards = new Object[DiscardUnderCards.Length];
            for (int i = 0; i < DiscardUnderCards.Length; i++)
            {
                (Vector2 offsetPx, float angle) = DiscardUnderCards[i];
                var position = new Vector3(offsetPx.x * UnitsPerPixel, -offsetPx.y * UnitsPerPixel, -i * CardThickness);
                underCards[i] = Quad($"Under{i}", pile, blank, cardSize, position, Quaternion.Euler(0f, 0f, angle)).gameObject;
            }

            float topZ = -DiscardUnderCards.Length * CardThickness;
            Transform top = Child("Top", pile, new Vector3(0f, 0f, topZ), Quaternion.Euler(0f, 0f, DiscardTopAngle));
            Transform topAnchor = Child("TopAnchor", top, new Vector3(0f, -cardSize.y / 2f, 0f), Quaternion.identity);
            topAnchor.localScale = Vector3.one * DiscardCardScale;

            // 덱 라벨과 같은 줄 · 같은 모양 (시안 `.lab`)
            TMP_Text count = NewText3D("CountText", root.transform, "버림 0", DeckCountFontPx, Ink, true, TextAlignmentOptions.Center,
                DeckCountSize, PileLabelPosition());

            var view = root.AddComponent<DiscardPileView>();
            GameScreenBuilder.Wire(view,
                ("actionCardPrefab", EnsureCardPrefab(CardType.Action)),
                ("counterCardPrefab", EnsureCardPrefab(CardType.Counter)),
                ("trapCardPrefab", EnsureCardPrefab(CardType.Trap)),
                ("topAnchor", topAnchor), ("countText", count));
            GameScreenBuilder.WireArray(view, "underCards", underCards);

            var presenter = root.AddComponent<DiscardPilePresenter>();
            GameScreenBuilder.Wire(presenter, ("deck", gameDeck));
            return view;
        }

        public static HandPresenter BuildHand(Transform table, DeckView deck, DiscardPileView discardPile, OpponentSeatsPresenter seats, DeckData gameDeck,
            PlayLineView playLine, TopCardLayerView topLayer, HandRevealView handReveal, Object[] coveredHud)
        {
            var root = new GameObject("Hand");
            root.transform.SetParent(table, false);
            root.transform.localPosition = HandPosition;

            var view = root.AddComponent<HandView>();
            GameScreenBuilder.Wire(view,
                ("actionCardPrefab", EnsureCardPrefab(CardType.Action)),
                ("counterCardPrefab", EnsureCardPrefab(CardType.Counter)),
                ("trapCardPrefab", EnsureCardPrefab(CardType.Trap)),
                ("topLayer", topLayer));
            root.AddComponent<HandInput>();

            // 버려지는 내 카드는 버림 더미의 맨 위 카드 자리로 날아가 닿는다
            var presenter = root.AddComponent<HandPresenter>();
            GameScreenBuilder.Wire(presenter,
                ("deck", gameDeck), ("drawOrigin", deck.transform), ("server", FindServer(nameof(HandPresenter))), ("seats", seats),
                ("discardSpot", discardPile.TopAnchor), ("playLine", playLine), ("topLayer", topLayer), ("revealView", handReveal));
            GameScreenBuilder.WireArray(presenter, "coveredHud", coveredHud);
            return presenter;
        }

        /// <summary>
        /// 체인 카드 — 내는 자리(1 단계) · 크게 두는 자리(3 단계 · 홀드) · 어둡게 덮는 판 (10-ui.md 2-1 「카드 연출 자리」 · 5-1).
        /// 열린 씬에 이것만 더할 때도 쓴다 — 이미 있으면 덮어쓰지 않고 null.
        /// </summary>
        public static ChainCardsPresenter BuildChainCards(Transform table, DiscardPileView discardPile, OpponentSeatsPresenter seats, CardOwnerTagView ownerTag, DeckData gameDeck, TopCardLayerView topLayer)
        {
            const string name = "ChainCards";
            if (table.Find(name) != null)
            {
                Debug.LogError($"[{nameof(TableBuilder)}] {TableName} 에 {name} 가 이미 있다. 덮어쓰지 않는다.");
                return null;
            }

            Transform root = Child(name, table, Vector3.zero, Quaternion.identity);

            // 카드 기준점이 아래 가운데라, 자리 = 카드 가운데에서 (카드 높이 × 배율 ÷ 2) 아래. 카메라에 가까워진 비율 k 만큼 위치 · 배율을 줄인다.
            float k = (CameraDistance + ShowZ) / CameraDistance;
            Vector3 bottom = CanvasToWorld(960f, ShowCenterYPx + CardSize.y / UnitsPerPixel * ShowScale / 2f) * k;
            Transform showSpot = Spot("ShowSpot", root, new Vector3(bottom.x, bottom.y, ShowZ));
            showSpot.localScale = Vector3.one * (ShowScale * k);

            MeshRenderer dimmer = Quad("Dimmer", root, EnsureDimMaterial(), DimSize, new Vector3(0f, 0f, DimZ), Quaternion.identity);
            SetHandLayer(dimmer.gameObject);
            dimmer.gameObject.SetActive(false);

            var presenter = root.gameObject.AddComponent<ChainCardsPresenter>();
            GameScreenBuilder.Wire(presenter,
                ("deck", gameDeck),
                ("actionCardPrefab", EnsureCardPrefab(CardType.Action)),
                ("counterCardPrefab", EnsureCardPrefab(CardType.Counter)),
                ("trapCardPrefab", EnsureCardPrefab(CardType.Trap)),
                ("seats", seats), ("ownerTag", ownerTag), ("dimmer", dimmer), ("topLayer", topLayer),
                ("showSpot", showSpot), ("discardSpot", discardPile.TopAnchor));
            return presenter;
        }

        /// <summary>
        /// 함정 칸 — 내 칸 3 + 좌석마다 3, 테이블 면에 누움 (10-ui.md 2-1 「함정 칸」). 좌석의 칸은 OpponentSeatsPresenter 에 잇고, 내 칸의 프레젠터를 돌려준다.
        /// 열린 씬에 이것만 더할 때도 쓴다 — 이미 있으면 덮어쓰지 않고 null.
        /// </summary>
        public static MyTrapsPresenter BuildTrapSlots(Transform table, OpponentSeatsPresenter seats, Transform previewSpot, DeckData gameDeck)
        {
            const string name = "TrapSlots";
            if (table.Find(name) != null)
            {
                Debug.LogError($"[{nameof(TableBuilder)}] {TableName} 에 {name} 가 이미 있다. 덮어쓰지 않는다.");
                return null;
            }

            Transform root = Child(name, table, Vector3.zero, Quaternion.identity);

            var seatSlots = new Object[SeatTrapSlots.Length];
            for (int i = 0; i < SeatTrapSlots.Length; i++)
                seatSlots[i] = BuildTrapSlotRow($"Seat{i + 1}", root, SeatTrapSlots[i], false);
            GameScreenBuilder.WireArray(seats, "trapSlots", seatSlots);

            TrapSlotsView mine = BuildTrapSlotRow("Mine", root, MyTrapSlots, true);
            var presenter = mine.gameObject.AddComponent<MyTrapsPresenter>();
            GameScreenBuilder.Wire(presenter,
                ("deck", gameDeck), ("trapCardPrefab", EnsureCardPrefab(CardType.Trap)), ("previewSpot", previewSpot));
            return presenter;
        }

        // 칸 3개 한 줄. 칸 루트 = 카드 기준점(아래 가운데) · 카드 배율 — 카드가 이 자리 · 회전 · 배율로 들어가고 나온다. tappable 이면 콜라이더 (내 칸).
        private static TrapSlotsView BuildTrapSlotRow(string name, Transform parent, (Vector2 first, Vector2 step, float widthPx, float angle) row, bool tappable)
        {
            Transform rowRoot = Child(name, parent, Vector3.zero, Quaternion.identity);
            float scale = row.widthPx * UnitsPerPixel / CardSize.x;
            Quaternion rotation = TableRotation * Quaternion.Euler(0f, 0f, row.angle);

            string emptyName = $"{TrapSlotTextureName}{Mathf.RoundToInt(row.widthPx)}";
            Material emptyMaterial = EnsureAlphaMaterial(emptyName, EnsureRoundTexture($"{GameScreenBuilder.CardFrameFolder}/{emptyName}.png", CardSize, 4f / scale, 10f / scale, TrapSlotFill, TrapSlotEdge, TrapSlotDashPx / scale, TrapSlotDashPx / scale));
            Material blank = EnsureMaterial("CardBlank", EnsureBlankTexture(CardSize));
            Material back = EnsureMaterial("CardBack", BackTexturePath);

            var slots = new Object[TrapSlotCount];
            for (int i = 0; i < TrapSlotCount; i++)
            {
                Vector2 center = row.first + row.step * i;
                Vector3 centerWorld = CanvasToWorld(center.x, TableAxisYPx) + TableRotation * new Vector3(0f, (TableAxisYPx - center.y) * UnitsPerPixel, TrapSlotZ);
                Vector3 bottom = centerWorld - rotation * new Vector3(0f, CardSize.y * scale / 2f, 0f);

                Transform slot = Child($"Slot{i}", rowRoot, bottom, rotation);
                slot.localScale = Vector3.one * scale;
                var middle = new Vector3(0f, CardSize.y / 2f, 0f);

                GameObject empty = Quad("Empty", slot, emptyMaterial, CardSize, middle, Quaternion.identity).gameObject;

                Transform filled = Child("Filled", slot, middle + new Vector3(0f, 0f, -LayerGap / scale), Quaternion.identity);
                Quad("Base", filled, blank, CardSize, Vector3.zero, Quaternion.identity);
                Quad("Back", filled, back, CardSize - Vector2.one * (8f * UnitsPerPixel / scale), new Vector3(0f, 0f, -LayerGap / scale), Quaternion.identity);
                filled.gameObject.SetActive(false);

                if (tappable)
                {
                    var collider = slot.gameObject.AddComponent<BoxCollider>();
                    collider.size = new Vector3(CardSize.x, CardSize.y, CardThickness);
                    collider.center = middle;
                }

                var view = slot.gameObject.AddComponent<TrapSlotView>();
                GameScreenBuilder.Wire(view, ("empty", empty), ("filled", filled.gameObject));
                slots[i] = view;
            }

            var rowView = rowRoot.gameObject.AddComponent<TrapSlotsView>();
            GameScreenBuilder.WireArray(rowView, "slots", slots);
            return rowView;
        }

        // 반투명 판 머티리얼 (알파 블렌드) — 빈 함정 칸. 있으면 덮어쓰지 않는다.
        private static Material EnsureAlphaMaterial(string name, string texturePath)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find(DimShaderName)) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 둥근 사각형 그림 — 안쪽 fill, 테두리 edge. 있으면 덮어쓰지 않는다. 안쪽은 색의 알파 그대로(알파 블렌드용). 크기 · 테 · 반지름은 유닛 · 캔버스 px.
        // dashPx > 0 이면 테두리를 점선으로 — 틈에는 fill (CSS dashed 처럼 바탕이 테두리 밑까지 깔린다).
        private static string EnsureRoundTexture(string path, Vector2 size, float borderPx, float radiusPx, Color fill, Color edge, float dashPx = 0f, float gapPx = 0f)
        {
            if (File.Exists(path))
                return path;

            float scale = BlankTexturePixelsPerPx / UnitsPerPixel;
            int width = Mathf.RoundToInt(size.x * scale), height = Mathf.RoundToInt(size.y * scale);
            float radius = radiusPx * BlankTexturePixelsPerPx, border = borderPx * BlankTexturePixelsPerPx;
            float dash = dashPx * BlankTexturePixelsPerPx, gap = gapPx * BlankTexturePixelsPerPx;
            var half = new Vector2(width / 2f, height / 2f);

            // 점선의 칸 = fill 위에 edge 를 알파 합성한 색 — 칸 자리에서도 바탕 판이 빠지지 않는다
            Color dashEdge = edge;
            if (dash > 0f)
            {
                float alpha = edge.a + fill.a * (1f - edge.a);
                dashEdge = alpha > 0f ? (edge * edge.a + fill * (fill.a * (1f - edge.a))) / alpha : Color.clear;
                dashEdge.a = alpha;
            }

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var d = new Vector2(Mathf.Abs(x + 0.5f - half.x), Mathf.Abs(y + 0.5f - half.y)) - half + Vector2.one * radius;
                    float distance = Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;

                    bool inDash = dash <= 0f || OnDash(new Vector2(x + 0.5f - half.x, y + 0.5f - half.y), half, radius, dash, gap);
                    Color color = Color.Lerp(fill, inDash ? dashEdge : fill, Mathf.Clamp01(distance + border + 0.5f));
                    if (distance > 0f)
                        color.a = 0f;
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return path;
        }

        // 점선의 칸 위인가 (p = 가운데 기준 픽셀 위치). 변마다 모서리 호의 가운데에서 가운데까지를 한 변으로 보고, 칸이 양 끝 모서리에 걸치도록 간격을 맞춘다 —
        // 모서리마다 칸이 하나 놓이고 변 길이가 달라도 칸이 잘리지 않는다. 네 귀퉁이가 같은 모양이라 한 귀퉁이(|p|)로 잰다.
        private static bool OnDash(Vector2 p, Vector2 half, float radius, float dash, float gap)
        {
            float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
            var corner = new Vector2(half.x - radius, half.y - radius);
            bool vertical;
            float along; // 변 가운데부터 잰 길이
            if (ax > corner.x && ay > corner.y)
            {
                float angle = Mathf.Atan2(ay - corner.y, ax - corner.x);
                vertical = angle < Mathf.PI / 4f;
                along = vertical ? corner.y + angle * radius : corner.x + (Mathf.PI / 2f - angle) * radius;
            }
            else
            {
                vertical = ay <= corner.y && (ax > corner.x || half.x - ax <= half.y - ay);
                along = vertical ? ay : ax;
            }

            float side = 2f * (vertical ? corner.y : corner.x) + Mathf.PI / 2f * radius;
            float period = side / Mathf.Max(1, Mathf.RoundToInt(side / (dash + gap)));
            return (side / 2f + along + dash / 2f) % period < dash;
        }

        // 어두운 판 머티리얼 — 잉크 색 · 알파 0 에서 시작 (ChainCardsPresenter 가 알파를 올린다). 있으면 덮어쓰지 않는다.
        private static Material EnsureDimMaterial()
        {
            string path = $"{MaterialFolder}/TableDim.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find(DimShaderName);
            if (shader == null)
                Debug.LogError($"[{nameof(TableBuilder)}] 셰이더가 없다: {DimShaderName}");

            var material = new Material(shader) { color = new Color(Ink.r, Ink.g, Ink.b, 0f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static readonly (string name, string framePath, CardType type)[] CardPrefabs =
        {
            ("Card_Action", GameScreenBuilder.ActionCardFramePath, CardType.Action),
            ("Card_Counter", GameScreenBuilder.CounterCardFramePath, CardType.Counter),
            ("Card_Trap", GameScreenBuilder.TrapCardFramePath, CardType.Trap),
        };

        // 종류의 카드 프리팹 — CardPrefabs 표에서 이름 · 틀 경로를 찾는다.
        private static CardView EnsureCardPrefab(CardType type)
        {
            foreach ((string name, string framePath, CardType entryType) in CardPrefabs)
                if (entryType == type)
                    return EnsureCardPrefab(name, framePath, type);
            throw new System.ArgumentOutOfRangeException(nameof(type), type, "CardPrefabs 표에 없는 카드 종류");
        }

        private const int EdgeCornerSegments = 8;

        /// <summary>카드 프리팹 3개를 같은 경로에 다시 만든다 (GUID 유지 — 씬 · HandView 참조가 그대로). 사람이 프리팹을 손봤다면 지워진다.</summary>
        [MenuItem("Chapchu/Game Screen/Rebuild Table Card Prefabs")]
        public static void RebuildCardPrefabs()
        {
            foreach ((string name, string framePath, CardType type) in CardPrefabs)
                EnsureCardPrefab(name, framePath, type, true);
            Debug.Log($"[{nameof(TableBuilder)}] 3D 카드 프리팹 3개 다시 만듦.");
        }

        /// <summary>이미 있는 카드 프리팹 3개를 그 자리에서 지금 생김새로 맞춘다 (UpgradeCard) — 프리팹 안 오브젝트가 그대로라 사람이 더한 자식은 남는다.</summary>
        [MenuItem("Chapchu/Game Screen/Upgrade Card Prefabs")]
        public static void UpgradeCardPrefabs()
        {
            foreach ((string name, string framePath, CardType type) in CardPrefabs)
            {
                string path = $"{PrefabFolder}/{name}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                UpgradeCard(root, type, framePath);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(TableBuilder)}] 3D 카드 프리팹 3개를 지금 생김새로 맞춤.");
        }

        /// <summary>
        /// 카드 한 장의 부분을 지금 생김새로 맞춘다 (시안 통합 UI v3.20 카드 4번 · v3.21 그림 준비 중). 없는 부분은 만들고, 있는 부분은 자리 · 크기 · 머티리얼 · 글자 모양을 맞춘다.
        ///   루트(콜라이더 · CardView · SortingGroup, 기준점 아래 가운데) ─ Shadow · Edge · Front(Frame · Art · Name · Description · Pending) · Back(Back)
        /// 앞면 · 뒷면은 두께(CardDepth)만큼 떨어져 서로 반대를 보고, 셰이더가 뒷면을 컬링하므로 보는 쪽만 그려진다. 전부 HandCards 레이어.
        /// 옛 생김새의 부분(빛 · 고른 테 · 오라 · 개판 별 · 덮는 판)은 지운다. 머티리얼의 모양 값(크기 · 모서리 · 별 자리)은 늘 맞추고,
        /// 색 · 흔들림 같은 생김새 값은 셰이더 기본값을 새로 만들 때(또는 옛 셰이더에서 바꿀 때)만 넣는다 — 사람이 머티리얼에서 고친 값은 덮어쓰지 않는다.
        /// </summary>
        public static void UpgradeCard(GameObject root, CardType type, string framePath)
        {
            Transform card = root.transform;
            var center = new Vector3(0f, CardSize.y / 2f, 0f);
            Color typeColor = TypeColor(type);

            var collider = Ensure<BoxCollider>(root);
            collider.size = new Vector3(CardSize.x, CardSize.y, CardThickness);
            collider.center = center;
            Ensure<SortingGroup>(root);
            if (!root.TryGetComponent(out CardView view))
                view = root.AddComponent<CardView>();

            foreach (string old in new[] { "Glow", "SelectRing", "Aura", "Sparks", "Shade" })
                RemoveChild(card, old);

            MeshRenderer shadow = EnsureQuad("Shadow", card, ShadowMaterial(), ShadowQuadPx * UnitsPerPixel, center + new Vector3(0f, 0f, ShadowBehind), CardPart.Shadow);
            EnsureEdge(card, center);

            Transform front = EnsureChild("Front", card, center + new Vector3(0f, 0f, -CardDepth / 2f), Quaternion.identity);
            MeshRenderer frame = EnsureQuad("Frame", front, PaperMaterial(Path.GetFileNameWithoutExtension(framePath), framePath), PaperQuadPx * UnitsPerPixel, Vector3.zero, CardPart.Frame);
            MeshRenderer art = EnsureQuad("Art", front, ArtMaterial(), BoxSize(ArtBox), BoxCenter(ArtBox, -FaceGap), CardPart.Art);
            art.enabled = true; // 그림 없는 카드도 「그림 준비 중」 칸을 그린다
            TMP_Text nameText = EnsureNameText(front);
            TMP_Text description = EnsureDescriptionText(front);
            TMP_Text pending = EnsurePendingText(front, typeColor);

            Transform back = EnsureChild("Back", card, center + new Vector3(0f, 0f, CardDepth / 2f), Quaternion.Euler(0f, 180f, 0f));
            MeshRenderer backFace = EnsureQuad("Back", back, PaperMaterial("CardBackFace", BackTexturePath), PaperQuadPx * UnitsPerPixel, Vector3.zero, CardPart.Back);

            // 계층 순서 — 그림자 · 옆면 · 앞면 · 뒷면 (사람이 더한 자식은 그 뒤)
            shadow.transform.SetSiblingIndex(0);
            card.Find("Edge").SetSiblingIndex(1);
            front.SetSiblingIndex(2);
            back.SetSiblingIndex(3);

            GameScreenBuilder.Wire(view,
                ("nameText", nameText), ("descriptionText", description), ("pendingText", pending),
                ("frameRenderer", frame), ("artRenderer", art), ("backRenderer", backFace), ("shadowRenderer", shadow));
            var serialized = new SerializedObject(view);
            serialized.FindProperty("typeColor").colorValue = typeColor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetHandLayer(root);
        }

        // 카드 프리팹 — 빈 루트를 만들고 부분은 UpgradeCard 가 붙인다. 있으면 그대로 돌려준다 (overwrite 면 다시 만든다).
        private static CardView EnsureCardPrefab(string name, string framePath, CardType type, bool overwrite = false)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<CardView>(path);
            if (existing != null && !overwrite)
                return existing;

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefab", "Table");

            var root = new GameObject(name);
            UpgradeCard(root, type, framePath);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<CardView>();
        }

        private static T Ensure<T>(GameObject go) where T : Component => go.TryGetComponent(out T component) ? component : go.AddComponent<T>();

        private static Transform EnsureChild(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            Transform child = parent.Find(name);
            if (child == null)
                return Child(name, parent, localPosition, localRotation);

            child.SetLocalPositionAndRotation(localPosition, localRotation);
            return child;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }

        // 이름의 Quad 자식 — 없으면 만들고, 자리 · 크기 · 머티리얼 · 그리는 순서를 맞춘다.
        private static MeshRenderer EnsureQuad(string name, Transform parent, Material material, Vector2 size, Vector3 localPosition, CardPart part)
        {
            Transform existing = parent.Find(name);
            MeshRenderer renderer = existing != null ? existing.GetComponent<MeshRenderer>() : Quad(name, parent, material, size, localPosition, Quaternion.identity);
            renderer.transform.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            renderer.sharedMaterial = material;
            renderer.sortingOrder = (int)part;
            return renderer;
        }

        // 이름 — 머리 띠 가운데, 흰 태백체 (시안 .card .nm). 4 자보다 긴 이름의 크기는 CardView, 그래도 칸을 넘치면 줄이고 말줄임.
        private static TMP_Text EnsureNameText(Transform front)
        {
            TMP_Text text = EnsureText("Name", front, NameFontPath, BoxSize(NameBox), BoxCenter(NameBox, -FaceGap));
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.characterSpacing = NameCharacterSpacing;
            SetFontSize(text, NameFontPx, NameFontMinPx);
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        // 설명 — 아래 칸 가운데, 잉크색 Pretendard 굵게 (시안 .card .tx). 줄은 띄어쓰기에서만 바꾼다(CardView). 칸을 넘치면 줄이고 말줄임.
        private static TMP_Text EnsureDescriptionText(Transform front)
        {
            TMP_Text text = EnsureText("Description", front, DescriptionFontPath, BoxSize(DescriptionBox), BoxCenter(DescriptionBox, -FaceGap));
            text.color = CardInk;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            text.characterSpacing = 0f;
            SetFontSize(text, DescriptionFontPx, DescriptionFontMinPx);
            // 줄 높이 — 글꼴의 기본 줄 높이(글꼴 Scale 이 곱해진다)에 더할 몫 (TMP 단위 em ÷ 100)
            var face = text.font.faceInfo;
            text.lineSpacing = (DescriptionLineHeightPx / DescriptionFontPx - face.lineHeight / face.pointSize * face.scale) * 100f;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        // 그림 준비 중 — 그림 칸 가운데 아래, 회보라에 종류 색을 섞은 태백체 (시안 v3.21 도감 .art.none). 그림이 없는 카드에만 CardView 가 켠다 — 꺼 둔 채로 저장.
        private static TMP_Text EnsurePendingText(Transform front, Color typeColor)
        {
            Vector2 size = new Vector2(BoxSize(ArtBox).x, PendingFontPx * 2f * UnitsPerPixel);
            TMP_Text text = EnsureText("Pending", front, NameFontPath, size, BoxCenter(ArtBox, -FaceGap) + new Vector3(0f, PendingTextYPx * UnitsPerPixel, 0f));
            text.text = PendingLabel;
            text.color = Color.Lerp(PendingGray, typeColor, 0.45f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.characterSpacing = 0f;
            text.enableAutoSizing = false;
            text.fontSize = PendingFontPx / 10f;
            text.overflowMode = TextOverflowModes.Overflow;
            text.gameObject.SetActive(false);
            return text;
        }

        // 카드 글자 크기 — 칸을 넘칠 때만 TMP 자동 크기가 줄인다 (최소에서 말줄임).
        private static void SetFontSize(TMP_Text text, float px, float minPx)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = minPx / 10f;
            text.fontSizeMax = px / 10f;
            text.fontSize = px / 10f;
        }

        // 이름의 글자 자식 — 없으면 만든다. 글꼴(경로) · 자리 · 크기 · 그리는 순서를 맞춘다. 카드 글자만 글꼴을 경로로 — 판의 숫자 글자는 GameScreenBuilder 글꼴.
        private static TMP_Text EnsureText(string name, Transform parent, string fontPath, Vector2 size, Vector3 localPosition)
        {
            Transform existing = parent.Find(name);
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>()
                : NewText3D(name, parent, name, NameFontPx, Color.white, false, TextAlignmentOptions.Center, size, localPosition);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font != null)
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
            else
                Debug.LogError($"[{nameof(TableBuilder)}] 글꼴이 없다: {fontPath}");

            text.fontStyle = FontStyles.Normal;
            var rect = (RectTransform)text.transform;
            rect.sizeDelta = size;
            rect.localPosition = localPosition;
            rect.localRotation = Quaternion.identity;
            text.GetComponent<MeshRenderer>().sortingOrder = (int)CardPart.Text;
            return text;
        }

        // 옆면 — 잉크 선 바깥 둘레를 두께만큼 늘인 띠 (잉크). 뒤집히거나 크게 기울 때 카드가 판처럼 보이게.
        private static void EnsureEdge(Transform card, Vector3 center)
        {
            Transform existing = card.Find("Edge");
            GameObject go = existing != null ? existing.gameObject : new GameObject("Edge", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(card, false);
            go.transform.SetLocalPositionAndRotation(center, Quaternion.identity);
            go.GetComponent<MeshFilter>().sharedMesh = EnsureEdgeMesh();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CardMaterial("CardEdge", CardEdgeShaderName, material => material.color = CardInk);
            renderer.sortingOrder = (int)CardPart.Edge;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // 옆면 메쉬 — 잉크 선 바깥(종이 테 + 선 두께 반) 둥근 사각형 둘레의 벽, 앞뒤 면 사이(두께 CardDepth). 바깥을 보는 면만.
        // 있으면 그 에셋에 다시 채운다 (GUID 유지 — 모양이 카드 크기를 따라간다).
        private static Mesh EnsureEdgeMesh()
        {
            string path = $"{MeshFolder}/CardEdge.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created)
                mesh = new Mesh { name = "CardEdge" };

            Vector2 half = (PaperHalfPx + Vector2.one * (InkWidthPx / 2f)) * UnitsPerPixel;
            float radius = (PaperCornerPx + InkWidthPx / 2f) * UnitsPerPixel;
            // 모서리 넷을 시계 반대 방향으로 (오른쪽 위 → 왼쪽 위 → 왼쪽 아래 → 오른쪽 아래) — 바깥을 보는 면이 앞면이 되는 순서
            float[] cornerX = { 1f, -1f, -1f, 1f }, cornerY = { 1f, 1f, -1f, -1f };
            int perCorner = EdgeCornerSegments + 1, count = perCorner * 4;
            var vertices = new Vector3[count * 2];
            var normals = new Vector3[count * 2];
            for (int i = 0; i < count; i++)
            {
                int corner = i / perCorner;
                float angle = (corner + i % perCorner / (float)EdgeCornerSegments) * Mathf.PI / 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var cornerCenter = new Vector2(cornerX[corner] * (half.x - radius), cornerY[corner] * (half.y - radius));
                Vector2 point = cornerCenter + direction * radius;
                vertices[i * 2] = new Vector3(point.x, point.y, -CardDepth / 2f);
                vertices[i * 2 + 1] = new Vector3(point.x, point.y, CardDepth / 2f);
                normals[i * 2] = normals[i * 2 + 1] = direction;
            }

            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                int a = i * 2, b = (i + 1) % count * 2;
                triangles[i * 6] = a;
                triangles[i * 6 + 1] = b;
                triangles[i * 6 + 2] = b + 1;
                triangles[i * 6 + 3] = a;
                triangles[i * 6 + 4] = b + 1;
                triangles[i * 6 + 5] = a + 1;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            if (created)
                return SaveMesh(mesh, path);

            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder(PrefabFolder, "Meshes");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // 카드 머티리얼 — 없으면 이 셰이더로 만들어 setup 값을 넣는다. 다른 셰이더(옛 카드 셰이더)면 그렇게 새로 만든 것으로 통째로 덮는다
        // (GUID 는 그대로 · 옛 셰이더의 값 · 그림 참조는 남지 않는다). 이미 이 셰이더면 사람이 고친 값을 그대로 둔다 — 모양 값은 부르는 쪽이 늘 맞춘다.
        private static Material CardMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[{nameof(TableBuilder)}] 셰이더가 없다: {shaderName}");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.shader == shader)
                return material;

            var fresh = new Material(shader) { name = name };
            setup(fresh);
            if (material == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }

            EditorUtility.CopySerialized(fresh, material);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 종이 면 (앞면 = 틀 그림 · 뒷면 = 뒷면 그림) — 그림을 앞면 칸에 얹고 종이 · 잉크 선 · 형광펜 · 별을 셰이더가 그린다. 모양 값은 늘 맞춘다.
        private static Material PaperMaterial(string name, string texturePath)
        {
            Material material = CardMaterial(name, CardPaperShaderName, created =>
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null)
                    Debug.LogError($"[{nameof(TableBuilder)}] 텍스처가 없다: {texturePath}");
                created.mainTexture = texture;
            });
            if (material == null)
                return null;

            material.SetVector("_Size", PaperQuadPx);
            material.SetVector("_Paper", new Vector4(PaperHalfPx.x, PaperHalfPx.y, PaperCornerPx, 0f));
            material.SetVector("_Face", new Vector4(FaceHalfPx.x, FaceHalfPx.y, FaceCornerPx, 0f));
            material.SetFloat("_InkWidth", InkWidthPx);
            material.SetFloat("_StarUnit", StarBoxPx / StarViewBox);
            for (int i = 0; i < StarPlacements.Length; i++)
                material.SetVector($"_Star{i + 1}", StarPx(i));
            EditorUtility.SetDirty(material);
            return material;
        }

        // 그림 칸 — 칸 크기 · 배지 구멍(그림 칸 가운데 기준, 위가 +)은 늘 맞춘다. 그림 · 종류 색은 CardView.
        private static Material ArtMaterial()
        {
            Material material = CardMaterial("CardArt", CardArtShaderName, _ => { });
            if (material == null)
                return null;

            Vector2 face = CardSize / UnitsPerPixel;
            Vector2 artCenter = (ArtBox.center - new Vector2(0.5f, 0.5f)) * face;
            Vector2 badge = new Vector2((BadgeCircle.x - 0.5f) * face.x, (0.5f - BadgeCircle.y) * face.y) - artCenter;
            material.SetVector("_Size", BoxSize(ArtBox) / UnitsPerPixel);
            material.SetVector("_Badge", new Vector4(badge.x, badge.y, BadgeCircle.z * face.x, 0f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // 그림자 — 종이 모양을 아래로 밀어 흐리게 (쉴 때 값). 깊이 비교 없이 카드 묶음 순서로 뒤 카드 위에 진다. 판 크기 · 모양은 늘 맞춘다.
        private static Material ShadowMaterial()
        {
            Material material = CardMaterial("CardShadow", CardSoftShaderName, created =>
            {
                created.color = CardShadowColor;
                created.SetFloat("_Offset", ShadowDropPx);
                created.SetFloat("_Blur", ShadowBlurPx);
                created.SetFloat("_ZTest", (float)CompareFunction.Always);
            });
            if (material == null)
                return null;

            material.SetVector("_Size", ShadowQuadPx);
            material.SetVector("_Half", PaperHalfPx);
            material.SetFloat("_Radius", PaperCornerPx);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureMaterial(string name, string texturePath)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "Cards");

            Shader shader = Shader.Find(CardShaderName);
            if (shader == null)
                Debug.LogError($"[{nameof(TableBuilder)}] 셰이더가 없다: {CardShaderName}");

            var material = new Material(shader);
            if (texturePath != null)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null)
                    Debug.LogError($"[{nameof(TableBuilder)}] 텍스처가 없다: {texturePath}");
                material.mainTexture = texture;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 받침 흰 카드 그림 — 둥근 모서리 흰 바탕에 잉크 테. 있으면 덮어쓰지 않는다. 컷아웃 셰이더라 바깥은 알파 0, 안은 1.
        private static string EnsureBlankTexture(Vector2 cardSize)
        {
            if (File.Exists(BlankTexturePath))
                return BlankTexturePath;

            float scale = BlankTexturePixelsPerPx / UnitsPerPixel; // 유닛 → 텍스처 px
            int width = Mathf.RoundToInt(cardSize.x * scale), height = Mathf.RoundToInt(cardSize.y * scale);
            float radius = BlankRadiusPx * BlankTexturePixelsPerPx, border = BlankBorderPx * BlankTexturePixelsPerPx;
            var half = new Vector2(width / 2f, height / 2f);

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 둥근 사각형까지의 거리 (안쪽이 음수)
                    var d = new Vector2(Mathf.Abs(x + 0.5f - half.x), Mathf.Abs(y + 0.5f - half.y)) - half + Vector2.one * radius;
                    float distance = Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;

                    Color color = Color.Lerp(Color.white, Ink, Mathf.Clamp01(distance + border + 0.5f));
                    color.a = distance <= 0f ? 1f : 0f;
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            File.WriteAllBytes(BlankTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(BlankTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(BlankTexturePath);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return BlankTexturePath;
        }

        #region 만들기 도우미

        // 카드 크기 비율 상자(왼쪽 아래 기준) → 카드 가운데 기준 로컬 좌표 · 크기.
        private static Vector2 BoxSize(Rect box) => new Vector2(box.width * CardSize.x, box.height * CardSize.y);
        private static Vector3 BoxCenter(Rect box, float z) => new Vector3((box.center.x - 0.5f) * CardSize.x, (box.center.y - 0.5f) * CardSize.y, z);

        private static Transform Child(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            return go.transform;
        }

        private static Transform Spot(string name, Transform parent, Vector3 localPosition) => Child(name, parent, localPosition, Quaternion.identity);

        // Quad 는 −Z 를 보는 1 × 1 면 — 카메라가 −Z 쪽에 있으므로 그대로 보인다. 충돌은 루트 BoxCollider 가 맡으니 MeshCollider 는 뗀다.
        private static MeshRenderer Quad(string name, Transform parent, Material material, Vector2 size, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<MeshCollider>());
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // 월드 TMP — 글자 크기는 캔버스 px ÷ 10 (TMP 3D 는 크기 10 이 1 유닛).
        private static TMP_Text NewText3D(string name, Transform parent, string text, float sizePx, Color color, bool bold,
            TextAlignmentOptions alignment, Vector2 size, Vector3 localPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.localPosition = localPosition;

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = sizePx / 10f;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_FontAsset font = bold ? GameScreenBuilder.BoldFont : GameScreenBuilder.RegularFont;
            if (font != null)
                tmp.font = font;
            else if (bold)
                tmp.fontStyle = FontStyles.Bold;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return tmp;
        }

        internal static PunGameServer FindServer(string owner)
        {
            var server = Object.FindFirstObjectByType<PunGameServer>();
            if (server == null)
                Debug.LogError($"[{nameof(TableBuilder)}] 씬에 {nameof(PunGameServer)} 가 없다 — {owner}.server 를 직접 연결할 것.");
            return server;
        }

        #endregion
    }
}
