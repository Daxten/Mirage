using UnityEngine;
using UnityEditor;
using Mirror.KCP;

namespace Mirror
{

    public static class NetworkMenu
    {
        // Start is called before the first frame update
        [MenuItem("GameObject/Network/NetworkManager", priority = 7)]
        public static GameObject CreateNetworkManager()
        {
            var go = new GameObject("NetworkManager", typeof(KcpTransport), typeof(NetworkSceneManager), typeof(NetworkClient), typeof(NetworkServer), typeof(NetworkManager), typeof(PlayerSpawner), typeof(NetworkManagerHud));

            KcpTransport transport = go.GetComponent<KcpTransport>();
            NetworkSceneManager nsm = go.GetComponent<NetworkSceneManager>();

            NetworkClient networkClient = go.GetComponent<NetworkClient>();
            networkClient.Transport = transport;

            NetworkServer networkServer = go.GetComponent<NetworkServer>();
            networkServer.transport = transport;

            NetworkManager networkManager = go.GetComponent<NetworkManager>();
            networkManager.client = networkClient;
            networkManager.server = networkServer;

            PlayerSpawner playerSpawner = go.GetComponent<PlayerSpawner>();
            playerSpawner.client = networkClient;
            playerSpawner.server = networkServer;
            playerSpawner.sceneManager = nsm;

            nsm.client = networkClient;
            nsm.server = networkServer;
            return go;
        }
    }
}