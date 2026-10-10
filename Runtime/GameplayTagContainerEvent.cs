using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    public struct GameplayTagContainerEvent
    {
        public EventType Type;
        public TagType TagGroup;
        public GameplayTag Tag;
        public int PreviousCount;
        public int NewCount;
        
        public bool IsAdded => Type == EventType.Added;
        public bool IsRemoved => Type == EventType.Removed;
        public bool IsRegistered => NewCount > 0 && PreviousCount == 0;
        public bool IsUnregistered => NewCount == 0 && PreviousCount > 0;
        // public bool IsCleared => Type == EventType.Cleared;
        public bool IsExplicit => TagGroup == TagType.Explicit;
        public bool IsImplicit => TagGroup == TagType.Implicit;
        
        public enum EventType : byte
        {
            [Tooltip("태그가 추가된경우 (카운트 증가)")]
            Added,
    
            [Tooltip("태그가 줄어든경우 (카운트 감소)")]
            Removed,
            
            // [Tooltip("태그가 클리어 된경우")]
            // Cleared,
        }
        
        public enum TagType
        {
            Explicit,
            Implicit
        }
    }
}