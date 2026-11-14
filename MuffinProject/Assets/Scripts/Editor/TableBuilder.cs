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

        private const string TableName = "Table";
        private const string HandCameraName = "HandCamera";
        private const string TopCameraName = "TopCardCamera";
        public const string HandLayerName = "HandCards"; // 내 카드 전용 레이어 — TagManager 에 없으면 빈 사용자 칸(8~)에 만든다
        private const string PrefabFolder = "Assets/Prefab/Table";
        private const string MaterialFolder = "Assets/Materials/Cards";
        private const string CardShaderName = "Unlit/Transparent Cutout"; // 덱 · 함정 칸 · 받침 카드 — 알파 컷아웃 + ZWrite, 뒷면 컬링 (카드 프리팹은 Chapchu/Card *)
        private const string BackTexturePath = "Assets/Sprites/Cards/CardBack.jpg";
        private const float CardThickness = 0.02f; // 더미 장당 두께
        private const float LayerGap = 0.005f;     // 그림 · 글자를 틀 앞에 띄우는 간격 — 같은 면이면 z 싸움

        // 카드 226 × 336 px → 2.26 × 3.36 유닛. 기준점 아래 가운데 (10-ui.md 2-1 「손패」). 틀 이미지 크기 — 콜라이더 · 손패 간격의 기준.
        private static readonly Vector2 CardSize = new Vector2(2.26f, 3.36f);
        // 그림 칸 · 이름 띠 · 설명 칸 (카드 크기 대비 비율, 왼쪽 아래 기준) — 프로토타입 v2 CF.front 그대로 (10-ui.md 2-1 「손패 · 카드 (3D 층)」).
        private static readonly Rect ArtBox = Rect.MinMaxRect(0.19f, 0.39f, 0.81f, 0.80f);
        private static readonly Rect NameBox = Rect.MinMaxRect(0.318f, 0.845f, 0.883f, 0.948f);
        private static readonly Rect DescriptionBox = Rect.MinMaxRect(0.085f, 0.06f, 0.915f, 0.347f);
        // 글자 크기 (캔버스 px) — 시안 틀 폭 820 에 84 · 72, 줄일 때 40 까지를 카드 폭 226 에 맞춘 값.
        private const float NameFontPx = 23.15f, NameFontMinPx = 11f, DescriptionFontPx = 19.8f, DescriptionFontMinPx = 11f;
        private const float DescriptionLineSpacing = -5f; // 줄 높이 1.28 (시안) — Galmuri 1.33 에서 TMP 단위(em ÷ 100)로 줄인다
        private const float NameShadowOffset = -0.51f;    // 이름 밑 그림자 0.07 em 아래 — TMP 밑깔림 오프셋 단위 = SDF 범위 6 / 44 pt

        // 카드 생김새 — 프로토타입 v2 C3D (카드 196 → 226, 1.153 배). 틀 이미지 바깥에 흰 여백 · 외곽선을 두른다 → 겉 크기 253.7 × 363.7 (캔버스 px).
        private const float CardMarginPx = 9.22f, CardOutlinePx = 4.61f, CardCornerPx = 9.22f; // 시안 M 8 · OL 4 · CR 8
        private const float CardDepth = 0.018f;  // 앞뒤 면 사이 두께 (시안 THICK 3) — 손패 장당 간격 0.02 보다 얇게 해 옆 카드와 겹치지 않는다
        private const float FaceGap = 0.003f;    // 그림 · 글자 · 덮는 판을 틀 앞에 띄우는 간격
        private const float GlowPadPx = 73.8f;   // 빛 판이 카드 겉보다 사방으로 큰 정도 (시안 GP 64)
        private const float ShadowPadPx = 55.4f, ShadowBlurPx = 27.7f, ShadowDropPx = 13.8f; // 그림자 판 여유 · 흐림 · 아래로 (시안 SP 48 · 24 · 12)
        private const float ShadowBehind = 0.012f;
        private const float CardFoil = 0.85f, CardGlare = 0.6f, BackGlare = 0.48f; // 홀로(그림만) · 빛 반사 (시안 LOOK)
        private static readonly Color CardInk = new Color32(0x2B, 0x28, 0x56, 0xFF);             // 시안 C3D INK — 뒷면 외곽선 · 옆면 · 설명 글자
        private static readonly Color CardShadowColor = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.3f);
        private static readonly Color CardShadeColor = new Color(0f, 0f, 0f, 0.48f);             // 낼 수 없는 카드 (시안 dark .48)
        private static readonly Color PawPaper = new Color32(0xF6, 0xF0, 0xE7, 0xFF);             // 그림 없는 카드의 바탕
        private static readonly Color SelectGlowColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);      // 고른 카드 (시안 GLOWS 1)
        private static readonly Color CounterGlowColor = new Color32(0x5E, 0xF0, 0x9C, 0xFF);     // 낼 수 있는 카운터 (시안 GLOWS 2)
        private static readonly Color RareAuraColor = new Color32(0xA7, 0x79, 0xFF, 0xFF);        // 희귀 오라 — 보라 (행동 카드 파란 테두리에 묻히지 않게)
        private const string MeshFolder = "Assets/Prefab/Table/Meshes";
        private const string SparksPrefabPath = "Assets/Prefab/Table/CardSparks.prefab"; // 개판 별 — 카드 프리팹 3개가 같은 것을 품는다
        private const string CardFaceShaderName = "Chapchu/Card Face";
        private const string CardGlowShaderName = "Chapchu/Card Glow";
        private const string CardSoftShaderName = "Chapchu/Card Soft";
        private const string CardSparkShaderName = "Chapchu/Card Spark";
        private const string CardEdgeShaderName = "Chapchu/Card Edge";
        private const string NameFontMaterialPrefix = "Galmuri11-Bold SDF - Card Name"; // TMP 재질 프리셋 목록에 보이게 글꼴 에셋 이름으로 시작

        // 카드 한 장 안의 그리는 순서 — 루트의 SortingGroup 이 카드를 묶고, 그 안에서 이 순서로 그린다.
        private enum CardPart { Shadow, Glow, Edge, Back, Frame, Art, Text, Shade }

        // 종류별 색 (시안 CF TYPES) — 외곽선 · 이름 그림자 · 설명 숫자
        private static (Color line, Color dark, Color number) TypeColors(CardType type) => type switch
        {
            CardType.Counter => (new Color32(0x6F, 0xA4, 0x34, 0xFF), new Color32(0x46, 0x70, 0x1C, 0xFF), new Color32(0x4F, 0x8A, 0x1D, 0xFF)),
            CardType.Trap => (new Color32(0xF6, 0x46, 0x12, 0xFF), new Color32(0xA8, 0x2C, 0x06, 0xFF), new Color32(0xD2, 0x3A, 0x0A, 0xFF)),
            _ => (new Color32(0x00, 0x9D, 0xFF, 0xFF), new Color32(0x00, 0x65, 0xA8, 0xFF), new Color32(0x00, 0x77, 0xC8, 0xFF)),
        };

        private static float CardPadPx => CardMarginPx + CardOutlinePx;
        private static Vector2 CardOuterPx => CardSize / UnitsPerPixel + Vector2.one * (CardPadPx * 2f);
        private static Vector2 CardOuterSize => CardOuterPx * UnitsPerPixel;

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
        private const string BlankTexturePath = "Assets/Sprites/Cards/CardBlank.png";
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
        private const string TrapSlotTexturePath = "Assets/Sprites/Cards/TrapSlotEmpty.png"; // 빈 칸 — 흰 45% · 잉크 30% 테 (시안의 점선은 실선으로)
        private static readonly Color TrapSlotFill = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color TrapSlotEdge = new Color(42f / 255f, 36f / 255f, 32f / 255f, 0.3f);

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
        /// 앞 층 카메라 — 메인 카메라의 자식(같은 자리 · 같은 화각)으로 TopCards 레이어만 투명 바탕에 그린다. 그림은 TopCardLayerView 가 렌더 텍스처로 받아 HUD 위에 얹는다.
        /// 평소에는 꺼 두고 뷰가 켠다. PhysicsRaycaster 는 앞 층 카드가 뒤에 있는 것(내 함정 칸 · 덱)의 탭을 가리게 하려고 붙인다. 이미 있으면 값만 맞춘다.
        /// </summary>
        public static Camera SetupTopCamera(Camera main)
        {
            int layer = EnsureLayer(TopCardLayerView.TopLayerName);
            main.cullingMask &= ~(1 << layer);

            Transform existing = main.transform.Find(TopCameraName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(TopCameraName, typeof(Camera), typeof(PhysicsRaycaster));
            go.transform.SetParent(main.transform, false);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var camera = go.GetComponent<Camera>();
            camera.CopyFrom(main);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << layer;
            camera.depth = main.depth + 2;
            camera.enabled = false;
            if (go.GetComponent<PhysicsRaycaster>() == null)
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
        /// 루트(뷰 · 프레젠터)는 그대로라 다른 곳의 참조(손패의 뽑기 자리 등)는 끊기지 않는다 — 열린 씬의 덱만 고칠 때도 쓴다.
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
                ("actionCardPrefab", EnsureCardPrefab("Card_Action", GameScreenBuilder.ActionCardFramePath, CardType.Action)),
                ("counterCardPrefab", EnsureCardPrefab("Card_Counter", GameScreenBuilder.CounterCardFramePath, CardType.Counter)),
                ("trapCardPrefab", EnsureCardPrefab("Card_Trap", GameScreenBuilder.TrapCardFramePath, CardType.Trap)),
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
                ("actionCardPrefab", EnsureCardPrefab("Card_Action", GameScreenBuilder.ActionCardFramePath, CardType.Action)),
                ("counterCardPrefab", EnsureCardPrefab("Card_Counter", GameScreenBuilder.CounterCardFramePath, CardType.Counter)),
                ("trapCardPrefab", EnsureCardPrefab("Card_Trap", GameScreenBuilder.TrapCardFramePath, CardType.Trap)),
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
        public static ChainCardsPresenter BuildChainCards(Transform table, DiscardPileView discardPile, OpponentSeatsPresenter seats, CardOwnerTagView ownerTag, DeckData gameDeck)
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
                ("actionCardPrefab", EnsureCardPrefab("Card_Action", GameScreenBuilder.ActionCardFramePath, CardType.Action)),
                ("counterCardPrefab", EnsureCardPrefab("Card_Counter", GameScreenBuilder.CounterCardFramePath, CardType.Counter)),
                ("trapCardPrefab", EnsureCardPrefab("Card_Trap", GameScreenBuilder.TrapCardFramePath, CardType.Trap)),
                ("seats", seats), ("ownerTag", ownerTag), ("dimmer", dimmer),
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
                ("deck", gameDeck), ("trapCardPrefab", EnsureCardPrefab("Card_Trap", GameScreenBuilder.TrapCardFramePath, CardType.Trap)), ("previewSpot", previewSpot));
            return presenter;
        }

        // 칸 3개 한 줄. 칸 루트 = 카드 기준점(아래 가운데) · 카드 배율 — 카드가 이 자리 · 회전 · 배율로 들어가고 나온다. tappable 이면 콜라이더 (내 칸).
        private static TrapSlotsView BuildTrapSlotRow(string name, Transform parent, (Vector2 first, Vector2 step, float widthPx, float angle) row, bool tappable)
        {
            Transform rowRoot = Child(name, parent, Vector3.zero, Quaternion.identity);
            float scale = row.widthPx * UnitsPerPixel / CardSize.x;
            Quaternion rotation = TableRotation * Quaternion.Euler(0f, 0f, row.angle);

            Material emptyMaterial = EnsureAlphaMaterial("TrapSlotEmpty", EnsureRoundTexture(TrapSlotTexturePath, CardSize, 0f, 4f / scale, 10f / scale, TrapSlotFill, TrapSlotEdge));
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

        // 둥근 사각형 그림 — 안쪽 fill, 테두리 edge. 있으면 덮어쓰지 않는다. solidAlpha 가 1 이면 안쪽 알파를 1 로(컷아웃 셰이더용), 0 이면 색의 알파 그대로(알파 블렌드용). 크기 · 테 · 반지름은 유닛 · 캔버스 px.
        private static string EnsureRoundTexture(string path, Vector2 size, float solidAlpha, float borderPx, float radiusPx, Color fill, Color edge)
        {
            if (File.Exists(path))
                return path;

            float scale = BlankTexturePixelsPerPx / UnitsPerPixel;
            int width = Mathf.RoundToInt(size.x * scale), height = Mathf.RoundToInt(size.y * scale);
            float radius = radiusPx * BlankTexturePixelsPerPx, border = borderPx * BlankTexturePixelsPerPx;
            var half = new Vector2(width / 2f, height / 2f);

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var d = new Vector2(Mathf.Abs(x + 0.5f - half.x), Mathf.Abs(y + 0.5f - half.y)) - half + Vector2.one * radius;
                    float distance = Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;

                    Color color = Color.Lerp(fill, edge, Mathf.Clamp01(distance + border + 0.5f));
                    if (distance > 0f)
                        color.a = 0f;
                    else if (solidAlpha > 0f)
                        color.a = solidAlpha;
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
        private const int EdgeCornerSegments = 8;
        private const int SparkPointCount = 64;

        /// <summary>카드 프리팹 3개를 같은 경로에 다시 만든다 (GUID 유지 — 씬 · HandView 참조가 그대로). 사람이 프리팹을 손봤다면 지워진다.</summary>
        [MenuItem("Chapchu/Game Screen/Rebuild Table Card Prefabs")]
        public static void RebuildCardPrefabs()
        {
            foreach ((string name, string framePath, CardType type) in CardPrefabs)
                EnsureCardPrefab(name, framePath, type, true);
            Debug.Log($"[{nameof(TableBuilder)}] 3D 카드 프리팹 3개 다시 만듦.");
        }

        /// <summary>이미 있는 카드 프리팹 3개를 그 자리에서 지금 생김새로 맞춘다 (UpgradeCard) — 프리팹 안 오브젝트가 그대로라 사람이 더한 자식 · 값은 남는다.</summary>
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
        /// 카드 한 장의 부분을 지금 생김새로 맞춘다 (10-ui.md 2-1 「손패 · 카드 (3D 층)」 · 「카드 등급 연출」, 프로토타입 v2 C3D). 없는 부분은 만들고, 있는 부분은 자리 · 크기 · 머티리얼만 고친다.
        ///   루트(콜라이더 · CardView · SortingGroup, 기준점 아래 가운데) ─ Shadow · Glow(카운터만) · SelectRing · Aura · Edge · Front(Frame · Art · Name · Description) · Back(Back) · Sparks · Shade
        /// 앞면 · 뒷면은 두께(CardDepth)만큼 떨어져 서로 반대를 보고, 셰이더가 뒷면을 컬링하므로 보는 쪽만 그려진다. 전부 HandCards 레이어.
        /// 머티리얼 · 파티클 값은 새로 만들 때(또는 옛 셰이더에서 바꿀 때)만, 글자 모양 · 숫자 색은 그 부분을 새로 만들 때만 넣는다 — 사람이 Inspector 에서 고친 값은 덮어쓰지 않는다.
        /// </summary>
        public static void UpgradeCard(GameObject root, CardType type, string framePath)
        {
            Transform card = root.transform;
            var center = new Vector3(0f, CardSize.y / 2f, 0f);
            Vector2 outer = CardOuterSize;
            Vector2 glowSize = outer + Vector2.one * (GlowPadPx * 2f * UnitsPerPixel);
            (Color line, Color _, Color number) = TypeColors(type);

            var collider = Ensure<BoxCollider>(root);
            collider.size = new Vector3(CardSize.x, CardSize.y, CardThickness);
            collider.center = center;
            Ensure<SortingGroup>(root);
            bool viewCreated = !root.TryGetComponent(out CardView view);
            if (viewCreated)
                view = root.AddComponent<CardView>();

            EnsureQuad("Shadow", card, ShadowMaterial(), outer + Vector2.one * (ShadowPadPx * 2f * UnitsPerPixel),
                center + new Vector3(0f, -ShadowDropPx * UnitsPerPixel, ShadowBehind), CardPart.Shadow);
            MeshRenderer glow = type == CardType.Counter ? EnsureQuad("Glow", card, GlowMaterial("CardGlowCounter", CounterGlowColor, true), glowSize, center, CardPart.Glow) : null;
            MeshRenderer ring = EnsureQuad("SelectRing", card, GlowMaterial("CardRingSelect", SelectGlowColor, false), glowSize, center, CardPart.Glow);
            MeshRenderer aura = EnsureQuad("Aura", card, AuraMaterial(), glowSize, center, CardPart.Glow);
            EnsureEdge(card, center);

            Transform front = EnsureChild("Front", card, center + new Vector3(0f, 0f, -CardDepth / 2f), Quaternion.identity);
            MeshRenderer frame = EnsureQuad("Frame", front, FaceMaterial(Path.GetFileNameWithoutExtension(framePath), framePath, line, CardGlare), outer, Vector3.zero, CardPart.Frame);
            MeshRenderer art = EnsureQuad("Art", front, ArtMaterial(), BoxSize(ArtBox), BoxCenter(ArtBox, -FaceGap), CardPart.Art);
            art.enabled = true; // 그림 없는 카드도 발자국을 그린다
            TMP_Text nameText = EnsureNameText(front, type);
            TMP_Text description = EnsureDescriptionText(front);

            Transform back = EnsureChild("Back", card, center + new Vector3(0f, 0f, CardDepth / 2f), Quaternion.Euler(0f, 180f, 0f));
            EnsureQuad("Back", back, FaceMaterial("CardBackFace", BackTexturePath, CardInk, BackGlare), outer, Vector3.zero, CardPart.Back);

            ParticleSystem sparks = EnsureSparks(card, center);
            MeshRenderer shade = EnsureQuad("Shade", card, ShadeMaterial(), outer, center + new Vector3(0f, 0f, -CardDepth / 2f - FaceGap * 3f), CardPart.Shade);

            // 시작 모습: 빛 · 오라 · 별 · 덮는 판은 꺼 둔다 — CardView 가 상태 · 등급으로 켠다
            foreach (MeshRenderer hidden in new[] { glow, ring, aura, shade })
            {
                if (hidden != null)
                    hidden.gameObject.SetActive(false);
            }
            sparks.gameObject.SetActive(false);

            GameScreenBuilder.Wire(view,
                ("nameText", nameText), ("descriptionText", description), ("frameRenderer", frame), ("artRenderer", art),
                ("glow", glow != null ? glow.gameObject : null), ("selectRing", ring.gameObject), ("shade", shade.gameObject),
                ("auraRenderer", aura), ("sparks", sparks));
            if (viewCreated)
            {
                var serialized = new SerializedObject(view);
                serialized.FindProperty("numberColor").colorValue = number;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
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

        // 이름 — 머리 띠 안, 흰 굵은 글자 + 종류 색 짙은 그림자 한 겹 (시안: 파랑 · 초록 · 주황 위에서도 읽히게). 넘치면 줄이고 말줄임.
        private static TMP_Text EnsureNameText(Transform front, CardType type)
        {
            TMP_Text text = EnsureText("Name", front, true, BoxSize(NameBox), BoxCenter(NameBox, -FaceGap * 2f), out bool created);
            if (!created)
                return text;

            text.fontSharedMaterial = NameMaterial(type);
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = NameFontMinPx / 10f;
            text.fontSizeMax = NameFontPx / 10f;
            text.fontSize = NameFontPx / 10f;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        // 설명 — 점선 아래 칸 가운데, 잉크 글자 (숫자 색 · 줄 고르기는 CardView). 넘치면 줄이고 말줄임.
        private static TMP_Text EnsureDescriptionText(Transform front)
        {
            TMP_Text text = EnsureText("Description", front, false, BoxSize(DescriptionBox), BoxCenter(DescriptionBox, -FaceGap * 2f), out bool created);
            if (!created)
                return text;

            text.color = CardInk;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = DescriptionFontMinPx / 10f;
            text.fontSizeMax = DescriptionFontPx / 10f;
            text.fontSize = DescriptionFontPx / 10f;
            text.lineSpacing = DescriptionLineSpacing;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        // 이름의 글자 자식 — 없으면 만들고(글꼴은 NewText3D 가 넣는다), 있으면 자리 · 크기 · 그리는 순서만 맞춘다. created 는 새로 만들었는지.
        private static TMP_Text EnsureText(string name, Transform parent, bool bold, Vector2 size, Vector3 localPosition, out bool created)
        {
            Transform existing = parent.Find(name);
            created = existing == null;
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>()
                : NewText3D(name, parent, name, NameFontPx, Color.white, bold, TextAlignmentOptions.Center, size, localPosition);
            var rect = (RectTransform)text.transform;
            rect.sizeDelta = size;
            rect.localPosition = localPosition;
            rect.localRotation = Quaternion.identity;
            text.GetComponent<MeshRenderer>().sortingOrder = (int)CardPart.Text;
            return text;
        }

        // 옆면 — 카드 겉 모양 둘레를 두께만큼 늘인 띠 (잉크). 뒤집히거나 크게 기울 때 카드가 판처럼 보이게 (시안 edge).
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

        // 개판 별 — CardSparks 프리팹을 카드 가운데에 품는다 (값을 고칠 곳이 한 군데).
        private static ParticleSystem EnsureSparks(Transform card, Vector3 center)
        {
            Transform sparks = card.Find("Sparks");
            if (sparks == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(EnsureSparksPrefab(), card);
                instance.name = "Sparks";
                sparks = instance.transform;
            }

            sparks.SetLocalPositionAndRotation(center, Quaternion.identity);
            return sparks.GetComponent<ParticleSystem>();
        }

        // 개판 별 프리팹 (10-ui.md 2-1 「카드 등급 연출」, 프로토타입 v2 PT) — 카드 둘레에서 바깥으로 피어올라 흩어지는 무지개 별 26개. 그림 파일 없이 Card Spark 셰이더가 별을 그린다.
        // 둘레의 꼭짓점(CardSparkPoints 메쉬, 법선 = 바깥)에서 그 방향으로 나가고 조금씩 위로 뜬다. 카드와 같이 움직이고(Local) 같이 커진다(Hierarchy). 있으면 그대로 쓴다.
        private static GameObject EnsureSparksPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SparksPrefabPath);
            if (existing != null)
                return existing;

            var go = new GameObject("CardSparks", typeof(ParticleSystem));
            var system = go.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.43f, 3.13f);   // 시안 1 ÷ (0.32 ~ 0.70) 초
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);        // 수명 동안 바깥으로 30 px
            main.startSize = new ParticleSystem.MinMaxCurve(0.138f, 0.277f);     // 시안 12 ~ 24 px × 1.153
            main.startColor = new ParticleSystem.MinMaxGradient(SparkRainbow()) { mode = ParticleSystemGradientMode.RandomColor };
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 26;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 12f; // 26개 ÷ 평균 수명 2.1 초

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Vertex;
            shape.mesh = EnsureSparkPointsMesh();

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.1f); // 수명 동안 위로 21 px (시안 18 × 1.153)
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            // 수명의 12% 동안 나타나고 55% 부터 사라진다 · 끝에는 0.55 배로 (시안)
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.55f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = CardMaterial("CardSpark", CardSparkShaderName, _ => { });
            renderer.sortingOrder = (int)CardPart.Glow; // 시안: 오라와 같이 카드 뒤
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            SetHandLayer(go);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, SparksPrefabPath);
            Object.DestroyImmediate(go);
            return saved;
        }

        // 별 색 — 무지개를 흰색 쪽으로 30% (시안 mix(1, 무지개, 0.7)). 파티클마다 이 중 하나를 고른다.
        private static Gradient SparkRainbow()
        {
            var keys = new GradientColorKey[7];
            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / (keys.Length - 1f);
                keys[i] = new GradientColorKey(Color.Lerp(Color.white, Color.HSVToRGB(t % 1f, 1f, 1f), 0.7f), t);
            }

            var gradient = new Gradient();
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        // 옆면 메쉬 — 겉 모양(둥근 사각형) 둘레의 벽, 앞뒤 면 사이(두께 CardDepth). 바깥을 보는 면만. 있으면 그대로 쓴다.
        private static Mesh EnsureEdgeMesh()
        {
            string path = $"{MeshFolder}/CardEdge.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
                return existing;

            Vector2 half = CardOuterSize / 2f;
            float radius = (CardCornerPx + CardOutlinePx) * UnitsPerPixel;
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

            var mesh = new Mesh { name = "CardEdge", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return SaveMesh(mesh, path);
        }

        // 별이 피어나는 자리 — 카드 겉 둘레에 고르게 놓은 꼭짓점 (법선 = 바깥). 파티클 메쉬 모양은 삼각형이 있어야 해서 1 px 폭 띠로 만든다. 있으면 그대로 쓴다.
        private static Mesh EnsureSparkPointsMesh()
        {
            string path = $"{MeshFolder}/CardSparkPoints.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
                return existing;

            Vector2 half = CardOuterSize / 2f;
            float[] sides = { half.x * 2f, half.y * 2f, half.x * 2f, half.y * 2f }; // 위 · 오른쪽 · 아래 · 왼쪽
            float perimeter = sides[0] + sides[1] + sides[2] + sides[3];
            var vertices = new Vector3[SparkPointCount * 2];
            var normals = new Vector3[SparkPointCount * 2];
            var triangles = new int[SparkPointCount * 6];
            for (int i = 0; i < SparkPointCount; i++)
            {
                float d = (i + 0.5f) / SparkPointCount * perimeter;
                Vector2 point, normal;
                if (d < sides[0]) { point = new Vector2(-half.x + d, half.y); normal = Vector2.up; }
                else if ((d -= sides[0]) < sides[1]) { point = new Vector2(half.x, half.y - d); normal = Vector2.right; }
                else if ((d -= sides[1]) < sides[2]) { point = new Vector2(half.x - d, -half.y); normal = Vector2.down; }
                else { d -= sides[2]; point = new Vector2(-half.x, -half.y + d); normal = Vector2.left; }
                vertices[i * 2] = point;
                vertices[i * 2 + 1] = point - normal * UnitsPerPixel;
                normals[i * 2] = normals[i * 2 + 1] = normal;

                int a = i * 2, b = (i + 1) % SparkPointCount * 2;
                triangles[i * 6] = a;
                triangles[i * 6 + 1] = b;
                triangles[i * 6 + 2] = b + 1;
                triangles[i * 6 + 3] = a;
                triangles[i * 6 + 4] = b + 1;
                triangles[i * 6 + 5] = a + 1;
            }

            var mesh = new Mesh { name = "CardSparkPoints", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return SaveMesh(mesh, path);
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder(PrefabFolder, "Meshes");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // 카드 머티리얼 — 없으면 만들고, 다른 셰이더(옛 컷아웃 · 그림 판)면 이 셰이더로 바꿔 값을 넣는다. 이미 이 셰이더면 사람이 고친 값을 그대로 둔다.
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

            if (material == null)
            {
                material = new Material(shader);
                setup(material);
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            // 옛 셰이더의 그림(링 · 어두운 판 텍스처)은 비운다 — 그 그림 파일을 지워도 빈 참조가 남지 않게
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", null);
            material.shader = shader;
            setup(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 카드 면 (틀 · 뒷면) — 텍스처를 흰 여백 안쪽에 얹고 바깥에 외곽선. 빛 계산용 카드 좌표(_CardRect)는 틀 이미지가 0 ~ 1.
        private static Material FaceMaterial(string name, string texturePath, Color outline, float glare) =>
            CardMaterial(name, CardFaceShaderName, material =>
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null)
                    Debug.LogError($"[{nameof(TableBuilder)}] 텍스처가 없다: {texturePath}");
                material.mainTexture = texture;
                material.SetFloat("_HasTex", 1f);
                material.SetFloat("_Opaque", 1f);
                material.SetVector("_Size", CardOuterPx);
                material.SetFloat("_Pad", CardPadPx);
                material.SetFloat("_Radius", CardCornerPx);
                material.SetFloat("_Outline", CardOutlinePx);
                material.SetColor("_OutlineColor", outline);
                float padX = CardPadPx * UnitsPerPixel / CardSize.x, padY = CardPadPx * UnitsPerPixel / CardSize.y;
                material.SetVector("_CardRect", new Vector4(-padX, -padY, 1f + padX, 1f + padY));
                material.SetFloat("_Foil", 0f);
                material.SetFloat("_Glare", glare);
            });

        // 그림 — 그림 칸 크기 그대로, 투명한 곳은 틀이 보인다. 홀로(은박 결)는 그림에만. 그림 없는 카드의 발자국 바탕도 여기서.
        private static Material ArtMaterial() =>
            CardMaterial("CardArt", CardFaceShaderName, material =>
            {
                material.SetFloat("_HasTex", 1f);
                material.SetFloat("_Opaque", 0f);
                material.SetVector("_Size", BoxSize(ArtBox) / UnitsPerPixel);
                material.SetFloat("_Pad", 0f);
                material.SetFloat("_Radius", 0f);
                material.SetFloat("_Outline", 0f);
                material.SetColor("_PawPaper", PawPaper);
                material.SetVector("_CardRect", new Vector4(ArtBox.xMin, ArtBox.yMin, ArtBox.xMax, ArtBox.yMax));
                material.SetFloat("_Foil", CardFoil);
                material.SetFloat("_Glare", CardGlare);
            });

        private static Material GlowMaterial(string name, Color color, bool pulse) =>
            CardMaterial(name, CardGlowShaderName, material =>
            {
                material.color = color;
                material.SetFloat("_Amount", 1f);
                material.SetFloat("_Pulse", pulse ? 1f : 0f);
                SetGlowShape(material);
            });

        // 등급 오라 — 세기 · 퍼짐 · 무지개는 CardView 가 등급으로 넣는다. 앞면이 보일 때만 (뒷면에서 등급이 새지 않게).
        private static Material AuraMaterial() =>
            CardMaterial("CardAura", CardGlowShaderName, material =>
            {
                material.color = RareAuraColor;
                material.SetFloat("_Amount", 0f);
                material.SetFloat("_FaceFade", 1f);
                SetGlowShape(material);
            });

        private static void SetGlowShape(Material material)
        {
            material.SetVector("_Size", CardOuterPx + Vector2.one * (GlowPadPx * 2f));
            material.SetVector("_Half", CardOuterPx / 2f);
            material.SetFloat("_Radius", CardCornerPx + CardOutlinePx);
        }

        // 그림자 — 카드 겉 모양을 흐리게, 조금 아래로. 깊이 비교 없이 카드 묶음 순서로 뒤 카드 위에 진다.
        private static Material ShadowMaterial() =>
            CardMaterial("CardShadow", CardSoftShaderName, material =>
            {
                material.color = CardShadowColor;
                material.SetVector("_Size", CardOuterPx + Vector2.one * (ShadowPadPx * 2f));
                material.SetVector("_Half", CardOuterPx / 2f);
                material.SetFloat("_Radius", CardCornerPx + CardOutlinePx);
                material.SetFloat("_Blur", ShadowBlurPx);
                material.SetFloat("_ZTest", (float)CompareFunction.Always);
            });

        // 낼 수 없는 카드를 덮는 어두운 판 — 카드 겉 모양 그대로, 글자보다 앞.
        private static Material ShadeMaterial() =>
            CardMaterial("CardShade", CardSoftShaderName, material =>
            {
                material.color = CardShadeColor;
                material.SetVector("_Size", CardOuterPx);
                material.SetVector("_Half", CardOuterPx / 2f);
                material.SetFloat("_Radius", CardCornerPx + CardOutlinePx);
                material.SetFloat("_Blur", 0f);
                material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            });

        // 이름 글자 재질 — 굵은 글꼴 재질에 밑깔림(종류 색을 짙게, 0.07 em 아래)을 켠 것. 있으면 그대로 쓴다.
        private static Material NameMaterial(CardType type)
        {
            string path = $"{MaterialFolder}/{NameFontMaterialPrefix} {type}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            TMP_FontAsset font = GameScreenBuilder.BoldFont;
            if (font == null)
                return null;

            var material = new Material(font.material);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, TypeColors(type).dark);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, NameShadowOffset);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            AssetDatabase.CreateAsset(material, path);
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
