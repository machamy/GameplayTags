using NUnit.Framework;
using Machamy.GameplayTags.Runtime;

namespace Machamy.GameplayTags.Tests
{
    public class GameplayTagSetTests
    {
        [SetUp]
        public void SetUp()
        {
            GameplayTagManager.InitializeIfNeeded();
            GameplayTagManager.ClearAllDynamicTags();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.ClearAllDynamicTags();
        }

        [Test]
        public void Test_Set_Add_IsPresenceOnly()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Oxygen.Low");
            GameplayTag tag = GameplayTagManager.RequestTag("State.Oxygen.Low");

            GameplayTagSet tagSet = new GameplayTagSet();

            Assert.IsTrue(tagSet.AddTag(tag));
            Assert.IsFalse(tagSet.AddTag(tag), "set은 같은 태그를 중복 저장하면 안 됩니다.");
            Assert.AreEqual(1, tagSet.CountTagExact(tag));
            Assert.AreEqual(1, tagSet.TotalTagCount);
        }

        [Test]
        public void Test_Set_HasTag_UsesHierarchy()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Player.Oxygen");
            GameplayTag child = GameplayTagManager.RequestTag("State.Player.Oxygen");
            GameplayTag parent = GameplayTagManager.RequestTag("State");

            GameplayTagSet tagSet = new GameplayTagSet();
            tagSet.AddTag(child);

            Assert.IsTrue(tagSet.HasTag(parent));
            Assert.AreEqual(1, tagSet.CountTag(parent));
        }

        [Test]
        public void Test_Set_Remove_ClearsPresence()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Stun");
            GameplayTag tag = GameplayTagManager.RequestTag("State.Stun");

            GameplayTagSet tagSet = new GameplayTagSet();
            tagSet.AddTag(tag);

            Assert.IsTrue(tagSet.RemoveTagOnce(tag));
            Assert.IsFalse(tagSet.HasTagExact(tag));
            Assert.AreEqual(0, tagSet.TotalTagCount);
        }

        [Test]
        public void Test_Set_PreservesInvalidSerializedTags()
        {
            string json = "{\"serializedTags\":[\"Ghost.Tag\"]}";

            GameplayTagSet tagSet = UnityEngine.JsonUtility.FromJson<GameplayTagSet>(json);
            string serializedAgain = UnityEngine.JsonUtility.ToJson(tagSet);

            Assert.AreEqual(0, tagSet.TotalTagCount);
            StringAssert.Contains("Ghost.Tag", serializedAgain);
        }

        [Test]
        public void Test_Set_Events_Basic_AddedRemoved()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Stun");
            GameplayTag stun = GameplayTagManager.RequestTag("State.Stun");

            GameplayTagSet tagSet = new GameplayTagSet();

            int addedCount = 0;
            int removedCount = 0;
            int registeredCount = 0;
            int unregisteredCount = 0;

            tagSet.OnExplicitCountChanged += (ev) =>
            {
                if (ev.IsAdded) addedCount++;
                if (ev.IsRemoved) removedCount++;
                if (ev.IsRegistered) registeredCount++;
                if (ev.IsUnregistered) unregisteredCount++;
            };

            Assert.IsTrue(tagSet.AddTag(stun));
            Assert.IsFalse(tagSet.AddTag(stun));
            Assert.IsTrue(tagSet.RemoveTagAll(stun));
            Assert.IsFalse(tagSet.RemoveTagAll(stun));

            Assert.AreEqual(1, addedCount);
            Assert.AreEqual(1, removedCount);
            Assert.AreEqual(1, registeredCount);
            Assert.AreEqual(1, unregisteredCount);
        }

        [Test]
        public void Test_Set_Events_ImplicitHierarchy_AndTargetedSubscription()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Effect.Fire.Burn");
            GameplayTag parent = GameplayTagManager.RequestTag("Effect.Fire");
            GameplayTag burn = GameplayTagManager.RequestTag("Effect.Fire.Burn");

            GameplayTagSet tagSet = new GameplayTagSet();

            int parentRegisteredCount = 0;
            int targetedBurnImplicitCount = 0;

            tagSet.OnImplicitCountChanged += (ev) =>
            {
                if (ev.Tag.Equals(parent) && ev.IsRegistered)
                {
                    parentRegisteredCount++;
                }
            };

            tagSet.RegisterImplicitTagEvent(burn, (ev) =>
            {
                if (ev.IsAdded)
                {
                    targetedBurnImplicitCount++;
                }
            });

            Assert.IsTrue(tagSet.AddTag(burn));

            Assert.AreEqual(1, parentRegisteredCount, "자식 태그 추가 시 부모 implicit 등록 이벤트가 발생해야 합니다.");
            Assert.AreEqual(1, targetedBurnImplicitCount, "타겟 implicit 구독은 해당 태그에서만 반응해야 합니다.");
        }

        [Test]
        public void Test_Set_Events_ClearTags_EmitsUnregisteredPerTag()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("A.B");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("C.D");
            GameplayTag tagB = GameplayTagManager.RequestTag("A.B");
            GameplayTag tagD = GameplayTagManager.RequestTag("C.D");

            GameplayTagSet tagSet = new GameplayTagSet();
            Assert.IsTrue(tagSet.AddTag(tagB));
            Assert.IsTrue(tagSet.AddTag(tagD));

            int unregisteredCount = 0;
            tagSet.OnExplicitCountChanged += (ev) =>
            {
                if (ev.IsUnregistered)
                {
                    unregisteredCount++;
                }
            };

            tagSet.ClearTags();

            Assert.AreEqual(0, tagSet.TotalTagCount);
            Assert.AreEqual(2, unregisteredCount, "ClearTags는 내부의 각 explicit 태그에 대해 Unregistered 이벤트를 발생시켜야 합니다.");
        }
    }
}
