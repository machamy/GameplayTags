using System;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 게임플레이 태그를 나타내는 구조체입니다.
    /// </summary>
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private string rawTagName;
        [SerializeField] private int rawTagId;

        // 에디터에서 아직 해결되지 않은 태그를 나타냅니다 (-10)
        private const int UnresolvedTagId = -10;
        
        internal GameplayTagSpec Spec => GetResolvedSpec();


        private int TagId
        {
            get
            {
                if (rawTagId >= 0)
                    return rawTagId;
                if (rawTagId == UnresolvedTagId && !string.IsNullOrEmpty(rawTagName))
                {
                    GameplayTag resolvedTag = GameplayTagManager.RequestTag(rawTagName);
                    if (resolvedTag.IsValid && resolvedTag.rawTagId >= 0)
                    {
                        rawTagId = resolvedTag.rawTagId;
                        return rawTagId;
                    }
                    else
                    {
                        rawTagId = -1;
                        return rawTagId;
                    }
                }
                return -1;
            }
        }

        /// <summary>
        /// None 태그를 반환합니다.
        /// </summary>
        public static GameplayTag None => GameplayTagSpec.None.Tag;
        
        /// <summary>
        /// 태그가 유효한지 확인합니다.
        /// </summary>
        public bool IsValid => TagId >= 0;

        /// <summary>
        /// 태그가 None인지 확인합니다.
        /// </summary>
        public bool IsNone => TagId == 0;

        /// <summary>
        /// 태그 이름을 가져옵니다.
        /// </summary>
        public string TagName
        {
            get
            {
                if (!IsValid)
                    return "Invalid";
                
                GameplayTagSpec spec = GameplayTagManager.GetDefinitionByID(rawTagId);
                return spec?.TagName ?? "Unknown";
            }
        }
        
        public int RawTagId => rawTagId;
        
        public string Description
        {
            get
            {
                if (!IsValid)
                    return "Invalid";
                
                return Spec?.Description ?? "Unknown";
            }
        }
        

        internal GameplayTag(GameplayTagSpec spec)
        {
            rawTagId = spec?.RuntimeIndex ?? -1;
            rawTagName = spec?.TagName ?? string.Empty;
        }

        /// <summary>
        /// 태그 Spec을 가져옵니다. 아직 해결되지 않은 경우 rawTagName으로 요청합니다.
        /// </summary>
        private GameplayTagSpec GetResolvedSpec()
        {
            // 이미 해결된 경우
            if (rawTagId >= 0)
                return GameplayTagManager.GetDefinitionByID(rawTagId);

            // UnresolvedTagId인 경우 rawTagName으로 요청
            if (rawTagId == UnresolvedTagId && !string.IsNullOrEmpty(rawTagName))
            {
                GameplayTag resolvedTag = GameplayTagManager.RequestTag(rawTagName);
                if (resolvedTag.IsValid && resolvedTag.rawTagId >= 0)
                {
                    // 해결된 태그로 업데이트 (mutable struct이므로 가능)
                    rawTagId = resolvedTag.rawTagId;
                    return GameplayTagManager.GetDefinitionByID(rawTagId);
                }
                else
                {
                    // 해결할 수 없으면 Invalid로 설정
                    rawTagId = -1;
                    return null;
                }
            }

            return null;
        }
        

        /// <summary>
        /// 이 태그가 다른 태그의 부모인지 확인합니다.
        /// </summary>
        public bool IsParentOf(GameplayTag other)
        {
            if (!IsValid || !other.IsValid)
                return false;

            GameplayTagSpec thisDef = Spec;
            GameplayTagSpec otherDef = other.Spec;

            return thisDef?.IsParentOf(otherDef) ?? false;
        }

        /// <summary>
        /// 이 태그가 다른 태그의 자식인지 확인합니다.
        /// </summary>
        public bool IsChildOf(GameplayTag other)
        {
            return other.IsParentOf(this);
        }

        
        /// <summary>
        /// 다른 태그와 동일한지 확인합니다. <br/>
        /// 만약 둘 다 유효하지 않은 경우 동일하지 않음으로 간주합니다.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(GameplayTag other)
        {
            // 둘 다 유효하지 않은 경우 동일하지 않음
            if (!IsValid && !other.IsValid)
                return false;
            return TagId == other.TagId;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayTag other && Equals(other);
        }

        public override int GetHashCode()
        {
            // UnresolvedTagId인 경우 rawTagName의 해시코드 사용
            if (rawTagId == UnresolvedTagId)
            {
                return rawTagName?.GetHashCode() ?? 0;
            }
            return rawTagId;
        }

        public override string ToString()
        {
            return TagName;
        }

        public static bool operator ==(GameplayTag left, GameplayTag right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameplayTag left, GameplayTag right)
        {
            return !left.Equals(right);
        }
    }
}
