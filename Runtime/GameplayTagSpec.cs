using System;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 개별 게임플레이 태그의 정의를 나타냅니다.
    /// </summary>
    internal class GameplayTagSpec
    {
        public static GameplayTagSpec None { get; } = new GameplayTagSpec("None", "Empty Tag", 0, null);
        public static GameplayTagSpec Invalid { get; } = new GameplayTagSpec("Invalid", "Invalid Tag", -1, null);
        
        public string TagName { get; private set; }
        public string Description { get; private set; }
        
        /// <summary>
        /// 해당 소스에서의 런타임 인덱스
        /// </summary>
        public int RuntimeIndex { get; private set; }
        public GameplayTag Tag { get; private set; }
        public IGameplayTagSource Source { get; private set; }
        
        // 계층 구조 정보
        public GameplayTagSpec Parent { get; private set; }
        public int Level { get; private set; }

        public GameplayTagSpec(string tagName, string description, int runtimeIndex, IGameplayTagSource source)
        {
            TagName = tagName;
            Description = description;
            RuntimeIndex = runtimeIndex;
            Source = source;
            
            Tag = new GameplayTag(this);
        }

        public void SetParent(GameplayTagSpec parent)
        {
            Parent = parent;
        }
        

        public bool IsParentOf(GameplayTagSpec other)
        {
            if (other == null || Level >= other.Level)
                return false;
            
            GameplayTagSpec current = other;
            while (current != null && current.Level > Level)
            {
                current = current.Parent;
            }
            return current == this;
        }

        public bool IsChildOf(GameplayTagSpec other)
        {
            return other?.IsParentOf(this) ?? false;
        }
    }
}

