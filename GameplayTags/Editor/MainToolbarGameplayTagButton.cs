using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Machamy.GameplayTags.Editor
{
    /// <summary>
    /// 메인 툴바에 GameplayTag 에디터 여는 버튼 추가
    /// </summary>
    public static class MainToolbarGameplayTagButton
    {
        [MainToolbarElement("Tools/GameplayTagEditor", defaultDockPosition = MainToolbarDockPosition.Right)]
        public static MainToolbarButton CreateButton()
        {
            var icon = EditorGUIUtility.IconContent("d_Settings").image as Texture2D;
            var content = new MainToolbarContent("🏷️ GameplayTag", icon, "Gameplay Tag Manager를 엽니다");
            var button = new MainToolbarButton(content, OnButtonClick);
            return button;
        }

        private static void OnButtonClick()
        {
            GameplayTagEditor.ShowWindow();
        }
    }
}

