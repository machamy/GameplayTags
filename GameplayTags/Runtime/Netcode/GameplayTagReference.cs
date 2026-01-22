

#if GAMEPLAYTAGS_USE_NETCODE
using System;
using Unity.Netcode;

namespace Machamy.GameplayTags.Runtime.Netcode
{

    /// <summary>
    /// Netcode용 게임플레이 태그 참조 클래스
    /// </summary>
    public struct GameplayTagReference : INetworkSerializable, IEquatable<GameplayTagReference>
    {
        private int _tagID;
        public GameplayTagReference(GameplayTag tag)
        {
            _tagID = tag.IsValid ? tag.RawTagId : -1;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _tagID);
        }

        public bool Equals(GameplayTagReference other)
        {
            return _tagID == other._tagID;
        }
        
        public override bool Equals(object obj)
        {
            return obj is GameplayTagReference other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return _tagID.GetHashCode();
        }
        
        public static implicit operator GameplayTag(GameplayTagReference tagRef)
        {
            if (tagRef._tagID >= 0)
            {
                return GameplayTagManager.GetTagByID(tagRef._tagID);
            }

            return GameplayTagSpec.Invalid.Tag;
        }
        
        public static implicit operator GameplayTagReference(GameplayTag tag)
        {
            return new GameplayTagReference(tag);
        }
    }

}

#endif