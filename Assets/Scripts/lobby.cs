using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting.UTP;

public class lobby : MonoBehaviour
{
    public static lobby Instance { get; private set; }

    public static event Action OnAuthenticated;
    public bool IsAuthenticated => UnityServices.State == ServicesInitializationState.Initialized 
                                   && AuthenticationService.Instance != null 
                                   && AuthenticationService.Instance.IsSignedIn;

    [Header("Lobby Settings")]
    [SerializeField] private string defaultGameplayScene = "SampleScene";

    // Tracks the lobby this local client/host is currently in
    private Lobby joinedLobby;
    private Lobby hostLobby;

    // Timers for UGS Lobby requirements
    private float heartbeatTimer = 15f;
    private float lobbyPollTimer = 1.5f;

    private const string KEY_RELAY_JOIN_CODE = "RelayJoinCode";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private async void Start()
    {
        await Authenticate();
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPollForUpdates();
    }

    // ==========================================
    // 1. AUTHENTICATION (With ParrelSync Support)
    // ==========================================
    private async Task Authenticate()
    {
        try
        {
            InitializationOptions options = new InitializationOptions();

#if UNITY_EDITOR
            // Isolate authentication profiles for ParrelSync clones so they don't overwrite each other
            if (Application.dataPath.Contains("_clone"))
            {
                string parentFolder = Directory.GetParent(Application.dataPath).Name;
                string cleanProfile = ("Clone_" + parentFolder).Replace("-", "_").Replace(".", "_");
                if (cleanProfile.Length > 28) cleanProfile = cleanProfile.Substring(0, 28);
                options.SetProfile(cleanProfile);
                Debug.Log($"[Lobby] ParrelSync Clone detected! Using profile: {cleanProfile}");
            }
            else
            {
                options.SetProfile("Primary");
            }
#endif

            await UnityServices.InitializeAsync(options);

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[Lobby] Signed in successfully! Player ID: {AuthenticationService.Instance.PlayerId}");
            }

            OnAuthenticated?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Authentication failed: {e.Message}");
        }
    }

    // ==========================================
    // 2. CREATE LOBBY + RELAY (HOST)
    // ==========================================
    public async Task<string> CreateLobby(string lobbyName, int maxPlayers = 4, bool isPrivate = false)
    {
        try
        {
            // Step 1: Create Relay Allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Step 2: Configure FishNet UnityTransport with Host Relay Data
            UnityTransport unityTransport = InstanceFinder.NetworkManager.GetComponent<UnityTransport>();
            unityTransport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            // Step 3: Create the UGS Lobby and embed the Relay Join Code inside Member Data
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        KEY_RELAY_JOIN_CODE,
                        new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                    }
                }
            };

            Lobby createdLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            hostLobby = createdLobby;
            joinedLobby = createdLobby;

            // Step 4: Start FishNet as Host (Server + Client)
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();

            Debug.Log($"[Lobby] Created Lobby: {createdLobby.Name} | LobbyCode: {createdLobby.LobbyCode} | Relay: {relayJoinCode}");
            return isPrivate ? createdLobby.LobbyCode : createdLobby.Id;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Create Lobby failed: {e.Message}");
            return null;
        }
    }

    // ==========================================
    // 3. JOIN LOBBY BY ID (From Browser List)
    // ==========================================
    public async Task<bool> JoinLobbyById(string lobbyId)
    {
        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;
            return await JoinRelay(relayJoinCode);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Join Lobby by ID failed: {e.Message}");
            return false;
        }
    }

    // ==========================================
    // 4. JOIN LOBBY BY CODE (Private Lobby)
    // ==========================================
    public async Task<bool> JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;
            return await JoinRelay(relayJoinCode);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Join Lobby by Code failed: {e.Message}");
            return false;
        }
    }

    // ==========================================
    // 5. CONNECT VIA RELAY (FishNet Client)
    // ==========================================
    public async Task<bool> JoinRelay(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport unityTransport = InstanceFinder.NetworkManager.GetComponent<UnityTransport>();
            unityTransport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            InstanceFinder.ClientManager.StartConnection();
            Debug.Log($"[Lobby] Connected to Relay with Code: {joinCode}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Join Relay failed: {e.Message}");
            return false;
        }
    }

    // ==========================================
    // 6. GET LOBBY LIST (Server Browser)
    // ==========================================
    public async Task<List<Lobby>> GetLobbiesList()
    {
        try
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("[Lobby] Cannot query lobbies: not authenticated yet.");
                return null;
            }

            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 20,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            return response.Results;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Query failed: {e.Message}");
            return null;
        }
    }

    // ==========================================
    // 7. HEARTBEAT & POLLING
    // ==========================================
    private async void HandleLobbyHeartbeat()
    {
        if (hostLobby == null) return;

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer <= 0f)
        {
            heartbeatTimer = 15f;
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Heartbeat failed: {e.Message}");
            }
        }
    }

    private async void HandleLobbyPollForUpdates()
    {
        if (joinedLobby == null) return;

        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer <= 0f)
        {
            lobbyPollTimer = 2f;
            try
            {
                joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Polling lobby failed: {e.Message}");
            }
        }
    }

    // ==========================================
    // 8. LEAVE LOBBY
    // ==========================================
    public async void LeaveLobby()
    {
        try
        {
            if (joinedLobby != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, playerId);
                joinedLobby = null;
                hostLobby = null;
            }

            if (InstanceFinder.NetworkManager != null)
            {
                if (InstanceFinder.IsServerStarted) InstanceFinder.ServerManager.StopConnection(true);
                else if (InstanceFinder.IsClientStarted) InstanceFinder.ClientManager.StopConnection();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Leave lobby error: {e.Message}");
        }
    }

    // ==========================================
    // 9. START GAMEPLAY SCENE (Host Only)
    // ==========================================
    public void StartGame()
    {
        if (!InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning("[Lobby] Only the host can start the game!");
            return;
        }

        SceneLoadData sld = new SceneLoadData(defaultGameplayScene)
        {
            ReplaceScenes = ReplaceOption.All
        };

        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }
}
