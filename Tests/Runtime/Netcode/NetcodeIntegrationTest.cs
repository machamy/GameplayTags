using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Machamy.GameplayTags.Tests.Netcode
{
    public abstract class NetcodeIntegrationTest
    {
        private const float DefaultTimeoutSeconds = 8f;
        private static readonly FieldInfo GlobalObjectIdHashField =
            typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo IsSceneObjectProperty =
            typeof(NetworkObject).GetProperty("IsSceneObject", BindingFlags.Instance | BindingFlags.Public);

        private readonly List<GameObject> _runtimePrefabs = new();
        private readonly List<NetworkManager> _clients = new();
        private int _nextPrefabHash = 0x51A7E57;

        protected NetworkManager ServerNetworkManager { get; private set; }
        protected IReadOnlyList<NetworkManager> ClientNetworkManagers => _clients;
        protected NetworkManager ClientNetworkManager => _clients.Count > 0 ? _clients[0] : null;
        protected ushort Port { get; private set; }

        protected IEnumerator StartServerAndClients(int clientCount = 1)
        {
            Assert.GreaterOrEqual(clientCount, 0);

            Port = AllocatePort();
            ServerNetworkManager = CreateNetworkManager("Test Server", Port, true);
            RegisterPrefabs(ServerNetworkManager);
            ServerNetworkManager.SetSingleton();
            Assert.IsTrue(ServerNetworkManager.StartServer(), "Failed to start test server.");

            for (int i = 0; i < clientCount; i++)
            {
                yield return CreateAndStartClient();
            }
        }

        protected IEnumerator CreateAndStartClient()
        {
            Assert.NotNull(ServerNetworkManager, "Start the server before creating clients.");

            NetworkManager client = CreateNetworkManager($"Test Client {_clients.Count}", Port, false);
            RegisterPrefabs(client);
            _clients.Add(client);

            Assert.IsTrue(client.StartClient(), "Failed to start test client.");
            yield return WaitForConditionOrTimeout(
                () => client.IsConnectedClient && ServerNetworkManager.ConnectedClientsIds.Contains(client.LocalClientId),
                "Timed out waiting for test client connection.");
        }

        protected GameObject CreateNetworkPrefab<TBehaviour>(string name) where TBehaviour : NetworkBehaviour
        {
            var prefab = new GameObject(name);
            var networkObject = prefab.AddComponent<NetworkObject>();
            GlobalObjectIdHashField?.SetValue(networkObject, unchecked((uint)_nextPrefabHash++));
            IsSceneObjectProperty?.SetValue(networkObject, false);
            prefab.AddComponent<TBehaviour>();
            Object.DontDestroyOnLoad(prefab);

            _runtimePrefabs.Add(prefab);
            if (ServerNetworkManager)
            {
                RegisterPrefab(ServerNetworkManager, prefab);
            }

            foreach (NetworkManager client in _clients)
            {
                RegisterPrefab(client, prefab);
            }

            return prefab;
        }

        protected NetworkObject SpawnNetworkObject(GameObject prefab)
        {
            Assert.NotNull(ServerNetworkManager, "Start the server before spawning network objects.");
            Assert.NotNull(prefab, "Cannot spawn a null prefab.");

            ServerNetworkManager.SetSingleton();
            var instance = Object.Instantiate(prefab);
            instance.name = $"{prefab.name} Instance";

            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            networkObject.Spawn();
            return networkObject;
        }

        protected bool TryGetClientBehaviour<TBehaviour>(
            NetworkManager client,
            NetworkObject serverObject,
            out TBehaviour behaviour) where TBehaviour : NetworkBehaviour
        {
            behaviour = null;
            if (client == null || serverObject == null)
            {
                return false;
            }

            if (!client.SpawnManager.SpawnedObjects.TryGetValue(serverObject.NetworkObjectId, out NetworkObject clientObject))
            {
                return false;
            }

            return clientObject.TryGetComponent(out behaviour);
        }

        protected IEnumerator WaitForSpawnOnClient<TBehaviour>(
            NetworkManager client,
            NetworkObject serverObject,
            Action<TBehaviour> onSpawned = null) where TBehaviour : NetworkBehaviour
        {
            TBehaviour spawned = null;
            yield return WaitForConditionOrTimeout(
                () => TryGetClientBehaviour(client, serverObject, out spawned),
                $"Timed out waiting for {typeof(TBehaviour).Name} to spawn on client.");

            onSpawned?.Invoke(spawned);
        }

        protected static IEnumerator WaitForConditionOrTimeout(
            Func<bool> condition,
            string timeoutMessage,
            float timeoutSeconds = DefaultTimeoutSeconds)
        {
            float startTime = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - startTime > timeoutSeconds)
                {
                    Assert.Fail(timeoutMessage);
                }

                yield return null;
            }
        }

        [UnityEngine.TestTools.UnityTearDown]
        public IEnumerator TearDownNetcode()
        {
            if (ServerNetworkManager != null && ServerNetworkManager.IsListening)
            {
                ServerNetworkManager.Shutdown(true);
            }

            foreach (NetworkManager client in _clients)
            {
                if (client != null && client.IsListening)
                {
                    client.Shutdown(true);
                }
            }

            yield return WaitForConditionOrTimeout(
                () => IsShutdownComplete(ServerNetworkManager) && _clients.All(IsShutdownComplete),
                "Timed out waiting for test NetworkManagers to shut down.");

            foreach (NetworkManager client in _clients)
            {
                if (client)
                {
                    Object.DestroyImmediate(client.gameObject);
                }
            }

            _clients.Clear();

            if (ServerNetworkManager)
            {
                Object.DestroyImmediate(ServerNetworkManager.gameObject);
                ServerNetworkManager = null;
            }

            foreach (GameObject prefab in _runtimePrefabs)
            {
                if (prefab)
                {
                    Object.DestroyImmediate(prefab);
                }
            }

            _runtimePrefabs.Clear();
        }

        private NetworkManager CreateNetworkManager(string name, ushort port, bool server)
        {
            var networkManagerObject = new GameObject(name);
            var transport = networkManagerObject.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, server ? "127.0.0.1" : null);

            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ForceSamePrefabs = false,
                AutoSpawnPlayerPrefabClientSide = false,
                TickRate = 30
            };

            Object.DontDestroyOnLoad(networkManagerObject);
            return networkManager;
        }

        private static bool IsShutdownComplete(NetworkManager networkManager)
        {
            return networkManager == null || (!networkManager.ShutdownInProgress && !networkManager.IsListening);
        }

        private void RegisterPrefabs(NetworkManager networkManager)
        {
            foreach (GameObject prefab in _runtimePrefabs)
            {
                RegisterPrefab(networkManager, prefab);
            }
        }

        private static void RegisterPrefab(NetworkManager networkManager, GameObject prefab)
        {
            if (networkManager == null || prefab == null || networkManager.IsListening)
            {
                return;
            }

            if (!networkManager.NetworkConfig.Prefabs.Contains(prefab))
            {
                networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            }
        }

        private static ushort AllocatePort()
        {
            return (ushort)UnityEngine.Random.Range(23000, 32000);
        }
    }
}
