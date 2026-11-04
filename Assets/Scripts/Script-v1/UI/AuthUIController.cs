using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        /// atau belum pernah ada data tersimpan (player baru), lalu menampilkan form yang tepat.
        /// </summary>
        public void DetectPlayerStatusAndSetInitialState()
        {
            bool hasSavedAccount = PlayerPrefs.HasKey("AUTH_TOKEN") || PlayerPrefs.HasKey("SAVED_USERNAME");

            if (hasSavedAccount)
            {
                string savedUser = PlayerPrefs.GetString("SAVED_USERNAME", string.Empty);
                if (loginUsernameInput != null && !string.IsNullOrEmpty(savedUser))
                {
                    loginUsernameInput.text = savedUser;
                }

                SetAuthState(AuthState.Login);
                ShowStatusMessage("Selamat datang kembali! Silakan masuk ke akun Anda.");
            }
            else
            {
                SetAuthState(AuthState.Register);
                ShowStatusMessage("Selamat datang di Eden Farm! Silakan daftarkan akun baru Anda.");
            }
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

            ShowStatusMessage($"<color=yellow>Memverifikasi akun {username}...</color>");
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

            ShowStatusMessage($"<color=yellow>Mendaftarkan akun {username}...</color>");
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
