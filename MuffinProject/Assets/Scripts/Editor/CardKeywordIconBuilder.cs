using System.IO;
using Chapchu.Cards;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 카드 키워드 아이콘 12개(11-card-list.md 5-1 · 5-4)를 TMP 스프라이트 에셋으로 만든다 — 카드 문구 안 <c>&lt;sprite="CardKeywords" name="…"&gt;</c>.
    /// 그림은 Sprites/UI/Common/card_keywords.png 한 장 — 128 칸 4 × 4(마지막 줄 빔), 칸 순서가 IconNames. 지금 그림은 시안 「카드 키워드 v2」 SVG 를 옮긴 임시 그림이다.
    /// 그림을 바꾸면(같은 칸 배치) 메뉴 Chapchu/Cards/Build Keyword Icons 를 다시 돌린다. 에셋은 TMP 가 이름으로 찾는 Resources/Sprite Assets/ 에 둔다.
    /// </summary>
    public static class CardKeywordIconBuilder
    {
        private const string TexturePath = "Assets/Sprites/UI/Common/card_keywords.png";
        private const string AssetFolder = "Assets/Resources/Sprite Assets";
        private const int Cell = 128;
        private const int Columns = 4;

        // 글 안 크기 · 자리 — TMP 는 아이콘 높이를 글꼴 ascent 에 맞춘다. 시안 .kw .ic: 1.15em · vertical-align -.21em · 뒤 .08em
        private const float GlyphScale = 1.15f;
        private const float RiseRatio = 0.82f;   // 아이콘 높이 중 글 바닥선 위로 올라가는 몫 — 나머지가 바닥선 아래로
        private const float AdvanceRatio = 1.08f; // 아이콘 폭 + 뒤 틈

        private static readonly string[] IconNames = { "burst", "skull", "heal", "gain", "lose", "lock", "skip", "uturn", "trapx", "swap", "reset", "clock" };

        [MenuItem("Chapchu/Cards/Build Keyword Icons")]
        public static void Build()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer == null)
            {
                Debug.LogError($"[{nameof(CardKeywordIconBuilder)}] 아이콘 그림이 없다: {TexturePath}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true; // 3D 카드는 원근으로 작아진다
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            string assetPath = $"{AssetFolder}/{CardKeywords.IconAssetName}.asset";
            Directory.CreateDirectory(AssetFolder);
            AssetDatabase.DeleteAsset(assetPath);

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Version").stringValue = "1.1.0"; // 비어 있으면 TMP 가 옛 에셋으로 보고 고치려 든다
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.spriteSheet = texture;

            for (int i = 0; i < IconNames.Length; i++)
            {
                int x = i % Columns * Cell;
                int y = texture.height - (i / Columns + 1) * Cell; // 글리프 칸은 아래가 0
                var metrics = new GlyphMetrics(Cell, Cell, Cell * (AdvanceRatio - 1f) / 2f, Cell * RiseRatio, Cell * AdvanceRatio);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, new GlyphRect(x, y, Cell, Cell), GlyphScale, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = IconNames[i], scale = 1f });
            }

            var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = $"{CardKeywords.IconAssetName} Material" };
            material.SetTexture(ShaderUtilities.ID_MainTex, texture);
            asset.material = material;
            AssetDatabase.AddObjectToAsset(material, asset);

            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(CardKeywordIconBuilder)}] {assetPath} — 아이콘 {IconNames.Length}개.");
        }
    }
}
