using System;
using System.Collections.Generic;
using System.Linq;
using Machamy.GameplayTags.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Machamy.GameplayTags.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public class GameplayTagDrawer : PropertyDrawer
    {
        private const string NoneLabel = "None";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            bool compact = HasCompactAttribute();

            SerializedProperty rawTagNameProp = property.FindPropertyRelative("rawTagName");
            string currentTagName = rawTagNameProp.stringValue;
            List<GameplayTagOption> options = BuildOptions(currentTagName);
            string[] displayedOptions = options.Select(option => option.DisplayName).ToArray();

            int selectedIndex = FindSelectedIndex(options, currentTagName);

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUI.Popup(position, compact ? GUIContent.none.text : label.text, selectedIndex, displayedOptions);
            if (EditorGUI.EndChangeCheck() && selectedIndex >= 0 && selectedIndex < options.Count)
            {
                rawTagNameProp.stringValue = options[selectedIndex].RawValue;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty rawTagNameProp = property.FindPropertyRelative("rawTagName");
            bool compact = HasCompactAttribute();

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.minWidth = 0f;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0f;

            if (!compact)
            {
                var labelElement = new Label(property.displayName);
                labelElement.style.minWidth = EditorGUIUtility.labelWidth - 4f;
                labelElement.style.marginRight = 4f;
                labelElement.style.flexShrink = 0f;
                row.Add(labelElement);
            }

            var pickerButton = new Button();
            pickerButton.style.flexGrow = 1f;
            pickerButton.style.flexShrink = 1f;
            pickerButton.style.minWidth = compact ? 72f : 0f;
            pickerButton.style.unityTextAlign = TextAnchor.MiddleLeft;

            row.Add(pickerButton);
            root.Add(row);

            var warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            warningBox.style.display = DisplayStyle.None;
            warningBox.style.marginTop = 4f;
            root.Add(warningBox);

            GameplayTagSearchPopup<GameplayTagOption> dropdown = null;
            dropdown = new GameplayTagSearchPopup<GameplayTagOption>(
                itemSource: () => BuildOptions(rawTagNameProp.stringValue),
                searchTextSelector: option => option.RawValue.Length == 0 ? option.DisplayName : option.RawValue,
                bindLabel: (label, option) =>
                {
                    label.text = option.DisplayName;
                    label.style.color = option.IsInvalid
                        ? new StyleColor(new Color(1f, 0.65f, 0.35f))
                        : new StyleColor(StyleKeyword.Null);
                },
                onItemChosen: option =>
                {
                    rawTagNameProp.serializedObject.Update();
                    rawTagNameProp.stringValue = option.RawValue;
                    rawTagNameProp.serializedObject.ApplyModifiedProperties();
                    dropdown.Close();
                    RefreshVisualState();
                });
            root.Add(dropdown);

            void RefreshVisualState()
            {
                string currentTagName = rawTagNameProp.stringValue;
                pickerButton.text = FormatSelectionLabel(currentTagName);

                bool isInvalid = IsInvalidSelection(currentTagName);
                warningBox.text = isInvalid ? $"Invalid tag: {currentTagName}" : string.Empty;
                warningBox.style.display = isInvalid ? DisplayStyle.Flex : DisplayStyle.None;
            }

            pickerButton.clicked += () =>
            {
                bool shouldOpen = dropdown.style.display != DisplayStyle.Flex;
                if (!shouldOpen)
                {
                    dropdown.Close();
                    return;
                }

                dropdown.Open();
            };

            root.TrackPropertyValue(rawTagNameProp, _ =>
            {
                RefreshVisualState();
                if (dropdown.style.display == DisplayStyle.Flex)
                {
                    dropdown.Refresh();
                }
            });

            RefreshVisualState();

            return root;
        }

        private bool HasCompactAttribute()
        {
            return fieldInfo?.GetCustomAttributes(typeof(GameplayTagCompactAttribute), true).Length > 0;
        }

        private List<GameplayTagOption> BuildOptions(string currentTagName)
        {
            return GameplayTagEditorUiUtility.BuildTagOptions(fieldInfo, currentTagName, includeNone: true);
        }

        private static int FindSelectedIndex(IReadOnlyList<GameplayTagOption> options, string currentTagName)
        {
            string normalizedValue = currentTagName ?? string.Empty;
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].RawValue, normalizedValue, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        private static string FormatSelectionLabel(string currentTagName)
        {
            if (string.IsNullOrEmpty(currentTagName))
            {
                return NoneLabel;
            }

            return IsInvalidSelection(currentTagName)
                ? $"Invalid: {currentTagName}"
                : currentTagName;
        }

        private static bool IsInvalidSelection(string currentTagName)
        {
            return GameplayTagEditorUiUtility.IsInvalidTagName(currentTagName);
        }
    }
}
