using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 게임플레이 태그 컨테이너 인터페이스 (카운터 기반)
    /// </summary>
    public interface IGameplayTagContainer : IReadOnlyGameplayTagContainer
    {
        /// <summary>
        /// 태그를 추가합니다 (카운트 증가)
        /// </summary>
        bool AddTag(GameplayTag tag);
        
        /// <summary>
        /// 태그가 없을 때만 추가합니다 (카운트가 0일 때만)
        /// </summary>
        bool AddTagUnique(GameplayTag tag);
        
        /// <summary>
        /// 태그를 한 번 제거합니다 (카운트 감소)
        /// </summary>
        bool RemoveTagOnce(GameplayTag tag);
        
        /// <summary>
        /// 태그를 모두 제거합니다 (카운트를 0으로)
        /// </summary>
        bool RemoveTagAll(GameplayTag tag);
        
       
    }
    
    public interface IReadOnlyGameplayTagContainer
    {
        /// <summary>
        /// 명시적 태그 전역 구독 이벤트
        /// </summary>
        event Action<GameplayTagContainerEvent> OnExplicitCountChanged;
    
        /// <summary>
        /// 묵시적 태그 전역 구독 이벤트
        /// </summary>
        event Action<GameplayTagContainerEvent> OnImplicitCountChanged;
        
        /// <summary>
        /// 태그가 존재하는지 확인합니다 (자식 태그 포함, 카운트 > 0)
        /// </summary>
        bool HasTag(GameplayTag tag);
        
        /// <summary>
        /// 정확히 일치하는 태그가 존재하는지 확인합니다 (자식 태그 제외)
        /// </summary>
        bool HasTagExact(GameplayTag tag);
        
        /// <summary>
        /// 태그의 개수를 반환합니다 (자식 태그 포함)
        /// </summary>
        int CountTag(GameplayTag tag);
        
        /// <summary>
        /// 정확히 일치하는 태그의 개수를 반환합니다 (자식 태그 제외)
        /// </summary>
        int CountTagExact(GameplayTag tag);
        
        /// <summary>
        /// 다른 컨테이너의 태그 중 하나라도 가지고 있는지 확인합니다 (자식 태그 포함)
        /// </summary>
        bool HasAnyTag(IReadOnlyGameplayTagContainer otherContainer);
        
        /// <summary>
        /// 다른 컨테이너의 태그 중 하나라도 정확히 일치하는지 확인합니다 (자식 태그 제외)
        /// </summary>
        bool HasAnyTagExact(IReadOnlyGameplayTagContainer otherContainer);
        
        /// <summary>
        /// 다른 컨테이너의 모든 태그를 가지고 있는지 확인합니다 (자식 태그 포함)
        /// </summary>
        bool HasAllTags(IReadOnlyGameplayTagContainer otherContainer);
        
        /// <summary>
        /// 다른 컨테이너의 모든 태그가 정확히 일치하는지 확인합니다 (자식 태그 제외)
        /// </summary>
        bool HasAllTagsExact(IReadOnlyGameplayTagContainer otherContainer);
        
        /// <summary>
        /// 모든 태그를 제거합니다
        /// </summary>
        void ClearTags();
        
        /// <summary>
        /// 모든 태그 목록을 가져옵니다 (카운트 > 0인 태그만)
        /// </summary>
        List<GameplayTag> GetAllTags();
        
        /// <summary>
        /// 고유 태그 개수 (카운트 > 0인 태그 개수)
        /// </summary>
        int UniqueTagCount { get; }
        
        /// <summary>
        /// 총 태그 개수 (모든 카운트 합산)
        /// </summary>
        int TotalTagCount { get; }
    }

    /// <summary>
    /// 게임플레이 태그 컨테이너 (카운터 기반)
    /// implicit tag count를 별도로 관리하여 HasTag/CountTag에서 빠르게 조회할 수 있도록 최적화되어 있습니다. <br/>
    /// implicit은 "자기자신을 포함"한 모든 부모 태그를 의미합니다. 따라서 태그 계층 구조에서 상위 태그에 대한 조회가 빠르게 이루어집니다. <br/>
    /// explicit은 "자기자신만"을 의미하며, AddTag/RemoveTag에서 명시적으로 추가/제거된 태그에 대한 카운트를 관리합니다. <br/>
    /// 태그가 추가/제거될 때마다 관련된 implicit 태그들의 카운트도 함께 업데이트되며, 이벤트가 발생합니다. <br/>
    /// </summary>
    [Serializable]
    public class GameplayTagContainer : IGameplayTagContainer, ISerializationCallbackReceiver, IReadOnlyGameplayTagContainer
    {
        static ProfilerMarker Marker_AddTag = new ProfilerMarker("GameplayTagContainer.AddTag");
        static ProfilerMarker Marker_RemoveTag = new ProfilerMarker("GameplayTagContainer.RemoveTag");
        static ProfilerMarker Marker_HasTag = new ProfilerMarker("GameplayTagContainer.HasTag");
        static ProfilerMarker Marker_CountTag = new ProfilerMarker("GameplayTagContainer.CountTag");
        
        
        
        [SerializeField] private List<string> serializedTags = new();

        [SerializeField] private List<int> serializedCounts = new List<int>();

        private Dictionary<GameplayTag, int> _tagCounts = new Dictionary<GameplayTag, int>();
        private Dictionary<GameplayTag, int> _implicitTagCounts = new Dictionary<GameplayTag, int>();
        private bool _needsResolve;
        
        
        private Dictionary<GameplayTag, Action<GameplayTagContainerEvent>> _implicitTagEventHandlers = new();
        private Dictionary<GameplayTag, Action<GameplayTagContainerEvent>> _explicitTagEventHandlers = new();
        
        /// <summary>
        /// 명시적 태그 전역 구독 이벤트입니다. <seealso cref="RegisterExplicitTagEvent"/> 를 통해 특정 태그에 대한 구독도 가능합니다.
        /// </summary>
        public event Action<GameplayTagContainerEvent> OnExplicitCountChanged;
        
        /// <summary>
        /// 묵시적 태그 전역 구독 이벤트입니다. <seealso cref="RegisterImplicitTagEvent"/> 를 통해 특정 태그에 대한 구독도 가능합니다.
        /// </summary>
        public event Action<GameplayTagContainerEvent> OnImplicitCountChanged;

        /// <summary>
        /// 특정 태그의 묵시적(Implicit) 변화를 구독합니다.
        /// </summary>
        public void RegisterImplicitTagEvent(GameplayTag tag, Action<GameplayTagContainerEvent> callback)
        {
            if (!_implicitTagEventHandlers.ContainsKey(tag))
                _implicitTagEventHandlers[tag] = null;
        
            _implicitTagEventHandlers[tag] += callback;
        }

        public void UnregisterImplicitTagEvent(GameplayTag tag, Action<GameplayTagContainerEvent> callback)
        {
            if (_implicitTagEventHandlers.ContainsKey(tag))
                _implicitTagEventHandlers[tag] -= callback;
        }
        
        /// <summary>
        /// 특정 태그의 명시적(Explicit) 변화를 구독합니다.
        /// </summary>
        public void RegisterExplicitTagEvent(GameplayTag tag, Action<GameplayTagContainerEvent> callback)
        {
            if (!_explicitTagEventHandlers.ContainsKey(tag))
                _explicitTagEventHandlers[tag] = null;
            _explicitTagEventHandlers[tag] += callback;
        } 
        
        public void UnregisterExplicitTagEvent(GameplayTag tag, Action<GameplayTagContainerEvent> callback)
        {
            if (_explicitTagEventHandlers.ContainsKey(tag))
                _explicitTagEventHandlers[tag] -= callback;
        }
        
        Queue<GameplayTagContainerEvent> eventQueue = new Queue<GameplayTagContainerEvent>();


        /// <summary>
        /// 직렬화된 데이터를 딕셔너리로 변환합니다.
        /// </summary>
        public void OnAfterDeserialize()
        {
            _tagCounts.Clear();
            _implicitTagCounts.Clear();
            _needsResolve = true;

            TryResolveIfNeeded();
        }

        private void TryResolveIfNeeded()
        {
            if (!_needsResolve || !GameplayTagManager.Initialized)
            {
                return;
            }

            int count = Math.Min(serializedTags.Count, serializedCounts.Count);
            for (int i = 0; i < count; i++)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(serializedTags[i]);
                int tagCount = serializedCounts[i];
                if (tag is { IsValid: true, IsNone: false } && tagCount > 0)
                {
                    if (_tagCounts.TryGetValue(tag, out int existingCount))
                    {
                        _tagCounts[tag] = existingCount + tagCount;
                    }
                    else
                    {
                        _tagCounts[tag] = tagCount;
                    }

                    AddImplicitTagCounts(tag, tagCount);
                }
            }

            ClearEvents();
            _needsResolve = false;
        }

        private bool IsReadyForResolvedAccess()
        {
            TryResolveIfNeeded();
            return !_needsResolve;
        }

        /// <summary>
        /// 딕셔너리를 직렬화 가능한 데이터로 변환합니다.
        /// </summary>
        public void OnBeforeSerialize()
        {
            TryResolveIfNeeded();
            if (_needsResolve)
            {
                return;
            }

            Dictionary<string, int> preservedInvalidTags = CollectInvalidSerializedTags(serializedTags, serializedCounts);
            serializedTags.Clear();
            serializedCounts.Clear();

            // 태그 이름으로 정렬하여 직렬화
            var sortedTags = new List<KeyValuePair<GameplayTag, int>>();
            foreach (var kvp in _tagCounts)
            {
                if (kvp.Value > 0)
                {
                    sortedTags.Add(kvp);
                }
            }

            sortedTags.Sort((a, b) => string.Compare(a.Key.TagName, b.Key.TagName, StringComparison.Ordinal));

            foreach (var kvp in sortedTags)
            {
                serializedTags.Add(kvp.Key.TagName);
                serializedCounts.Add(kvp.Value);
            }

            foreach (KeyValuePair<string, int> invalidTag in new SortedDictionary<string, int>(preservedInvalidTags, StringComparer.Ordinal))
            {
                serializedTags.Add(invalidTag.Key);
                serializedCounts.Add(invalidTag.Value);
            }
        }

        public bool AddTag(GameplayTag tag)
        {
            if (!IsReadyForResolvedAccess())
            {
                return false;
            }

            if (!tag.IsValid || tag.IsNone)
                return false;

            int previousCount;
            if (_tagCounts.ContainsKey(tag))
            {
                previousCount = _tagCounts[tag];
                _tagCounts[tag]++;
            }
            else
            {
                previousCount = 0;
                _tagCounts[tag] = 1;
            }
            
            EnqueueEvent(new GameplayTagContainerEvent
            {
                Type = GameplayTagContainerEvent.EventType.Added,
                TagGroup = GameplayTagContainerEvent.TagType.Explicit,
                Tag = tag,
                PreviousCount = previousCount,
                NewCount = previousCount + 1
            });
            
            AddImplicitTagCounts(tag, 1);
            BroadcastEvents();
            return true;
        }

        public bool AddTagUnique(GameplayTag tag)
        {
            if (!IsReadyForResolvedAccess())
            {
                return false;
            }

            if (!tag.IsValid || tag.IsNone)
                return false;

            if (_tagCounts.ContainsKey(tag) && _tagCounts[tag] > 0)
                return false;
            
            int previousCount = _tagCounts.ContainsKey(tag) ? _tagCounts[tag] : 0;
            _tagCounts[tag] = 1;
            EnqueueEvent(new GameplayTagContainerEvent
            {
                Type = GameplayTagContainerEvent.EventType.Added,
                TagGroup = GameplayTagContainerEvent.TagType.Explicit,
                Tag = tag,
                PreviousCount = previousCount,
                NewCount = 1
            });
            AddImplicitTagCounts(tag, 1);
            BroadcastEvents();
            return true;
        }

        public bool RemoveTagOnce(GameplayTag tag)
        {
            if (!IsReadyForResolvedAccess())
            {
                return false;
            }

            if (!tag.IsValid || !_tagCounts.ContainsKey(tag))
                return false;

            var count = _tagCounts[tag];
            
            if (count <= 0)
            {
                return false;
            }
            int newCount = count - 1;
            if (newCount <= 0)
            {
                _tagCounts.Remove(tag);
            }
            else
            {
                _tagCounts[tag] = newCount;
            }
            
            EnqueueEvent(new GameplayTagContainerEvent
            {
                Type = GameplayTagContainerEvent.EventType.Removed,
                TagGroup = GameplayTagContainerEvent.TagType.Explicit,
                Tag = tag,
                PreviousCount = count,
                NewCount = count - 1
            });
            
            RemoveImplicitTagCounts(tag, 1);

            
            BroadcastEvents();
            
            return true;
        }

        public bool RemoveTagAll(GameplayTag tag)
        {
            if (!IsReadyForResolvedAccess())
            {
                return false;
            }

            if (!tag.IsValid)
                return false;

            int countToRemove = _tagCounts.ContainsKey(tag) ? _tagCounts[tag] : 0;
            if (countToRemove <= 0)
            {
                return false;
            }

            bool removed = _tagCounts.Remove(tag);
            if (removed)
            {
                EnqueueEvent(new GameplayTagContainerEvent
                {
                    Type = GameplayTagContainerEvent.EventType.Removed,
                    TagGroup = GameplayTagContainerEvent.TagType.Explicit,
                    Tag = tag,
                    PreviousCount = countToRemove,
                    NewCount = 0
                });
                RemoveImplicitTagCounts(tag, countToRemove);
            }
            BroadcastEvents();
            return removed;
        }

        public bool HasTag(GameplayTag tag)
        {
            if (!IsReadyForResolvedAccess())
            {
                return false;
            }

            return CountTag(tag) > 0;
        }

        public bool HasTagExact(GameplayTag tag)
        {
            if (!IsReadyForResolvedAccess())
            {
                return false;
            }

            return _tagCounts.ContainsKey(tag) && _tagCounts[tag] > 0;
        }

        public int CountTag(GameplayTag tag)
        {
            if (!IsReadyForResolvedAccess())
            {
                return 0;
            }

            return _implicitTagCounts.TryGetValue(tag, out int count) ? count : 0;
        }

        public int CountTagExact(GameplayTag tag)
        {
            if (!IsReadyForResolvedAccess())
            {
                return 0;
            }

            return _tagCounts.ContainsKey(tag) ? _tagCounts[tag] : 0;
        }

        public bool HasAnyTag(IReadOnlyGameplayTagContainer otherContainer)
        {
            if (otherContainer == null)
                return false;

            foreach (var tag in otherContainer.GetAllTags())
            {
                if (HasTag(tag))
                    return true;
            }

            return false;
        }

        public bool HasAnyTagExact(IReadOnlyGameplayTagContainer otherContainer)
        {
            if (otherContainer == null)
                return false;

            foreach (var tag in otherContainer.GetAllTags())
            {
                if (HasTagExact(tag))
                    return true;
            }

            return false;
        }

        public bool HasAllTags(IReadOnlyGameplayTagContainer otherContainer)
        {
            if (otherContainer == null)
                return false;

            foreach (var tag in otherContainer.GetAllTags())
            {
                if (!HasTag(tag))
                    return false;
            }

            return true;
        }

        public bool HasAllTagsExact(IReadOnlyGameplayTagContainer otherContainer)
        {
            if (otherContainer == null)
                return false;

            foreach (var tag in otherContainer.GetAllTags())
            {
                if (!HasTagExact(tag))
                    return false;
            }

            return true;
        }

        public void ClearTags()
        {
            var handle = ListPool<GameplayTag>.Get(out var tagsToRemove);
            foreach (var kvp in _tagCounts)
            {
                if (kvp.Value > 0)
                {
                    tagsToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var tag in tagsToRemove)
            {
                RemoveTagAll(tag); 
            }
            
            _tagCounts.Clear();
            _implicitTagCounts.Clear();
            
            _needsResolve = false;
        }

        public List<GameplayTag> GetAllTags()
        {
            if (!IsReadyForResolvedAccess())
            {
                return new List<GameplayTag>();
            }

            return new List<GameplayTag>(_tagCounts.Keys);
        }

        public int UniqueTagCount
        {
            get
            {
                if (!IsReadyForResolvedAccess())
                {
                    return 0;
                }

                return _tagCounts.Count;
            }
        }

        public int TotalTagCount
        {
            get
            {
                if (!IsReadyForResolvedAccess())
                {
                    return 0;
                }

                int total = 0;
                foreach (var count in _tagCounts.Values)
                {
                    total += count;
                }

                return total;
            }
        }

        private static Dictionary<string, int> CollectInvalidSerializedTags(List<string> tags = null, List<int> counts = null)
        {
            tags ??= new List<string>();
            counts ??= new List<int>();

            var preservedInvalidTags = new Dictionary<string, int>(StringComparer.Ordinal);
            int count = Math.Min(tags.Count, counts.Count);
            for (int i = 0; i < count; i++)
            {
                string tagName = tags[i];
                int tagCount = counts[i];
                if (string.IsNullOrEmpty(tagName) || tagCount <= 0)
                {
                    continue;
                }

                if (GameplayTagManager.IsValidTagName(tagName))
                {
                    continue;
                }

                if (preservedInvalidTags.TryGetValue(tagName, out int existingCount))
                {
                    preservedInvalidTags[tagName] = existingCount + tagCount;
                }
                else
                {
                    preservedInvalidTags[tagName] = tagCount;
                }
            }

            return preservedInvalidTags;
        }

        private void AddImplicitTagCounts(GameplayTag tag, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            GameplayTag current = tag;
            while (current.IsValid && !current.IsNone)
            {
                if (_implicitTagCounts.TryGetValue(current, out int existingCount))
                {
                    _implicitTagCounts[current] = existingCount + amount;
                }
                else
                {
                    _implicitTagCounts[current] = amount;
                }
                
                EnqueueEvent(new GameplayTagContainerEvent
                {
                    Type = GameplayTagContainerEvent.EventType.Added,
                    TagGroup = GameplayTagContainerEvent.TagType.Implicit,
                    Tag = current,
                    PreviousCount = _implicitTagCounts[current] - amount,
                    NewCount = _implicitTagCounts[current]
                });

                current = current.ParentTag;
            }
        }

        private void RemoveImplicitTagCounts(GameplayTag tag, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            GameplayTag current = tag;
            while (current.IsValid && !current.IsNone)
            {
                Debug.Assert(_implicitTagCounts.ContainsKey(current), $"Implicit tag count should exist for {current.TagName}");
                if (!_implicitTagCounts.TryGetValue(current, out int existingCount))
                {
                    current = current.ParentTag;
                    continue;
                }

                int nextCount = existingCount - amount;
                if (nextCount > 0)
                {
                    _implicitTagCounts[current] = nextCount;
                }
                else
                {
                    _implicitTagCounts.Remove(current);
                }
                
                EnqueueEvent(new GameplayTagContainerEvent
                {
                    Type = GameplayTagContainerEvent.EventType.Removed,
                    TagGroup = GameplayTagContainerEvent.TagType.Implicit,
                    Tag = current,
                    PreviousCount = existingCount,
                    NewCount = nextCount
                });

                current = current.ParentTag;
            }
        }
        
        private void BroadcastEvents()
        {
            while (eventQueue.Count > 0)
            {
                GameplayTagContainerEvent evt = eventQueue.Dequeue();
        
                if (evt.TagGroup == GameplayTagContainerEvent.TagType.Explicit)
                {
                    OnExplicitCountChanged?.Invoke(evt);
                    
                    if (_explicitTagEventHandlers.TryGetValue(evt.Tag, out var handler))
                    {
                        handler?.Invoke(evt);
                    }
                }
                else
                {
                    OnImplicitCountChanged?.Invoke(evt);
            
                    if (_implicitTagEventHandlers.TryGetValue(evt.Tag, out var handler))
                    {
                        handler?.Invoke(evt);
                    }
                }
            }
        }
        
        private void EnqueueEvent(GameplayTagContainerEvent evt) => eventQueue.Enqueue(evt);
        
        private void ClearEvents() => eventQueue.Clear();
    }
}

