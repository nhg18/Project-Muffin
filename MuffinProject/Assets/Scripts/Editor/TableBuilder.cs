using System.IO;
using Chapchu.Cards;
using Chapchu.Network;
using Chapchu.UI.GameScreen;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 인게임 3D 층(카드 · 덱 · 버림 더미 · 손패 · 카드 내는 자리)을 조립한다 — 10-ui.md 2-1 「3D 층」. GameScreenBuilder.Build 가 부른다.
    /// 단위: 캔버스 100px = 1 유닛, 테이블 면 z = 0, 카메라는 원근 · 수직 FOV 40° 로 1080px 높이가 화면을 채우는 거리에. 좌석 이름표는 2D 층(HUD v2).
    /// 카드 · 덱 머티리얼(Unlit · 컷아웃 · 뒷면 컬링)과 카드 프리팹 3개는 없으면 만들고, 있으면 덮어쓰지 않는다.
    /// 내 카드(손패 · 내는 카드)는 HandCards 레이어 — 메인 카메라 자식인 손패 카메라가 깊이만 지우고 그 위에 그린다. 기울인 카드(4° · 40°)의 윗부분이 덱 뒤로 들어가 가려지지 않게 (10-ui.md 2-1 「3D 층 구현」 손패 층).
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
        public const string HandLayerName = "HandCards"; // 내 카드 전용 레이어 — TagManager 에 없으면 빈 사용자 칸(8~)에 만든다
        private const string PrefabFolder = "Assets/Prefab/Table";
        private const string MaterialFolder = "Assets/Materials/Cards";
        private const string CardShaderName = "Unlit/Transparent Cutout"; // 알파 컷아웃 + ZWrite — 겹친 카드가 깊이로 정렬된다. 뒷면 컬링
        private const string BackTexturePath = "Assets/Sprites/Cards/CardBack.jpg";
        private const float CardThickness = 0.02f; // 더미 장당 두께
        private const float LayerGap = 0.005f;     // 그림 · 글자를 틀 앞에 띄우는 간격 — 같은 면이면 z 싸움

        // 카드 226 × 336 px → 2.26 × 3.36 유닛. 기준점 아래 가운데 (10-ui.md 2-1 「손패」).
        private static readonly Vector2 CardSize = new Vector2(2.26f, 3.36f);
        // 그림 칸 x 0.19 ~ 0.81 · y 0.39 ~ 0.80, 이름 띠 x 0.33 ~ 0.89 · y 0.85 ~ 0.95, 설명 x 0.11 ~ 0.89 · y 0.07 ~ 0.34 (카드 크기 대비 비율, 왼쪽 아래 기준).
        private static readonly Rect ArtBox = Rect.MinMaxRect(0.1925f, 0.389f, 0.8075f, 0.801f);
        private static readonly Rect NameBox = Rect.MinMaxRect(0.3256f, 0.8482f, 0.8896f, 0.9502f);
        private static readonly Rect DescriptionBox = Rect.MinMaxRect(0.11f, 0.074f, 0.89f, 0.336f);
        private const float NameFontPx = 18f, NameFontMinPx = 12f, DescriptionFontPx = 15f, DescriptionFontMinPx = 10f;

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

        // 손패 — 내 패널(좌 32 + 472 + 24)과 찹츄 버튼(우 32 + 392 + 28) 사이 가운데 x 1016, 아래 28 기준 원호, 양 끝 (±452, −34) (10-ui.md 2-1 「부채꼴」 HUD v2).
        private static readonly Vector3 HandPosition = CanvasToWorld(1016f, 1080f - 28f);
        private const float HandFanHalfWidth = 4.52f;
        private const float HandFanDrop = 0.34f;

        // 카드 내는 자리 · 버림 자리 — 시안 DROP (480, 310) · DISC (555, 310) × 2: 방석 가운데에 내고 오른쪽에 버린다 (HUD v2).
        private static readonly Vector3 PlaySpotPosition = CanvasToWorld(960f, 620f);
        private static readonly Vector3 DiscardSpotPosition = CanvasToWorld(1110f, 620f);

        // 버림 더미 (10-ui.md 2-1 「버림 더미」, 시안 DISC) — 카드 0.6배. 받침 흰 카드 2장(아래부터: 캔버스 px 오프셋 · 반시계 각도) 위에 맨 위 카드 −3°.
        private const float DiscardCardScale = 0.6f;
        private const float DiscardTopAngle = -3f;
        private static readonly (Vector2 offsetPx, float angle)[] DiscardUnderCards = { (new Vector2(-20f, -8f), 7f), (new Vector2(-10f, -4f), 3f) };
        // 받침 흰 카드 그림 — 없으면 만든다. 카드 0.6배(136 × 202)의 2배 크기, 흰 바탕 · 잉크 테 4 · 반지름 14 (캔버스 px).
        private const string BlankTexturePath = "Assets/Sprites/Cards/CardBlank.png";
        private const int BlankTexturePixelsPerPx = 2;
        private const float BlankBorderPx = 4f, BlankRadiusPx = 14f;

        private static readonly Color Ink = new Color32(0x2a, 0x24, 0x20, 0xFF);

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
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -CameraDistance), Quaternion.identity);
            if (camera.GetComponent<PhysicsRaycaster>() == null)
                camera.gameObject.AddComponent<PhysicsRaycaster>();
            SetupHandCamera(camera);
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
                ("actionCardPrefab", EnsureCardPrefab("Card_Action", GameScreenBuilder.ActionCardFramePath)),
                ("counterCardPrefab", EnsureCardPrefab("Card_Counter", GameScreenBuilder.CounterCardFramePath)),
                ("trapCardPrefab", EnsureCardPrefab("Card_Trap", GameScreenBuilder.TrapCardFramePath)),
                ("topAnchor", topAnchor), ("countText", count));
            GameScreenBuilder.WireArray(view, "underCards", underCards);

            var presenter = root.AddComponent<DiscardPilePresenter>();
            GameScreenBuilder.Wire(presenter, ("deck", gameDeck));
            return view;
        }

        public static HandPresenter BuildHand(Transform table, DeckView deck, DiscardPileView discardPile, RectTransform dropZone, OpponentSeatsPresenter seats, DeckData gameDeck)
        {
            var root = new GameObject("Hand");
            root.transform.SetParent(table, false);
            root.transform.localPosition = HandPosition;

            SplineContainer container = root.AddComponent<SplineContainer>();
            Spline spline = container.Spline;
            spline.Clear();
            spline.Add(new BezierKnot(new float3(-HandFanHalfWidth, -HandFanDrop, 0f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(float3.zero), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(HandFanHalfWidth, -HandFanDrop, 0f)), TangentMode.AutoSmooth);

            var view = root.AddComponent<HandView>();
            GameScreenBuilder.Wire(view,
                ("actionCardPrefab", EnsureCardPrefab("Card_Action", GameScreenBuilder.ActionCardFramePath)),
                ("counterCardPrefab", EnsureCardPrefab("Card_Counter", GameScreenBuilder.CounterCardFramePath)),
                ("trapCardPrefab", EnsureCardPrefab("Card_Trap", GameScreenBuilder.TrapCardFramePath)));

            Transform playSpot = Spot("PlaySpot", table, PlaySpotPosition);

            // 버려지는 내 카드는 버림 더미의 맨 위 카드 자리로 날아가 닿는다
            var presenter = root.AddComponent<HandPresenter>();
            GameScreenBuilder.Wire(presenter,
                ("deck", gameDeck), ("drawOrigin", deck.transform), ("server", FindServer(nameof(HandPresenter))), ("dropZone", dropZone), ("seats", seats),
                ("playSpot", playSpot), ("discardSpot", discardPile.TopAnchor));
            return presenter;
        }

        /// <summary>카드 프리팹 3개를 같은 경로에 다시 만든다 (GUID 유지 — 씬 · HandView 참조가 그대로). 사람이 프리팹을 손봤다면 지워진다.</summary>
        [MenuItem("Chapchu/Game Screen/Rebuild Table Card Prefabs")]
        public static void RebuildCardPrefabs()
        {
            EnsureCardPrefab("Card_Action", GameScreenBuilder.ActionCardFramePath, true);
            EnsureCardPrefab("Card_Counter", GameScreenBuilder.CounterCardFramePath, true);
            EnsureCardPrefab("Card_Trap", GameScreenBuilder.TrapCardFramePath, true);
            Debug.Log($"[{nameof(TableBuilder)}] 3D 카드 프리팹 3개 다시 만듦.");
        }

        // 카드 프리팹 — 루트(BoxCollider · CardView, 기준점 아래 가운데) → Front(틀 · 그림 · 이름 · 설명) · Back(뒷면, Y 180°). 전부 HandCards 레이어.
        // 틀 · 뒷면 Quad 는 같은 면에 서로 반대를 보고 있고 머티리얼이 뒷면을 컬링하므로 보는 쪽만 그려진다. 그림 · 글자는 틀보다 조금 앞에.
        private static CardView EnsureCardPrefab(string name, string framePath, bool overwrite = false)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<CardView>(path);
            if (existing != null && !overwrite)
                return existing;

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefab", "Table");

            Material frame = EnsureMaterial(Path.GetFileNameWithoutExtension(framePath), framePath);
            Material back = EnsureMaterial("CardBack", BackTexturePath);
            Material art = EnsureMaterial("CardArt", null); // 그림은 CardView 가 프로퍼티 블록으로 입힌다

            var root = new GameObject(name, typeof(BoxCollider));
            var collider = root.GetComponent<BoxCollider>();
            collider.size = new Vector3(CardSize.x, CardSize.y, CardThickness);
            collider.center = new Vector3(0f, CardSize.y / 2f, 0f);

            Transform front = Child("Front", root.transform, new Vector3(0f, CardSize.y / 2f, 0f), Quaternion.identity);
            Quad("Frame", front, frame, CardSize, Vector3.zero, Quaternion.identity);

            MeshRenderer artRenderer = Quad("Art", front, art, BoxSize(ArtBox), BoxCenter(ArtBox, -LayerGap), Quaternion.identity);
            artRenderer.enabled = false;

            TMP_Text nameText = NewText3D("Name", front, "Name", NameFontPx, Color.white, true, TextAlignmentOptions.Left,
                BoxSize(NameBox), BoxCenter(NameBox, -LayerGap * 2f));
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = NameFontMinPx / 10f;
            nameText.fontSizeMax = NameFontPx / 10f;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            // 더미는 흰 글자인데 흰 바탕 위라 보이지 않아 잉크로 쓴다.
            TMP_Text description = NewText3D("Description", front, "Description", DescriptionFontPx, Ink, false, TextAlignmentOptions.Top,
                BoxSize(DescriptionBox), BoxCenter(DescriptionBox, -LayerGap * 2f));
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = DescriptionFontMinPx / 10f;
            description.fontSizeMax = DescriptionFontPx / 10f;
            description.overflowMode = TextOverflowModes.Ellipsis;

            Transform backRoot = Child("Back", root.transform, new Vector3(0f, CardSize.y / 2f, 0f), Quaternion.Euler(0f, 180f, 0f));
            Quad("Back", backRoot, back, CardSize, Vector3.zero, Quaternion.identity);

            var view = root.AddComponent<CardView>();
            GameScreenBuilder.Wire(view, ("nameText", nameText), ("descriptionText", description), ("artRenderer", artRenderer));
            SetHandLayer(root);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<CardView>();
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

        private static PunGameServer FindServer(string owner)
        {
            var server = Object.FindFirstObjectByType<PunGameServer>();
            if (server == null)
                Debug.LogError($"[{nameof(TableBuilder)}] 씬에 {nameof(PunGameServer)} 가 없다 — {owner}.server 를 직접 연결할 것.");
            return server;
        }

        #endregion
    }
}
