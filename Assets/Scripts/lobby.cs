using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using FishNet.Transporting.UTP;

public class lobby : MonoBehaviour
{
    public static lobby Instance { get; private set; }

    public static event Action OnAuthenticated;
    public static event Action OnLobbyLeft;
    public static event Action<string> OnHostDisconnected;

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
    private float lobbyPollTimer = 2.5f;
    private bool isHeartbeatRunning = false;
    private bool isPollRunning = false;
    private bool isSubscribedToClientEvents = false;

    private CancellationTokenSource cancellationTokenSource;

    private const string KEY_RELAY_JOIN_CODE = "RelayJoinCode";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkEvents();
        CancelActiveTasks();
    }

    private async void Start()
    {
        SubscribeToNetworkEvents();
        await Authenticate();
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPollForUpdates();
    }

    private void OnApplicationQuit()
    {
        CancelActiveTasks();

        if (joinedLobby != null)
        {
            try
            {
                string lobbyId = joinedLobby.Id;
                string playerId = AuthenticationService.Instance?.PlayerId;
                bool isHost = (hostLobby != null || (playerId != null && joinedLobby.HostId == playerId));

                if (isHost)
                {
                    LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                }
                else if (!string.IsNullOrEmpty(playerId))
                {
                    LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Teardown on quit warning: {e.Message}");
            }
        }

        if (InstanceFinder.NetworkManager != null)
        {
            if (InstanceFinder.IsServerStarted) InstanceFinder.ServerManager.StopConnection(true);
            else if (InstanceFinder.IsClientStarted) InstanceFinder.ClientManager.StopConnection();
        }
    }

    // ==========================================
    // NETWORK EVENT HANDLING (FishNet)
    // ==========================================
    private void SubscribeToNetworkEvents()
    {
        if (isSubscribedToClientEvents) return;

        if (InstanceFinder.ClientManager != null)
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
            isSubscribedToClientEvents = true;
        }
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!isSubscribedToClientEvents) return;

        if (InstanceFinder.ClientManager != null)
        {
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
        }
        isSubscribedToClientEvents = false;
    }

    private void OnClientConnectionStateChanged(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            // If we are a client in a lobby and the host disconnected / shut down the server
            if (joinedLobby != null && hostLobby == null)
            {
                Debug.Log("[Lobby] Host connection stopped. Ejecting client back to lobby browser...");
                HandleHostDisconnectedOrClosed("Host disconnected from the lobby.");
            }
        }
    }

    private void HandleHostDisconnectedOrClosed(string reason)
    {
        CancelActiveTasks();
        joinedLobby = null;
        hostLobby = null;

        if (InstanceFinder.NetworkManager != null && InstanceFinder.IsClientStarted)
        {
            InstanceFinder.ClientManager.StopConnection();
        }

        OnHostDisconnected?.Invoke(reason);

        // If currently in gameplay scene, transition back to Lobby scene
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Lobby")
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
        }
    }

    private void CancelActiveTasks()
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
    }

    // ==========================================
    // 1. AUTHENTICATION & ANTI-GHOSTING PURGE
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

            // Anti-Ghosting Protocol: Purge any stale lobby reservations from previous abrupt quits
            await PurgeLingeringLobbies();

            OnAuthenticated?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Authentication failed: {e.Message}");
        }
    }

    private async Task PurgeLingeringLobbies()
    {
        try
        {
            List<string> joinedLobbyIds = await LobbyService.Instance.GetJoinedLobbiesAsync();
            if (joinedLobbyIds != null && joinedLobbyIds.Count > 0)
            {
                Debug.Log($"[Lobby] Found {joinedLobbyIds.Count} lingering lobby session(s). Purging ghost memberships...");
                string playerId = AuthenticationService.Instance.PlayerId;

                foreach (string lobbyId in joinedLobbyIds)
                {
                    try
                    {
                        // If we were host, delete the entire zombie lobby
                        await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                        Debug.Log($"[Lobby] Successfully deleted lingering host lobby: {lobbyId}");
                    }
                    catch (LobbyServiceException)
                    {
                        // If not host (Forbidden), remove ourselves as ghost player
                        try
                        {
                            await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                            Debug.Log($"[Lobby] Successfully removed ghost player from lobby: {lobbyId}");
                        }
                        catch (Exception innerEx)
                        {
                            Debug.LogWarning($"[Lobby] Could not remove player from lingering lobby {lobbyId}: {innerEx.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Lobby] Note: Could not query joined lobbies on startup: {ex.Message}");
        }
    }

    // ==========================================
    // 2. CREATE LOBBY + RELAY (HOST)
    // ==========================================
    public async Task<string> CreateLobby(string lobbyName, int maxPlayers = 4, bool isPrivate = false)
    {
        try
        {
            CancelActiveTasks();
            cancellationTokenSource = new CancellationTokenSource();

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
            heartbeatTimer = 15f;
            lobbyPollTimer = 2.5f;

            // Step 4: Start FishNet as Host (Server + Client)
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();

            Debug.Log($"[Lobby] Created Lobby: {createdLobby.Name} | LobbyCode: {createdLobby.LobbyCode} | Relay: {relayJoinCode}");
            return isPrivate ? createdLobby.LobbyCode : createdLobby.Id;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[Lobby] Create Lobby UGS error ({e.Reason}): {e.Message}");
            return null;
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
            CancelActiveTasks();
            cancellationTokenSource = new CancellationTokenSource();

            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            hostLobby = null;
            lobbyPollTimer = 2.5f;

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;
            return await JoinRelay(relayJoinCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[Lobby] Join Lobby by ID UGS error ({e.Reason}): {e.Message}");
            return false;
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
            CancelActiveTasks();
            cancellationTokenSource = new CancellationTokenSource();

            joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            hostLobby = null;
            lobbyPollTimer = 2.5f;

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;
            return await JoinRelay(relayJoinCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[Lobby] Join Lobby by Code UGS error ({e.Reason}): {e.Message}");
            return false;
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
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[Lobby] Query UGS error ({e.Reason}): {e.Message}");
            return null;
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
        if (hostLobby == null || isHeartbeatRunning) return;

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer <= 0f)
        {
            heartbeatTimer = 15f;
            isHeartbeatRunning = true;
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"[Lobby] Heartbeat UGS error ({e.Reason}): {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Heartbeat failed: {e.Message}");
            }
            finally
            {
                isHeartbeatRunning = false;
            }
        }
    }

    private async void HandleLobbyPollForUpdates()
    {
        if (joinedLobby == null || isPollRunning) return;

        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer <= 0f)
        {
            lobbyPollTimer = 2.5f;
            isPollRunning = true;
            try
            {
                joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.LobbyNotFound || e.ErrorCode == 404)
                {
                    Debug.LogWarning("[Lobby] Lobby no longer exists on UGS cloud. Host may have closed it.");
                    HandleHostDisconnectedOrClosed("The lobby has been deleted by the host.");
                }
                else
                {
                    Debug.LogWarning($"[Lobby] Polling lobby UGS error ({e.Reason}): {e.Message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Polling lobby failed: {e.Message}");
            }
            finally
            {
                isPollRunning = false;
            }
        }
    }

    // ==========================================
    // 8. LEAVE LOBBY
    // ==========================================
    public async Task LeaveLobby()
    {
        CancelActiveTasks();

        try
        {
            if (joinedLobby != null)
            {
                string lobbyId = joinedLobby.Id;
                string playerId = AuthenticationService.Instance?.PlayerId;
                bool isHost = (hostLobby != null || (playerId != null && joinedLobby.HostId == playerId));

                if (isHost)
                {
                    Debug.Log($"[Lobby] Host is leaving. Deleting lobby {lobbyId} from UGS cloud...");
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                }
                else if (!string.IsNullOrEmpty(playerId))
                {
                    Debug.Log($"[Lobby] Client is leaving lobby {lobbyId}...");
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"[Lobby] Leave lobby UGS error ({e.Reason}): {e.Message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Leave lobby unexpected error: {e.Message}");
        }
        finally
        {
            joinedLobby = null;
            hostLobby = null;

            if (InstanceFinder.NetworkManager != null)
            {
                if (InstanceFinder.IsServerStarted) InstanceFinder.ServerManager.StopConnection(true);
                else if (InstanceFinder.IsClientStarted) InstanceFinder.ClientManager.StopConnection();
            }

            OnLobbyLeft?.Invoke();
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
