using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace EdenFarm.Auth.UI
{
    /// <summary>
    /// Controller UI Autentikasi (AuthUIController).
    /// Mengelola Canvas, Panel Login, Panel Register, deteksi status player (baru vs terdaftar),
    /// serta input TextMeshPro (TMP_InputField) yang dirancang sesuai API Contract Backend.
    /// Mematuhi Unity Runtime UI Pattern (Canvas selalu aktif, hanya child panel yang di-toggle).
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

            // Otomatis cari referensi jika belum diset di inspector
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
            // Cek apakah ada saved token atau saved username di PlayerPrefs
            bool hasSavedAccount = PlayerPrefs.HasKey("AUTH_TOKEN") || PlayerPrefs.HasKey("SAVED_USERNAME");

            if (hasSavedAccount)
            {
                // Player sudah pernah login/registrasi -> arahkan ke mode Login
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
                // Player baru -> buka mode Register terlebih dahulu
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
            // Pipeline HTTP request akan disambungkan pada tahap berikutnya
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
            // Pipeline HTTP request akan disambungkan pada tahap berikutnya
        }

        public void ShowStatusMessage(string message)
        {
            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = message;
            }
        }

        // =========================================================================
        // ZERO-CLICK AUTO DISCOVERY & RUNTIME GENERATOR
        // Mematuhi unity-runtime-ui skill (bisa jalan tanpa setup manual di Editor)
        // =========================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitAuthSceneUI()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name.Equals("Auth", StringComparison.OrdinalIgnoreCase))
            {
                if (Instance == null && FindObjectOfType<AuthUIController>() == null)
                {
                    CreateRuntimeAuthUI();
                }
            }
        }

        public static AuthUIController CreateRuntimeAuthUI()
        {
            // 1. Pastikan EventSystem tersedia
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // 2. Buat Canvas Root (SELALU AKTIF)
            GameObject canvasGo = new GameObject("AuthCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Pasang controller pada Canvas root
            AuthUIController controller = canvasGo.AddComponent<AuthUIController>();

            // 3. Main Container Card
            GameObject cardObj = new GameObject("AuthCard");
            cardObj.transform.SetParent(canvasGo.transform, false);
            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(700, 680);

            Image cardImg = cardObj.AddComponent<Image>();
            cardImg.color = new Color(0.12f, 0.14f, 0.20f, 0.95f);

            // Title Header
            GameObject titleGo = new GameObject("AuthTitleText");
            titleGo.transform.SetParent(cardObj.transform, false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -30);
            titleRect.sizeDelta = new Vector2(0, 50);
            TextMeshProUGUI titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "EDEN FARM AUTHENTICATION";
            titleTmp.fontSize = 28;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;
            controller.authTitleText = titleTmp;

            // Status Message
            GameObject statusGo = new GameObject("StatusFeedbackText");
            statusGo.transform.SetParent(cardObj.transform, false);
            RectTransform statusRect = statusGo.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(1, 1);
            statusRect.pivot = new Vector2(0.5f, 1);
            statusRect.anchoredPosition = new Vector2(0, -85);
            statusRect.sizeDelta = new Vector2(600, 40);
            TextMeshProUGUI statusTmp = statusGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "Silakan login atau daftar akun baru.";
            statusTmp.fontSize = 16;
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            controller.statusFeedbackText = statusTmp;

            // 4. Panel Login
            GameObject loginPanel = new GameObject("LoginPanel");
            loginPanel.transform.SetParent(cardObj.transform, false);
            RectTransform lpRect = loginPanel.AddComponent<RectTransform>();
            lpRect.anchorMin = Vector2.zero;
            lpRect.anchorMax = Vector2.one;
            lpRect.offsetMin = new Vector2(40, 40);
            lpRect.offsetMax = new Vector2(-40, -140);
            controller.loginPanelRoot = loginPanel;

            controller.loginUsernameInput = CreateTMPInputField(loginPanel.transform, "LoginUsernameInput", "Masukkan Username...", new Vector2(0, 60), false);
            controller.loginPasswordInput = CreateTMPInputField(loginPanel.transform, "LoginPasswordInput", "Masukkan Password...", new Vector2(0, -10), true);

            controller.loginSubmitButton = CreateUIButton(loginPanel.transform, "LoginSubmitBtn", "MASUK (LOGIN)", new Vector2(0, -90), new Color(0.18f, 0.65f, 0.38f, 1f));
            controller.switchToRegisterButton = CreateUIButton(loginPanel.transform, "SwitchToRegBtn", "Belum punya akun? Daftar Baru", new Vector2(0, -150), new Color(0.25f, 0.35f, 0.55f, 1f));

            // 5. Panel Register
            GameObject regPanel = new GameObject("RegisterPanel");
            regPanel.transform.SetParent(cardObj.transform, false);
            RectTransform rpRect = regPanel.AddComponent<RectTransform>();
            rpRect.anchorMin = Vector2.zero;
            rpRect.anchorMax = Vector2.one;
            rpRect.offsetMin = new Vector2(40, 40);
            rpRect.offsetMax = new Vector2(-40, -140);
            controller.registerPanelRoot = regPanel;

            controller.registerUsernameInput = CreateTMPInputField(regPanel.transform, "RegUsernameInput", "Username (min. 3 karakter)*", new Vector2(0, 110), false);
            controller.registerPasswordInput = CreateTMPInputField(regPanel.transform, "RegPasswordInput", "Password (min. 6 karakter)*", new Vector2(0, 45), true);
            controller.registerNicknameInput = CreateTMPInputField(regPanel.transform, "RegNicknameInput", "Nama Panggilan / Nickname (opsional)", new Vector2(0, -20), false);
            controller.registerBirthDateInput = CreateTMPInputField(regPanel.transform, "RegBirthDateInput", "Tanggal Lahir YYYY-MM-DD (opsional)", new Vector2(0, -85), false);

            controller.registerSubmitButton = CreateUIButton(regPanel.transform, "RegisterSubmitBtn", "BUAT AKUN BARU", new Vector2(0, -155), new Color(0.20f, 0.60f, 0.90f, 1f));
            controller.switchToLoginButton = CreateUIButton(regPanel.transform, "SwitchToLoginBtn", "Sudah punya akun? Masuk di sini", new Vector2(0, -215), new Color(0.35f, 0.40f, 0.50f, 1f));

            controller.DetectPlayerStatusAndSetInitialState();
            return controller;
        }

        private static TMP_InputField CreateTMPInputField(Transform parent, string name, string placeholderText, Vector2 pos, bool isPassword)
        {
            GameObject inputGo = new GameObject(name);
            inputGo.transform.SetParent(parent, false);

            RectTransform rect = inputGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(500, 50);

            Image bg = inputGo.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.22f, 0.30f, 1f);

            // Text Viewport
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputGo.transform, false);
            RectTransform taRect = textArea.AddComponent<RectTransform>();
            taRect.anchorMin = Vector2.zero;
            taRect.anchorMax = Vector2.one;
            taRect.offsetMin = new Vector2(15, 5);
            taRect.offsetMax = new Vector2(-15, -5);

            // Placeholder
            GameObject phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            RectTransform phRect = phGo.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;
            TextMeshProUGUI phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholderText;
            phTmp.fontSize = 18;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.6f, 0.65f, 0.75f, 0.6f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Content Text
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI contentTmp = textGo.AddComponent<TextMeshProUGUI>();
            contentTmp.fontSize = 18;
            contentTmp.color = Color.white;
            contentTmp.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.textViewport = taRect;
            inputField.textComponent = contentTmp;
            inputField.placeholder = phTmp;
            inputField.fontAsset = contentTmp.font;

            if (isPassword)
            {
                inputField.contentType = TMP_InputField.ContentType.Password;
                inputField.inputType = TMP_InputField.InputType.Password;
            }

            return inputField;
        }

        private static Button CreateUIButton(Transform parent, string name, string label, Vector2 pos, Color btnColor)
        {
            GameObject btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);

            RectTransform rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(480, 46);

            Image img = btnGo.AddComponent<Image>();
            img.color = btnColor;

            Button btn = btnGo.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = btnColor * 1.15f;
            cb.pressedColor = btnColor * 0.85f;
            btn.colors = cb;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            RectTransform tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
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
        }
    }
}
