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
    /// 인게임 3D 층(테이블 방석 · 카드 · 덱 · 손패 · 카드 내는 자리 · 버림 자리)을 조립한다 — 10-ui.md 2-1 「3D 층」. GameScreenBuilder.Build 가 부른다.
    /// 단위: 캔버스 100px = 1 유닛, 테이블 면 z = 0, 카메라는 원근 · 수직 FOV 40° 로 1080px 높이가 화면을 채우는 거리에. 좌석 이름표는 2D 층(HUD v2).
    /// 카드 · 덱 머티리얼(Unlit · 컷아웃 · 뒷면 컬링)과 카드 프리팹 3개는 없으면 만들고, 있으면 덮어쓰지 않는다.
    /// </summary>
    public static class TableBuilder
    {
        public const float UnitsPerPixel = 0.01f;
        // 시안 perspective 1500px 을 캔버스 높이 1080 에 대면 수직 FOV 2·atan(540 / 1500) ≈ 40°.
        public const float CameraFieldOfView = 40f;
        // 1080px(10.8 유닛)이 화면을 채우는 거리 — 5.4 / tan 20° ≈ 14.85.
        public static readonly float CameraDistance = 5.4f / Mathf.Tan(CameraFieldOfView * 0.5f * Mathf.Deg2Rad);

        private const string TableName = "Table";
        private const string PrefabFolder = "Assets/Prefab/Table";
        private const string MaterialFolder = "Assets/Materials/Cards";
        private const string CardShaderName = "Unlit/Transparent Cutout"; // 알파 컷아웃 + ZWrite — 겹친 카드가 깊이로 정렬된다. 뒷면 컬링
        private const string CushionShaderName = "Unlit/Transparent";      // 방석 — 부드러운 가장자리 알파
        private const int TextSortingOrder = 1; // 3D 글자는 방석(반투명, 순서 0)보다 뒤에 그린다 — 원근 거리 정렬에 맡기면 가장자리의 덱 글자가 방석 뒤로 간다. 카드 틀은 불투명(깊이) 이라 영향 없음
        private const string BackTexturePath = "Assets/Sprites/Cards/CardBack.jpg";
        private const string CushionTexturePath = "Assets/Sprites/UI/Hud/table_cushion.png";
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
        private static readonly Vector2 CushionSize = new Vector2(11.2f, 5.6f);
        private static readonly Vector3 CushionPosition = CanvasToWorld(960f, 620f) + new Vector3(0f, 0f, 0.01f);

        // 덱 179 × 259 · 중심 (810, 620) (10-ui.md 2-1 「덱」, HUD v2 — 시안 DECK (405, 310) × 2). 뒷면 5장을 겹쳐 두께를 낸다.
        private static readonly Vector2 DeckCardSize = new Vector2(1.79f, 2.59f);
        private static readonly Vector3 DeckPosition = CanvasToWorld(810f, 620f);
        private const int DeckStackCount = 5;
        private const float DeckCountFontPx = 30f; // 시안 .lab 15 × 2
        private static readonly Vector2 DeckCountSize = new Vector2(1.6f, 0.3f);
        private static readonly Vector2 DeckCountOffset = new Vector2(0f, -1.5f); // 덱 아래쪽 바깥 — 시안 「덱 n」 라벨 자리 (HUD v2)

        // 손패 — 내 패널(좌 32 + 472 + 24)과 찹츄 버튼(우 32 + 392 + 28) 사이 가운데 x 1016, 아래 28 기준 원호, 양 끝 (±452, −34) (10-ui.md 2-1 「부채꼴」 HUD v2).
        private static readonly Vector3 HandPosition = CanvasToWorld(1016f, 1080f - 28f);
        private const float HandFanHalfWidth = 4.52f;
        private const float HandFanDrop = 0.34f;

        // 카드 내는 자리 · 버림 자리 — 시안 DROP (480, 310) · DISC (555, 310) × 2: 방석 가운데에 내고 오른쪽에 버린다 (HUD v2).
        private static readonly Vector3 PlaySpotPosition = CanvasToWorld(960f, 620f);
        private static readonly Vector3 DiscardSpotPosition = CanvasToWorld(1110f, 620f);

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
            return camera;
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

        /// <summary>테이블 방석 — 카드 뒤에 눕힌 그림 Quad. 포인터는 받지 않는다 (콜라이더 없음).</summary>
        public static void BuildCushion(Transform table)
        {
            Material cushion = EnsureMaterial("TableCushion", CushionTexturePath, CushionShaderName);
            Quad("Cushion", table, cushion, CushionSize, CushionPosition, Quaternion.identity);
        }

        public static DeckView BuildDeck(Transform table)
        {
            var root = new GameObject("Deck", typeof(BoxCollider));
            root.transform.SetParent(table, false);
            root.transform.localPosition = DeckPosition;

            Material back = EnsureMaterial("CardBack", BackTexturePath);
            for (int i = 0; i < DeckStackCount; i++)
                Quad($"Back{i}", root.transform, back, DeckCardSize, new Vector3(0f, 0f, -i * CardThickness), Quaternion.identity);

            var collider = root.GetComponent<BoxCollider>();
            collider.size = new Vector3(DeckCardSize.x, DeckCardSize.y, DeckStackCount * CardThickness);
            collider.center = new Vector3(0f, 0f, -(DeckStackCount - 1) * CardThickness / 2f);

            TMP_Text count = NewText3D("CountText", root.transform, "덱 -", DeckCountFontPx, Ink, true, TextAlignmentOptions.Center,
                DeckCountSize, new Vector3(DeckCountOffset.x, DeckCountOffset.y, -DeckStackCount * CardThickness - LayerGap));

            var view = root.AddComponent<DeckView>();
            GameScreenBuilder.Wire(view, ("countText", count));

            var presenter = root.AddComponent<DeckPresenter>();
            GameScreenBuilder.Wire(presenter, ("server", FindServer(nameof(DeckPresenter))));
            return view;
        }

        public static HandPresenter BuildHand(Transform table, DeckView deck, RectTransform dropZone, OpponentSeatsPresenter seats, DeckData gameDeck)
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
            Transform discardSpot = Spot("DiscardSpot", table, DiscardSpotPosition);

            var presenter = root.AddComponent<HandPresenter>();
            GameScreenBuilder.Wire(presenter,
                ("deck", gameDeck), ("drawOrigin", deck.transform), ("server", FindServer(nameof(HandPresenter))), ("dropZone", dropZone), ("seats", seats),
                ("playSpot", playSpot), ("discardSpot", discardSpot));
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

        // 카드 프리팹 — 루트(BoxCollider · CardView, 기준점 아래 가운데) → Front(틀 · 그림 · 이름 · 설명) · Back(뒷면, Y 180°).
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

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<CardView>();
        }

        private static Material EnsureMaterial(string name, string texturePath, string shaderName = CardShaderName)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "Cards");

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                Debug.LogError($"[{nameof(TableBuilder)}] 셰이더가 없다: {shaderName}");

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
            renderer.sortingOrder = TextSortingOrder;
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
