using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SiloSystem;

namespace TruckOrder
{
    /// <summary>
    /// UI Canvas System untuk menampilkan bubble / papan pesanan truk aktif.
    /// Menggunakan sistem Canvas UI (ScreenSpaceOverlay dengan sortingOrder tinggi)
    /// sehingga selalu berada di atas layer truk dan objek world lainnya.
    /// Mematuhi standar arsitektur 'unity-runtime-ui' di mana controller Canvas
    /// selalu aktif di hierarki dan hanya child panel (panelRoot) yang di-toggle.
    /// </summary>
    public class TruckOrderBubbleUI : MonoBehaviour
    {
        public static TruckOrderBubbleUI Instance { get; private set; }

        [Header("Panel Root (Hanya panel ini yang di-toggle SetActive)")]
        [Tooltip("Child panel GameObject yang diaktifkan/dinonaktifkan saat order aktif/selesai.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("RectTransform dari panelRoot untuk penempatan posisi di layar.")]
        [SerializeField] private RectTransform panelRect;

        [Header("Komponen Visual UI")]
        [Tooltip("Image latar belakang kotak pesanan (Order Box).")]
        [SerializeField] private Image boxImage;

        [Tooltip("Image ikon tanaman pesanan (Padi).")]
        [SerializeField] private Image itemIconImage;

        [Tooltip("Teks kuantitas progres (contoh: '0/5').")]
        [SerializeField] private TextMeshProUGUI progressText;

        [Tooltip("Teks countdown sisa waktu tunggu (contoh: '01:45').")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Sprite Assets")]
        [SerializeField] private Sprite orderBoxSprite;
        [SerializeField] private Sprite padiIconSprite;

        [Header("Penjejakan Posisi (World-to-Screen)")]
        [Tooltip("Jika true, posisi panel akan melayang di atas truk aktif di loading spot. Jika false, berada di defaultWorldPosition.")]
        [SerializeField] private bool followActiveTruck = true;

        [Tooltip("Offset posisi world di atas titik tengah truk.")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

        [Tooltip("Posisi world default jika truk belum aktif atau followActiveTruck dimatikan.")]
        [SerializeField] private Vector3 defaultWorldPosition = new Vector3(5.35f, -0.3f, 0f);

        private OrderSystem _orderSystem;
        private Camera _cachedCamera;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

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
            LoadDefaultSpritesIfNeeded();

            // Aturan Emas unity-runtime-ui: Panel modal awal disembunyikan via panelRoot
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Start()
        {
            BindOrderSystem();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnbindOrderSystem();
        }

        public void AutoDiscoverReferences()
        {
            if (panelRoot == null)
            {
                Transform foundChild = transform.Find("OrderBoxPanel");
                if (foundChild != null)
                {
                    panelRoot = foundChild.gameObject;
                    panelRect = foundChild.GetComponent<RectTransform>();
                }
            }
            else if (panelRect == null)
            {
                panelRect = panelRoot.GetComponent<RectTransform>();
            }

            Transform searchRoot = (panelRoot != null) ? panelRoot.transform : transform;

            if (boxImage == null) boxImage = searchRoot.GetComponent<Image>();
            if (itemIconImage == null) itemIconImage = searchRoot.Find("ItemIcon")?.GetComponent<Image>();
            if (progressText == null) progressText = searchRoot.Find("ProgressText")?.GetComponent<TextMeshProUGUI>();
            if (timerText == null) timerText = searchRoot.Find("TimerText")?.GetComponent<TextMeshProUGUI>();
        }

        private void BindOrderSystem()
        {
            _orderSystem = OrderSystem.Instance;
            if (_orderSystem == null)
            {
                _orderSystem = FindObjectOfType<OrderSystem>();
            }

            if (_orderSystem != null)
            {
                _orderSystem.OnOrderActivated -= HandleOrderActivated;
                _orderSystem.OnOrderActivated += HandleOrderActivated;

                _orderSystem.OnOrderProgressChanged -= HandleOrderProgressChanged;
                _orderSystem.OnOrderProgressChanged += HandleOrderProgressChanged;

                _orderSystem.OnOrderCompleted -= HandleOrderCompleted;
                _orderSystem.OnOrderCompleted += HandleOrderCompleted;

                _orderSystem.OnOrderFailed -= HandleOrderFailed;
                _orderSystem.OnOrderFailed += HandleOrderFailed;

                if (_orderSystem.HasActiveOrder)
                {
                    HandleOrderActivated(_orderSystem.ActiveOrder);
                }
            }
        }

        private void UnbindOrderSystem()
        {
            if (_orderSystem != null)
            {
                _orderSystem.OnOrderActivated -= HandleOrderActivated;
                _orderSystem.OnOrderProgressChanged -= HandleOrderProgressChanged;
                _orderSystem.OnOrderCompleted -= HandleOrderCompleted;
                _orderSystem.OnOrderFailed -= HandleOrderFailed;
            }
        }

        private void LateUpdate()
        {
            // Update posisi panel mengikuti posisi truk di dunia (World To Screen)
            if (panelRoot != null && panelRoot.activeSelf)
            {
                UpdatePanelScreenPosition();

                if (_orderSystem != null && _orderSystem.ActiveTruck != null)
                {
                    float remaining = _orderSystem.ActiveTruck.RemainingPatienceTime;
                    UpdateTimerDisplay(remaining);
                }
            }
        }

        private void UpdatePanelScreenPosition()
        {
            if (panelRect == null) return;

            if (_cachedCamera == null)
            {
                _cachedCamera = Camera.main;
            }

            if (_cachedCamera == null) return;

            Vector3 targetWorld = (followActiveTruck && _orderSystem != null && _orderSystem.ActiveTruck != null)
                ? _orderSystem.ActiveTruck.transform.position + worldOffset
                : defaultWorldPosition;

            // Kunci Z ke 0 agar jarak proyeksi ortografis stabil
            targetWorld.z = 0f;

            Vector3 screenPos = _cachedCamera.WorldToScreenPoint(targetWorld);
            panelRect.position = screenPos;
        }

        private void HandleOrderActivated(Order order)
        {
            if (order == null) return;

            OpenUI();
            UpdateProgressDisplay(order.CurrentAmount, order.RequiredAmount);
            UpdatePanelScreenPosition();
        }

        private void HandleOrderProgressChanged(Order order, int added)
        {
            if (order != null)
            {
                UpdateProgressDisplay(order.CurrentAmount, order.RequiredAmount);
            }
        }

        private void HandleOrderCompleted(Order order)
        {
            if (progressText != null)
            {
                progressText.text = $"{order.RequiredAmount}/{order.RequiredAmount}";
                progressText.color = new Color(0.1f, 0.65f, 0.2f, 1f); // Hijau selesai
            }
            if (timerText != null)
            {
                timerText.text = "Selesai!";
                timerText.color = new Color(0.1f, 0.65f, 0.2f, 1f);
            }

            CancelInvoke(nameof(CloseUI));
            Invoke(nameof(CloseUI), 1.2f);
        }

        private void HandleOrderFailed(Order order)
        {
            if (timerText != null)
            {
                timerText.text = "Waktu Habis!";
                timerText.color = new Color(0.85f, 0.2f, 0.15f, 1f);
            }

            CancelInvoke(nameof(CloseUI));
            Invoke(nameof(CloseUI), 1.2f);
        }

        public void OpenUI()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }

        public void CloseUI()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void UpdateProgressDisplay(int current, int required)
        {
            if (progressText != null)
            {
                progressText.text = $"{current}/{required}";
                progressText.color = (current >= required)
                    ? new Color(0.1f, 0.65f, 0.2f, 1f)
                    : new Color(0.2f, 0.15f, 0.1f, 1f);
            }
        }

        private void UpdateTimerDisplay(float seconds)
        {
            if (timerText == null) return;

            int mins = Mathf.Max(0, Mathf.FloorToInt(seconds / 60f));
            int secs = Mathf.Max(0, Mathf.FloorToInt(seconds % 60f));
            timerText.text = $"{mins:00}:{secs:00}";

            if (seconds < 30f)
            {
                timerText.color = new Color(0.9f, 0.15f, 0.15f, 1f);
            }
            else
            {
                timerText.color = new Color(0.35f, 0.2f, 0.1f, 1f);
            }
        }

        private void LoadDefaultSpritesIfNeeded()
        {
            if (orderBoxSprite == null)
            {
                orderBoxSprite = LoadOrderBoxSprite();
            }
            if (padiIconSprite == null)
            {
                padiIconSprite = LoadPadiSprite();
            }

            if (boxImage != null && boxImage.sprite == null && orderBoxSprite != null)
            {
                boxImage.sprite = orderBoxSprite;
            }
            if (itemIconImage != null && itemIconImage.sprite == null && padiIconSprite != null)
            {
                itemIconImage.sprite = padiIconSprite;
            }
        }

        private static Sprite LoadOrderBoxSprite()
        {
#if UNITY_EDITOR
            string boxPath = "Assets/SpriteSheet/Order Box.png";
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(boxPath);
#else
            return null;
#endif
        }

        private static Sprite LoadPadiSprite()
        {
#if UNITY_EDITOR
            string path = "Assets/SpriteSheet/Padi_animated-Sheet.png";
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite s && (s.name.Contains("3") || s.name.Contains("2")))
                {
                    return s;
                }
            }
#endif
            return null;
        }

        // ==========================================
        // RUNTIME CANVAS UI AUTO-GENERATION
        // ==========================================
        public static TruckOrderBubbleUI CreateRuntimeCanvasUI()
        {
            if (Instance != null) return Instance;

            // 1. Pastikan EventSystem ada
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 2. Buat atau cari Canvas khusus TruckOrderCanvas
            GameObject canvasGo = GameObject.Find("TruckOrderCanvas");
            Canvas canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;

            if (canvas == null)
            {
                canvasGo = new GameObject("TruckOrderCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // Selalu di atas layer truk dan world

                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // 3. Controller pada Canvas root (Controller Always Active)
            TruckOrderBubbleUI ui = canvasGo.GetComponent<TruckOrderBubbleUI>();
            if (ui == null)
            {
                ui = canvasGo.AddComponent<TruckOrderBubbleUI>();
            }

            // 4. Buat Modal Panel Root (OrderBoxPanel) sebagai anak Canvas
            Transform existingPanel = canvasGo.transform.Find("OrderBoxPanel");
            GameObject panelObj;
            RectTransform panelRect;

            if (existingPanel != null)
            {
                panelObj = existingPanel.gameObject;
                panelRect = panelObj.GetComponent<RectTransform>();
            }
            else
            {
                panelObj = new GameObject("OrderBoxPanel");
                panelObj.transform.SetParent(canvasGo.transform, false);
                panelRect = panelObj.AddComponent<RectTransform>();
                panelRect.sizeDelta = new Vector2(160, 140);
                panelRect.pivot = new Vector2(0.5f, 0.5f);

                Image bgImg = panelObj.AddComponent<Image>();
                bgImg.raycastTarget = false;
                Sprite boxSprite = LoadOrderBoxSprite();
                if (boxSprite != null) bgImg.sprite = boxSprite;

                // Child 1: ItemIcon (Padi)
                GameObject iconObj = new GameObject("ItemIcon");
                iconObj.transform.SetParent(panelObj.transform, false);
                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.12f, 0.44f);
                iconRect.anchorMax = new Vector2(0.48f, 0.86f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;

                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                Sprite padiSprite = LoadPadiSprite();
                if (padiSprite != null) iconImg.sprite = padiSprite;

                // Child 2: ProgressText ("0/5")
                GameObject progObj = new GameObject("ProgressText");
                progObj.transform.SetParent(panelObj.transform, false);
                RectTransform progRect = progObj.AddComponent<RectTransform>();
                progRect.anchorMin = new Vector2(0.50f, 0.44f);
                progRect.anchorMax = new Vector2(0.88f, 0.86f);
                progRect.offsetMin = Vector2.zero;
                progRect.offsetMax = Vector2.zero;

                TextMeshProUGUI progTmp = progObj.AddComponent<TextMeshProUGUI>();
                progTmp.text = "0/0";
                progTmp.fontSize = 24;
                progTmp.fontStyle = FontStyles.Bold;
                progTmp.alignment = TextAlignmentOptions.Center;
                progTmp.color = new Color(0.2f, 0.15f, 0.1f, 1f);

                // Child 3: TimerText ("01:45")
                GameObject timerObj = new GameObject("TimerText");
                timerObj.transform.SetParent(panelObj.transform, false);
                RectTransform timerRect = timerObj.AddComponent<RectTransform>();
                timerRect.anchorMin = new Vector2(0.1f, 0.14f);
                timerRect.anchorMax = new Vector2(0.9f, 0.40f);
                timerRect.offsetMin = Vector2.zero;
                timerRect.offsetMax = Vector2.zero;

                TextMeshProUGUI timerTmp = timerObj.AddComponent<TextMeshProUGUI>();
                timerTmp.text = "00:00";
                timerTmp.fontSize = 20;
                timerTmp.fontStyle = FontStyles.Bold;
                timerTmp.alignment = TextAlignmentOptions.Center;
                timerTmp.color = new Color(0.85f, 0.2f, 0.15f, 1f);

                ui.panelRoot = panelObj;
                ui.panelRect = panelRect;
                ui.boxImage = bgImg;
                ui.itemIconImage = iconImg;
                ui.progressText = progTmp;
                ui.timerText = timerTmp;
                ui.orderBoxSprite = boxSprite;
                ui.padiIconSprite = padiSprite;
            }

            // Sembunyikan panel modal awal (Controller tetap aktif)
            panelObj.SetActive(false);

            Debug.Log("<color=cyan>[TruckOrderBubbleUI]</color> Runtime Canvas UI berhasil dibuat di hierarki.");
            return ui;
        }

        // ==========================================
        // AUTO SCENE LOAD HOOK
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

            if (Instance == null && FindObjectOfType<TruckOrderBubbleUI>() == null)
            {
                CreateRuntimeCanvasUI();
            }
        }
    }
}
