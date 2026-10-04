using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Pengaturan Scene")]
    [Tooltip("Ketik nama scene lobby kamu secara persis (huruf besar/kecil berpengaruh)")]
    public string namaSceneLobby = "Lobby";

    // 1. Fungsi untuk tombol Lobby
    public void MoveToLobby()
    {
        Debug.Log("Berpindah ke scene: " + namaSceneLobby);
        SceneManager.LoadScene(namaSceneLobby);
    }

    // 2. Fungsi untuk tombol Quit
    public void QuitGame()
    {
        Debug.Log("Game ditutup!");
        
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}