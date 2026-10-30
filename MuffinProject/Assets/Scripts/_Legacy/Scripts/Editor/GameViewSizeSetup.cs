using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// Game 뷰 해상도 프리셋을 PC(Standalone) · Android · iOS 그룹에 등록한다. 게임은 가로 고정이라 모두 가로(긴 변 × 짧은 변)로 넣는다.
    /// 기준 문서: docs/systems/15-screen.md 8절. 여러 번 실행해도 같은 이름 · 크기는 다시 넣지 않는다.
    /// Unity 가 Game 뷰 크기 API 를 공개하지 않아 내부 타입(GameViewSizes)을 리플렉션으로 쓴다.
    /// </summary>
    public static class GameViewSizeSetup
    {
        private static readonly (string Name, int Width, int Height)[] Presets =
        {
            // 15-screen 8절 — 비율 기준
            ("20:9", 2400, 1080),
            ("19.5:9", 2340, 1080),
            ("16:9", 1920, 1080),
            ("16:10", 2560, 1600),
            ("4:3", 2048, 1536),
            ("Fold 펼침", 2176, 1812),

            // 많이 쓰는 iPhone (폴더블 · Air 같은 예외 모델 제외)
            ("iPhone 11 · XR", 1792, 828),
            ("iPhone 12 · 13 · 14 · 16e", 2532, 1170),
            ("iPhone 15 · 15 Pro · 16", 2556, 1179),
            ("iPhone 16 Pro · 17 · 17 Pro", 2622, 1206),
            ("iPhone 15 Plus · 15 Pro Max · 16 Plus", 2796, 1290),
            ("iPhone 16 Pro Max · 17 Pro Max", 2868, 1320),

            // 많이 쓰는 Galaxy (Z Fold · Z Flip 제외)
            ("Galaxy S23 · S24 · S25", 2340, 1080),
            ("Galaxy S24+ · S25+ · Ultra (QHD+)", 3120, 1440),
            ("Galaxy A16 · A36 · A56", 2340, 1080),
            ("Galaxy A05 · A06", 1600, 720),
        };

        // Game 뷰는 에디터의 현재 플랫폼 그룹만 보여 준다. 플랫폼이 PC 여도 보이도록 Standalone 에도 넣는다.
        private static readonly GameViewSizeGroupType[] Groups =
        {
            GameViewSizeGroupType.Standalone,
            GameViewSizeGroupType.Android,
            GameViewSizeGroupType.iOS,
        };

        [MenuItem("Chapchu/Game View/Add Mobile Presets")]
        public static void AddPresets()
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            Type sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
            if (sizesType == null || sizeType == null || sizeKindType == null)
            {
                Debug.LogError("[GameViewSizeSetup] Game 뷰 내부 타입을 찾지 못했다. Unity 버전을 확인할 것.");
                return;
            }

            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                .GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            MethodInfo getGroup = sizesType.GetMethod("GetGroup");
            ConstructorInfo newSize = sizeType.GetConstructor(new[] { sizeKindType, typeof(int), typeof(int), typeof(string) });
            object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");

            int added = 0;
            foreach (GameViewSizeGroupType groupType in Groups)
            {
                object group = getGroup.Invoke(sizes, new object[] { groupType });
                foreach ((string name, int width, int height) in Presets)
                {
                    if (Contains(group, name, width, height))
                        continue;

                    object size = newSize.Invoke(new[] { fixedResolution, width, height, (object)name });
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    added++;
                }
            }

            sizesType.GetMethod("SaveToHDD")?.Invoke(sizes, null);
            Debug.Log($"[GameViewSizeSetup] 프리셋 {added}개 추가 (PC · Android · iOS 그룹)");
        }

        private static bool Contains(object group, string name, int width, int height)
        {
            Type groupType = group.GetType();
            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            MethodInfo getSize = groupType.GetMethod("GetGameViewSize");

            for (int i = 0; i < count; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                Type type = size.GetType();
                if ((string)type.GetProperty("baseText").GetValue(size) == name
                    && (int)type.GetProperty("width").GetValue(size) == width
                    && (int)type.GetProperty("height").GetValue(size) == height)
                    return true;
            }

            return false;
        }
    }
}
