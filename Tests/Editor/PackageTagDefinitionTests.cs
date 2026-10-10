using Machamy.GameplayTags.Runtime;
using NUnit.Framework;

[assembly: GameplayTagDef("Test.Package.Defined", "패키지 정의 태그 테스트")]

namespace Machamy.GameplayTags.Tests
{
    /// <summary>
    /// 패키지 어셈블리가 선언한 태그가 생성 코드 없이 런타임에 풀리는지 검증한다.
    /// </summary>
    public class PackageTagDefinitionTests
    {
        [Test]
        public void PackageAssemblyTag_IsResolvable()
        {
            Assert.IsTrue(GameplayTagManager.TryRequestTag("Test.Package.Defined", out var tag));
            Assert.IsTrue(tag.IsValid);
        }

        [Test]
        public void UnknownTag_TryRequest_ReturnsFalse()
        {
            Assert.IsFalse(GameplayTagManager.TryRequestTag("Test.Package.NotDefined.XYZ", out _));
        }
    }
}
