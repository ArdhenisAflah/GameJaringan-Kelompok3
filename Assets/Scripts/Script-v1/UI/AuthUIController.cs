using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EdenFarm.Auth.DTO;
using EdenFarm.Auth.Session;
using EdenFarm.Auth.Networking;

namespace EdenFarm.Auth.UI
{
    /// <summary>
    /// Controller UI Autentikasi (AuthUIController).
    /// Mengelola Canvas, Panel Login, Panel Register, deteksi status player (baru vs terdaftar),
    /// serta input TextMeshPro (TMP_InputField) yang dirancang sesuai API Contract Backend.
    /// Komponen ini terpasang langsung pada Canvas di hierarki scene Auth.unity.
    /// </summary>
    public class AuthUIController : MonoBehaviour
    {
        public static AuthUIController Instance { get; private set; }

        public enum AuthState
        {
            Login,      // Mode untuk player yang sudah terdaftar
            Register    // Mode untuk player baru
        }

        [Header("State")]
        [SerializeField] private AuthState currentAuthState = AuthState.Login;

        [Header("Panel Roots (Child Panels)")]
        [SerializeField] private GameObject loginPanelRoot;
        [SerializeField] private GameObject registerPanelRoot;

        [Header("Login Input Fields (Sesuai POST /api/login)")]
        [SerializeField] private TMP_InputField loginUsernameInput;
        [SerializeField] private TMP_InputField loginPasswordInput;
        [SerializeField] private Button loginSubmitButton;
        [SerializeField] private Button switchToRegisterButton;

        [Header("Register Input Fields (Sesuai POST /api/register)")]
        [SerializeField] private TMP_InputField registerUsernameInput;
        [SerializeField] private TMP_InputField registerPasswordInput;
        [SerializeField] private TMP_InputField registerNicknameInput;
        [SerializeField] private TMP_InputField registerBirthDateInput;
        [SerializeField] private Button registerSubmitButton;
        [SerializeField] private Button switchToLoginButton;

        [Header("Feedback / Status Text")]
        [SerializeField] private TextMeshProUGUI statusFeedbackText;
        [SerializeField] private TextMeshProUGUI authTitleText;

        public AuthState CurrentState => currentAuthState;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            AutoDiscoverReferences();
        }

        private void Start()
        {
            BindButtonEvents();
            DetectPlayerStatusAndSetInitialState();
        }

        /// <summary>
        /// Mendeteksi apakah player sudah pernah login sebelumnya (player lama)
        /// atau belum pernah ada data tersimpan (player baru), lalu menjalankan Auto-Login jika token ada.
        /// </summary>
        public void DetectPlayerStatusAndSetInitialState()
        {
            string lastUsername = SessionManager.GetLastUsername();
            if (loginUsernameInput != null && !string.IsNullOrEmpty(lastUsername))
            {
                loginUsernameInput.text = lastUsername;
            }

            if (SessionManager.HasSavedSession())
            {
                SetAuthState(AuthState.Login);
                var savedSession = SessionManager.GetCurrentSession();
                if (savedSession != null && !string.IsNullOrEmpty(savedSession.token))
                {
                    // Lakukan Auto-Login via endpoint /api/auth/verify
                    AttemptAutoLogin(savedSession.token);
                    return;
                }

                ShowStatusMessage("Selamat datang kembali! Silakan masuk ke akun Anda.");
            }
            else
            {
                SetAuthState(AuthState.Register);
                ShowStatusMessage("Selamat datang di Eden Farm! Silakan daftarkan akun baru Anda.");
            }
        }

        private void AttemptAutoLogin(string token)
        {
            SetInputInteractable(false);
            ShowStatusMessage("<color=yellow>Menghubungkan & memverifikasi sesi tersimpan...</color>");

            EnsureNetworkClient();
            AuthNetworkClient.Instance.SendVerifySession(token, (response) =>
            {
                SetInputInteractable(true);
                // Perbarui sesi dengan data terbaru dari server
                SessionManager.SaveSession(token, response.userId, response.data);
                ShowStatusMessage($"<color=green>Sesi aktif! Selamat datang kembali, {response.data.nickname}!</color>");
                OnAuthSuccessNavigateToLobby();
            }, (errorMsg) =>
            {
                SetInputInteractable(true);
                // Token tidak valid atau expired di server -> bersihkan sesi lokal
                SessionManager.ClearSession();
                ShowStatusMessage($"<color=orange>{errorMsg}</color>");
            });
        }

        /// <summary>
        /// Mengganti tampilan panel UI antara form Login dan Register.
        /// </summary>
        public void SetAuthState(AuthState newState)
        {
            currentAuthState = newState;

            if (currentAuthState == AuthState.Login)
            {
                if (loginPanelRoot != null) loginPanelRoot.SetActive(true);
                if (registerPanelRoot != null) registerPanelRoot.SetActive(false);

                if (authTitleText != null) authTitleText.text = "MASUK KE EDEN FARM";
            }
            else
            {
                if (loginPanelRoot != null) loginPanelRoot.SetActive(false);
                if (registerPanelRoot != null) registerPanelRoot.SetActive(true);

                if (authTitleText != null) authTitleText.text = "DAFTAR AKUN BARU";
            }
        }

        public void SwitchToRegister() => SetAuthState(AuthState.Register);
        public void SwitchToLogin() => SetAuthState(AuthState.Login);

        private void BindButtonEvents()
        {
            if (switchToRegisterButton != null)
                switchToRegisterButton.onClick.AddListener(SwitchToRegister);

            if (switchToLoginButton != null)
                switchToLoginButton.onClick.AddListener(SwitchToLogin);

            if (loginSubmitButton != null)
                loginSubmitButton.onClick.AddListener(OnLoginSubmitClicked);

            if (registerSubmitButton != null)
                registerSubmitButton.onClick.AddListener(OnRegisterSubmitClicked);
        }

        private void OnLoginSubmitClicked()
        {
            string username = loginUsernameInput != null ? loginUsernameInput.text.Trim() : "";
            string password = loginPasswordInput != null ? loginPasswordInput.text : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowStatusMessage("<color=red>Username dan password wajib diisi!</color>");
                return;
            }

            SetInputInteractable(false);
            ShowStatusMessage($"<color=yellow>Memverifikasi akun {username}...</color>");

            EnsureNetworkClient();
            var req = new LoginRequestDTO(username, password);

            AuthNetworkClient.Instance.SendLogin(req, (response) =>
            {
                SetInputInteractable(true);
                // Simpan token sesi dan data akun ke penyimpanan lokal perangkat
                SessionManager.SaveSession(response.token, response.userId, response.data);
                ShowStatusMessage($"<color=green>Login berhasil! Selamat datang, {response.data.nickname}!</color>");
                OnAuthSuccessNavigateToLobby();
            }, (errorMsg) =>
            {
                SetInputInteractable(true);
                ShowStatusMessage($"<color=red>{errorMsg}</color>");
            });
        }

        private void OnRegisterSubmitClicked()
        {
            string username = registerUsernameInput != null ? registerUsernameInput.text.Trim() : "";
            string password = registerPasswordInput != null ? registerPasswordInput.text : "";
            string nickname = registerNicknameInput != null ? registerNicknameInput.text.Trim() : "";
            string birthDate = registerBirthDateInput != null ? registerBirthDateInput.text.Trim() : "";

            if (string.IsNullOrEmpty(username) || username.Length < 3)
            {
                ShowStatusMessage("<color=red>Username minimal harus 3 karakter!</color>");
                return;
            }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                ShowStatusMessage("<color=red>Password minimal harus 6 karakter!</color>");
                return;
            }

            SetInputInteractable(false);
            ShowStatusMessage($"<color=yellow>Mendaftarkan akun {username} ke server...</color>");

            EnsureNetworkClient();
            var req = new RegisterRequestDTO(username, password, nickname, birthDate);

            AuthNetworkClient.Instance.SendRegister(req, (response) =>
            {
                ShowStatusMessage("<color=green>Akun berhasil didaftarkan! Mengalihkan ke login...</color>");

                // Lakukan auto-login langsung dengan kredensial yang baru didaftarkan
                var loginReq = new LoginRequestDTO(username, password);
                AuthNetworkClient.Instance.SendLogin(loginReq, (loginRes) =>
                {
                    SetInputInteractable(true);
                    SessionManager.SaveSession(loginRes.token, loginRes.userId, loginRes.data);
                    ShowStatusMessage($"<color=green>Selamat datang di Eden Farm, {loginRes.data.nickname}!</color>");
                    OnAuthSuccessNavigateToLobby();
                }, (loginErr) =>
                {
                    SetInputInteractable(true);
                    // Jika auto-login gagal, arahkan player ke form login biasa
                    SetAuthState(AuthState.Login);
                    if (loginUsernameInput != null) loginUsernameInput.text = username;
                    ShowStatusMessage("<color=green>Akun terdaftar!</color> Silakan masuk.");
                });
            }, (errorMsg) =>
            {
                SetInputInteractable(true);
                ShowStatusMessage($"<color=red>{errorMsg}</color>");
            });
        }

        private void SetInputInteractable(bool interactable)
        {
            if (loginSubmitButton != null) loginSubmitButton.interactable = interactable;
            if (registerSubmitButton != null) registerSubmitButton.interactable = interactable;
            if (switchToRegisterButton != null) switchToRegisterButton.interactable = interactable;
            if (switchToLoginButton != null) switchToLoginButton.interactable = interactable;
            if (loginUsernameInput != null) loginUsernameInput.interactable = interactable;
            if (loginPasswordInput != null) loginPasswordInput.interactable = interactable;
            if (registerUsernameInput != null) registerUsernameInput.interactable = interactable;
            if (registerPasswordInput != null) registerPasswordInput.interactable = interactable;
            if (registerNicknameInput != null) registerNicknameInput.interactable = interactable;
            if (registerBirthDateInput != null) registerBirthDateInput.interactable = interactable;
        }

        private void OnAuthSuccessNavigateToLobby()
        {
            StartCoroutine(NavigateToLobbyRoutine());
        }

        private System.Collections.IEnumerator NavigateToLobbyRoutine()
        {
            yield return new WaitForSeconds(1.0f);

            // Buka scene Lobby jika tersedia di Build Settings
            if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Lobby") >= 0 ||
                UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Lobby.unity") >= 0)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
            }
            else
            {
                // Fallback direct load
                try
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AuthUIController] Tidak dapat memuat scene Lobby: {e.Message}");
                }
            }
        }

        private void EnsureNetworkClient()
        {
            if (AuthNetworkClient.Instance == null)
            {
                GameObject clientGo = new GameObject("AuthNetworkClient");
                clientGo.AddComponent<AuthNetworkClient>();
            }
        }

        public void ShowStatusMessage(string message)
        {
            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = message;
            }
        }

        private void AutoDiscoverReferences()
        {
            if (loginPanelRoot == null)
            {
                var lp = transform.Find("AuthCard/LoginPanel");
                if (lp != null) loginPanelRoot = lp.gameObject;
            }

            if (registerPanelRoot == null)
            {
                var rp = transform.Find("AuthCard/RegisterPanel");
                if (rp != null) registerPanelRoot = rp.gameObject;
            }

            if (authTitleText == null)
            {
                var t = transform.Find("AuthCard/AuthTitleText");
                if (t != null) authTitleText = t.GetComponent<TextMeshProUGUI>();
            }

            if (statusFeedbackText == null)
            {
                var s = transform.Find("AuthCard/StatusFeedbackText");
                if (s != null) statusFeedbackText = s.GetComponent<TextMeshProUGUI>();
            }

            // Temukan input login jika belum dislot
            if (loginUsernameInput == null && loginPanelRoot != null)
            {
                var u = loginPanelRoot.transform.Find("LoginUsernameInput");
                if (u != null) loginUsernameInput = u.GetComponent<TMP_InputField>();
            }
            if (loginPasswordInput == null && loginPanelRoot != null)
            {
                var p = loginPanelRoot.transform.Find("LoginPasswordInput");
                if (p != null) loginPasswordInput = p.GetComponent<TMP_InputField>();
            }
            if (loginSubmitButton == null && loginPanelRoot != null)
            {
                var b = loginPanelRoot.transform.Find("LoginSubmitBtn");
                if (b != null) loginSubmitButton = b.GetComponent<Button>();
            }
            if (switchToRegisterButton == null && loginPanelRoot != null)
            {
                var s = loginPanelRoot.transform.Find("SwitchToRegBtn");
                if (s != null) switchToRegisterButton = s.GetComponent<Button>();
            }

            // Temukan input register jika belum dislot
            if (registerUsernameInput == null && registerPanelRoot != null)
            {
                var u = registerPanelRoot.transform.Find("RegUsernameInput");
                if (u != null) registerUsernameInput = u.GetComponent<TMP_InputField>();
            }
            if (registerPasswordInput == null && registerPanelRoot != null)
            {
                var p = registerPanelRoot.transform.Find("RegPasswordInput");
                if (p != null) registerPasswordInput = p.GetComponent<TMP_InputField>();
            }
            if (registerNicknameInput == null && registerPanelRoot != null)
            {
                var n = registerPanelRoot.transform.Find("RegNicknameInput");
                if (n != null) registerNicknameInput = n.GetComponent<TMP_InputField>();
            }
            if (registerBirthDateInput == null && registerPanelRoot != null)
            {
                var d = registerPanelRoot.transform.Find("RegBirthDateInput");
                if (d != null) registerBirthDateInput = d.GetComponent<TMP_InputField>();
            }
            if (registerSubmitButton == null && registerPanelRoot != null)
            {
                var b = registerPanelRoot.transform.Find("RegisterSubmitBtn");
                if (b != null) registerSubmitButton = b.GetComponent<Button>();
            }
            if (switchToLoginButton == null && registerPanelRoot != null)
            {
                var s = registerPanelRoot.transform.Find("SwitchToLoginBtn");
                if (s != null) switchToLoginButton = s.GetComponent<Button>();
            }
        }
    }
}
