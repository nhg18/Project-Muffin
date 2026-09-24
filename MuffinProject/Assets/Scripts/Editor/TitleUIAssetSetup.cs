using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 타이틀 화면 UI 에셋(스프라이트 임포트 설정, TMP 폰트 에셋, 머티리얼 프리셋)을 한 번에 준비한다.
    /// 기준 문서: docs/systems/12-title-ui.md. 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class TitleUIAssetSetup
    {
        private const string SpriteDir = "Assets/Sprites/UI/";
        private const string FontDir = "Assets/Fonts/";

        // 기존 프로젝트 폰트. 한글 11,172자가 미리 구워진 정적 아틀라스라 새로 만들지 않고 그대로 쓴다.
        private const string MainFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Hakgyoansim Dunggeunmiso OTF B SDF.asset";
        private const string PlexMonoAssetPath = FontDir + "IBMPlexMono SDF.asset";

        private static readonly Color32 Stroke = new(0x3A, 0x22, 0x46, 0xFF);

        [MenuItem("Chapchu/Title UI/Setup Assets")]
        public static void SetupAll()
        {
            ConfigureSprites();
            GetOrCreateFontAsset(FontDir + "IBMPlexMono-Regular.ttf", PlexMonoAssetPath, 48, 6, 512, AsciiPrintable() + "·");

            TMP_FontAsset mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontAssetPath);
            if (mainFont != null)
                CreateMaterialPresets(mainFont);
            else
                Debug.LogWarning($"[TitleUIAssetSetup] 폰트 에셋 없음: {MainFontAssetPath}");

            AssetDatabase.SaveAssets();
            Debug.Log("[TitleUIAssetSetup] 완료");
        }

        private static void ConfigureSprites()
        {
            // 둥근 사각은 반경 64 원본 하나를 Image.pixelsPerUnitMultiplier(= 64 / 원하는 반경)로 재사용한다.
            ConfigureSprite(SpriteDir + "ui_round64.png", 64, false);
            ConfigureSprite(SpriteDir + "ui_round64_top_highlight.png", 64, false);
            ConfigureSprite(SpriteDir + "ui_shadow_soft.png", 101, false);
            ConfigureSprite(SpriteDir + "title_overlay_radial.png", 0, false);
            ConfigureSprite(SpriteDir + "icon_sound.png", 0, false);
            ConfigureSprite(SpriteDir + "icon_menu.png", 0, false);
            ConfigureSprite("Assets/Sprites/TitleBackground.png", 0, true);
        }

        private static void ConfigureSprite(string path, int border, bool compressed)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[TitleUIAssetSetup] 스프라이트 없음: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            // 둥근 모서리·그라데이션은 압축하면 계단/띠가 생긴다.
            importer.textureCompression = compressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        private static TMP_FontAsset GetOrCreateFontAsset(string ttfPath, string assetPath, int samplingSize, int padding, int atlasSize, string prewarm)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[TitleUIAssetSetup] 폰트 없음: {ttfPath}");
                return null;
            }

            // 버전 표기 전용. 들어갈 문자가 ASCII 뿐이라 Dynamic 으로 두고 필요한 글자만 미리 굽는다.
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, samplingSize, padding, GlyphRenderMode.SDFAA,
                atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
                return null;

            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            fontAsset.name = name;
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            fontAsset.atlasTexture.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);

            // CreateFontAsset 은 Mobile 셰이더를 붙인다. 로고 외곽선·그림자 품질을 위해 기본 SDF 셰이더로 바꾼다.
            fontAsset.material.shader = Shader.Find("TextMeshPro/Distance Field");
            fontAsset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            fontAsset.TryAddCharacters(prewarm);
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        // 수치는 12-title-ui.md 8절의 px 값을 TMP 정규화 단위로 옮긴 시작값이다. 아티팩트와 겹쳐 보며 최종 조정한다(플랜 S6).
        private static void CreateMaterialPresets(TMP_FontAsset fontAsset)
        {
            // 로고 190px: 외곽선 5 · 그림자 (6, 6) 90%
            Material logo = GetOrCreatePreset(fontAsset, "Logo");
            SetOutline(logo, 0.3f);
            SetUnderlay(logo, new Color32(Stroke.r, Stroke.g, Stroke.b, 230), 0.2f, -0.2f, 0.3f);

            // 부제 36px: 외곽선 2.5 · 그림자 (0, 3) 85%
            Material subtitle = GetOrCreatePreset(fontAsset, "Subtitle");
            SetOutline(subtitle, 0.45f);
            SetUnderlay(subtitle, new Color32(Stroke.r, Stroke.g, Stroke.b, 217), 0f, -0.5f, 0.45f);

            // 에러 18px: 그림자 (0, 2) 50%
            Material error = GetOrCreatePreset(fontAsset, "Error");
            SetOutline(error, 0f);
            SetUnderlay(error, new Color32(Stroke.r, Stroke.g, Stroke.b, 128), 0f, -0.7f, 0f);
        }

        private static Material GetOrCreatePreset(TMP_FontAsset fontAsset, string suffix)
        {
            // TMP 는 "폰트 에셋 이름 + ..." 으로 시작하고 같은 아틀라스를 쓰는 머티리얼을 프리셋 목록에 보여준다.
            string path = $"{FontDir}{fontAsset.name} - {suffix}.mat";
            Material preset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (preset != null)
                return preset;

            preset = new Material(fontAsset.material) { name = $"{fontAsset.name} - {suffix}" };
            // 기존 폰트 에셋의 기본 머티리얼은 Mobile 셰이더다. 프리셋만 기본 SDF 셰이더로 바꾼다(원본 머티리얼은 다른 씬이 쓰므로 건드리지 않는다).
            preset.shader = Shader.Find("TextMeshPro/Distance Field");
            AssetDatabase.CreateAsset(preset, path);
            return preset;
        }

        private static void SetOutline(Material material, float width)
        {
            material.SetColor("_OutlineColor", (Color)Stroke);
            material.SetFloat("_OutlineWidth", width);
            // TMP 외곽선은 글자 안쪽도 깎는다. CSS paint-order(stroke → fill)처럼 면을 유지하려고 같은 양만큼 부풀린다.
            material.SetFloat("_FaceDilate", width);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
        }

        private static void SetUnderlay(Material material, Color color, float offsetX, float offsetY, float dilate)
        {
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", color);
            material.SetFloat("_UnderlayOffsetX", offsetX);
            material.SetFloat("_UnderlayOffsetY", offsetY);
            material.SetFloat("_UnderlayDilate", dilate);
            material.SetFloat("_UnderlaySoftness", 0f);
            EditorUtility.SetDirty(material);
        }

        private static string AsciiPrintable()
        {
            System.Text.StringBuilder builder = new();
            for (char c = ' '; c <= '~'; c++)
                builder.Append(c);
            return builder.ToString();
        }
    }
}
