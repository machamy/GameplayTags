using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Machamy.GameplayTags.Runtime;

namespace Machamy.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagContainer))]
    public class GameplayTagContainerDrawer : PropertyDrawer
    {
        private const int UnresolvedTagId = -10;
        private const float LineHeight = 18f;
        private const float Padding = 2f;
        
        private bool _foldout = true;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = LineHeight + Padding;
            
            if (_foldout)
            {
                SerializedProperty tagsProp = property.FindPropertyRelative("serializedTags");
                
                int count = tagsProp != null ? tagsProp.arraySize : 0;
                height += (LineHeight + Padding) * count;
                height += LineHeight + Padding; // 추가 버튼용
            }
            
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty tagsProp = property.FindPropertyRelative("serializedTags");
            SerializedProperty countsProp = property.FindPropertyRelative("serializedCounts");

            if (tagsProp == null || countsProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Error: Property not found");
                EditorGUI.EndProperty();
                return;
            }

            // 폴드아웃 헤더
            Rect foldoutRect = new Rect(position.x, position.y, position.width, LineHeight);
            _foldout = EditorGUI.Foldout(foldoutRect, _foldout, 
                $"{label.text} ({tagsProp.arraySize} tags)", true);

            if (_foldout)
            {
                EditorGUI.indentLevel++;
                float currentY = position.y + LineHeight + Padding;

                // 모든 태그 가져오기
                GameplayTagManager.InitializeIfNeeded();
                var allTags = GameplayTagManager.GetAllTags().ToArray();
                
                // 드롭다운 옵션 생성
                List<string> tagNames = new List<string> { "Select Tag..." };
                tagNames.AddRange(allTags.Select(t => t.TagName));

                // 기존 태그 표시
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    Rect itemRect = new Rect(position.x, currentY, position.width, LineHeight);
                    
                    // 태그와 카운트를 한 줄에 표시
                    float labelWidth = position.width * 0.6f;
                    float countWidth = 60f;
                    float removeWidth = 60f;
                    
                    Rect tagRect = new Rect(itemRect.x, itemRect.y, labelWidth, LineHeight);
                    Rect countRect = new Rect(itemRect.x + labelWidth + 5, itemRect.y, countWidth, LineHeight);
                    Rect removeRect = new Rect(itemRect.x + labelWidth + countWidth + 10, itemRect.y, removeWidth, LineHeight);

                    // 태그 이름 표시
                    SerializedProperty tagProp = tagsProp.GetArrayElementAtIndex(i);
                    SerializedProperty tagNameProp = tagProp.FindPropertyRelative("rawTagName");
                    string tagName = tagNameProp != null ? tagNameProp.stringValue : "Unknown";
                    EditorGUI.LabelField(tagRect, tagName);

                    // 카운트 표시 및 수정
                    SerializedProperty countProp = countsProp.GetArrayElementAtIndex(i);
                    EditorGUI.BeginChangeCheck();
                    int newCount = EditorGUI.IntField(countRect, countProp.intValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        countProp.intValue = Mathf.Max(1, newCount);
                        property.serializedObject.ApplyModifiedProperties();
                        
                        // 직렬화 상태 업데이트 및 딕셔너리 동기화
                        var targetObject = property.serializedObject.targetObject;
                        if (targetObject != null)
                        {
                            EditorUtility.SetDirty(targetObject);
                            
                            // 대상 객체의 필드 값 가져오기 및 OnAfterDeserialize 호출
                            var containerField = fieldInfo.GetValue(targetObject);
                            if (containerField is ISerializationCallbackReceiver receiver)
                            {
                                receiver.OnAfterDeserialize();
                            }
                        }
                    }

                    // 제거 버튼
                    if (GUI.Button(removeRect, "Remove"))
                    {
                        tagsProp.DeleteArrayElementAtIndex(i);
                        countsProp.DeleteArrayElementAtIndex(i);
                        property.serializedObject.ApplyModifiedProperties();
                        
                        // 직렬화 상태 업데이트 및 딕셔너리 동기화
                        var targetObject = property.serializedObject.targetObject;
                        if (targetObject != null)
                        {
                            EditorUtility.SetDirty(targetObject);
                            
                            // 대상 객체의 필드 값 가져오기 및 OnAfterDeserialize 호출
                            var containerField = fieldInfo.GetValue(targetObject);
                            if (containerField is ISerializationCallbackReceiver receiver)
                            {
                                receiver.OnAfterDeserialize();
                            }
                        }
                        break;
                    }

                    currentY += LineHeight + Padding;
                }

                // 새 태그 추가 버튼
                Rect addRect = new Rect(position.x, currentY, position.width, LineHeight);
                
                EditorGUI.BeginChangeCheck();
                int selectedIndex = EditorGUI.Popup(addRect, 0, tagNames.ToArray());
                
                if (EditorGUI.EndChangeCheck() && selectedIndex > 0)
                {
                    string selectedTagName = tagNames[selectedIndex];
                    
                    // 이미 존재하는 태그인지 확인
                    bool tagExists = false;
                    for (int i = 0; i < tagsProp.arraySize; i++)
                    {
                        SerializedProperty existingTagProp = tagsProp.GetArrayElementAtIndex(i);
                        SerializedProperty existingTagNameProp = existingTagProp.FindPropertyRelative("rawTagName");
                        if (existingTagNameProp != null && existingTagNameProp.stringValue == selectedTagName)
                        {
                            // 카운트 증가
                            SerializedProperty existingCountProp = countsProp.GetArrayElementAtIndex(i);
                            existingCountProp.intValue++;
                            tagExists = true;
                            break;
                        }
                    }
                    
                    if (!tagExists)
                    {
                        // 새 태그 추가
                        tagsProp.arraySize++;
                        countsProp.arraySize++;
                        
                        SerializedProperty newTagProp = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
                        SerializedProperty newTagNameProp = newTagProp.FindPropertyRelative("rawTagName");
                        SerializedProperty newTagIdProp = newTagProp.FindPropertyRelative("tagId");
                        
                        if (newTagNameProp != null && newTagIdProp != null)
                        {
                            newTagNameProp.stringValue = selectedTagName;
                            newTagIdProp.intValue = UnresolvedTagId;
                        }
                        
                        SerializedProperty newCountProp = countsProp.GetArrayElementAtIndex(countsProp.arraySize - 1);
                        newCountProp.intValue = 1;
                    }
                    
                    property.serializedObject.ApplyModifiedProperties();
                    
                    // 직렬화 상태 업데이트 및 딕셔너리 동기화
                    var targetObject = property.serializedObject.targetObject;
                    if (targetObject != null)
                    {
                        EditorUtility.SetDirty(targetObject);
                        
                        // 대상 객체의 필드 값 가져오기 및 OnAfterDeserialize 호출
                        var containerField = fieldInfo.GetValue(targetObject);
                        if (containerField is ISerializationCallbackReceiver receiver)
                        {
                            receiver.OnAfterDeserialize();
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}

