using System;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 개별 게임플레이 태그의 정의를 나타냅니다.
    /// </summary>
    internal class GameplayTagSpec : IEquatable<GameplayTagSpec>, IComparable<GameplayTagSpec>, IComparable
    {
        public static GameplayTagSpec None { get; } = new GameplayTagSpec("None", "Empty Tag", 0, null);
        
        public string TagName { get; }
        public string Description { get; private set; }
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
            Level = Parent != null ? Parent.Level + 1 : 0;
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

        public bool Equals(GameplayTagSpec other)
        {
            if (other == null)
                return false;
            return RuntimeIndex == other.RuntimeIndex;
        }

        public int CompareTo(GameplayTagSpec other)
        {
            if (other == null)
                return 1;
            return RuntimeIndex.CompareTo(other.RuntimeIndex);
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;
            if (obj is GameplayTagSpec otherSpec)
                return CompareTo(otherSpec);
            throw new ArgumentException("Object is not a GameplayTagSpec");
        }

        public override int GetHashCode()
        {
            return TagName.GetHashCode();
        }
    }
}

