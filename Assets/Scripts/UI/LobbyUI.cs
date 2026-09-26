using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;

public class LobbyUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject lobbyBrowserPanel;
    [SerializeField] private GameObject insideRoomPanel;

    [Header("Create Room Inputs")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Toggle privateToggle;
    [SerializeField] private Button createRoomButton;

    [Header("Join By Code Inputs")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinByCodeButton;

    [Header("Lobby List View")]
    [SerializeField] private Transform lobbyListContainer; // Parent with VerticalLayoutGroup
    [SerializeField] private LobbyItemCard lobbyCardPrefab; // The Card prefab
    [SerializeField] private Button refreshListButton;

    [Header("Inside Room UI")]
    [SerializeField] private TextMeshProUGUI roomTitleText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText; // Shows join code for private rooms
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveRoomButton;

    private void OnEnable()
    {
        lobby.OnAuthenticated += OnUserAuthenticated;
        lobby.OnLobbyLeft += OnLobbyLeftHandler;
        lobby.OnHostDisconnected += OnHostDisconnectedHandler;
    }

    private void OnDisable()
    {
        lobby.OnAuthenticated -= OnUserAuthenticated;
        lobby.OnLobbyLeft -= OnLobbyLeftHandler;
        lobby.OnHostDisconnected -= OnHostDisconnectedHandler;
    }

    private void Start()
    {
        // Hook up button listeners
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);

        if (joinByCodeButton != null)
            joinByCodeButton.onClick.AddListener(OnJoinByCodeClicked);

        if (refreshListButton != null)
            refreshListButton.onClick.AddListener(RefreshLobbyList);

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);

        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);

        if (lobby.Instance != null && lobby.Instance.IsAuthenticated)
        {
            ShowBrowserPanel();
        }
        else
        {
            // If panels exist, show browser in waiting state
            if (lobbyBrowserPanel != null) lobbyBrowserPanel.SetActive(true);
            if (insideRoomPanel != null) insideRoomPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);

        if (joinByCodeButton != null)
            joinByCodeButton.onClick.RemoveListener(OnJoinByCodeClicked);

        if (refreshListButton != null)
            refreshListButton.onClick.RemoveListener(RefreshLobbyList);

        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnStartGameClicked);

        if (leaveRoomButton != null)
            leaveRoomButton.onClick.RemoveListener(OnLeaveRoomClicked);
    }

    private void OnUserAuthenticated()
    {
        ShowBrowserPanel();
    }

    private void OnLobbyLeftHandler()
    {
        ShowBrowserPanel();
    }

    private void OnHostDisconnectedHandler(string reason)
    {
        Debug.LogWarning($"[LobbyUI] Ejected from room: {reason}");
        ShowBrowserPanel();
    }

    private void ShowBrowserPanel()
    {
        if (lobbyBrowserPanel != null) lobbyBrowserPanel.SetActive(true);
        if (insideRoomPanel != null) insideRoomPanel.SetActive(false);

        RefreshLobbyList();
    }

    private void ShowInsideRoomPanel(string roomName, string code = "")
    {
        if (lobbyBrowserPanel != null) lobbyBrowserPanel.SetActive(false);
        if (insideRoomPanel != null) insideRoomPanel.SetActive(true);

        if (roomTitleText != null) roomTitleText.text = roomName;
        if (lobbyCodeText != null)
        {
            lobbyCodeText.gameObject.SetActive(!string.IsNullOrEmpty(code));
            lobbyCodeText.text = $"Room Code: {code}";
        }

        // Only the host should see the "Start Game" button
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(FishNet.InstanceFinder.IsServerStarted);
        }
    }

    // ==========================================
    // ACTIONS
    // ==========================================
    public async void RefreshLobbyList()
    {
        if (lobby.Instance == null || !lobby.Instance.IsAuthenticated)
        {
            return;
        }

        // Clear existing cards
        if (lobbyListContainer != null)
        {
            foreach (Transform child in lobbyListContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // Fetch lobbies
        List<Lobby> lobbies = await lobby.Instance.GetLobbiesList();
        if (lobbies == null) return;

        // Spawn a card for each active lobby
        if (lobbyCardPrefab != null && lobbyListContainer != null)
        {
            foreach (Lobby item in lobbies)
            {
                LobbyItemCard card = Instantiate(lobbyCardPrefab, lobbyListContainer);
                card.Setup(item, OnJoinLobbyCardClicked);
            }
        }
    }

    private async void OnCreateRoomClicked()
    {
        string roomName = (roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text)) ? roomNameInput.text : "My Room";
        bool isPrivate = privateToggle != null && privateToggle.isOn;

        string codeOrId = await lobby.Instance.CreateLobby(roomName, 4, isPrivate);
        if (!string.IsNullOrEmpty(codeOrId))
        {
            ShowInsideRoomPanel(roomName, isPrivate ? codeOrId : "");
        }
    }

    private async void OnJoinByCodeClicked()
    {
        if (joinCodeInput == null) return;
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        bool success = await lobby.Instance.JoinLobbyByCode(code);
        if (success)
        {
            ShowInsideRoomPanel("Private Room", code);
        }
    }

    private async void OnJoinLobbyCardClicked(Lobby targetLobby)
    {
        bool success = await lobby.Instance.JoinLobbyById(targetLobby.Id);
        if (success)
        {
            ShowInsideRoomPanel(targetLobby.Name);
        }
    }

    private void OnStartGameClicked()
    {
        lobby.Instance.StartGame();
    }

    private async void OnLeaveRoomClicked()
    {
        if (lobby.Instance != null)
        {
            await lobby.Instance.LeaveLobby();
        }
        ShowBrowserPanel();
    }

#if UNITY_EDITOR
    public void SetEditorReferences(
        GameObject browserPanel,
        GameObject insidePanel,
        TMP_InputField roomInput,
        Toggle privToggle,
        Button createBtn,
        TMP_InputField joinInput,
        Button joinBtn,
        Transform listContainer,
        LobbyItemCard cardPrefab,
        Button refreshBtn,
        TextMeshProUGUI titleText,
        TextMeshProUGUI codeText,
        Button startBtn,
        Button leaveBtn)
    {
        lobbyBrowserPanel = browserPanel;
        insideRoomPanel = insidePanel;
        roomNameInput = roomInput;
        privateToggle = privToggle;
        createRoomButton = createBtn;
        joinCodeInput = joinInput;
        joinByCodeButton = joinBtn;
        lobbyListContainer = listContainer;
        lobbyCardPrefab = cardPrefab;
        refreshListButton = refreshBtn;
        roomTitleText = titleText;
        lobbyCodeText = codeText;
        startGameButton = startBtn;
        leaveRoomButton = leaveBtn;

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
