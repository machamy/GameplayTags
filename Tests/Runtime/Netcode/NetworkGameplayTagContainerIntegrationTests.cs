#if GAMEPLAYTAGS_USE_NETCODE
using System.Collections;
using Machamy.GameplayTags.Runtime;
using Machamy.GameplayTags.Runtime.Netcode;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Machamy.GameplayTags.Tests.Netcode
{
    public sealed class NetworkGameplayTagTestBehaviour : NetworkBehaviour
    {
        public readonly NetworkGameplayTagContainer Tags = new();
        public readonly NetworkVariable<GameplayTagReference> TagReference = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void ServerSetReference(GameplayTag tag)
        {
            TagReference.Value = tag;
        }
    }

    public class NetworkGameplayTagContainerIntegrationTests : NetcodeIntegrationTest
    {
        private const string ParentTagName = "Test.Netcode.Status";
        private const string ChildTagName = "Test.Netcode.Status.Burning";
        private GameObject _prefab;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameplayTagManager.InitializeIfNeeded();
            GameplayTagManager.ClearAllDynamicTags();
            GameplayTagManager.RegisterDynamicTagIncludeParentTags(ChildTagName);

            _prefab = CreateNetworkPrefab<NetworkGameplayTagTestBehaviour>("NetworkGameplayTag Test Object");
            yield return StartServerAndClients();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameplayTagManager.ClearAllDynamicTags();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ServerTagMutationsReplicateToClient()
        {
            GameplayTag childTag = GameplayTagManager.RequestTag(ChildTagName);
            GameplayTag parentTag = GameplayTagManager.RequestTag(ParentTagName);
            NetworkObject serverObject = SpawnNetworkObject(_prefab);
            var serverBehaviour = serverObject.GetComponent<NetworkGameplayTagTestBehaviour>();

            NetworkGameplayTagTestBehaviour clientBehaviour = null;
            yield return WaitForSpawnOnClient<NetworkGameplayTagTestBehaviour>(
                ClientNetworkManager,
                serverObject,
                spawned => clientBehaviour = spawned);

            serverBehaviour.Tags.AddTag(childTag);
            serverBehaviour.Tags.AddTag(childTag);

            yield return WaitForConditionOrTimeout(
                () => clientBehaviour.Tags.CountTagExact(childTag) == 2 &&
                      clientBehaviour.Tags.CountTag(parentTag) == 2,
                "Timed out waiting for replicated tag add.");

            serverBehaviour.Tags.RemoveTagOnce(childTag);
            yield return WaitForConditionOrTimeout(
                () => clientBehaviour.Tags.CountTagExact(childTag) == 1 &&
                      clientBehaviour.Tags.CountTag(parentTag) == 1,
                "Timed out waiting for replicated tag remove.");

            serverBehaviour.Tags.ClearTags();
            yield return WaitForConditionOrTimeout(
                () => clientBehaviour.Tags.TotalTagCount == 0 &&
                      !clientBehaviour.Tags.HasTag(parentTag),
                "Timed out waiting for replicated tag clear.");
        }

        [UnityTest]
        public IEnumerator LateJoiningClientReceivesFullTagSnapshot()
        {
            GameplayTag childTag = GameplayTagManager.RequestTag(ChildTagName);
            NetworkObject serverObject = SpawnNetworkObject(_prefab);
            var serverBehaviour = serverObject.GetComponent<NetworkGameplayTagTestBehaviour>();
            serverBehaviour.Tags.AddTag(childTag);
            serverBehaviour.Tags.AddTag(childTag);

            yield return WaitForConditionOrTimeout(
                () => TryGetClientBehaviour(
                          ClientNetworkManager,
                          serverObject,
                          out NetworkGameplayTagTestBehaviour firstClientBehaviour) &&
                      firstClientBehaviour.Tags.CountTagExact(childTag) == 2,
                "Timed out waiting for first client tag snapshot.");

            yield return CreateAndStartClient();
            NetworkManager lateClient = ClientNetworkManagers[1];

            yield return WaitForConditionOrTimeout(
                () => TryGetClientBehaviour(
                          lateClient,
                          serverObject,
                          out NetworkGameplayTagTestBehaviour lateClientBehaviour) &&
                      lateClientBehaviour.Tags.CountTagExact(childTag) == 2,
                "Timed out waiting for late-joining client tag snapshot.");
        }

        [UnityTest]
        public IEnumerator GameplayTagReferenceReplicatesThroughNetworkVariable()
        {
            GameplayTag childTag = GameplayTagManager.RequestTag(ChildTagName);
            NetworkObject serverObject = SpawnNetworkObject(_prefab);
            var serverBehaviour = serverObject.GetComponent<NetworkGameplayTagTestBehaviour>();

            NetworkGameplayTagTestBehaviour clientBehaviour = null;
            yield return WaitForSpawnOnClient<NetworkGameplayTagTestBehaviour>(
                ClientNetworkManager,
                serverObject,
                spawned => clientBehaviour = spawned);

            serverBehaviour.ServerSetReference(childTag);
            yield return WaitForConditionOrTimeout(
                () => ((GameplayTag)clientBehaviour.TagReference.Value).Equals(childTag),
                "Timed out waiting for GameplayTagReference replication.");
        }
    }
}
#endif
