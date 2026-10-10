#if GAMEPLAYTAGS_USE_NETCODE
using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace Machamy.GameplayTags.Runtime.Netcode
{
    /// <summary>
    /// Netcode for GameObjects를 사용한 네트워크 동기화 가능한 GameplayTag 컨테이너
    /// NetworkVariableBase를 상속받아 NetworkVariable로 직접 사용 가능 (카운터 기반)
    /// NetworkList와 유사한 방식으로 동작
    /// </summary>
    /// <example>
    /// 사용 예제:
    /// <code>
    /// public class MyNetworkBehaviour : NetworkBehaviour
    /// {
    ///     // NetworkVariable처럼 선언
    ///     private NetworkGameplayTagContainer _tags = new NetworkGameplayTagContainer();
    ///     
    ///     void Start()
    ///     {
    ///         // 이벤트 구독
    ///         _tags.OnTagCountChanged += OnTagChanged;
    ///         _tags.OnListChanged += OnListChanged;
    ///     }
    ///     
    ///     [Rpc(SendTo.Server)]
    ///     void AddTagServerRpc(GameplayTag tag)
    ///     {
    ///         _tags.AddTag(tag); // 자동으로 모든 클라이언트에 동기화됨
    ///     }
    ///     
    ///     void OnTagChanged(NetworkGameplayTagContainer.TagCountChangeEvent evt)
    ///     {
    ///         Debug.Log($"Tag {evt.Tag} changed: {evt.OldCount} -> {evt.NewCount}");
    ///     }
    ///     
    ///     void OnListChanged(NetworkGameplayTagContainer.ListEvent evt)
    ///     {
    ///         Debug.Log($"List event: {evt.Type}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public class NetworkGameplayTagContainer : NetworkVariableBase, IGameplayTagContainer, IReadOnlyGameplayTagContainer
    {
        /// <summary>
        /// 태그 카운트 엔트리 구조체
        /// </summary>
        public struct TagCountEntry : INetworkSerializable, IEquatable<TagCountEntry>
        {
            public GameplayTag Tag;
            public int Count;

            public TagCountEntry(GameplayTag tag, int count)
            {
                Tag = tag;
                Count = count;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                int tagId = Tag.InternalTagID;
                serializer.SerializeValue(ref tagId);
                serializer.SerializeValue(ref Count);
                
                if (serializer.IsReader)
                {
                    // Reader 모드에서는 tagId로부터 GameplayTag 재구성
                    Tag = GameplayTagManager.GetTagByID(tagId);
                }
            }

            public bool Equals(TagCountEntry other)
            {
                return Tag.Equals(other.Tag) && Count == other.Count;
            }

            public override bool Equals(object obj)
            {
                return obj is TagCountEntry other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Tag, Count);
            }
        }

        /// <summary>
        /// 변경 이벤트 타입
        /// </summary>
        public enum EventType : byte
        {
            Add = 0,
            Remove = 1,
            Value = 2,
            Clear = 3,
            Full = 4
        }

        /// <summary>
        /// 리스트 변경 이벤트 구조체
        /// </summary>
        public struct ListEvent
        {
            public EventType Type;
            public int Index;
            public TagCountEntry Value;
        }

        private readonly List<ListEvent> _dirtyEvents = new List<ListEvent>();
        private readonly Dictionary<GameplayTag, int> _cachedTagCounts = new Dictionary<GameplayTag, int>();
        private readonly Dictionary<GameplayTag, int> _implicitTagCounts = new Dictionary<GameplayTag, int>();
        private readonly Dictionary<GameplayTag, Action<GameplayTagContainerEvent>> _implicitTagEventHandlers = new();
        private readonly Dictionary<GameplayTag, Action<GameplayTagContainerEvent>> _explicitTagEventHandlers = new();
        private readonly Queue<GameplayTagContainerEvent> _tagEventQueue = new();

        /// <summary>
        /// 리스트 변경 이벤트 델리게이트
        /// </summary>
        public delegate void OnListChangedDelegate(ListEvent changeEvent);

        /// <summary>
        /// 리스트 변경 이벤트
        /// </summary>
        public event OnListChangedDelegate OnListChanged;

        /// <summary>
        /// 명시적 태그 전역 구독 이벤트입니다.
        /// </summary>
        public event Action<GameplayTagContainerEvent> OnExplicitCountChanged;

        /// <summary>
        /// 묵시적 태그 전역 구독 이벤트입니다.
        /// </summary>
        public event Action<GameplayTagContainerEvent> OnImplicitCountChanged;

        /// <summary>
        /// 태그 개수 변경 이벤트 (하위 호환성)
        /// </summary>
        public event Action<TagCountChangeEvent> OnTagCountChanged;

        /// <summary>
        /// 태그 개수 변경 이벤트 데이터
        /// </summary>
        public struct TagCountChangeEvent
        {
            public GameplayTag Tag;
            public int OldCount;
            public int NewCount;
            public bool WasAdded => OldCount == 0 && NewCount > 0;
            public bool WasRemoved => OldCount > 0 && NewCount == 0;
        }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public NetworkGameplayTagContainer() : this(
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server)
        {
        }

        /// <summary>
        /// 생성자
        /// </summary>
        public NetworkGameplayTagContainer(
            NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server)
            : base(readPerm, writePerm)
        {
        }

        /// <summary>
        /// 엔트리 개수 (명시적 태그 개수)
        /// </summary>
        public int Count => _cachedTagCounts.Count;

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

        private void OnListEventOccurred(ListEvent changeEvent)
        {
            OnListChanged?.Invoke(changeEvent);

            switch (changeEvent.Type)
            {
                case EventType.Add:
                case EventType.Value:
                    ApplyExplicitCount(changeEvent.Value.Tag, changeEvent.Value.Count);
                    BroadcastTagEvents();
                    break;

                case EventType.Remove:
                    ApplyExplicitCount(changeEvent.Value.Tag, 0);
                    BroadcastTagEvents();
                    break;

                case EventType.Clear:
                    ClearAllTagCountsWithEvents();
                    BroadcastTagEvents();
                    break;

                case EventType.Full:
                    RebuildImplicitCacheFromExplicit();
                    break;
            }
        }

        private void RebuildImplicitCacheFromExplicit()
        {
            _implicitTagCounts.Clear();
            foreach (KeyValuePair<GameplayTag, int> kvp in _cachedTagCounts)
            {
                if (kvp.Value > 0)
                {
                    AddImplicitTagCounts(kvp.Key, kvp.Value, false);
                }
            }

            ClearTagEvents();
        }

        private void ApplyExplicitCount(GameplayTag tag, int newCount)
        {
            if (!tag.IsValid || tag.IsNone)
            {
                return;
            }

            int oldCount = _cachedTagCounts.TryGetValue(tag, out int existingCount) ? existingCount : 0;
            if (oldCount == newCount)
            {
                return;
            }

            if (newCount > 0)
            {
                _cachedTagCounts[tag] = newCount;
            }
            else
            {
                _cachedTagCounts.Remove(tag);
                newCount = 0;
            }

            OnTagCountChanged?.Invoke(new TagCountChangeEvent
            {
                Tag = tag,
                OldCount = oldCount,
                NewCount = newCount
            });

            EnqueueTagEvent(new GameplayTagContainerEvent
            {
                Type = newCount > oldCount ? GameplayTagContainerEvent.EventType.Added : GameplayTagContainerEvent.EventType.Removed,
                TagGroup = GameplayTagContainerEvent.TagType.Explicit,
                Tag = tag,
                PreviousCount = oldCount,
                NewCount = newCount
            });

            int delta = newCount - oldCount;
            if (delta > 0)
            {
                AddImplicitTagCounts(tag, delta, true);
            }
            else if (delta < 0)
            {
                RemoveImplicitTagCounts(tag, -delta, true);
            }
        }

        private void ClearAllTagCountsWithEvents()
        {
            if (_cachedTagCounts.Count == 0)
            {
                _implicitTagCounts.Clear();
                return;
            }

            var tagsToClear = new List<KeyValuePair<GameplayTag, int>>(_cachedTagCounts);
            for (int i = 0; i < tagsToClear.Count; i++)
            {
                KeyValuePair<GameplayTag, int> entry = tagsToClear[i];
                ApplyExplicitCount(entry.Key, 0);
            }
        }

        private void AddImplicitTagCounts(GameplayTag tag, int amount, bool enqueueEvent)
        {
            if (amount <= 0)
            {
                return;
            }

            GameplayTag current = tag;
            while (current.IsValid && !current.IsNone)
            {
                int previousCount = _implicitTagCounts.TryGetValue(current, out int existingCount) ? existingCount : 0;
                int newCount = previousCount + amount;
                _implicitTagCounts[current] = newCount;

                if (enqueueEvent)
                {
                    EnqueueTagEvent(new GameplayTagContainerEvent
                    {
                        Type = GameplayTagContainerEvent.EventType.Added,
                        TagGroup = GameplayTagContainerEvent.TagType.Implicit,
                        Tag = current,
                        PreviousCount = previousCount,
                        NewCount = newCount
                    });
                }

                current = current.ParentTag;
            }
        }

        private void RemoveImplicitTagCounts(GameplayTag tag, int amount, bool enqueueEvent)
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

                if (enqueueEvent)
                {
                    EnqueueTagEvent(new GameplayTagContainerEvent
                    {
                        Type = GameplayTagContainerEvent.EventType.Removed,
                        TagGroup = GameplayTagContainerEvent.TagType.Implicit,
                        Tag = current,
                        PreviousCount = previousCount,
                        NewCount = newCount
                    });
                }

                current = current.ParentTag;
            }
        }

        private void EnqueueTagEvent(GameplayTagContainerEvent tagEvent)
        {
            _tagEventQueue.Enqueue(tagEvent);
        }

        private void BroadcastTagEvents()
        {
            while (_tagEventQueue.Count > 0)
            {
                GameplayTagContainerEvent tagEvent = _tagEventQueue.Dequeue();
                if (tagEvent.TagGroup == GameplayTagContainerEvent.TagType.Explicit)
                {
                    OnExplicitCountChanged?.Invoke(tagEvent);
                    if (_explicitTagEventHandlers.TryGetValue(tagEvent.Tag, out Action<GameplayTagContainerEvent> explicitHandler))
                    {
                        explicitHandler?.Invoke(tagEvent);
                    }

                    continue;
                }

                OnImplicitCountChanged?.Invoke(tagEvent);
                if (_implicitTagEventHandlers.TryGetValue(tagEvent.Tag, out Action<GameplayTagContainerEvent> implicitHandler))
                {
                    implicitHandler?.Invoke(tagEvent);
                }
            }
        }

        private void ClearTagEvents()
        {
            _tagEventQueue.Clear();
        }

        #region IGameplayTagContainer 구현

        public bool AddTag(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            int currentCount = CountTagExact(tag);
            int newCount = currentCount + 1;
            EventType eventType = currentCount == 0 ? EventType.Add : EventType.Value;

            var changeEvent = new ListEvent
            {
                Type = eventType,
                Index = -1,
                Value = new TagCountEntry(tag, newCount)
            };

            _dirtyEvents.Add(changeEvent);
            OnListEventOccurred(changeEvent);
            MarkNetworkBehaviourDirty();
            return true;
        }

        public bool AddTagUnique(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            if (HasTagExact(tag))
            {
                return false;
            }

            var changeEvent = new ListEvent
            {
                Type = EventType.Add,
                Index = -1,
                Value = new TagCountEntry(tag, 1)
            };

            _dirtyEvents.Add(changeEvent);
            OnListEventOccurred(changeEvent);
            MarkNetworkBehaviourDirty();
            return true;
        }

        public bool RemoveTagOnce(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            int currentCount = CountTagExact(tag);
            if (currentCount <= 0)
            {
                return false;
            }

            int newCount = currentCount - 1;
            var changeEvent = new ListEvent
            {
                Type = newCount > 0 ? EventType.Value : EventType.Remove,
                Index = -1,
                Value = new TagCountEntry(tag, newCount > 0 ? newCount : currentCount)
            };

            _dirtyEvents.Add(changeEvent);
            OnListEventOccurred(changeEvent);
            MarkNetworkBehaviourDirty();
            return true;
        }

        public bool RemoveTagAll(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            int currentCount = CountTagExact(tag);
            if (currentCount <= 0)
            {
                return false;
            }

            var changeEvent = new ListEvent
            {
                Type = EventType.Remove,
                Index = -1,
                Value = new TagCountEntry(tag, currentCount)
            };

            _dirtyEvents.Add(changeEvent);
            OnListEventOccurred(changeEvent);
            MarkNetworkBehaviourDirty();
            return true;
        }

        public bool HasTag(GameplayTag tag)
        {
            return CountTag(tag) > 0;
        }
        
        public bool HasTagExact(GameplayTag tag)
        {
            return _cachedTagCounts.TryGetValue(tag, out int count) && count > 0;
        }

        public int CountTag(GameplayTag tag)
        {
            return _implicitTagCounts.TryGetValue(tag, out int count) ? count : 0;
        }
        
        public int CountTagExact(GameplayTag tag)
        {
            return _cachedTagCounts.TryGetValue(tag, out int count) ? count : 0;
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
            if (_cachedTagCounts.Count <= 0)
            {
                return;
            }

            var changeEvent = new ListEvent
            {
                Type = EventType.Clear,
                Index = -1,
                Value = default
            };

            _dirtyEvents.Add(changeEvent);
            OnListEventOccurred(changeEvent);
            MarkNetworkBehaviourDirty();
        }

        public List<GameplayTag> GetAllTags()
        {
            return new List<GameplayTag>(_cachedTagCounts.Keys);
        }

        public int UniqueTagCount => _cachedTagCounts.Count;

        public int TotalTagCount
        {
            get
            {
                int total = 0;
                foreach (var count in _cachedTagCounts.Values)
                {
                    total += count;
                }
                return total;
            }
        }

        #endregion

        public override void Dispose()
        {
            _dirtyEvents.Clear();
            _cachedTagCounts.Clear();
            _implicitTagCounts.Clear();
            _implicitTagEventHandlers.Clear();
            _explicitTagEventHandlers.Clear();
            ClearTagEvents();
            base.Dispose();
        }

        #region NetworkVariableBase 구현

        public override void WriteDelta(FastBufferWriter writer)
        {
            // base.IsDirty()가 true면 Full 동기화
            if (base.IsDirty())
            {
                writer.WriteValueSafe((ushort)1);
                writer.WriteValueSafe(EventType.Full);
                WriteField(writer);
                return;
            }

            // Delta 이벤트 전송
            writer.WriteValueSafe((ushort)_dirtyEvents.Count);
            foreach (ListEvent evt in _dirtyEvents)
            {
                writer.WriteValueSafe(evt.Type);
                switch (evt.Type)
                {
                    case EventType.Add:
                    case EventType.Remove:
                    case EventType.Value:
                        SerializeTagCountEntry(writer, evt.Value);
                        break;
                    case EventType.Clear:
                        // Clear는 추가 데이터 없음
                        break;
                }
            }
        }

        public override void WriteField(FastBufferWriter writer)
        {
            writer.WriteValueSafe((ushort)_cachedTagCounts.Count);
            foreach (KeyValuePair<GameplayTag, int> kvp in _cachedTagCounts)
            {
                SerializeTagCountEntry(writer, new TagCountEntry(kvp.Key, kvp.Value));
            }
        }

        public override void ReadField(FastBufferReader reader)
        {
            _cachedTagCounts.Clear();
            _implicitTagCounts.Clear();
            ClearTagEvents();

            reader.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++)
            {
                TagCountEntry entry = DeserializeTagCountEntry(reader);
                if (!entry.Tag.IsValid || entry.Tag.IsNone || entry.Count <= 0)
                {
                    continue;
                }

                if (_cachedTagCounts.TryGetValue(entry.Tag, out int existingCount))
                {
                    _cachedTagCounts[entry.Tag] = existingCount + entry.Count;
                }
                else
                {
                    _cachedTagCounts[entry.Tag] = entry.Count;
                }
            }

            RebuildImplicitCacheFromExplicit();
        }

        public override void ReadDelta(FastBufferReader reader, bool keepDirtyDelta)
        {
            // keepDirtyDelta는 서버에서만 true로 설정됨
            bool isServer = keepDirtyDelta;
            reader.ReadValueSafe(out ushort deltaCount);

            for (int i = 0; i < deltaCount; i++)
            {
                reader.ReadValueSafe(out EventType eventType);

                switch (eventType)
                {
                    case EventType.Add:
                    case EventType.Remove:
                    case EventType.Value:
                    {
                        TagCountEntry value = DeserializeTagCountEntry(reader);
                        var changeEvent = new ListEvent
                        {
                            Type = eventType,
                            Index = -1,
                            Value = value
                        };

                        OnListEventOccurred(changeEvent);

                        if (isServer)
                        {
                            _dirtyEvents.Add(changeEvent);
                        }

                        break;
                    }

                    case EventType.Clear:
                    {
                        var changeEvent = new ListEvent
                        {
                            Type = eventType,
                            Index = -1,
                            Value = default
                        };

                        OnListEventOccurred(changeEvent);

                        if (isServer)
                        {
                            _dirtyEvents.Add(changeEvent);
                        }

                        break;
                    }

                    case EventType.Full:
                    {
                        ReadField(reader);

                        var changeEvent = new ListEvent
                        {
                            Type = eventType,
                            Index = -1,
                            Value = default
                        };

                        OnListEventOccurred(changeEvent);
                        break;
                    }
                }
            }

            if (isServer)
            {
                MarkNetworkBehaviourDirty();
            }
        }

        public override bool IsDirty()
        {
            return base.IsDirty() || _dirtyEvents.Count > 0;
        }

        public override void ResetDirty()
        {
            base.ResetDirty();
            if (_dirtyEvents.Count > 0)
            {
                _dirtyEvents.Clear();
            }
        }

        private void SerializeTagCountEntry(FastBufferWriter writer, TagCountEntry entry)
        {
            writer.WriteValueSafe(entry.Tag.InternalTagID);
            writer.WriteValueSafe(entry.Count);
        }

        private TagCountEntry DeserializeTagCountEntry(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int tagId);
            reader.ReadValueSafe(out int count);
            var tag = GameplayTagManager.GetTagByID(tagId);
            return new TagCountEntry(tag, count);
        }

        #endregion
    }
}

#endif

