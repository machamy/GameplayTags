using System;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 어셈블리에 게임플레이 태그를 등록하기 위한 어트리뷰트
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class GameplayTagDefAttribute : Attribute
    {
        public string TagName { get; }
        public string Description { get; }

        public GameplayTagDefAttribute(string tagName, string description = "")
        {
            TagName = tagName;
            Description = description;
        }
    }
}