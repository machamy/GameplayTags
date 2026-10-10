using System;
using System.Collections.Generic;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 게임플레이 태그 집합 (존재 여부만 관리)
    /// </summary>
    [Serializable]
    public class GameplayTagSet : IGameplayTagContainer, ISerializationCallbackReceiver, IReadOnlyGameplayTagContainer
    {
        [SerializeField] private List<string> serializedTags = new();

        private readonly Dictionary<string, GameplayTag> _tagsByName = new(StringComparer.Ordinal);
        private readonly Dictionary<GameplayTag, int> _implicitTagCounts = new();
        private readonly Dictionary<GameplayTag, Action<GameplayTagContainerEvent>> _implicitTagEventHandlers = new();
        private readonly Dictionary<GameplayTag, Action<GameplayTagContainerEvent>> _explicitTagEventHandlers = new();
        private readonly Queue<GameplayTagContainerEvent> _eventQueue = new();

        private bool _suppressEvents;

        /// <summary>
        /// 명시적 태그 전역 구독 이벤트입니다.
        /// </summary>
        public event Action<GameplayTagContainerEvent> OnExplicitCountChanged;

        /// <summary>
        /// 묵시적 태그 전역 구독 이벤트입니다.
        /// </summary>
        public event Action<GameplayTagContainerEvent> OnImplicitCountChanged;

        /// <summary>
        /// 특정 태그의 묵시적(Implicit) 변화를 구독합니다.
        /// </summary>
        public void RegisterImplicitTagEvent(GameplayTag tag, Action<GameplayTagContainerEvent> callback)
        {
            if (!_implicitTagEventHandlers.ContainsKey(tag))
            {
                _implicitTagEventHandlers[tag] = null;
            }

            _implicitTagEventHandlers[tag] += callback;
        }

        public void UnregisterImplicitTagEvent(GameplayTag tag, Action<GameplayTagContainerEvent> callback)
        {
            if (!_implicitTagEventHandlers.TryGetValue(tag, out Action<GameplayTagContainerEvent> handler))
            {
                return;
            }

            handler -= callback;
            if (handler == null)
            {
                _implicitTagEventHandlers.Remove(tag);
                return;
            }

            _implicitTagEventHandlers[tag] = handler;
        }

        /// <summary>
        /// 특정 태그의 명시적(Explicit) 변화를 구독합니다.
        /// </summary>
        public void RegisterExplicitTagEvent(GameplayTag tag, Action<GameplayTagContainerEvent> callback)
        {
            if (!_explicitTagEventHandlers.ContainsKey(tag))
            {
                _explicitTagEventHandlers[tag] = null;
            }

            _explicitTagEventHandlers[tag] += callback;
        }

        public void UnregisterExplicitTagEvent(GameplayTag tag, Action<GameplayTagContainerEvent> callback)
        {
            if (!_explicitTagEventHandlers.TryGetValue(tag, out Action<GameplayTagContainerEvent> handler))
            {
                return;
            }

            handler -= callback;
            if (handler == null)
            {
                _explicitTagEventHandlers.Remove(tag);
                return;
            }

            _explicitTagEventHandlers[tag] = handler;
        }

        public void OnAfterDeserialize()
        {
            _tagsByName.Clear();
            _implicitTagCounts.Clear();
            _suppressEvents = true;
            EnsureDeserialized();
            _suppressEvents = false;
            ClearEvents();
        }

        public void OnBeforeSerialize()
        {
            List<string> preservedInvalidTags = CollectInvalidSerializedTags(serializedTags);
            serializedTags.Clear();

            var sortedTags = new List<KeyValuePair<string, GameplayTag>>(_tagsByName);
            sortedTags.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));

            foreach (KeyValuePair<string, GameplayTag> entry in sortedTags)
            {
                GameplayTag tag = entry.Value;
                if (tag.IsValid && !tag.IsNone)
                {
                    serializedTags.Add(entry.Key);
                }
            }

            preservedInvalidTags.Sort(StringComparer.Ordinal);
            serializedTags.AddRange(preservedInvalidTags);
        }

        public bool AddTag(GameplayTag tag)
        {
            if (!tag.IsValid || tag.IsNone)
            {
                return false;
            }

            if (_tagsByName.ContainsKey(tag.TagName))
            {
                return false;
            }

            _tagsByName[tag.TagName] = tag;
            EnqueueEvent(new GameplayTagContainerEvent
            {
                Type = GameplayTagContainerEvent.EventType.Added,
                TagGroup = GameplayTagContainerEvent.TagType.Explicit,
                Tag = tag,
                PreviousCount = 0,
                NewCount = 1
            });

            AddImplicitTagCounts(tag, 1);
            BroadcastEvents();
            return true;
        }

        public bool AddTagUnique(GameplayTag tag)
        {
            return AddTag(tag);
        }

        public bool RemoveTagOnce(GameplayTag tag)
        {
            return RemoveTagAll(tag);
        }

        public bool RemoveTag(GameplayTag tag)
        {
            return RemoveTagAll(tag);
        }

        public bool RemoveTagAll(GameplayTag tag)
        {
            if (!tag.IsValid || tag.IsNone)
            {
                return false;
            }

            if (!_tagsByName.Remove(tag.TagName))
            {
                return false;
            }

            EnqueueEvent(new GameplayTagContainerEvent
            {
                Type = GameplayTagContainerEvent.EventType.Removed,
                TagGroup = GameplayTagContainerEvent.TagType.Explicit,
                Tag = tag,
                PreviousCount = 1,
                NewCount = 0
            });

            RemoveImplicitTagCounts(tag, 1);
            BroadcastEvents();
            return true;
        }

        public bool HasTag(GameplayTag tag)
        {
            return CountTag(tag) > 0;
        }

        public bool HasTagExact(GameplayTag tag)
        {
            return tag.IsValid && !tag.IsNone && _tagsByName.ContainsKey(tag.TagName);
        }

        public int CountTag(GameplayTag tag)
        {
            if (!tag.IsValid || tag.IsNone)
            {
                return 0;
            }

            return _implicitTagCounts.TryGetValue(tag, out int count) ? count : 0;
        }

        public int CountTagExact(GameplayTag tag)
        {
            return HasTagExact(tag) ? 1 : 0;
        }

        public bool HasAnyTag(IReadOnlyGameplayTagContainer otherContainer)
        {
            if (otherContainer == null)
            {
                return false;
            }

            foreach (GameplayTag tag in otherContainer.GetAllTags())
            {
                if (HasTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAnyTagExact(IReadOnlyGameplayTagContainer otherContainer)
        {
            if (otherContainer == null)
            {
                return false;
            }

            foreach (GameplayTag tag in otherContainer.GetAllTags())
            {
                if (HasTagExact(tag))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAllTags(IReadOnlyGameplayTagContainer otherContainer)
        {
            if (otherContainer == null)
            {
                return false;
            }

            foreach (GameplayTag tag in otherContainer.GetAllTags())
            {
                if (!HasTag(tag))
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasAllTagsExact(IReadOnlyGameplayTagContainer otherContainer)
        {
            if (otherContainer == null)
            {
                return false;
            }

            foreach (GameplayTag tag in otherContainer.GetAllTags())
            {
                if (!HasTagExact(tag))
                {
                    return false;
                }
            }

            return true;
        }

        public void ClearTags()
        {
            if (_tagsByName.Count == 0)
            {
                return;
            }

            List<GameplayTag> tagsToRemove = new(_tagsByName.Values);
            for (int i = 0; i < tagsToRemove.Count; i++)
            {
                RemoveTagAll(tagsToRemove[i]);
            }

            _tagsByName.Clear();
            _implicitTagCounts.Clear();
            ClearEvents();
        }

        public List<GameplayTag> GetAllTags()
        {
            return new List<GameplayTag>(_tagsByName.Values);
        }

        public int UniqueTagCount => _tagsByName.Count;

        public int TotalTagCount => _tagsByName.Count;

        private void EnsureDeserialized()
        {
            for (int i = 0; i < serializedTags.Count; i++)
            {
                string tagName = serializedTags[i];
                GameplayTag tag = GameplayTagManager.Initialized
                    ? GameplayTagManager.RequestTag(tagName)
                    : GameplayTagManager.CreateUnresolvedTag(tagName);

                if (!tag.IsValid || tag.IsNone || _tagsByName.ContainsKey(tag.TagName))
                {
                    continue;
                }

                _tagsByName[tag.TagName] = tag;
                AddImplicitTagCounts(tag, 1);
            }
        }

        private static List<string> CollectInvalidSerializedTags(List<string> tags)
        {
            tags ??= new List<string>();

            var preservedInvalidTags = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < tags.Count; i++)
            {
                string tagName = tags[i];
                if (string.IsNullOrEmpty(tagName))
                {
                    continue;
                }

                if (GameplayTagManager.IsValidTagName(tagName))
                {
                    continue;
                }

                preservedInvalidTags.Add(tagName);
            }

            return new List<string>(preservedInvalidTags);
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
                _implicitTagCounts.TryGetValue(current, out int previousCount);
                int newCount = previousCount + amount;
                _implicitTagCounts[current] = newCount;

                EnqueueEvent(new GameplayTagContainerEvent
                {
                    Type = GameplayTagContainerEvent.EventType.Added,
                    TagGroup = GameplayTagContainerEvent.TagType.Implicit,
                    Tag = current,
                    PreviousCount = previousCount,
                    NewCount = newCount
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
                if (!_implicitTagCounts.TryGetValue(current, out int previousCount))
                {
                    current = current.ParentTag;
                    continue;
                }

                int newCount = previousCount - amount;
                if (newCount > 0)
                {
                    _implicitTagCounts[current] = newCount;
                }
                else
                {
                    _implicitTagCounts.Remove(current);
                    newCount = 0;
                }

                EnqueueEvent(new GameplayTagContainerEvent
                {
                    Type = GameplayTagContainerEvent.EventType.Removed,
                    TagGroup = GameplayTagContainerEvent.TagType.Implicit,
                    Tag = current,
                    PreviousCount = previousCount,
                    NewCount = newCount
                });

                current = current.ParentTag;
            }
        }

        private void BroadcastEvents()
        {
            if (_suppressEvents)
            {
                return;
            }

            while (_eventQueue.Count > 0)
            {
                GameplayTagContainerEvent ev = _eventQueue.Dequeue();
                if (ev.TagGroup == GameplayTagContainerEvent.TagType.Explicit)
                {
                    OnExplicitCountChanged?.Invoke(ev);
                    if (_explicitTagEventHandlers.TryGetValue(ev.Tag, out Action<GameplayTagContainerEvent> explicitHandler))
                    {
                        explicitHandler?.Invoke(ev);
                    }

                    continue;
                }

                OnImplicitCountChanged?.Invoke(ev);
                if (_implicitTagEventHandlers.TryGetValue(ev.Tag, out Action<GameplayTagContainerEvent> implicitHandler))
                {
                    implicitHandler?.Invoke(ev);
                }
            }
        }

        private void EnqueueEvent(GameplayTagContainerEvent ev)
        {
            if (_suppressEvents)
            {
                return;
            }

            _eventQueue.Enqueue(ev);
        }

        private void ClearEvents()
        {
            _eventQueue.Clear();
        }
    }
}
