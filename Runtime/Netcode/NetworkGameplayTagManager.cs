using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime.Netcode
{
    [DefaultExecutionOrder(-10)]
    public class NetworkGameplayTagManager : NetworkBehaviour
    {
        private readonly NetworkList<NetworkTagRegistration> _tagRegistrations =
            new NetworkList<NetworkTagRegistration>(
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server
            );

        private readonly HashSet<int> _registeredIds = new HashSet<int>();


        private readonly List<(string Name, int Id)> _pendingTags = new List<(string, int)>();

        [Serializable]
        private struct NetworkTagRegistration : INetworkSerializable, IEquatable<NetworkTagRegistration>
        {
            public FixedString128Bytes TagName;
            public int TagId;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref TagName);
                serializer.SerializeValue(ref TagId);
            }

            public bool Equals(NetworkTagRegistration other) => TagName == other.TagName && TagId == other.TagId;
            public override bool Equals(object obj) => obj is NetworkTagRegistration other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(TagName, TagId);
        }
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                SyncExistingDynamicTags();
                GameplayTagManager.OnDynamicTagRegistered += OnServerDynamicTagRegistered;
            }
            else
            {
                // 초기 데이터 처리
                foreach (var registration in _tagRegistrations)
                {
                    TryRegisterOrQueue(registration.TagName.ToString(), registration.TagId);
                }

                _tagRegistrations.OnListChanged += OnTagListChanged;
            }
        }

        private void OnTagListChanged(NetworkListEvent<NetworkTagRegistration> changeEvent)
        {
            if (changeEvent.Type == NetworkListEvent<NetworkTagRegistration>.EventType.Add)
            {
                TryRegisterOrQueue(changeEvent.Value.TagName.ToString(), changeEvent.Value.TagId);
            }
        }

        /// <summary>
        /// 태그 등록을 시도하고, 실패 시(부모 없음 등) 대기열에 넣습니다.
        /// 성공 시 대기열을 다시 확인합니다.
        /// </summary>
        private void TryRegisterOrQueue(string tagName, int tagId)
        {
            if (_registeredIds.Contains(tagId)) return;

            // 이미 로컬 시스템에 존재하는지 확인 (ID 충돌 방지)
            var existingTag = GameplayTagManager.GetTagByID(tagId);
            if (existingTag.IsValid)
            {
                _registeredIds.Add(tagId);
                return;
            }

            // 등록 시도
            bool success = GameplayTagManager.RegisterDynamicTagWithIdStrict(tagName, tagId, false);

            if (success)
            {
                _registeredIds.Add(tagId);
                // 성공했으므로, 이 태그를 부모로 기다리던 다른 태그들이 있는지 확인
                ProcessPendingQueue();
            }
            else
            {
                // 실패, 대기열에 추가
                if (!_pendingTags.Contains((tagName, tagId)))
                {
                    _pendingTags.Add((tagName, tagId));
                    Debug.Log($"[NetworkTag] 부모 태그 대기 중... 보류됨: {tagName} (ID: {tagId})");
                }
            }
        }

        /// <summary>
        /// 보류된 태그들을 다시 처리합니다.
        /// </summary>
        private void ProcessPendingQueue()
        {
            if (_pendingTags.Count == 0) return;
            
            bool progressMade = true;

            // 한 번이라도 성공하면 다시 시도
            while (progressMade && _pendingTags.Count > 0)
            {
                progressMade = false;
                for (int i = _pendingTags.Count - 1; i >= 0; i--)
                {
                    var (name, id) = _pendingTags[i];
                    
                    if (GameplayTagManager.RegisterDynamicTagWithIdStrict(name, id, false))
                    {
                        _registeredIds.Add(id);
                        _pendingTags.RemoveAt(i);
                        progressMade = true;
                    }
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                GameplayTagManager.OnDynamicTagRegistered -= OnServerDynamicTagRegistered;

            if (IsClient)
                _tagRegistrations.OnListChanged -= OnTagListChanged;

            foreach (var id in _registeredIds)
            {
                GameplayTagManager.UnregisterDynamicTagById(id);
            }
            _registeredIds.Clear();
            _pendingTags.Clear();
        }

        private void SyncExistingDynamicTags()
        {
            var dynamicTags = GameplayTagManager.DynamicTagDefinitions;
            if (dynamicTags == null) return;
            foreach (var tagSpec in dynamicTags) RegisterTagToNetworkList(tagSpec.TagName, tagSpec.RuntimeIndex);
        }

        private void OnServerDynamicTagRegistered(GameplayTagSpec spec) =>
            RegisterTagToNetworkList(spec.TagName, spec.RuntimeIndex);

        private void RegisterTagToNetworkList(string tagName, int tagId)
        {
            if (_registeredIds.Contains(tagId)) return;
            if (System.Text.Encoding.UTF8.GetByteCount(tagName) > 128)
            {
                Debug.LogError($"[NetworkTag] Name too long: {tagName}");
                return;
            }

            _registeredIds.Add(tagId);
            _tagRegistrations.Add(new NetworkTagRegistration { TagName = tagName, TagId = tagId });
        }
    }
}