using System;
using System.Collections.Generic;
using System.Reflection;
using Machamy.GameplayTags.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Machamy.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagSet))]
    public class GameplayTagSetDrawer : PropertyDrawer
    {
        private const float LineHeight = 20f;
        private const float Spacing = 2f;
        private readonly Dictionary<string, bool> _foldoutStates = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            string key = property.propertyPath;
            if (!_foldoutStates.ContainsKey(key))
            {
                _foldoutStates[key] = true;
            }

            SerializedProperty tagsProp = property.FindPropertyRelative("serializedTags");
            if (!_foldoutStates[key] || tagsProp == null)
            {
                return LineHeight;
            }

            return LineHeight + (LineHeight + Spacing) * (tagsProp.arraySize + 1);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty tagsProp = property.FindPropertyRelative("serializedTags");
            if (tagsProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Error: serializedTags not found");
                EditorGUI.EndProperty();
                return;
            }

            string key = property.propertyPath;
            if (!_foldoutStates.ContainsKey(key))
            {
                _foldoutStates[key] = true;
            }

            Rect foldoutRect = new Rect(position.x, position.y, position.width, LineHeight);
            _foldoutStates[key] = EditorGUI.Foldout(foldoutRect, _foldoutStates[key], BuildSummaryText(label.text, tagsProp), true);
            if (!_foldoutStates[key])
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float y = position.y + LineHeight + Spacing;
            int indexToDelete = -1;

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                Rect rowRect = new Rect(position.x, y, position.width, LineHeight);
                float indent = EditorGUI.indentLevel * 15f;
                float usableWidth = rowRect.width - indent;
                Rect labelRect = new Rect(rowRect.x + indent, rowRect.y, usableWidth - 48f, LineHeight);
                Rect deleteRect = new Rect(labelRect.xMax + 6f, rowRect.y, 42f, LineHeight);

                string tagName = tagsProp.GetArrayElementAtIndex(i).stringValue;
                bool isInvalid = GameplayTagEditorUiUtility.IsInvalidTagName(tagName);
                GUIContent displayName = new GUIContent(
                    isInvalid ? $"Invalid: {tagName}" : tagName,
                    isInvalid ? "Currently missing from the tag registry." : string.Empty);
                EditorGUI.LabelField(labelRect, displayName);

                if (GUI.Button(deleteRect, "Del"))
                {
                    indexToDelete = i;
                }

                y += LineHeight + Spacing;
            }

            if (indexToDelete >= 0)
            {
                RemoveAt(property, tagsProp, indexToDelete);
            }

            Rect addRect = new Rect(position.x + EditorGUI.indentLevel * 15f, y, position.width - EditorGUI.indentLevel * 15f, LineHeight);
            GameplayTagManager.InitializeIfNeeded();

            List<GameplayTagOption> options = BuildAddOptions(tagsProp);
            string[] displayedOptions = new string[options.Count + 1];
            displayedOptions[0] = "[ Add Tag... ]";
            for (int i = 0; i < options.Count; i++)
            {
                displayedOptions[i + 1] = options[i].DisplayName;
            }

            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUI.Popup(addRect, 0, displayedOptions);
            if (EditorGUI.EndChangeCheck() && selectedIndex > 0)
            {
                AddIfMissing(property, tagsProp, options[selectedIndex - 1].RawValue);
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty tagsProp = property.FindPropertyRelative("serializedTags");
            if (tagsProp == null)
            {
                return new Label("Error: serializedTags not found");
            }

            var root = new VisualElement();
            var foldout = new Foldout
            {
                value = property.isExpanded
            };
            root.Add(foldout);

            var invalidHelpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            invalidHelpBox.style.display = DisplayStyle.None;
            invalidHelpBox.style.marginBottom = 4f;
            foldout.Add(invalidHelpBox);

            var rowsContainer = new VisualElement();
            rowsContainer.style.flexDirection = FlexDirection.Column;
            foldout.Add(rowsContainer);

            var addButton = new Button
            {
                text = "Add Tag"
            };
            addButton.style.marginTop = 6f;
            addButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            foldout.Add(addButton);

            var alreadyAddedTags = new HashSet<string>(StringComparer.Ordinal);
            GameplayTagSearchPopup<GameplayTagOption> addPanel = null;
            addPanel = new GameplayTagSearchPopup<GameplayTagOption>(
                itemSource: () => BuildAddOptions(tagsProp, alreadyAddedTags),
                searchTextSelector: option => option.RawValue,
                bindLabel: (label, option) =>
                {
                    label.text = option.DisplayName;
                    label.style.color = option.IsInvalid || option.IsDimmed
                        ? new StyleColor(new Color(1f, 0.65f, 0.35f))
                        : new StyleColor(StyleKeyword.Null);
                },
                onItemChosen: option =>
                {
                    AddIfMissing(property, tagsProp, option.RawValue);
                    RefreshAll();
                },
                onRefresh: () =>
                {
                    alreadyAddedTags.Clear();
                    for (int i = 0; i < tagsProp.arraySize; i++)
                    {
                        alreadyAddedTags.Add(tagsProp.GetArrayElementAtIndex(i).stringValue);
                    }
                });
            foldout.Add(addPanel);

            void RefreshRows()
            {
                rowsContainer.Clear();

                int invalidCount = 0;
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    string tagName = tagsProp.GetArrayElementAtIndex(i).stringValue;
                    bool isInvalid = GameplayTagEditorUiUtility.IsInvalidTagName(tagName);
                    if (isInvalid)
                    {
                        invalidCount++;
                    }

                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;

                    var tagLabel = new Label(isInvalid ? $"Invalid: {tagName}" : tagName);
                    tagLabel.style.flexGrow = 1f;
                    tagLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    if (isInvalid)
                    {
                        tagLabel.style.color = new StyleColor(new Color(1f, 0.65f, 0.35f));
                        tagLabel.tooltip = "Currently missing from the tag registry.";
                    }

                    var deleteButton = new Button(() =>
                    {
                        int currentIndex = FindTagIndex(tagsProp, tagName);
                        if (currentIndex >= 0)
                        {
                            RemoveAt(property, tagsProp, currentIndex);
                        }

                        RefreshAll();
                    })
                    {
                        text = "Del"
                    };
                    deleteButton.style.width = 42f;

                    row.Add(tagLabel);
                    row.Add(deleteButton);
                    rowsContainer.Add(row);
                }

                invalidHelpBox.text = invalidCount > 0
                    ? $"{invalidCount} invalid tag entr{(invalidCount == 1 ? "y is" : "ies are")} preserved in serialized data."
                    : string.Empty;
                invalidHelpBox.style.display = invalidCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void RefreshHeader()
            {
                foldout.text = BuildSummaryText(property.displayName, tagsProp);
            }

            void RefreshAll()
            {
                RefreshHeader();
                RefreshRows();
                if (addPanel.style.display == DisplayStyle.Flex)
                {
                    addPanel.Refresh();
                }
            }

            foldout.RegisterValueChangedCallback(evt => property.isExpanded = evt.newValue);
            addButton.clicked += () =>
            {
                bool shouldOpen = addPanel.style.display != DisplayStyle.Flex;
                if (!shouldOpen)
                {
                    addPanel.Close();
                    return;
                }

                addPanel.Open();
            };

            root.TrackPropertyValue(tagsProp, _ => RefreshAll());

            RefreshAll();
            return root;
        }

        private static string BuildSummaryText(string displayName, SerializedProperty tagsProp)
        {
            int invalidCount = 0;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (GameplayTagEditorUiUtility.IsInvalidTagName(tagsProp.GetArrayElementAtIndex(i).stringValue))
                {
                    invalidCount++;
                }
            }

            return invalidCount > 0
                ? $"{displayName} ({tagsProp.arraySize} tags, {invalidCount} invalid)"
                : $"{displayName} ({tagsProp.arraySize} tags)";
        }

        private List<GameplayTagOption> BuildAddOptions(SerializedProperty tagsProp, ISet<string> alreadyAddedTags = null)
        {
            HashSet<string> dimmedTags = alreadyAddedTags != null
                ? new HashSet<string>(alreadyAddedTags, StringComparer.Ordinal)
                : CollectExistingTagNames(tagsProp);

            return GameplayTagEditorUiUtility.BuildTagOptions(
                fieldInfo,
                currentTagName: null,
                includeNone: false,
                dimmedTagNames: dimmedTags);
        }

        private static HashSet<string> CollectExistingTagNames(SerializedProperty tagsProp)
        {
            var tagNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                string tagName = tagsProp.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(tagName))
                {
                    tagNames.Add(tagName);
                }
            }

            return tagNames;
        }

        private static void AddIfMissing(SerializedProperty property, SerializedProperty tagsProp, string tagName)
        {
            if (FindTagIndex(tagsProp, tagName) >= 0)
            {
                return;
            }

            tagsProp.arraySize++;
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
            property.serializedObject.ApplyModifiedProperties();
            SyncToSet(property);
        }

        private static void RemoveAt(SerializedProperty property, SerializedProperty tagsProp, int index)
        {
            if (index < 0 || index >= tagsProp.arraySize)
            {
                return;
            }

            tagsProp.DeleteArrayElementAtIndex(index);
            property.serializedObject.ApplyModifiedProperties();
            SyncToSet(property);
        }

        private static int FindTagIndex(SerializedProperty tagsProp, string tagName)
        {
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (string.Equals(tagsProp.GetArrayElementAtIndex(i).stringValue, tagName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SyncToSet(SerializedProperty property)
        {
            object targetObject = property.serializedObject.targetObject;
            if (targetObject == null)
            {
                return;
            }

            EditorUtility.SetDirty((UnityEngine.Object)targetObject);
            if (GetActualObject(property) is GameplayTagSet tagSet)
            {
                tagSet.OnAfterDeserialize();
            }
        }

        private static object GetActualObject(SerializedProperty property)
        {
            string path = property.propertyPath.Replace(".Array.data[", "[");
            object obj = property.serializedObject.targetObject;
            string[] elements = path.Split('.');

            foreach (string element in elements)
            {
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf("[", StringComparison.Ordinal));
                    string indexStr = element.Substring(element.IndexOf("[", StringComparison.Ordinal))
                        .Replace("[", string.Empty)
                        .Replace("]", string.Empty);
                    int index = Convert.ToInt32(indexStr);
                    obj = GetFieldValue(obj, elementName, index);
                }
                else
                {
                    obj = GetFieldValue(obj, element);
                }
            }

            return obj;
        }

        private static object GetFieldValue(object source, string name)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(source);
                }

                PropertyInfo property = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    return property.GetValue(source, null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object GetFieldValue(object source, string name, int index)
        {
            if (!(GetFieldValue(source, name) is System.Collections.IEnumerable enumerable))
            {
                return null;
            }

            System.Collections.IEnumerator enumerator = enumerable.GetEnumerator();
            try
            {
                for (int i = 0; i <= index; i++)
                {
                    if (!enumerator.MoveNext())
                    {
                        return null;
                    }
                }

                return enumerator.Current;
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }
}
