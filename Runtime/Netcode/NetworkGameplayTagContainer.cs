

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
    public class NetworkGameplayTagContainer : NetworkVariableBase, IGameplayTagContainer
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
                int tagId = Tag.RawTagId;
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

        private List<TagCountEntry> _list = new List<TagCountEntry>();
        private List<ListEvent> _dirtyEvents = new List<ListEvent>();
        private readonly Dictionary<GameplayTag, int> _cachedTagCounts = new Dictionary<GameplayTag, int>();

        /// <summary>
        /// 리스트 변경 이벤트 델리게이트
        /// </summary>
        public delegate void OnListChangedDelegate(ListEvent changeEvent);

        /// <summary>
        /// 리스트 변경 이벤트
        /// </summary>
        public event OnListChangedDelegate OnListChanged;

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
        /// 리스트 아이템 개수
        /// </summary>
        public int Count => _list.Count;

        private void OnListEventOccurred(ListEvent changeEvent)
        {
            OnListChanged?.Invoke(changeEvent);

            switch (changeEvent.Type)
            {
                case EventType.Add:
                case EventType.Value:
                    {
                        var entry = changeEvent.Value;
                        int oldCount = _cachedTagCounts.ContainsKey(entry.Tag) ? _cachedTagCounts[entry.Tag] : 0;
                        
                        if (entry.Count > 0)
                        {
                            _cachedTagCounts[entry.Tag] = entry.Count;
                        }
                        else
                        {
                            _cachedTagCounts.Remove(entry.Tag);
                        }

                        OnTagCountChanged?.Invoke(new TagCountChangeEvent
                        {
                            Tag = entry.Tag,
                            OldCount = oldCount,
                            NewCount = entry.Count
                        });
                    }
                    break;

                case EventType.Remove:
                    {
                        var entry = changeEvent.Value;
                        int oldCount = _cachedTagCounts.ContainsKey(entry.Tag) ? _cachedTagCounts[entry.Tag] : 0;
                        _cachedTagCounts.Remove(entry.Tag);

                        OnTagCountChanged?.Invoke(new TagCountChangeEvent
                        {
                            Tag = entry.Tag,
                            OldCount = oldCount,
                            NewCount = 0
                        });
                    }
                    break;

                case EventType.Clear:
                    {
                        var oldCounts = new Dictionary<GameplayTag, int>(_cachedTagCounts);
                        _cachedTagCounts.Clear();

                        foreach (var kvp in oldCounts)
                        {
                            OnTagCountChanged?.Invoke(new TagCountChangeEvent
                            {
                                Tag = kvp.Key,
                                OldCount = kvp.Value,
                                NewCount = 0
                            });
                        }
                    }
                    break;

                case EventType.Full:
                    SyncCacheFromList();
                    break;
            }
        }

        private void SyncCacheFromList()
        {
            _cachedTagCounts.Clear();
            foreach (var entry in _list)
            {
                if (entry.Count > 0)
                {
                    _cachedTagCounts[entry.Tag] = entry.Count;
                }
            }
        }

        private int FindEntryIndex(GameplayTag tag)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].Tag.Equals(tag))
                    return i;
            }
            return -1;
        }

        #region IGameplayTagContainer 구현

        public bool AddTag(GameplayTag tag)
        {
            if (!tag.IsValid)
                return false;

            int currentCount = CountTag(tag);
            int newCount = currentCount + 1;
            int index = FindEntryIndex(tag);
            
            var newEntry = new TagCountEntry(tag, newCount);
            if (index >= 0)
            {
                _list[index] = newEntry;
                var changeEvent = new ListEvent
                {
                    Type = EventType.Value,
                    Index = index,
                    Value = newEntry
                };
                _dirtyEvents.Add(changeEvent);
                OnListEventOccurred(changeEvent);
            }
            else
            {
                _list.Add(newEntry);
                var changeEvent = new ListEvent
                {
                    Type = EventType.Add,
                    Index = _list.Count - 1,
                    Value = newEntry
                };
                _dirtyEvents.Add(changeEvent);
                OnListEventOccurred(changeEvent);
            }
            
            MarkNetworkBehaviourDirty();
            return true;
        }

        public bool AddTagUnique(GameplayTag tag)
        {
            if (!tag.IsValid)
                return false;

            if (HasTag(tag))
                return false;

            var newEntry = new TagCountEntry(tag, 1);
            _list.Add(newEntry);
            
            var changeEvent = new ListEvent
            {
                Type = EventType.Add,
                Index = _list.Count - 1,
                Value = newEntry
            };
            _dirtyEvents.Add(changeEvent);
            OnListEventOccurred(changeEvent);
            MarkNetworkBehaviourDirty();
            
            return true;
        }

        public bool RemoveTagOnce(GameplayTag tag)
        {
            if (!tag.IsValid)
                return false;

            int currentCount = CountTag(tag);
            if (currentCount <= 0)
                return false;

            int newCount = currentCount - 1;
            int index = FindEntryIndex(tag);

            if (index < 0)
                return false;

            if (newCount > 0)
            {
                var newEntry = new TagCountEntry(tag, newCount);
                _list[index] = newEntry;
                
                var changeEvent = new ListEvent
                {
                    Type = EventType.Value,
                    Index = index,
                    Value = newEntry
                };
                _dirtyEvents.Add(changeEvent);
                OnListEventOccurred(changeEvent);
            }
            else
            {
                var oldEntry = _list[index];
                _list.RemoveAt(index);
                
                var changeEvent = new ListEvent
                {
                    Type = EventType.Remove,
                    Index = index,
                    Value = oldEntry
                };
                _dirtyEvents.Add(changeEvent);
                OnListEventOccurred(changeEvent);
            }
            
            MarkNetworkBehaviourDirty();
            return true;
        }

        public bool RemoveTagAll(GameplayTag tag)
        {
            if (!tag.IsValid)
                return false;

            int index = FindEntryIndex(tag);
            if (index < 0)
                return false;

            var oldEntry = _list[index];
            _list.RemoveAt(index);
            
            var changeEvent = new ListEvent
            {
                Type = EventType.Remove,
                Index = index,
                Value = oldEntry
            };
            _dirtyEvents.Add(changeEvent);
            OnListEventOccurred(changeEvent);
            MarkNetworkBehaviourDirty();
            
            return true;
        }

        public bool HasTag(GameplayTag tag)
        {
            return _cachedTagCounts.ContainsKey(tag) && _cachedTagCounts[tag] > 0;
        }

        public int CountTag(GameplayTag tag)
        {
            return _cachedTagCounts.ContainsKey(tag) ? _cachedTagCounts[tag] : 0;
        }

        public bool HasTagIncludeChildren(GameplayTag tag)
        {
            if (HasTag(tag))
                return true;

            foreach (var kvp in _cachedTagCounts)
            {
                if (kvp.Value > 0 && kvp.Key.IsChildOf(tag))
                    return true;
            }
            return false;
        }

        public int CountTagIncludeChildren(GameplayTag tag)
        {
            int count = CountTag(tag);

            foreach (var kvp in _cachedTagCounts)
            {
                if (kvp.Key.IsChildOf(tag))
                {
                    count += kvp.Value;
                }
            }
            return count;
        }

        public bool HasAnyTags(IGameplayTagContainer otherContainer)
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

        public bool HasAllTags(IGameplayTagContainer otherContainer)
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

        public void ClearTags()
        {
            if (_list.Count > 0)
            {
                _list.Clear();
                
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
            _list?.Clear();
            _dirtyEvents?.Clear();
            _cachedTagCounts.Clear();
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
            foreach (var evt in _dirtyEvents)
            {
                writer.WriteValueSafe(evt.Type);
                switch (evt.Type)
                {
                    case EventType.Add:
                        SerializeTagCountEntry(writer, evt.Value);
                        break;
                    case EventType.Remove:
                        SerializeTagCountEntry(writer, evt.Value);
                        break;
                    case EventType.Value:
                        writer.WriteValueSafe(evt.Index);
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
            writer.WriteValueSafe((ushort)_list.Count);
            foreach (var entry in _list)
            {
                SerializeTagCountEntry(writer, entry);
            }
        }

        public override void ReadField(FastBufferReader reader)
        {
            _list.Clear();
            reader.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++)
            {
                var entry = DeserializeTagCountEntry(reader);
                _list.Add(entry);
            }
            SyncCacheFromList();
        }

        public override void ReadDelta(FastBufferReader reader, bool keepDirtyDelta)
        {
            // keepDirtyDelta는 서버에서만 true로 설정됨
            var isServer = keepDirtyDelta;
            reader.ReadValueSafe(out ushort deltaCount);
            
            for (int i = 0; i < deltaCount; i++)
            {
                reader.ReadValueSafe(out EventType eventType);
                
                switch (eventType)
                {
                    case EventType.Add:
                        {
                            var value = DeserializeTagCountEntry(reader);
                            _list.Add(value);

                            var changeEvent = new ListEvent
                            {
                                Type = eventType,
                                Index = _list.Count - 1,
                                Value = value
                            };

                            OnListEventOccurred(changeEvent);

                            if (isServer)
                            {
                                _dirtyEvents.Add(changeEvent);
                            }
                        }
                        break;

                    case EventType.Remove:
                        {
                            var value = DeserializeTagCountEntry(reader);
                            int index = FindEntryIndex(value.Tag);
                            if (index >= 0)
                            {
                                _list.RemoveAt(index);

                                var changeEvent = new ListEvent
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = value
                                };

                                OnListEventOccurred(changeEvent);

                                if (isServer)
                                {
                                    _dirtyEvents.Add(changeEvent);
                                }
                            }
                        }
                        break;

                    case EventType.Value:
                        {
                            reader.ReadValueSafe(out int index);
                            var value = DeserializeTagCountEntry(reader);
                            
                            if (index >= 0 && index < _list.Count)
                            {
                                _list[index] = value;

                                var changeEvent = new ListEvent
                                {
                                    Type = eventType,
                                    Index = index,
                                    Value = value
                                };

                                OnListEventOccurred(changeEvent);

                                if (isServer)
                                {
                                    _dirtyEvents.Add(changeEvent);
                                }
                            }
                        }
                        break;

                    case EventType.Clear:
                        {
                            _list.Clear();

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
                        }
                        break;

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
                        }
                        break;
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
            writer.WriteValueSafe(entry.Tag.RawTagId);
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

