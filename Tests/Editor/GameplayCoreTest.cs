using NUnit.Framework;
using Machamy.GameplayTags.Runtime;

namespace Machamy.GameplayTags.Tests
{
    public class GameplayTagCoreTests
    {
        [SetUp]
        public void SetUp()
        {
            GameplayTagManager.ReloadTags();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.ClearAllDynamicTags();
        }

        // 1. 태그 등록 및 기본 조회 테스트
        [Test]
        public void Test_Manager_RegisterAndRequest()
        {
            string tagName = "Hero.Ability.Jump";
            
            // 등록 전에는 유효하지 않아야 함
            GameplayTag invalidTag = GameplayTagManager.RequestTag(tagName);
            Assert.IsFalse(invalidTag.IsValid, "등록되지 않은 태그는 유효하지 않아야 합니다.");
            Assert.AreNotEqual(GameplayTag.None, invalidTag);

            // 등록
            bool success = GameplayTagManager.RegisterDynamicTagIncludeParentTags(tagName);
            Assert.IsTrue(success);

            // 등록 후 조회
            GameplayTag validTag = GameplayTagManager.RequestTag(tagName);
            Assert.IsTrue(validTag.IsValid, "등록된 태그는 유효해야 합니다.");
            Assert.AreEqual(tagName, validTag.TagName);
            
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Manual.Test");
            GameplayTag tag = GameplayTagManager.RequestTag("Manual.Test");
            Assert.IsTrue(tag.IsValid, "수동 등록된 태그는 유효해야 합니다.");
        }

        // 2. 태그 계층 구조(Hierarchy) 논리 테스트 (가장 중요)
        [Test]
        public void Test_Tag_IsChildOf_Logic()
        {
            // 구조: Animal -> Animal.Cat -> Animal.Cat.Tiger
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Animal.Cat.Tiger");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Plant.Tree");

            GameplayTag animal = GameplayTagManager.RequestTag("Animal");
            GameplayTag cat = GameplayTagManager.RequestTag("Animal.Cat");
            GameplayTag tiger = GameplayTagManager.RequestTag("Animal.Cat.Tiger");
            GameplayTag tree = GameplayTagManager.RequestTag("Plant.Tree");

            // 1. 직계 자식 확인
            Assert.IsTrue(cat.IsChildOf(animal), "Cat은 Animal의 자식이어야 합니다.");
            Assert.IsTrue(tiger.IsChildOf(cat), "Tiger는 Cat의 자식이어야 합니다.");

            // 2. 손자 확인 (깊은 상속)
            Assert.IsTrue(tiger.IsChildOf(animal), "Tiger는 Animal의 자식(후손)이어야 합니다.");

            // 3. 역관계 확인 (부모는 자식의 자식이 아님)
            Assert.IsFalse(animal.IsChildOf(cat), "Animal은 Cat의 자식이 아닙니다.");

            // 4. 무관계 확인
            Assert.IsFalse(tree.IsChildOf(animal), "Tree는 Animal의 자식이 아닙니다.");
            
            // 5. 자기 자신 확인 (구현에 따라 다를 수 있으나 보통 IsChildOf는 False 혹은 True.
            // 여기서는 '포함 관계'가 아닌 '상속 관계'라면 False가 맞고, '매칭'이라면 True입니다.
            // 만약 코드가 'startWith' 로직이라면 자기 자신도 True일 수 있습니다. 
            // *일반적인 GameplayTag 로직상 자기 자신 매칭은 MatchesTag로 처리합니다.*)
        }

        // 3. 부모 태그 자동 생성 테스트
        [Test]
        public void Test_Manager_AutoParentGeneration()
        {
            // A.B.C를 등록하면 A와 A.B가 자동으로 생겨야 함
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Region.Forest.Deep");

            GameplayTag region = GameplayTagManager.RequestTag("Region");
            GameplayTag forest = GameplayTagManager.RequestTag("Region.Forest");
            GameplayTag deep = GameplayTagManager.RequestTag("Region.Forest.Deep");

            Assert.IsTrue(region.IsValid, "루트 태그(Region)가 자동 생성되어야 합니다.");
            Assert.IsTrue(forest.IsValid, "중간 태그(Forest)가 자동 생성되어야 합니다.");
            Assert.IsTrue(deep.IsValid, "타겟 태그(Deep)가 생성되어야 합니다.");
        }

        // 4. ID 강제 할당 테스트 (네트워크/저장 데이터용)
        [Test]
        public void Test_Manager_StrictIdRegistration()
        {
            int forcedId = 999;
            string tagName = "Network";

            // 특정 ID로 등록
            bool success = GameplayTagManager.RegisterDynamicTagWithIdStrict(tagName, forcedId);
            Assert.IsTrue(success);

            GameplayTag tag = GameplayTagManager.RequestTag(tagName);
            
            // ID 확인
            Assert.AreEqual(forcedId, tag.Spec.RuntimeIndex, "태그 ID가 강제 할당된 값과 일치해야 합니다.");
            
            // ID로 역조회 확인
            GameplayTag tagFromId = GameplayTagManager.GetTagByID(forcedId);
            Assert.AreEqual(tag, tagFromId, "ID로 조회한 태그와 이름으로 조회한 태그가 같아야 합니다.");
        }

        // 5. 구조체 동등성(Equality) 테스트
        [Test]
        public void Test_Tag_Equality()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Color.Red");
            
            GameplayTag tag1 = GameplayTagManager.RequestTag("Color.Red");
            GameplayTag tag2 = GameplayTagManager.RequestTag("Color.Red"); // 다시 요청
            GameplayTag tag3 = GameplayTagManager.CreateUnresolvedTag("Color.Red"); // 이름만 같은 불안정 태그

            Assert.AreEqual("Color.Red", tag1.TagName, "태그 이름이 일치해야 합니다.");
            
            // 같은 매니저에서 가져온 태그끼리 비교
            Assert.AreEqual(tag1, tag2, "같은 이름으로 요청한 태그는 구조적으로 같아야 합니다.");
            Assert.IsTrue(tag1 == tag2, "== 연산자가 true여야 합니다.");

            // 이름만 같은 불안정 태그와 비교
            Assert.IsTrue(tag1 == tag3, "== 연산자가 true여야 합니다.");
            Assert.AreEqual(tag1, tag3, "이름이 같은 태그는 같아야 합니다.");
 
        }

        [Test]
        public void Test_InvalidTags_KeepDistinctRawNames()
        {
            GameplayTag invalidA = GameplayTagManager.RequestTag("Unknown.TagA");
            GameplayTag invalidB = GameplayTagManager.RequestTag("Unknown.TagB");
            GameplayTag invalidAClone = GameplayTagManager.RequestTag("Unknown.TagA");

            Assert.IsFalse(invalidA.IsValid);
            Assert.IsFalse(invalidB.IsValid);
            Assert.AreEqual("Unknown.TagA", invalidA.TagName);
            Assert.AreEqual("Unknown.TagB", invalidB.TagName);
            Assert.AreEqual(invalidA, invalidAClone, "같은 잘못된 태그 이름은 같은 invalid 태그로 취급되어야 합니다.");
            Assert.AreNotEqual(invalidA, invalidB, "서로 다른 잘못된 태그 이름은 합쳐지면 안 됩니다.");
            Assert.AreEqual(invalidA.GetHashCode(), invalidAClone.GetHashCode(), "같은 invalid 태그 이름은 같은 해시를 가져야 합니다.");
            Assert.AreNotEqual(invalidA.GetHashCode(), invalidB.GetHashCode(), "서로 다른 invalid 태그 이름은 해시도 분리되어야 합니다.");
        }

        // 6. [중요] 직렬화 전처리 로직 (Trim, None 처리)
        // 시나리오: 인스펙터에서 공백을 실수로 넣었을 때 잘라내는지
        [Test]
        public void Test_Struct_Serialization_Logic()
        {
            // 케이스 1: 공백 제거 (Trim)
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("   Dirty.Name   ");
            GameplayTag dirtyTag = GameplayTagManager.RequestTag("Dirty.Name");
            
            // 유니티 저장 시점에 호출되는 콜백 수동 호출
            dirtyTag.OnBeforeSerialize();
            
            Assert.AreEqual("Dirty.Name", dirtyTag.RawTagName, "OnBeforeSerialize에서 앞뒤 공백이 제거되어야 합니다.");

            // 케이스 2: None 태그 처리
            GameplayTag noneTag = GameplayTag.None;
            noneTag.OnBeforeSerialize();
            
            Assert.AreEqual(string.Empty, noneTag.RawTagName, "None 태그는 직렬화 시 빈 문자열로 저장되어야 합니다.");

            GameplayTag invalidTag = GameplayTagManager.RequestTag("Missing.Tag");
            invalidTag.OnBeforeSerialize();

            Assert.AreEqual("Missing.Tag", invalidTag.RawTagName, "Invalid 태그도 원래 이름으로 직렬화되어야 합니다.");
        }

        // 7. 해시코드 충돌 방지 테스트 (Dictionary 키로 쓸 때 필수)
        [Test]
        public void Test_Struct_HashCode()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Hash.A");
            GameplayTagManager.RegisterDynamicTagChecked("Hash.B");
            
            GameplayTag tagA = GameplayTagManager.RequestTag("Hash.A");
            GameplayTag tagB = GameplayTagManager.RequestTag("Hash.B");
            
            // 서로 다른 태그가 우연히 같은 해시코드를 가지면 Dictionary에서 심각한 버그 발생
            Assert.AreNotEqual(tagA.GetHashCode(), tagB.GetHashCode(), "서로 다른 태그는 다른 해시코드를 가져야 합니다.");
            
            GameplayTag tagA_Clone = GameplayTagManager.RequestTag("Hash.A");
            Assert.AreEqual(tagA.GetHashCode(), tagA_Clone.GetHashCode(), "같은 태그는 반드시 같은 해시코드를 가져야 합니다.");

            if (tagA.IsValid)
            {
                Assert.AreEqual(tagA.GetHashCode(), tagA.Spec.GetHashCode(),"태그와 스펙은 동일한 해시코드를 가져야 합니다.");
                Assert.AreEqual(tagB.GetHashCode(), tagB.Spec.GetHashCode(),"태그와 스펙은 동일한 해시코드를 가져야 합니다.");
            }

        }
        
    }
}
