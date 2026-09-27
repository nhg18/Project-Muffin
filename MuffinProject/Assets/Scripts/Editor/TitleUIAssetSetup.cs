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
        private const string SpriteDir = "Assets/Sprites/UI/Common/";
        private const string BackgroundDir = "Assets/Sprites/UI/Background/";
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
            CreateVerticalOverlay(BackgroundDir + "bg_overlay_vertical.png");
            // 배경이 반투명한 박스(입력창 · 시스템 버튼)는 테두리를 링으로 그린다.
            // 꽉 찬 사각 2장을 겹치면 배경 아래로 테두리색이 비쳐 CSS(배경 위에 테두리)와 달라진다.
            CreateRing(SpriteDir + "ui_ring_r20_w4.png", 4f / 20f);
            CreateRing(SpriteDir + "ui_ring_r14_w2.png", 2f / 14f);

            // 둥근 사각은 반경 64 원본 하나를 Image.pixelsPerUnitMultiplier(= 64 / 원하는 반경)로 재사용한다.
            ConfigureSprite(SpriteDir + "ui_round64.png", 64, false);
            ConfigureSprite(SpriteDir + "ui_ring_r20_w4.png", 64, false);
            ConfigureSprite(SpriteDir + "ui_ring_r14_w2.png", 64, false);
            ConfigureSprite(SpriteDir + "ui_round64_top_highlight.png", 64, false);
            ConfigureSprite(SpriteDir + "ui_shadow_soft.png", 101, false);
            ConfigureSprite(BackgroundDir + "bg_overlay_vertical.png", 0, false);
            ConfigureSprite(SpriteDir + "icon_sound.png", 0, false);
            ConfigureSprite(SpriteDir + "icon_menu.png", 0, false);
            ConfigureSprite(BackgroundDir + "bg_title.png", 0, true);
        }

        // 12-title-ui.md 9-1: 위 → 아래 #2E2440 26% → 40% 지점 10% → 46%. 색은 고정이고 알파만 변한다.
        private static void CreateVerticalOverlay(string path)
        {
            const int width = 4;
            const int height = 256;
            Color32 baseColor = new(0x2E, 0x24, 0x40, 0xFF);

            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                // 텍스처 y 는 아래가 0 이므로 CSS 기준(위가 0) 위치로 뒤집는다.
                float t = 1f - (y + 0.5f) / height;
                float alpha = t < 0.4f
                    ? Mathf.Lerp(0.26f, 0.10f, t / 0.4f)
                    : Mathf.Lerp(0.10f, 0.46f, (t - 0.4f) / 0.6f);

                Color32 color = baseColor;
                color.a = (byte)Mathf.RoundToInt(alpha * 255f);
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, color);
            }

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        // ui_round64.png 와 같은 160px · 반경 64 윤곽의 링. 두께는 (테두리 / 반경) 비율로 받는다 → 64 / 반경 배율로 쓰면 원하는 두께가 된다.
        private static void CreateRing(string path, float thicknessRatio)
        {
            const int size = 160;
            const float radius = 64f;
            float half = size / 2f;
            float thickness = radius * thicknessRatio;

            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 둥근 사각의 부호 거리(안쪽 음수). 안쪽 윤곽은 같은 거리장을 두께만큼 줄인 것.
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                    float outer = Mathf.Clamp01(0.5f - distance);
                    float inner = Mathf.Clamp01(0.5f - (distance + thickness));
                    byte alpha = (byte)Mathf.RoundToInt((outer - inner) * 255f);
                    texture.SetPixel(x, y, new Color32(0xFF, 0xFF, 0xFF, alpha));
                }
            }

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
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
        // 12-title-ui.md 8절의 px 값을 그대로 환산한다. 프리셋은 글자 크기별이다(같은 px 라도 크기마다 SDF 값이 다르다).
        private static void CreateMaterialPresets(TMP_FontAsset fontAsset)
        {
            // 로고 190: 외곽선 5 · 그림자 (6, 6) 90%
            ApplyTextEffects(GetOrCreatePreset(fontAsset, "Logo"), fontAsset, 190f, 5f, new Vector2(6f, 6f), 0.9f);
            // 부제 36: 외곽선 2.5 · 그림자 (0, 3) 85%
            ApplyTextEffects(GetOrCreatePreset(fontAsset, "Subtitle"), fontAsset, 36f, 2.5f, new Vector2(0f, 3f), 0.85f);
            // 에러 18: 외곽선 없음 · 그림자 (0, 2) 50%
            ApplyTextEffects(GetOrCreatePreset(fontAsset, "Error"), fontAsset, 18f, 0f, new Vector2(0f, 2f), 0.5f);
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

        /// <summary>
        /// CSS 의 -webkit-text-stroke(paint-order: stroke fill) + 하드 text-shadow 를 TMP SDF 머티리얼로 옮긴다.
        /// </summary>
        /// <param name="stroke">글자 바깥으로 보이는 외곽선 두께 px (CSS stroke 폭의 절반)</param>
        /// <param name="shadow">그림자 (오른쪽, 아래) px. 브라우저는 외곽선을 뺀 글자 면 모양으로 그림자를 그린다(아티팩트 렌더링으로 확인)</param>
        private static void ApplyTextEffects(Material material, TMP_FontAsset fontAsset, float fontSize, float stroke, Vector2 shadow, float shadowAlpha)
        {
            // TMP 는 기본적으로 효과 값을 패딩 한도 안으로 줄여(Ratios) px 로 환산할 수 없다. 끄고 직접 환산한다.
            material.EnableKeyword("RATIOS_OFF");

            // 셰이더 1 단위 = GradientScale 텍셀. 텍셀 → px 는 글자 크기 / 아틀라스 샘플링 크기.
            float pxPerUnit = material.GetFloat(ShaderUtilities.ID_GradientScale) * fontSize / fontAsset.faceInfo.pointSize;

            // TMP 외곽선은 면 가장자리를 중심으로 안팎 반씩 그려진다.
            // 면을 두께의 절반만큼 부풀리면 원래 글자 면은 그대로 두고 바깥에 stroke 만큼 보인다.
            float halfStroke = stroke * 0.5f / pxPerUnit;
            material.SetFloat(ShaderUtilities.ID_FaceDilate, halfStroke);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, halfStroke);
            material.SetColor(ShaderUtilities.ID_OutlineColor, ToShaderColor(Stroke));

            // TMP 언더레이는 부풀린 면을 기준으로 그려지므로, 같은 양만큼 줄여 원래 글자 면 모양으로 되돌린다.
            material.EnableKeyword("UNDERLAY_ON");
            Color shadowColor = (Color)Stroke;
            shadowColor.a = shadowAlpha;
            material.SetColor(ShaderUtilities.ID_UnderlayColor, ToShaderColor(shadowColor));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadow.x / pxPerUnit);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -shadow.y / pxPerUnit);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, -halfStroke);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);

            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
        }

        // TMP 셰이더의 외곽선 · 언더레이 색은 [HDR] 이라 Linear 색 공간에서 자동 변환되지 않는다.
        // 인스펙터 색상 선택기는 알아서 바꿔 저장하지만 스크립트는 직접 선형 값으로 넣어야 디자인 색(sRGB)과 같아진다.
        private static Color ToShaderColor(Color color) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        private static string AsciiPrintable()
        {
            System.Text.StringBuilder builder = new();
            for (char c = ' '; c <= '~'; c++)
                builder.Append(c);
            return builder.ToString();
        }
    }
}
