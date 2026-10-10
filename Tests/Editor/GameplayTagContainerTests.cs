
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Machamy.GameplayTags.Runtime;

namespace Machamy.GameplayTags.Tests
{
    public class GameplayTagContainerTests
    {
        private static readonly FieldInfo IsInitializedField =
            typeof(GameplayTagManager).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Static);

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

        // 1. 기본 추가/제거 및 유니크 기능 테스트
        [Test]
        public void Test_Container_AddRemove_Unique()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Status.Stun");
            GameplayTag stunTag = GameplayTagManager.RequestTag("Status.Stun");

            GameplayTagContainer container = new GameplayTagContainer();

            // 일반 추가 (중복 허용)
            container.AddTag(stunTag);
            container.AddTag(stunTag);
            Assert.AreEqual(2, container.CountTagExact(stunTag), "AddTag는 중복을 허용해야 합니다.");

            // 유니크 추가 (이미 존재하므로 실패해야 함)
            bool uniqueResult = container.AddTagUnique(stunTag);
            Assert.IsFalse(uniqueResult, "이미 태그가 존재하면 AddTagUnique는 false를 반환해야 합니다.");
            Assert.AreEqual(2, container.CountTagExact(stunTag), "AddTagUnique 실패 시 카운트가 증가하면 안 됩니다.");

            // 하나 제거
            container.RemoveTagOnce(stunTag);
            Assert.AreEqual(1, container.CountTagExact(stunTag));

            // 유니크 추가 (이제 가능해야 함? 아니요, 1개 남아있으므로 실패)
            uniqueResult = container.AddTagUnique(stunTag);
            Assert.IsFalse(uniqueResult);

            // 모두 제거
            container.RemoveTagAll(stunTag);
            Assert.AreEqual(0, container.CountTagExact(stunTag));

            // 완전히 비어있을 때 유니크 추가 성공 확인
            uniqueResult = container.AddTagUnique(stunTag);
            Assert.IsTrue(uniqueResult);
            Assert.AreEqual(1, container.CountTagExact(stunTag));
        }

        // 2. 계층 구조 카운팅 테스트 (가장 중요)
        [Test]
        public void Test_Container_Hierarchy_Counting()
        {
            // 상황: 화염(Fire) 속성 공격을 2번, 화염구(Fire.Ball)를 3번 맞음
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Element.Fire.Ball");
            
            GameplayTag fireTag = GameplayTagManager.RequestTag("Element.Fire");
            GameplayTag fireBallTag = GameplayTagManager.RequestTag("Element.Fire.Ball");

            GameplayTagContainer container = new GameplayTagContainer();

            // Fire 2개 추가
            container.AddTag(fireTag);
            container.AddTag(fireTag);

            // Fire.Ball 3개 추가
            container.AddTag(fireBallTag);
            container.AddTag(fireBallTag);
            container.AddTag(fireBallTag);

            // 검증 1: Exact 카운트 (정확히 그 태그만)
            Assert.AreEqual(2, container.CountTagExact(fireTag), "Element.Fire 자체는 2개여야 합니다.");
            Assert.AreEqual(3, container.CountTagExact(fireBallTag), "Element.Fire.Ball 자체는 3개여야 합니다.");

            // 검증 2: 계층형 카운트 (자식 포함)
            // Fire 태그의 개수는 (자신 2개) + (자식 Fire.Ball 3개) = 5개여야 함
            Assert.AreEqual(5, container.CountTag(fireTag), "CountTag는 자식 태그의 수량까지 합산해야 합니다.");
        }

        // 3. HasTag 계층 구조 테스트
        [Test]
        public void Test_Container_HasTag_Hierarchy()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Debuff.Poison");
            GameplayTag poisonTag = GameplayTagManager.RequestTag("State.Debuff.Poison");
            GameplayTag debuffTag = GameplayTagManager.RequestTag("State.Debuff");
            GameplayTag stateTag = GameplayTagManager.RequestTag("State");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(poisonTag);

            // 독(Poison)이 있으면 디버프(Debuff) 상태인가? -> True
            Assert.IsTrue(container.HasTag(poisonTag), "본인 태그 확인");
            Assert.IsTrue(container.HasTag(debuffTag), "부모 태그로 확인 시 True여야 합니다 (자식이 있으므로).");
            Assert.IsTrue(container.HasTag(stateTag), "조상 태그로 확인 시 True여야 합니다.");

            // 역관계: 디버프 태그만 넣었을 때 독 태그가 있다고 나오면 안 됨
            container.ClearTags();
            container.AddTag(debuffTag);
            
            Assert.IsTrue(container.HasTag(debuffTag));
            Assert.IsFalse(container.HasTag(poisonTag), "부모를 가졌다고 자식을 가진 것으로 판단하면 안 됩니다.");
        }

        // 4. HasAllTags (교집합/포함관계) 정밀 테스트
        [Test]
        public void Test_Container_HasAll_Requirements()
        {
            // RPG 장비 착용 조건 시나리오
            // 요구사항: [Job.Warrior, Level.10]
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Job.Warrior");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Level.10");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Level.99");

            GameplayTag warrior = GameplayTagManager.RequestTag("Job.Warrior");
            GameplayTag lvl10 = GameplayTagManager.RequestTag("Level.10");
            GameplayTag lvl99 = GameplayTagManager.RequestTag("Level.99");

            GameplayTagContainer requirements = new GameplayTagContainer();
            requirements.AddTag(warrior);
            requirements.AddTag(lvl10);

            // 케이스 1: 조건 충족
            GameplayTagContainer myCharacter = new GameplayTagContainer();
            myCharacter.AddTag(warrior);
            myCharacter.AddTag(lvl10);
            myCharacter.AddTag(lvl99); // 불필요한 태그가 있어도 상관없음

            Assert.IsTrue(myCharacter.HasAllTags(requirements), "모든 요구 조건을 충족하면 True여야 합니다.");

            // 케이스 2: 조건 미충족 (하나 부족)
            GameplayTagContainer newbie = new GameplayTagContainer();
            newbie.AddTag(warrior);
            
            Assert.IsFalse(newbie.HasAllTags(requirements), "하나라도 부족하면 False여야 합니다.");

            // 케이스 3: 계층 구조를 이용한 조건 충족
            // 요구사항이 [Effect.Fire] 인데, 내가 [Effect.Fire.Burn]을 가지고 있다면?
            // HasTag 로직에 따라 자식을 가지고 있으면 부모를 가진 것으로 판단하므로 True가 나와야 함.
            
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Effect.Fire.Burn");
            GameplayTag effectFire = GameplayTagManager.RequestTag("Effect.Fire");
            GameplayTag effectBurn = GameplayTagManager.RequestTag("Effect.Fire.Burn");

            GameplayTagContainer fireRequirement = new GameplayTagContainer();
            fireRequirement.AddTag(effectFire); // "불 속성 효과가 필요해"

            GameplayTagContainer myEffects = new GameplayTagContainer();
            myEffects.AddTag(effectBurn); // "난 화상 효과를 가지고 있어"

            Assert.IsTrue(myEffects.HasAllTags(fireRequirement), "자식 태그를 가지고 있으면 부모 태그 요구사항을 충족해야 합니다.");
        }

        // 5. HasAnyTags (하나라도 일치) 테스트
        [Test]
        public void Test_Container_HasAny_Interaction()
        {
            // 면역 시스템 시나리오
            // 면역 목록: [Damage.Fire, Damage.Ice]
            // 들어오는 공격: [Damage.Fire]
            
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Damage.Fire");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Damage.Ice");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Damage.Physical");

            GameplayTag fire = GameplayTagManager.RequestTag("Damage.Fire");
            GameplayTag ice = GameplayTagManager.RequestTag("Damage.Ice");
            GameplayTag physical = GameplayTagManager.RequestTag("Damage.Physical");

            GameplayTagContainer immunity = new GameplayTagContainer();
            immunity.AddTag(fire);
            immunity.AddTag(ice);

            GameplayTagContainer incomingFire = new GameplayTagContainer();
            incomingFire.AddTag(fire);

            GameplayTagContainer incomingPhysical = new GameplayTagContainer();
            incomingPhysical.AddTag(physical);

            Assert.IsTrue(immunity.HasAnyTag(incomingFire), "겹치는 태그가 있으면 True");
            Assert.IsFalse(immunity.HasAnyTag(incomingPhysical), "겹치는 태그가 없으면 False");
        }
        
        // 6. 예외 처리 및 무효 태그 테스트
        [Test]
        public void Test_Container_InvalidTags()
        {
            GameplayTagContainer container = new GameplayTagContainer();
            
            // 유효하지 않은 태그 추가 시도
            bool added = container.AddTag(GameplayTag.None);
            Assert.IsFalse(added, "None 태그는 추가되지 않아야 합니다.");
            
            // 빈 컨테이너 확인
            Assert.AreEqual(0, container.TotalTagCount);
            
            // null 컨테이너와 비교
            Assert.IsFalse(container.HasAnyTag(null), "null 컨테이너 비교는 false여야 합니다.");
            Assert.IsFalse(container.HasAllTags(null), "null 컨테이너 비교는 false여야 합니다.");
        }
        
        // 7. 깊은 계층 구조(Deep Hierarchy) 테스트
        [Test]
        public void Test_Container_DeepHierarchy()
        {
            // 구조: Kingdom -> Phylum -> Class -> Order (4단계)
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Kingdom.Phylum.Class.Order");

            GameplayTag kingdom = GameplayTagManager.RequestTag("Kingdom");
            GameplayTag phylum = GameplayTagManager.RequestTag("Kingdom.Phylum");
            GameplayTag order = GameplayTagManager.RequestTag("Kingdom.Phylum.Class.Order");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(order); // 가장 하위 태그 추가

            // 1. HasTag 검증
            Assert.IsTrue(container.HasTag(order), "본인 확인");
            Assert.IsTrue(container.HasTag(phylum), "할아버지 태그 확인");
            Assert.IsTrue(container.HasTag(kingdom), "증조할아버지(루트) 태그 확인");

            // 2. CountTag 검증
            Assert.AreEqual(1, container.CountTag(kingdom), "루트 태그로 조회 시 자식 태그 개수가 포함되어야 합니다.");
        }

        // 8. 형제(Sibling) 태그 독립성 테스트
        [Test]
        public void Test_Container_SiblingIndependence()
        {
            // 구조: Color.Red vs Color.Blue
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Color.Red");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Color.Blue");

            GameplayTag red = GameplayTagManager.RequestTag("Color.Red");
            GameplayTag blue = GameplayTagManager.RequestTag("Color.Blue");
            GameplayTag color = GameplayTagManager.RequestTag("Color");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(red);

            // 부모는 공유하므로 Color는 True
            Assert.IsTrue(container.HasTag(color));
            
            // 형제끼리는 영향 없음
            Assert.IsFalse(container.HasTag(blue), "Red를 가지고 있다고 Blue가 있다고 판단하면 안 됩니다.");
            Assert.AreEqual(0, container.CountTag(blue));
        }

        // 9. 복합 카운팅 (부모와 자식이 섞여 있을 때)
        [Test]
        public void Test_Container_MixedHierarchyCounting()
        {
            // 상황: [A] 1개, [A.B] 2개, [A.B.C] 3개 보유
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("A.B.C");
            GameplayTag tagA = GameplayTagManager.RequestTag("A");
            GameplayTag tagB = GameplayTagManager.RequestTag("A.B");
            GameplayTag tagC = GameplayTagManager.RequestTag("A.B.C");

            GameplayTagContainer container = new GameplayTagContainer();
            
            // A 1개
            container.AddTag(tagA);
            
            // A.B 2개
            container.AddTag(tagB);
            container.AddTag(tagB);
            
            // A.B.C 3개
            container.AddTag(tagC);
            container.AddTag(tagC);
            container.AddTag(tagC);

            // 검증
            // A의 개수 = 1(본인) + 2(자식 B) + 3(손자 C) = 6
            Assert.AreEqual(6, container.CountTag(tagA), "A의 총 개수는 모든 자손의 합이어야 합니다.");
            
            // A.B의 개수 = 2(본인) + 3(자식 C) = 5
            // *부모 A는 포함되지 않음*
            Assert.AreEqual(5, container.CountTag(tagB), "A.B의 총 개수는 본인과 자식의 합이어야 합니다.");
            
            // A.B.C의 개수 = 3
            Assert.AreEqual(3, container.CountTag(tagC));
        }

        // 10. 컨테이너 간 "Exact" vs "Hierarchy" 매칭 차이
        [Test]
        public void Test_Container_ExactVsHierarchy_Matching()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Weapon.Sword.Longsword");
            
            GameplayTag sword = GameplayTagManager.RequestTag("Weapon.Sword");
            GameplayTag longsword = GameplayTagManager.RequestTag("Weapon.Sword.Longsword");

            // 내 인벤토리: 롱소드(자식) 보유
            GameplayTagContainer inventory = new GameplayTagContainer();
            inventory.AddTag(longsword);

            // 요구사항 1: 그냥 "검(Sword)"이면 됨 (계층 허용)
            GameplayTagContainer reqGeneral = new GameplayTagContainer();
            reqGeneral.AddTag(sword);

            // 요구사항 2: 반드시 "검(Sword)" 태그 그 자체여야 함 (Exact)
            GameplayTagContainer reqExact = new GameplayTagContainer();
            reqExact.AddTag(sword);

            // 테스트
            Assert.IsTrue(inventory.HasAllTags(reqGeneral), "일반 검사는 자식 태그(Longsword)가 부모 요구사항(Sword)을 충족해야 합니다.");
            Assert.IsFalse(inventory.HasAllTagsExact(reqExact), "Exact 검사는 태그가 정확히 일치해야 합니다. (Longsword != Sword)");
        }

        // 11. 유니크 추가의 계층 구조 독립성
        [Test]
        public void Test_Container_Unique_HierarchyIndependence()
        {
            // 질문: 부모 태그가 있을 때 자식 태그를 Unique로 추가할 수 있는가? -> 가능해야 함 (별개의 태그임)
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Effect.Buff");
            GameplayTag buff = GameplayTagManager.RequestTag("Effect.Buff");
            GameplayTag healBuff = GameplayTagManager.RegisterDynamicTagChecked("Effect.Buff.Heal") 
                ? GameplayTagManager.RequestTag("Effect.Buff.Heal") 
                : GameplayTag.None; // 등록 로직에 따라 가져옴

            GameplayTagContainer container = new GameplayTagContainer();
            
            // 1. 부모 추가
            container.AddTag(buff);
            
            // 2. 자식 유니크 추가 시도
            bool added = container.AddTagUnique(healBuff);
            
            Assert.IsTrue(added, "부모 태그가 있어도 자식 태그는 별도로 유니크 추가가 가능해야 합니다.");
            Assert.AreEqual(1, container.CountTagExact(buff));
            Assert.AreEqual(1, container.CountTagExact(healBuff));
            
            // 3. 자식 중복 추가 시도
            bool addedAgain = container.AddTagUnique(healBuff);
            Assert.IsFalse(addedAgain, "이미 자식 태그가 있으므로 실패해야 합니다.");
        }
        [Test]
        public void Test_Container_PreservesInvalidSerializedTags()
        {
            string json = "{\"serializedTags\":[\"Ghost.Tag\"],\"serializedCounts\":[2]}";

            GameplayTagContainer container = JsonUtility.FromJson<GameplayTagContainer>(json);
            string serializedAgain = JsonUtility.ToJson(container);

            Assert.AreEqual(0, container.TotalTagCount, "invalid 태그는 런타임 컨테이너 카운트에 포함되면 안 됩니다.");
            StringAssert.Contains("Ghost.Tag", serializedAgain, "invalid 태그 raw name은 직렬화 데이터에 유지되어야 합니다.");
            StringAssert.Contains("\"serializedCounts\":[2]", serializedAgain, "invalid 태그 카운트도 유지되어야 합니다.");
        }
        [Test]
        public void Test_Container_RemoveTagAll_UpdatesImplicitParentCounts()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Debuff.Poison");

            GameplayTag poisonTag = GameplayTagManager.RequestTag("State.Debuff.Poison");
            GameplayTag debuffTag = GameplayTagManager.RequestTag("State.Debuff");
            GameplayTag stateTag = GameplayTagManager.RequestTag("State");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(poisonTag);
            container.AddTag(poisonTag);

            Assert.IsTrue(container.RemoveTagAll(poisonTag));
            Assert.AreEqual(0, container.CountTagExact(poisonTag));
            Assert.AreEqual(0, container.CountTag(debuffTag));
            Assert.AreEqual(0, container.CountTag(stateTag));
            Assert.IsFalse(container.HasTag(debuffTag));
            Assert.IsFalse(container.HasTag(stateTag));
        }

        [Test]
        public void Test_Container_ClearTags_ClearsImplicitParentCounts()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Debuff.Poison");

            GameplayTag poisonTag = GameplayTagManager.RequestTag("State.Debuff.Poison");
            GameplayTag debuffTag = GameplayTagManager.RequestTag("State.Debuff");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(poisonTag);

            container.ClearTags();

            Assert.AreEqual(0, container.TotalTagCount);
            Assert.AreEqual(0, container.CountTag(poisonTag));
            Assert.AreEqual(0, container.CountTag(debuffTag));
            Assert.IsFalse(container.HasTag(poisonTag));
            Assert.IsFalse(container.HasTag(debuffTag));
        }

        [Test]
        public void Test_Container_RemoveTagOnce_PreservesOtherImplicitParentCounts()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Debuff.Poison");

            GameplayTag debuffTag = GameplayTagManager.RequestTag("State.Debuff");
            GameplayTag poisonTag = GameplayTagManager.RequestTag("State.Debuff.Poison");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(debuffTag);
            container.AddTag(poisonTag);
            container.AddTag(poisonTag);

            Assert.IsTrue(container.RemoveTagOnce(poisonTag));
            Assert.AreEqual(1, container.CountTagExact(debuffTag));
            Assert.AreEqual(1, container.CountTagExact(poisonTag));
            Assert.AreEqual(2, container.CountTag(debuffTag));
            Assert.IsTrue(container.HasTag(debuffTag));
            Assert.IsTrue(container.HasTag(poisonTag));
        }

        [Test]
        public void Test_Container_RemoveTagAll_PreservesOtherImplicitParentCounts()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Debuff.Poison");

            GameplayTag debuffTag = GameplayTagManager.RequestTag("State.Debuff");
            GameplayTag poisonTag = GameplayTagManager.RequestTag("State.Debuff.Poison");
            GameplayTag stateTag = GameplayTagManager.RequestTag("State");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(debuffTag);
            container.AddTag(poisonTag);
            container.AddTag(poisonTag);

            Assert.IsTrue(container.RemoveTagAll(poisonTag));
            Assert.AreEqual(1, container.CountTagExact(debuffTag));
            Assert.AreEqual(0, container.CountTagExact(poisonTag));
            Assert.AreEqual(1, container.CountTag(debuffTag));
            Assert.AreEqual(1, container.CountTag(stateTag));
            Assert.IsTrue(container.HasTag(debuffTag));
            Assert.IsFalse(container.HasTag(poisonTag));
        }

        [Test]
        public void Test_Container_Deserialization_MergesDuplicateSerializedValidTags()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Debuff.Poison");
            GameplayTag poisonTag = GameplayTagManager.RequestTag("State.Debuff.Poison");
            GameplayTag debuffTag = GameplayTagManager.RequestTag("State.Debuff");

            string json = "{\"serializedTags\":[\"State.Debuff.Poison\",\"State.Debuff.Poison\"],\"serializedCounts\":[2,3]}";

            GameplayTagContainer container = JsonUtility.FromJson<GameplayTagContainer>(json);

            Assert.AreEqual(5, container.CountTagExact(poisonTag));
            Assert.AreEqual(5, container.CountTag(debuffTag));
            Assert.AreEqual(5, container.TotalTagCount);
        }

        [Test]
        public void Test_Container_InvalidSerializedTags_CanBecomeValid_AfterRegistration()
        {
            const string json = "{\"serializedTags\":[\"Late.Tag\"],\"serializedCounts\":[2]}";

            GameplayTagContainer container = JsonUtility.FromJson<GameplayTagContainer>(json);
            Assert.AreEqual(0, container.TotalTagCount);

            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Late.Tag");
            GameplayTag lateTag = GameplayTagManager.RequestTag("Late.Tag");

            container.OnAfterDeserialize();

            Assert.AreEqual(2, container.CountTagExact(lateTag));
            Assert.AreEqual(2, container.TotalTagCount);
            Assert.IsTrue(container.HasTag(lateTag));
        }

        [Test]
        public void Test_Container_DeferredResolve_BuildsCountsAfterManagerInitialization()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("State.Debuff.Poison");

            const string json = "{\"serializedTags\":[\"State.Debuff.Poison\"],\"serializedCounts\":[2]}";
            bool originalInitialized = (bool)IsInitializedField.GetValue(null);

            try
            {
                IsInitializedField.SetValue(null, false);
                GameplayTagContainer container = JsonUtility.FromJson<GameplayTagContainer>(json);

                Assert.AreEqual(0, container.TotalTagCount);
                Assert.AreEqual(0, container.GetAllTags().Count);

                IsInitializedField.SetValue(null, originalInitialized);

                GameplayTag poisonTag = GameplayTagManager.RequestTag("State.Debuff.Poison");
                GameplayTag debuffTag = GameplayTagManager.RequestTag("State.Debuff");

                Assert.AreEqual(2, container.CountTagExact(poisonTag));
                Assert.AreEqual(2, container.CountTag(debuffTag));
                Assert.IsTrue(container.HasTag(debuffTag));
            }
            finally
            {
                IsInitializedField.SetValue(null, originalInitialized);
            }
        }
        
        // 12. 기본적인 이벤트 발생 및 등록/해제(Registered/Unregistered) 상태 테스트
        [Test]
        public void Test_Container_Events_Basic_AddedRemoved()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Status.Stun");
            GameplayTag stunTag = GameplayTagManager.RequestTag("Status.Stun");
            GameplayTagContainer container = new GameplayTagContainer();

            int increaseCount = 0;
            int decreaseCount = 0;
            int registeredCount = 0;
            int unregisteredCount = 0;

            container.OnExplicitCountChanged += (ev) => {
                if (ev.IsAdded) increaseCount++;           // 카운트가 오를 때마다
                if (ev.IsRemoved) decreaseCount++;         // 카운트가 내릴 때마다
                if (ev.IsRegistered) registeredCount++;    // 0 -> 1이 될 때만
                if (ev.IsUnregistered) unregisteredCount++;// 1 이상 -> 0이 될 때만
            };

            // 0 -> 1
            container.AddTag(stunTag);
            Assert.AreEqual(1, increaseCount);
            Assert.AreEqual(1, registeredCount, "최초 추가이므로 IsRegistered가 터져야 합니다.");

            // 1 -> 2
            container.AddTag(stunTag);
            Assert.AreEqual(2, increaseCount, "중첩 추가되므로 IsAdded는 계속 터져야 합니다.");
            Assert.AreEqual(1, registeredCount, "이미 존재하므로 IsRegistered는 추가로 터지면 안 됩니다.");

            // 2 -> 1
            container.RemoveTagOnce(stunTag);
            Assert.AreEqual(1, decreaseCount);
            Assert.AreEqual(0, unregisteredCount, "아직 1개 남아있으므로 IsUnregistered는 터지면 안 됩니다.");

            // 1 -> 0
            container.RemoveTagOnce(stunTag);
            Assert.AreEqual(2, decreaseCount);
            Assert.AreEqual(1, unregisteredCount, "카운트가 0이 되었으므로 IsUnregistered가 터져야 합니다.");
        }

        // 13. 계층 구조 이벤트 전파 테스트 (Implicit Event)
        [Test]
        public void Test_Container_Events_Hierarchy_Propagation()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Parent.Child");
            GameplayTag parentTag = GameplayTagManager.RequestTag("Parent");
            GameplayTag childTag = GameplayTagManager.RequestTag("Parent.Child");
            
            GameplayTagContainer container = new GameplayTagContainer();

            List<GameplayTag> implicitRegisteredTags = new List<GameplayTag>();

            container.OnImplicitCountChanged += (ev) => {
                if (ev.IsRegistered)
                    implicitRegisteredTags.Add(ev.Tag);
            };

            // 자식 태그 추가
            container.AddTag(childTag);

            // 검증: 자식 추가 시 자식 본인과 부모 모두 Implicit(묵시적) 등록 이벤트가 터져야 함
            Assert.Contains(childTag, implicitRegisteredTags, "자식 태그 Registered 이벤트 누락");
            Assert.Contains(parentTag, implicitRegisteredTags, "부모 태그로의 Implicit Registered 전파 누락");
        }

        // 14. 특정 태그 구독(Targeted Subscription) 테스트 (매우 중요)
        [Test]
        public void Test_Container_Events_TargetedSubscription()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Effect.Burn");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Effect.Freeze");
            
            GameplayTag burn = GameplayTagManager.RequestTag("Effect.Burn");
            GameplayTag freeze = GameplayTagManager.RequestTag("Effect.Freeze");
            
            GameplayTagContainer container = new GameplayTagContainer();
            
            bool burnEventFired = false;
            
            // "화상(Burn)" 태그에만 리스너를 달아둠
            container.RegisterImplicitTagEvent(burn, (ev) => {
                burnEventFired = true;
            });

            // 빙결 태그를 추가해봄
            container.AddTag(freeze);
            Assert.IsFalse(burnEventFired, "구독하지 않은 태그(Freeze)가 추가되었는데 이벤트가 반응하면 안 됩니다.");

            // 화상 태그를 추가해봄
            container.AddTag(burn);
            Assert.IsTrue(burnEventFired, "구독한 태그(Burn)가 추가되었을 때만 이벤트가 반응해야 합니다.");
        }

        // 15. RemoveTagAll 시 이벤트 동작 테스트
        [Test]
        public void Test_Container_Events_RemoveTagAll()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Status.Poison");
            GameplayTag poison = GameplayTagManager.RequestTag("Status.Poison");
            
            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(poison);
            container.AddTag(poison);
            container.AddTag(poison); // 카운트 3

            int unregisteredEvents = 0;
            container.OnExplicitCountChanged += (ev) => {
                if (ev.IsUnregistered) unregisteredEvents++;
            };

            // 3개를 한꺼번에 지워도 IsUnregistered 이벤트는 딱 한 번만 발생해야 함
            container.RemoveTagAll(poison);
            
            Assert.AreEqual(1, unregisteredEvents, "RemoveTagAll 시 IsUnregistered 이벤트는 한 번만 발생해야 합니다.");
            Assert.AreEqual(0, container.CountTagExact(poison));
        }

        // 16. 유니크 추가 시 이벤트 동작 테스트
        [Test]
        public void Test_Container_Events_AddTagUnique()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Status.Shield");
            GameplayTag shield = GameplayTagManager.RequestTag("Status.Shield");
            
            GameplayTagContainer container = new GameplayTagContainer();
            int increaseEvents = 0;

            container.OnExplicitCountChanged += (ev) => {
                if (ev.IsAdded) increaseEvents++;
            };

            container.AddTagUnique(shield); // 성공: Added 발생
            container.AddTagUnique(shield); // 실패: 이벤트 발생 안 함
            
            Assert.AreEqual(1, increaseEvents, "AddTagUnique 실패 시 이벤트를 발생시켜선 안 됩니다.");
        }

        // 17. ClearTags 시 완벽한 해제 이벤트(Unregistered) 발생 테스트
        [Test]
        public void Test_Container_Events_ClearTags()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("A.B");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("C.D");
            GameplayTag tagB = GameplayTagManager.RequestTag("A.B");
            GameplayTag tagD = GameplayTagManager.RequestTag("C.D");
            
            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(tagB);
            container.AddTag(tagD);

            int unregisteredCount = 0;
            container.OnExplicitCountChanged += (ev) => {
                Debug.Log($"Tag: {ev.Tag}, Count: {ev.NewCount}, IsUnregistered: {ev.IsUnregistered}");
                if (ev.IsUnregistered) unregisteredCount++;
            };

            container.ClearTags();
            
            Assert.AreEqual(0, container.TotalTagCount);
            Assert.AreEqual(0, container.CountTag(tagB));
            Assert.AreEqual(0, container.CountTag(tagD));

            Assert.AreEqual(2, unregisteredCount, "ClearTags 시 내부의 모든 태그에 대해 각각 Unregistered 이벤트가 발생해야 합니다.");
        }
    }
}
