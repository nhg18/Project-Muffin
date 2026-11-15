using System;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Game.Effects;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 카드 에셋 인스펙터 — 효과 목록을 + 메뉴로 종류를 골라 추가하고, 끌어서 순서를 바꾼다 (CODE_CONVENTION 9절).
    /// 메뉴에는 CardEffect 를 상속한 클래스가 자동으로 나온다. 새 효과를 만들면 여기는 고치지 않는다.
    /// </summary>
    [CustomEditor(typeof(CardData))]
    public class CardDataEditor : Editor
    {
        private const string EffectsField = nameof(CardData.effects);
        private const string EffectSuffix = "Effect";

        private SerializedProperty _effects;
        private ReorderableList _list;

        private void OnEnable()
        {
            _effects = serializedObject.FindProperty(EffectsField);
            _list = new ReorderableList(serializedObject, _effects, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "카드 효과 — 위에서부터 순서대로 실행"),
                elementHeightCallback = GetElementHeight,
                drawElementCallback = DrawElement,
                onAddDropdownCallback = ShowAddMenu
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, EffectsField);
            EditorGUILayout.Space();
            _list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        private float GetElementHeight(int index)
        {
            return EditorGUI.GetPropertyHeight(_effects.GetArrayElementAtIndex(index), true) + EditorGUIUtility.standardVerticalSpacing;
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = _effects.GetArrayElementAtIndex(index);
            object effect = element.managedReferenceValue;
            string label = effect == null ? "(비어 있음 — 지우고 다시 추가)" : GetDisplayName(effect.GetType());

            rect.xMin += 10f; // 접기 화살표가 끌기 손잡이와 겹치지 않게
            rect.height -= EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(rect, element, new GUIContent(label), true);
        }

        private void ShowAddMenu(Rect buttonRect, ReorderableList list)
        {
            var menu = new GenericMenu();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<CardEffect>().Where(t => !t.IsAbstract).OrderBy(t => t.Name))
                menu.AddItem(new GUIContent(GetDisplayName(type)), false, () => AddEffect(type));

            menu.DropDown(buttonRect);
        }

        private void AddEffect(Type type)
        {
            serializedObject.Update();
            int index = _effects.arraySize;
            _effects.arraySize++;

            SerializedProperty element = _effects.GetArrayElementAtIndex(index);
            element.managedReferenceValue = Activator.CreateInstance(type); // 늘린 칸에는 앞 칸 값이 들어 있으므로 새 인스턴스로 바꾼다
            element.isExpanded = true;
            serializedObject.ApplyModifiedProperties();
        }

        // DamageEffect → Damage (11-card-list.md 5절의 효과 타입 이름)
        private static string GetDisplayName(Type type)
        {
            string name = type.Name;
            return name.EndsWith(EffectSuffix) ? name.Substring(0, name.Length - EffectSuffix.Length) : name;
        }
    }
}
