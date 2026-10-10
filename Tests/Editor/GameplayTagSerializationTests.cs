using System;
using NUnit.Framework;
using UnityEngine;
using Machamy.GameplayTags.Runtime;

namespace Machamy.GameplayTags.Tests
{
    public class GameplayTagSerializationTests
    {
        [Serializable]
        private class GameplayTagHolder
        {
            public GameplayTag Tag;
        }

        [SetUp]
        public void SetUp()
        {
            GameplayTagManager.ReloadTags();
            GameplayTagManager.ClearAllDynamicTags();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.ClearAllDynamicTags();
        }

        // 1. JSON 직렬화/역직렬화 라운드 트립 (데이터 보존 확인)
        [Test]
        public void Test_Serialization_Json_RoundTrip()
        {
            // [준비] 태그 등록
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("SaveData.Gold");
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("SaveData.HP");

            GameplayTag goldTag = GameplayTagManager.RequestTag("SaveData.Gold");
            GameplayTag hpTag = GameplayTagManager.RequestTag("SaveData.HP");

            // [설정] 원본 컨테이너에 데이터 채우기
            GameplayTagContainer originalContainer = new GameplayTagContainer();
            originalContainer.AddTag(goldTag);
            originalContainer.AddTag(goldTag); // Gold: 2
            originalContainer.AddTag(hpTag);   // HP: 1

            // [실행 1] 직렬화 (메모리 -> JSON 문자열)
            // JsonUtility는 내부적으로 OnBeforeSerialize를 호출합니다.
            string json = JsonUtility.ToJson(originalContainer);

            // 디버그용: 실제 JSON이 어떻게 생겼는지 확인 가능
            // Debug.Log(json);

            // [실행 2] 역직렬화 (JSON 문자열 -> 메모리)
            // JsonUtility는 내부적으로 OnAfterDeserialize를 호출합니다.
            GameplayTagContainer loadedContainer = JsonUtility.FromJson<GameplayTagContainer>(json);

            // [검증] 데이터가 유지되었는지 확인
            Assert.AreEqual(2, loadedContainer.CountTagExact(goldTag), "Gold 태그 개수가 복구되어야 합니다.");
            Assert.AreEqual(1, loadedContainer.CountTagExact(hpTag), "HP 태그 개수가 복구되어야 합니다.");
            Assert.AreEqual(3, loadedContainer.TotalTagCount, "총 태그 개수가 일치해야 합니다.");
        }

        // 2. 직렬화 시 정렬(Sorting) 로직 테스트
        // (저장 파일의 변경 사항을 최소화하기 위해 보통 이름을 기준으로 정렬해서 저장함)
        [Test]
        public void Test_Serialization_IsDeterministic_Sorted()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Tag.Zebra");
            GameplayTagManager.RegisterDynamicTagChecked("Tag.Apple");

            GameplayTag zebra = GameplayTagManager.RequestTag("Tag.Zebra");
            GameplayTag apple = GameplayTagManager.RequestTag("Tag.Apple");

            GameplayTagContainer container = new GameplayTagContainer();

            // 순서를 뒤죽박죽으로 넣음 (Zebra 먼저)
            container.AddTag(zebra);
            container.AddTag(apple);

            // 직렬화 수행
            string json = JsonUtility.ToJson(container);

            // JSON 문자열에서 순서 확인 (Apple이 Zebra보다 먼저 나와야 함)
            int indexApple = json.IndexOf("Tag.Apple");
            int indexZebra = json.IndexOf("Tag.Zebra");

            Assert.AreNotEqual(-1, indexApple);
            Assert.AreNotEqual(-1, indexZebra);
            Assert.Less(indexApple, indexZebra, "직렬화 시 태그 이름 알파벳 순으로 정렬되어야 합니다. (Apple < Zebra)");
        }

        // 3. 수동(Manual) 직렬화 콜백 호출 테스트
        // JsonUtility 없이 로직 자체만 단위 테스트하고 싶을 때
        [Test]
        public void Test_Serialization_Manual_Callback()
        {
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Manual.Test");
            GameplayTag tag = GameplayTagManager.RequestTag("Manual.Test");

            GameplayTagContainer container = new GameplayTagContainer();
            container.AddTag(tag);

            // 1. 직렬화 전(BeforeSerialize) 직접 호출
            // 이 시점에 내부 리스트(serializedTags)에 데이터가 들어가야 함
            container.OnBeforeSerialize();

            // 2. 데이터를 비움 (메모리 상의 Dictionary를 날림)
            container.ClearTags();
            Assert.AreEqual(0, container.TotalTagCount);

            // 3. 역직렬화 후(AfterDeserialize) 직접 호출
            // 내부 리스트에 있던 데이터를 다시 Dictionary로 복구해야 함
            container.OnAfterDeserialize();

            // 4. 복구 확인
            Assert.AreEqual(1, container.CountTagExact(tag), "수동 콜백 호출로 데이터가 복구되어야 합니다.");
        }

        // 4. (심화) 태그가 등록되지 않은 상태에서의 로드 테스트
        // 주의: 현재 작성해주신 컨테이너 코드 로직상, 매니저에 태그가 없으면 로드되지 않고 무시됩니다.
        // 이 테스트는 그 동작(무시됨)이 의도대로인지 확인합니다.
        [Test]
        public void Test_Deserialization_With_Unregistered_Tags()
        {
            // 1. 태그를 등록하고 데이터 생성
            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Ghost.Tag");
            GameplayTag ghost = GameplayTagManager.RequestTag("Ghost.Tag");

            GameplayTagContainer original = new GameplayTagContainer();
            original.AddTag(ghost);
            string json = JsonUtility.ToJson(original);

            // 2. 시스템 리셋 (태그 정의를 다 날림)
            GameplayTagManager.ReloadTags();
            GameplayTagManager.ClearAllDynamicTags();
            // 이제 "Ghost.Tag"는 매니저에 존재하지 않음

            // 3. 로드 시도
            GameplayTagContainer loaded = JsonUtility.FromJson<GameplayTagContainer>(json);

            // 4. 검증
            // 작성하신 코드의 EnsureDeserialized에서 tag.IsValid를 체크하므로,
            // 등록되지 않은 태그는 RequestTag 결과가 Invalid라 로드되지 않아야 합니다.
            Assert.AreEqual(0, loaded.TotalTagCount, "등록되지 않은 태그는 로드 시 무시되어야 합니다.");
        }

        [Test]
        public void Test_InvalidTag_RoundTrip_PreservesRawName()
        {
            GameplayTagHolder original = new GameplayTagHolder
            {
                Tag = GameplayTagManager.RequestTag("Ghost.Tag")
            };

            string json = JsonUtility.ToJson(original);
            GameplayTagHolder loaded = JsonUtility.FromJson<GameplayTagHolder>(json);

            Assert.IsFalse(loaded.Tag.IsValid, "로드 후에도 invalid 상태가 유지되어야 합니다.");
            Assert.AreEqual("Ghost.Tag", loaded.Tag.RawTagName, "invalid 태그 이름이 None으로 사라지면 안 됩니다.");
            Assert.AreEqual("Ghost.Tag", loaded.Tag.TagName, "표시 이름도 원래 invalid 이름을 유지해야 합니다.");
        }

        [Test]
        public void Test_InvalidTag_CanBecomeValid_AfterTagsReload()
        {
            GameplayTagHolder loaded = JsonUtility.FromJson<GameplayTagHolder>("{\"Tag\":{\"rawTagName\":\"Late.Tag\"}}");

            Assert.IsFalse(loaded.Tag.IsValid, "등록 전에는 invalid여야 합니다.");
            Assert.AreEqual("Late.Tag", loaded.Tag.TagName);

            GameplayTagManager.RegisterDynamicTagIncludeParentTags("Late.Tag");

            Assert.IsTrue(loaded.Tag.IsValid, "태그가 나중에 등록되면 다시 valid로 해석되어야 합니다.");
            Assert.AreEqual("Late.Tag", loaded.Tag.TagName);
        }
    }
}
