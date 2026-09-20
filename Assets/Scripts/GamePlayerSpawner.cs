using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GamePlayerSpawner : MonoBehaviour
{
    [Header("Player Spawning")]
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private int nextSpawnIndex = 0;
    private readonly Dictionary<int, NetworkObject> spawnedPlayers = new Dictionary<int, NetworkObject>();

    private void Awake()
    {
        if (InstanceFinder.NetworkManager != null)
        {
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
            InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }
    }

    private void OnDestroy()
    {
        if (InstanceFinder.NetworkManager != null)
        {
            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;

            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }
    }

    private void Start()
    {
        if (!InstanceFinder.IsServerStarted) return;

        // Only spawn for connections that are ALREADY confirmed to have loaded this scene.
        // Any clients still loading will trigger OnClientPresenceChangeEnd once their load finishes.
        Scene currentScene = gameObject.scene;
        foreach (NetworkConnection conn in InstanceFinder.ServerManager.Clients.Values)
        {
            if (conn != null && conn.IsActive && conn.Scenes.Contains(currentScene))
            {
                SpawnForConnection(conn);
            }
        }
    }

    private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
    {
        if (!InstanceFinder.IsServerStarted) return;

        // When a connection finishes loading and is added to this scene, spawn their character
        if (args.Added && args.Scene == gameObject.scene)
        {
            SpawnForConnection(args.Connection);
        }
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            if (conn != null && spawnedPlayers.ContainsKey(conn.ClientId))
            {
                spawnedPlayers.Remove(conn.ClientId);
            }
        }
    }

    private void SpawnForConnection(NetworkConnection conn)
    {
        if (conn == null || !conn.IsActive) return;
        if (spawnedPlayers.ContainsKey(conn.ClientId)) return;

        if (playerPrefab == null)
        {
            Debug.LogError("[GamePlayerSpawner] Player prefab is not assigned!");
            return;
        }

        // Determine spawn location
        Transform spawn = (spawnPoints != null && spawnPoints.Length > 0)
            ? spawnPoints[nextSpawnIndex % spawnPoints.Length]
            : transform;
        nextSpawnIndex++;

        // Instantiate in the current scene
        NetworkObject playerObj = Instantiate(playerPrefab, spawn.position, spawn.rotation);

        // Explicitly spawn into this scene and assign ownership to this connection
        InstanceFinder.ServerManager.Spawn(playerObj, conn, gameObject.scene);

        spawnedPlayers[conn.ClientId] = playerObj;
        Debug.Log($"[GamePlayerSpawner] Spawned player for Client ID: {conn.ClientId} at {spawn.position}");

        // Rebuild observers for the new connection so they see all existing players
        InstanceFinder.ServerManager.Objects.RebuildObservers(conn);

        // Rebuild observers for the newly spawned player so existing players see this new one
        InstanceFinder.ServerManager.Objects.RebuildObservers(playerObj);
    }

#if UNITY_EDITOR
    public void SetEditorReferences(NetworkObject prefab, Transform[] spawns)
    {
        playerPrefab = prefab;
        spawnPoints = spawns;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
