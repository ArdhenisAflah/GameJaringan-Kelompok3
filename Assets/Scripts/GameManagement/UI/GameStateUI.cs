using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace GameManagement.UI
{
    /// <summary>
    /// Controller UI mandiri untuk Game Over, Victory, dan HUD indikator kegagalan pesanan.
    /// Mematuhi Golden Rule unity-runtime-ui: Controller Canvas selalu aktif,
    /// hanya child panel yang dinonaktifkan (SetActive(false)), dan dilengkapi
    /// builder runtime otomatis tanpa butuh setup klik menu Editor.
    /// </summary>
    public class GameStateUI : MonoBehaviour
    {
        public static GameStateUI Instance { get; private set; }

        [Header("Panel References")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject hudPanel;

        [Header("Game Over UI Elements")]
        [SerializeField] private Image gameOverImage;
        [SerializeField] private TextMeshProUGUI gameOverReasonText;
        [SerializeField] private TextMeshProUGUI gameOverCountdownText;

        [Header("Victory UI Elements")]
        [SerializeField] private Image victoryImage;
        [SerializeField] private TextMeshProUGUI victoryMessageText;

        [Header("HUD Elements")]
        [SerializeField] private TextMeshProUGUI failedOrderHudText;

        [Header("Sprite Assets")]
        [SerializeField] private Sprite gameOverSprite;
        [SerializeField] private Sprite victorySprite;

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

            AutoDiscoverReferencesIfNeeded();
            LoadSpritesIfNeeded();

            // Sembunyikan panel modal di awal (Controller tetap aktif)
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(true);
        }

        private void Start()
        {
            BindGameStateManagerEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnbindGameStateManagerEvents();
        }

        // ==========================================
        // 1. EVENT BINDINGS
        // ==========================================
        private void BindGameStateManagerEvents()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnGameOverTriggered += HandleGameOverTriggered;
                GameStateManager.Instance.OnVictoryTriggered += HandleVictoryTriggered;
                GameStateManager.Instance.OnFailedOrderCountChanged += HandleFailedOrderCountChanged;
                GameStateManager.Instance.OnShutdownCountdownTick += HandleShutdownCountdownTick;

                // Inisialisasi tampilan HUD awal
                HandleFailedOrderCountChanged(GameStateManager.Instance.FailedOrderCount.Value, GameStateManager.Instance.MaxFailedOrders);
            }
        }

        private void UnbindGameStateManagerEvents()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnGameOverTriggered -= HandleGameOverTriggered;
                GameStateManager.Instance.OnVictoryTriggered -= HandleVictoryTriggered;
                GameStateManager.Instance.OnFailedOrderCountChanged -= HandleFailedOrderCountChanged;
                GameStateManager.Instance.OnShutdownCountdownTick -= HandleShutdownCountdownTick;
            }
        }

        // ==========================================
        // 2. UI EVENT HANDLERS
        // ==========================================
        private void HandleGameOverTriggered(GameOverReason reason)
        {
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);

                if (gameOverImage != null && gameOverSprite != null)
                {
                    gameOverImage.sprite = gameOverSprite;
                }

                if (gameOverReasonText != null)
                {
                    string msg = reason switch
                    {
                        GameOverReason.FailedOrdersExceeded => $"Batas Pesanan Gagal Terlampaui ({GameStateManager.Instance?.FailedOrderCount.Value ?? 3}/{GameStateManager.Instance?.MaxFailedOrders ?? 3})!",
                        GameOverReason.HostDisconnected => "Host terputus dari permainan!",
                        _ => "Permainan Berakhir!"
                    };
                    gameOverReasonText.text = msg;
                }

                if (gameOverCountdownText != null)
                {
                    float delay = GameStateManager.Instance != null ? GameStateManager.Instance.ShutdownDelay : 5f;
                    gameOverCountdownText.text = $"Kembali ke Lobby dalam {Mathf.CeilToInt(delay)} detik...";
                }
            }
        }

        private void HandleVictoryTriggered()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);

                if (victoryImage != null && victorySprite != null)
                {
                    victoryImage.sprite = victorySprite;
                }

                if (victoryMessageText != null)
                {
                    victoryMessageText.text = "Selamat! Semua pesanan berhasil diselesaikan!";
                }
            }
        }

        private void HandleFailedOrderCountChanged(int current, int max)
        {
            if (failedOrderHudText != null)
            {
                string colorHex = current switch
                {
                    0 => "#4CAF50", // Hijau
                    1 => "#FFEB3B", // Kuning
                    2 => "#FF9800", // Oranye
                    _ => "#F44336"  // Merah
                };

                failedOrderHudText.text = $"Pesanan Gagal: <color={colorHex}><b>{current} / {max}</b></color>";
            }
        }

        private void HandleShutdownCountdownTick(float remaining)
        {
            if (gameOverCountdownText != null && gameOverPanel != null && gameOverPanel.activeSelf)
            {
                int sec = Mathf.CeilToInt(remaining);
                gameOverCountdownText.text = sec > 0 
                    ? $"Kembali ke Lobby dalam {sec} detik..." 
                    : "Menutup room...";
            }
        }

        // ==========================================
        // 3. SPRITE LOADING & REFERENCE DISCOVERY
        // ==========================================
        private void AutoDiscoverReferencesIfNeeded()
        {
            if (gameOverPanel == null) gameOverPanel = transform.Find("GameOverPanel")?.gameObject;
            if (victoryPanel == null) victoryPanel = transform.Find("VictoryPanel")?.gameObject;
            if (hudPanel == null) hudPanel = transform.Find("HUDPanel")?.gameObject;

            if (gameOverPanel != null)
            {
                if (gameOverImage == null) gameOverImage = gameOverPanel.transform.Find("GameOverImage")?.GetComponent<Image>();
                if (gameOverReasonText == null) gameOverReasonText = gameOverPanel.transform.Find("ReasonText")?.GetComponent<TextMeshProUGUI>();
                if (gameOverCountdownText == null) gameOverCountdownText = gameOverPanel.transform.Find("CountdownText")?.GetComponent<TextMeshProUGUI>();
            }

            if (victoryPanel != null)
            {
                if (victoryImage == null) victoryImage = victoryPanel.transform.Find("VictoryImage")?.GetComponent<Image>();
                if (victoryMessageText == null) victoryMessageText = victoryPanel.transform.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
            }

            if (hudPanel != null && failedOrderHudText == null)
            {
                failedOrderHudText = hudPanel.transform.Find("FailedOrderText")?.GetComponent<TextMeshProUGUI>();
            }
        }

        private void LoadSpritesIfNeeded()
        {
            if (gameOverSprite == null)
            {
                gameOverSprite = LoadSpriteFromAssetPath("Assets/SpriteSheet/Game Over Image.png");
            }
            if (victorySprite == null)
            {
                victorySprite = LoadSpriteFromAssetPath("Assets/SpriteSheet/You Win Image.png");
            }
        }

        private static Sprite LoadSpriteFromAssetPath(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            return null;
#endif
        }

        // ==========================================
        // 4. RUNTIME ZERO-CLICK BUILDER
        // ==========================================
        public static GameStateUI CreateRuntimeCanvasUI()
        {
            // 1. Pastikan EventSystem tersedia
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            // 2. Buat Root Canvas (Controller selalu aktif)
            GameObject canvasObj = new GameObject("GameStateCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150; // Di atas seluruh UI permainan

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            GameStateUI ui = canvasObj.AddComponent<GameStateUI>();

            // 3. Buat HUD Panel (Pojok Kiri Atas)
            GameObject hudObj = new GameObject("HUDPanel");
            hudObj.transform.SetParent(canvasObj.transform, false);
            RectTransform hudRect = hudObj.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 1f);
            hudRect.anchorMax = new Vector2(0f, 1f);
            hudRect.pivot = new Vector2(0f, 1f);
            hudRect.anchoredPosition = new Vector2(24f, -24f);
            hudRect.sizeDelta = new Vector2(280f, 50f);

            Image hudBg = hudObj.AddComponent<Image>();
            hudBg.color = new Color(0.1f, 0.1f, 0.12f, 0.75f);

            GameObject hudTextObj = new GameObject("FailedOrderText");
            hudTextObj.transform.SetParent(hudObj.transform, false);
            RectTransform hudTextRect = hudTextObj.AddComponent<RectTransform>();
            hudTextRect.anchorMin = Vector2.zero;
            hudTextRect.anchorMax = Vector2.one;
            hudTextRect.offsetMin = new Vector2(12f, 0f);
            hudTextRect.offsetMax = new Vector2(-12f, 0f);

            TextMeshProUGUI hudTmp = hudTextObj.AddComponent<TextMeshProUGUI>();
            hudTmp.text = "Pesanan Gagal: 0 / 3";
            hudTmp.fontSize = 20;
            hudTmp.fontStyle = FontStyles.Bold;
            hudTmp.alignment = TextAlignmentOptions.MidlineLeft;
            hudTmp.color = Color.white;

            // 4. Buat Game Over Panel (Fullscreen Overlay)
            GameObject goPanelObj = new GameObject("GameOverPanel");
            goPanelObj.transform.SetParent(canvasObj.transform, false);
            RectTransform goPanelRect = goPanelObj.AddComponent<RectTransform>();
            goPanelRect.anchorMin = Vector2.zero;
            goPanelRect.anchorMax = Vector2.one;
            goPanelRect.offsetMin = Vector2.zero;
            goPanelRect.offsetMax = Vector2.zero;

            Image goBg = goPanelObj.AddComponent<Image>();
            goBg.color = new Color(0.05f, 0.05f, 0.08f, 0.88f); // Dark translucent backdrop

            // Child 4a: Game Over Image (Center)
            GameObject goImgObj = new GameObject("GameOverImage");
            goImgObj.transform.SetParent(goPanelObj.transform, false);
            RectTransform goImgRect = goImgObj.AddComponent<RectTransform>();
            goImgRect.anchorMin = new Vector2(0.5f, 0.5f);
            goImgRect.anchorMax = new Vector2(0.5f, 0.5f);
            goImgRect.pivot = new Vector2(0.5f, 0.5f);
            goImgRect.anchoredPosition = new Vector2(0f, 60f);
            goImgRect.sizeDelta = new Vector2(480f, 320f);

            Image goImg = goImgObj.AddComponent<Image>();
            goImg.preserveAspect = true;
            Sprite goSprite = LoadSpriteFromAssetPath("Assets/SpriteSheet/Game Over Image.png");
            if (goSprite != null) goImg.sprite = goSprite;

            // Child 4b: Reason Text
            GameObject goReasonObj = new GameObject("ReasonText");
            goReasonObj.transform.SetParent(goPanelObj.transform, false);
            RectTransform goReasonRect = goReasonObj.AddComponent<RectTransform>();
            goReasonRect.anchorMin = new Vector2(0.5f, 0.5f);
            goReasonRect.anchorMax = new Vector2(0.5f, 0.5f);
            goReasonRect.pivot = new Vector2(0.5f, 0.5f);
            goReasonRect.anchoredPosition = new Vector2(0f, -130f);
            goReasonRect.sizeDelta = new Vector2(800f, 50f);

            TextMeshProUGUI goReasonTmp = goReasonObj.AddComponent<TextMeshProUGUI>();
            goReasonTmp.text = "Batas Pesanan Gagal Terlampaui (3/3)!";
            goReasonTmp.fontSize = 26;
            goReasonTmp.fontStyle = FontStyles.Bold;
            goReasonTmp.alignment = TextAlignmentOptions.Center;
            goReasonTmp.color = new Color(1f, 0.75f, 0.2f, 1f);

            // Child 4c: Countdown Text
            GameObject goCountObj = new GameObject("CountdownText");
            goCountObj.transform.SetParent(goPanelObj.transform, false);
            RectTransform goCountRect = goCountObj.AddComponent<RectTransform>();
            goCountRect.anchorMin = new Vector2(0.5f, 0.5f);
            goCountRect.anchorMax = new Vector2(0.5f, 0.5f);
            goCountRect.pivot = new Vector2(0.5f, 0.5f);
            goCountRect.anchoredPosition = new Vector2(0f, -180f);
            goCountRect.sizeDelta = new Vector2(800f, 40f);

            TextMeshProUGUI goCountTmp = goCountObj.AddComponent<TextMeshProUGUI>();
            goCountTmp.text = "Kembali ke Lobby dalam 5 detik...";
            goCountTmp.fontSize = 20;
            goCountTmp.alignment = TextAlignmentOptions.Center;
            goCountTmp.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

            // 5. Buat Victory Panel (Fullscreen Overlay)
            GameObject winPanelObj = new GameObject("VictoryPanel");
            winPanelObj.transform.SetParent(canvasObj.transform, false);
            RectTransform winPanelRect = winPanelObj.AddComponent<RectTransform>();
            winPanelRect.anchorMin = Vector2.zero;
            winPanelRect.anchorMax = Vector2.one;
            winPanelRect.offsetMin = Vector2.zero;
            winPanelRect.offsetMax = Vector2.zero;

            Image winBg = winPanelObj.AddComponent<Image>();
            winBg.color = new Color(0.05f, 0.08f, 0.05f, 0.88f);

            // Child 5a: You Win Image
            GameObject winImgObj = new GameObject("VictoryImage");
            winImgObj.transform.SetParent(winPanelObj.transform, false);
            RectTransform winImgRect = winImgObj.AddComponent<RectTransform>();
            winImgRect.anchorMin = new Vector2(0.5f, 0.5f);
            winImgRect.anchorMax = new Vector2(0.5f, 0.5f);
            winImgRect.pivot = new Vector2(0.5f, 0.5f);
            winImgRect.anchoredPosition = new Vector2(0f, 60f);
            winImgRect.sizeDelta = new Vector2(480f, 320f);

            Image winImg = winImgObj.AddComponent<Image>();
            winImg.preserveAspect = true;
            Sprite winSprite = LoadSpriteFromAssetPath("Assets/SpriteSheet/You Win Image.png");
            if (winSprite != null) winImg.sprite = winSprite;

            // Child 5b: Message Text
            GameObject winMsgObj = new GameObject("MessageText");
            winMsgObj.transform.SetParent(winPanelObj.transform, false);
            RectTransform winMsgRect = winMsgObj.AddComponent<RectTransform>();
            winMsgRect.anchorMin = new Vector2(0.5f, 0.5f);
            winMsgRect.anchorMax = new Vector2(0.5f, 0.5f);
            winMsgRect.pivot = new Vector2(0.5f, 0.5f);
            winMsgRect.anchoredPosition = new Vector2(0f, -130f);
            winMsgRect.sizeDelta = new Vector2(800f, 50f);

            TextMeshProUGUI winMsgTmp = winMsgObj.AddComponent<TextMeshProUGUI>();
            winMsgTmp.text = "Selamat! Semua pesanan berhasil dipenuhi!";
            winMsgTmp.fontSize = 26;
            winMsgTmp.fontStyle = FontStyles.Bold;
            winMsgTmp.alignment = TextAlignmentOptions.Center;
            winMsgTmp.color = new Color(0.3f, 1f, 0.4f, 1f);

            // Assign referensi ke component UI
            ui.gameOverPanel = goPanelObj;
            ui.victoryPanel = winPanelObj;
            ui.hudPanel = hudObj;

            ui.gameOverImage = goImg;
            ui.gameOverReasonText = goReasonTmp;
            ui.gameOverCountdownText = goCountTmp;

            ui.victoryImage = winImg;
            ui.victoryMessageText = winMsgTmp;
            ui.failedOrderHudText = hudTmp;

            ui.gameOverSprite = goSprite;
            ui.victorySprite = winSprite;

            // Sembunyikan panel modal awal (Controller Canvas tetap aktif)
            goPanelObj.SetActive(false);
            winPanelObj.SetActive(false);
            hudObj.SetActive(true);

            Debug.Log("<color=cyan>[GameStateUI]</color> Runtime Canvas UI berhasil dibuat di hierarki.");
            return ui;
        }

        // ==========================================
        // 5. AUTO SCENE LOAD HOOK
        // ==========================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitSceneHook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            CheckSceneAndCreateUI(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            CheckSceneAndCreateUI(scene);
        }

        private static void CheckSceneAndCreateUI(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.name != "SampleScene") return;

            if (Instance == null && FindObjectOfType<GameStateUI>() == null)
            {
                CreateRuntimeCanvasUI();
            }
        }
    }
}
