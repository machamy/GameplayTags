using System;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 게임플레이 태그를 나타내는 구조체입니다.
    /// </summary>
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>, ISerializationCallbackReceiver, IComparable<GameplayTag>, IComparable
    {
        [SerializeField] private string rawTagName;

        private GameplayTagSpec _spec;
        private bool _isInvalid;

        internal static GameplayTag GetInvalid(string tagName)
        {
            string trimmedTagName = tagName?.Trim() ?? string.Empty;

            return new GameplayTag
            {
                rawTagName = trimmedTagName,
                _spec = null,
                _isInvalid = !string.IsNullOrEmpty(trimmedTagName)
            };
        }

        internal GameplayTagSpec Spec
        {
            get
            {
                if (_spec != null)
                {
                    return _spec;
                }

                if (string.IsNullOrEmpty(rawTagName))
                {
                    return GameplayTagSpec.None;
                }

                if (GameplayTagManager.Initialized)
                {
                    ResolveAgainstCurrentRegistryIfPossible();
                    return _spec;
                }
                return null;
            }
        }
        private int TagId => Spec?.RuntimeIndex ?? -1;

        /// <summary>
        /// None 태그를 반환합니다.
        /// </summary>
        public static GameplayTag None => GameplayTagSpec.None.Tag;


        /// <summary>
        /// 태그가 None인지 확인합니다.
        /// </summary>
        public bool IsNone => string.IsNullOrEmpty(rawTagName) || _spec == GameplayTagSpec.None;

        /// <summary>
        /// 태그가 유효한지 확인합니다.
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (IsNone || _spec != null || !GameplayTagManager.Initialized)
                {
                    return !_isInvalid;
                }

                ResolveAgainstCurrentRegistryIfPossible();
                return !_isInvalid;
            }
        }

        public int InternalTagID => TagId;

        /// <summary>
        /// 태그 이름을 가져옵니다.
        /// </summary>
        public string TagName => _isInvalid ? rawTagName : Spec?.TagName ?? rawTagName;

        public string RawTagName => rawTagName;

        public string Description
        {
            get
            {
                if (_isInvalid)
                {
                    return "Invalid Tag";
                }

                return Spec?.Description ?? "Unknown";
            }
        }

        public GameplayTag ParentTag => Spec?.Parent?.Tag ?? None;

        internal GameplayTag(GameplayTagSpec spec)
        {
            rawTagName = spec?.TagName ?? string.Empty;
            _spec = spec;
            _isInvalid = false;
        }

        internal static GameplayTag FromTagName(string tagName)
        {
            return new GameplayTag
            {
                rawTagName = tagName,
                _spec = null,
                _isInvalid = false
            };
        }

        /// <summary>
        /// 이 태그가 다른 태그의 부모인지 확인합니다.
        /// </summary>
        public bool IsParentOf(GameplayTag other)
        {
            GameplayTagSpec thisDef = Spec;
            GameplayTagSpec otherDef = other.Spec;
            return thisDef?.IsParentOf(otherDef) ?? false;
        }

        /// <summary>
        /// 이 태그가 다른 태그의 자식인지 확인합니다
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
            if (IsNone && other.IsNone)
            {
                return true;
            }

            if (TryGetComparableId(out int leftId) && other.TryGetComparableId(out int rightId))
            {
                return leftId == rightId;
            }

            return string.Equals(rawTagName, other.rawTagName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayTag other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (TryGetComparableId(out _))
            {
                return Spec.GetHashCode();
            }
            return rawTagName != null ? rawTagName.GetHashCode() : 0;
        }


        public void OnBeforeSerialize()
        {
            rawTagName = rawTagName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(rawTagName) || _spec == GameplayTagSpec.None)
            {
                rawTagName = string.Empty;
            }
            else if (!_isInvalid && _spec != null)
            {
                rawTagName = TagName;
            }
        }

        public void OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(rawTagName))
            {
                this = None;
            }
            else
            {
                if (GameplayTagManager.Initialized)
                {
                    ResolveAgainstCurrentRegistryIfPossible();
                }
                else
                {
                    _spec = null;
                    _isInvalid = false;
                }
            }
        }

        public override string ToString()
        {
            return TagName;
        }

        public int CompareTo(object obj)
        {
            if (obj is GameplayTag other)
            {
                return CompareTo(other);
            }
            throw new ArgumentException("Object is not a GameplayTag");
        }

        public static bool operator ==(GameplayTag left, GameplayTag right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameplayTag left, GameplayTag right)
        {
            return !left.Equals(right);
        }


        public int CompareTo(GameplayTag other)
        {
            if (TryGetComparableId(out _) && other.TryGetComparableId(out _))
            {
                return Spec.CompareTo(other.Spec);
            }

            return string.Compare(rawTagName, other.rawTagName, StringComparison.Ordinal);
        }

        public bool IsValidName()
        {
            return GameplayTagManager.IsValidTagName(rawTagName);
        }

        private bool TryGetComparableId(out int tagId)
        {
            if (IsNone)
            {
                tagId = 0;
                return false;
            }

            GameplayTagSpec spec = Spec;
            if (spec != null)
            {
                tagId = spec.RuntimeIndex;
                return true;
            }

            tagId = -1;
            return false;
        }

        private void ResolveAgainstCurrentRegistryIfPossible()
        {
            if (string.IsNullOrEmpty(rawTagName) || !GameplayTagManager.Initialized)
            {
                return;
            }

            GameplayTag resolvedTag = GameplayTagManager.RequestTag(rawTagName);
            _spec = resolvedTag._spec;
            _isInvalid = resolvedTag._isInvalid;
        }
    }
}
