using System;
using System.Collections.Generic;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 게임플레이 태그 컨테이너 인터페이스 (카운터 기반)
    /// </summary>
    public interface IGameplayTagContainer
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
        
        /// <summary>
        /// 태그가 존재하는지 확인합니다 (카운트 > 0)
        /// </summary>
        bool HasTag(GameplayTag tag);
        
        /// <summary>
        /// 태그의 개수를 반환합니다
        /// </summary>
        int CountTag(GameplayTag tag);
        
        /// <summary>
        /// 자식 태그를 포함하여 태그가 존재하는지 확인합니다
        /// </summary>
        bool HasTagIncludeChildren(GameplayTag tag);
        
        /// <summary>
        /// 자식 태그를 포함한 태그 개수를 반환합니다
        /// </summary>
        int CountTagIncludeChildren(GameplayTag tag);
        
        /// <summary>
        /// 다른 컨테이너의 태그 중 하나라도 가지고 있는지 확인합니다
        /// </summary>
        bool HasAnyTags(IGameplayTagContainer otherContainer);
        
        /// <summary>
        /// 다른 컨테이너의 모든 태그를 가지고 있는지 확인합니다
        /// </summary>
        bool HasAllTags(IGameplayTagContainer otherContainer);
        
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
    /// </summary>
    [Serializable]
    public class GameplayTagContainer : IGameplayTagContainer, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<GameplayTag> serializedTags = new List<GameplayTag>();
        
        [SerializeField]
        private List<int> serializedCounts = new List<int>();
        
        private Dictionary<GameplayTag, int> _tagCounts = new Dictionary<GameplayTag, int>();
        
        /// <summary>
        /// 직렬화된 데이터를 딕셔너리로 변환합니다.
        /// </summary>
        public void OnAfterDeserialize()
        {
            _tagCounts.Clear();
            for (int i = 0; i < serializedTags.Count && i < serializedCounts.Count; i++)
            {
                if (serializedTags[i].IsValid && serializedCounts[i] > 0)
                {
                    _tagCounts[serializedTags[i]] = serializedCounts[i];
                }
            }
        }
        
        /// <summary>
        /// 딕셔너리를 직렬화 가능한 데이터로 변환합니다.
        /// </summary>
        public void OnBeforeSerialize()
        {
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
                serializedTags.Add(kvp.Key);
                serializedCounts.Add(kvp.Value);
            }
        }
        
        public bool AddTag(GameplayTag tag)
        {
            if (!tag.IsValid)
                return false;
                
            if (_tagCounts.ContainsKey(tag))
            {
                _tagCounts[tag]++;
            }
            else
            {
                _tagCounts[tag] = 1;
            }
            return true;
        }
        
        public bool AddTagUnique(GameplayTag tag)
        {
            if (!tag.IsValid)
                return false;
                
            if (_tagCounts.ContainsKey(tag) && _tagCounts[tag] > 0)
                return false;
                
            _tagCounts[tag] = 1;
            return true;
        }
        
        public bool RemoveTagOnce(GameplayTag tag)
        {
            if (!tag.IsValid || !_tagCounts.ContainsKey(tag))
                return false;
                
            _tagCounts[tag]--;
            if (_tagCounts[tag] <= 0)
            {
                _tagCounts.Remove(tag);
            }
            return true;
        }
        
        public bool RemoveTagAll(GameplayTag tag)
        {
            if (!tag.IsValid)
                return false;
                
            return _tagCounts.Remove(tag);
        }
        
        public bool HasTag(GameplayTag tag)
        {
            return _tagCounts.ContainsKey(tag) && _tagCounts[tag] > 0;
        }
        
        public int CountTag(GameplayTag tag)
        {
            return _tagCounts.ContainsKey(tag) ? _tagCounts[tag] : 0;
        }

        public bool HasTagIncludeChildren(GameplayTag tag)
        {
            if (HasTag(tag))
                return true;

            // 자식 태그가 있는지 확인
            foreach (var ownedTag in _tagCounts.Keys)
            {
                if (_tagCounts[ownedTag] > 0 && ownedTag.IsChildOf(tag))
                    return true;
            }

            return false;
        }
        
        public int CountTagIncludeChildren(GameplayTag tag)
        {
            int count = CountTag(tag);

            // 자식 태그의 개수도 포함
            foreach (var kvp in _tagCounts)
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
            _tagCounts.Clear();
        }

        public List<GameplayTag> GetAllTags()
        {
            return new List<GameplayTag>(_tagCounts.Keys);
        }
        
        public int UniqueTagCount => _tagCounts.Count;
        
        public int TotalTagCount
        {
            get
            {
                int total = 0;
                foreach (var count in _tagCounts.Values)
                {
                    total += count;
                }
                return total;
            }
        }
    }
}

