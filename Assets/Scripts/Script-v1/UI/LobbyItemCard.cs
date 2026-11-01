using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;

public class LobbyItemCard : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button joinButton;

    private Lobby currentLobby;
    private Action<Lobby> onJoinClickedAction;

    /// <summary>
    /// Configures the card with lobby data and bind the join callback.
    /// </summary>
    public void Setup(Lobby lobby, Action<Lobby> onJoinClicked)
    {
        currentLobby = lobby;
        onJoinClickedAction = onJoinClicked;

        if (lobbyNameText != null)
            lobbyNameText.text = lobby.Name;

        if (playerCountText != null)
            playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(HandleJoinClicked);
        }
    }

    private void HandleJoinClicked()
    {
        onJoinClickedAction?.Invoke(currentLobby);
    }
}
