using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Machamy.GameplayTags.Runtime;

namespace Machamy.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public class GameplayTagDrawer : PropertyDrawer
    {
        private const int UnresolvedTagId = -10;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty rawTagNameProp = property.FindPropertyRelative("rawTagName");
            SerializedProperty tagIdProp = property.FindPropertyRelative("tagId");

            // 레이블 표시
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // 모든 태그 가져오기
            GameplayTagManager.InitializeIfNeeded();
            var allTags = GameplayTagManager.GetAllTags().ToArray();
            
            // 드롭다운 옵션 생성
            List<string> tagNames = new List<string> { "None" };
            tagNames.AddRange(allTags.Select(t => t.TagName));

            // 현재 선택된 인덱스 찾기
            int selectedIndex = 0;
            string currentTagName = rawTagNameProp.stringValue;
            
            if (!string.IsNullOrEmpty(currentTagName))
            {
                selectedIndex = tagNames.IndexOf(currentTagName);
                if (selectedIndex < 0)
                    selectedIndex = 0;
            }

            // 드롭다운 표시
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, selectedIndex, tagNames.ToArray());
            
            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex == 0)
                {
                    // None 선택
                    rawTagNameProp.stringValue = string.Empty;
                    tagIdProp.intValue = 0;
                }
                else
                {
                    // 태그 선택
                    string selectedTagName = tagNames[newIndex];
                    rawTagNameProp.stringValue = selectedTagName;
                    tagIdProp.intValue = UnresolvedTagId;
                }
                
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }
    }
}