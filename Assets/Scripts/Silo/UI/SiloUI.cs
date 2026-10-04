using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;

namespace SiloSystem.UI
{
    /// <summary>
    /// Komponen controller UI Silo ("satu item satu box").
    /// Mengelola pembukaan panel saat interaksi 'O', pembuatan box sesuai jenis item,
    /// pemanggilan endpoint API-like untuk insert/pick item, dan penanganan feedback transaksi.
    /// </summary>
    public class SiloUI : MonoBehaviour
    {
        public static SiloUI Instance { get; private set; }

        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Header & Info")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI capacityText;
        [SerializeField] private Button closeButton;

        [Header("Item Grid Container")]
        [Tooltip("Transform parent (Content) tempat kartu box item digenerate.")]
        [SerializeField] private Transform itemContainer;
        [Tooltip("Prefab kartu slot item (SiloItemCard).")]
        [SerializeField] private GameObject itemCardPrefab;

        [Header("Player Hand Status")]
        [SerializeField] private TextMeshProUGUI playerHandStatusText;
        [SerializeField] private Button depositHeldItemButton;

        [Header("Notifikasi Feedback Transaksi")]
        [SerializeField] private TextMeshProUGUI feedbackText;
        [SerializeField] private float feedbackDuration = 3f;

        [Header("Katalog Data Item (Opsional)")]
        [SerializeField] private List<HarvestItemData> itemDatabase = new();

        private SiloStorage _activeSilo;
        private SiloInteractionTrigger _activeTrigger;
        private PlayerInventory _localPlayerInventory;
        private NetworkObject _localPlayerNob;
        private float _feedbackTimer = 0f;
        private readonly List<SiloItemCard> _spawnedCards = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            AutoDiscoverPanelReferences();

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseUI);
            }

            if (depositHeldItemButton != null)
            {
                depositHeldItemButton.onClick.AddListener(HandleDepositHeldItemClicked);
            }

            // Mulai dalam keadaan tertutup
            panelRoot.SetActive(false);
            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        }

        public void AutoDiscoverPanelReferences()
        {
            if (panelRoot == null) panelRoot = gameObject;
            if (titleText == null) titleText = transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            if (capacityText == null) capacityText = transform.Find("CapacityText")?.GetComponent<TextMeshProUGUI>();
            if (closeButton == null) closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
            if (itemContainer == null) itemContainer = transform.Find("ItemContainer") ?? transform.Find("Scroll View/Viewport/Content") ?? transform.Find("Content");
            if (playerHandStatusText == null) playerHandStatusText = transform.Find("PlayerHandStatusText")?.GetComponent<TextMeshProUGUI>();
            if (depositHeldItemButton == null) depositHeldItemButton = transform.Find("DepositHeldItemButton")?.GetComponent<Button>();
            if (feedbackText == null) feedbackText = transform.Find("FeedbackText")?.GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            FindAndBindSilo();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            UnbindSiloEvents();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseUI);
            }

            if (depositHeldItemButton != null)
            {
                depositHeldItemButton.onClick.RemoveListener(HandleDepositHeldItemClicked);
            }
        }

        private void Update()
        {
            // Auto-hide notifikasi feedback setelah durasi tertentu
            if (_feedbackTimer > 0f)
            {
                _feedbackTimer -= Time.deltaTime;
                if (_feedbackTimer <= 0f && feedbackText != null)
                {
                    feedbackText.gameObject.SetActive(false);
                }
            }

            // Pastikan referensi pemain lokal terikat
            if (_localPlayerInventory == null || _localPlayerNob == null)
            {
                TryBindLocalPlayer();
            }

            // Shortcut ESC untuk menutup UI jika sedang terbuka
            if (panelRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseUI();
            }
        }

        private void FindAndBindSilo()
        {
            if (_activeSilo == null)
            {
                _activeSilo = FindObjectOfType<SiloStorage>();
            }

            if (_activeSilo != null)
            {
                _activeTrigger = _activeSilo.GetComponent<SiloInteractionTrigger>();

                _activeSilo.OnStorageChanged += RefreshUI;
                _activeSilo.OnTransactionFeedback += ShowFeedback;

                if (_activeTrigger != null)
                {
                    _activeTrigger.OnSiloOpened += OpenUI;
                    _activeTrigger.OnSiloClosed += HandleTriggerClosed;
                }
            }
        }

        private void UnbindSiloEvents()
        {
            if (_activeSilo != null)
            {
                _activeSilo.OnStorageChanged -= RefreshUI;
                _activeSilo.OnTransactionFeedback -= ShowFeedback;
            }

            if (_activeTrigger != null)
            {
                _activeTrigger.OnSiloOpened -= OpenUI;
                _activeTrigger.OnSiloClosed -= HandleTriggerClosed;
            }

            if (_localPlayerInventory != null)
            {
                _localPlayerInventory.OnHeldItemChanged -= HandlePlayerHeldItemChanged;
            }
        }

        private void TryBindLocalPlayer()
        {
            var players = FindObjectsOfType<PlayerInventory>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].IsOwner)
                {
                    _localPlayerInventory = players[i];
                    _localPlayerNob = players[i].GetComponent<NetworkObject>();
                    _localPlayerInventory.OnHeldItemChanged += HandlePlayerHeldItemChanged;
                    UpdatePlayerHandUI();
                    break;
                }
            }
        }

        private void HandlePlayerHeldItemChanged(HarvestSlot slot)
        {
            UpdatePlayerHandUI();
            RefreshUI();
        }

        public void OpenUI()
        {
            if (_activeSilo == null)
            {
                FindAndBindSilo();
            }

            TryBindLocalPlayer();
            panelRoot.SetActive(true);
            RefreshUI();
        }

        public void CloseUI()
        {
            panelRoot.SetActive(false);
            if (_activeTrigger != null && _activeTrigger.IsOpen)
            {
                _activeTrigger.CloseSilo();
            }
        }

        private void HandleTriggerClosed()
        {
            panelRoot.SetActive(false);
        }

        /// <summary>
        /// Membuat dan memperbarui kartu box sesuai prinsip "satu item satu box".
        /// </summary>
        public void RefreshUI()
        {
            if (!panelRoot.activeSelf || _activeSilo == null) return;

            // 1. Perbarui Info Kapasitas
            int totalTersimpan = _activeSilo.GetTotalItemCount();
            int maxKapasitas = _activeSilo.MaxCapacity;
            if (capacityText != null)
            {
                capacityText.text = $"Kapasitas: {totalTersimpan} / {maxKapasitas} unit";
            }

            if (titleText != null)
            {
                titleText.text = _activeSilo.SiloName;
            }

            // 2. Kumpulkan seluruh jenis item yang perlu ditampilkan
            // Aturan Batch 2: "box di generated sesuai jenis item, bukan kuantitas item"
            // Tampilkan setiap jenis panen yang stoknya > 0 di Silo ATAU sedang dipegang pemain
            HashSet<HarvestType> itemTypesToShow = new HashSet<HarvestType>();

            var stored = _activeSilo.storedItems;
            for (int i = 0; i < stored.Count; i++)
            {
                if (stored[i].itemType != HarvestType.None && stored[i].quantity > 0)
                {
                    itemTypesToShow.Add(stored[i].itemType);
                }
            }

            if (_localPlayerInventory != null && _localPlayerInventory.HasItem)
            {
                itemTypesToShow.Add(_localPlayerInventory.HeldType);
            }

            // Bersihkan / Nonaktifkan box lama jika melebihi kebutuhan
            int cardIndex = 0;
            foreach (HarvestType type in itemTypesToShow)
            {
                int quantityInSilo = _activeSilo.GetItemCount(type);
                bool canDeposit = (_localPlayerInventory != null && _localPlayerInventory.HasItem && _localPlayerInventory.HeldType == type);

                HarvestItemData data = GetItemData(type);
                Sprite icon = data != null ? data.icon : null;
                string displayName = data != null ? data.displayName : type.ToString();

                SiloItemCard card = GetOrCreateCard(cardIndex);
                card.gameObject.SetActive(true);
                card.Configure(type, quantityInSilo, icon, displayName, canDeposit, OnPickItemRequested, OnDepositItemRequested);

                cardIndex++;
            }

            // Sembunyikan kartu ekstra yang tidak digunakan
            for (int i = cardIndex; i < _spawnedCards.Count; i++)
            {
                _spawnedCards[i].gameObject.SetActive(false);
            }

            UpdatePlayerHandUI();
        }

        private SiloItemCard GetOrCreateCard(int index)
        {
            if (index < _spawnedCards.Count)
            {
                return _spawnedCards[index];
            }

            GameObject cardObj = null;
            if (itemCardPrefab != null && itemContainer != null)
            {
                cardObj = Instantiate(itemCardPrefab, itemContainer);
            }
            else
            {
                // Fallback runtime card generator jika prefab belum di-assign di editor
                cardObj = CreateRuntimeCardObject();
            }

            SiloItemCard newCard = cardObj.GetComponent<SiloItemCard>();
            if (newCard == null)
            {
                newCard = cardObj.AddComponent<SiloItemCard>();
            }

            _spawnedCards.Add(newCard);
            return newCard;
        }

        private void UpdatePlayerHandUI()
        {
            if (playerHandStatusText == null) return;

            if (_localPlayerInventory != null && _localPlayerInventory.HasItem)
            {
                playerHandStatusText.text = $"Tangan Pemain: <b>{_localPlayerInventory.HeldType}</b> x{_localPlayerInventory.HeldQuantity}";
                if (depositHeldItemButton != null) depositHeldItemButton.interactable = true;
            }
            else
            {
                playerHandStatusText.text = "Tangan Pemain: <i>Kosong</i>";
                if (depositHeldItemButton != null) depositHeldItemButton.interactable = false;
            }
        }

        // ==========================================
        // CONSUME API ENDPOINTS KE SILO STORAGE
        // ==========================================
        private void OnPickItemRequested(HarvestType type)
        {
            if (_activeSilo == null || _localPlayerNob == null) return;

            // Panggil API-like function: Request Pick Item dari Silo
            _activeSilo.Api_RequestPickItem(_localPlayerNob, type, 1);
        }

        private void OnDepositItemRequested(HarvestType type)
        {
            if (_activeSilo == null || _localPlayerNob == null) return;

            // Panggil API-like function: Request Insert Item ke Silo
            _activeSilo.Api_RequestInsertHeldItem(_localPlayerNob, 1);
        }

        private void HandleDepositHeldItemClicked()
        {
            if (_activeSilo == null || _localPlayerNob == null) return;

            // Panggil API-like function: Setor item yang sedang dibawa pemain
            _activeSilo.Api_RequestInsertHeldItem(_localPlayerNob, 1);
        }

        private void ShowFeedback(bool success, string message)
        {
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(true);
                feedbackText.text = message;
                feedbackText.color = success ? new Color(0.4f, 1f, 0.4f, 1f) : new Color(1f, 0.5f, 0.4f, 1f);
                _feedbackTimer = feedbackDuration;
            }
        }

        private HarvestItemData GetItemData(HarvestType type)
        {
            for (int i = 0; i < itemDatabase.Count; i++)
            {
                if (itemDatabase[i] != null && itemDatabase[i].harvestType == type)
                {
                    return itemDatabase[i];
                }
            }
            return null;
        }

        private GameObject CreateRuntimeCardObject()
        {
            GameObject cardGo = new GameObject("SiloItemBox");
            if (itemContainer != null) cardGo.transform.SetParent(itemContainer, false);

            RectTransform rect = cardGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(210, 220);

            Image bg = cardGo.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.17f, 0.24f, 0.95f);

            // 1. Icon Image
            GameObject iconGo = new GameObject("IconImage");
            iconGo.transform.SetParent(cardGo.transform, false);
            RectTransform iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(0, 50);
            iconRect.sizeDelta = new Vector2(60, 60);
            Image iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.white;

            // 2. Name Text
            GameObject nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(cardGo.transform, false);
            RectTransform nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchoredPosition = new Vector2(0, 5);
            nameRect.sizeDelta = new Vector2(190, 26);
            TextMeshProUGUI nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.fontSize = 18;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = Color.white;

            // 3. Quantity Text
            GameObject qtyGo = new GameObject("QuantityText");
            qtyGo.transform.SetParent(cardGo.transform, false);
            RectTransform qtyRect = qtyGo.AddComponent<RectTransform>();
            qtyRect.anchoredPosition = new Vector2(0, -22);
            qtyRect.sizeDelta = new Vector2(190, 22);
            TextMeshProUGUI qtyTmp = qtyGo.AddComponent<TextMeshProUGUI>();
            qtyTmp.fontSize = 14;
            qtyTmp.alignment = TextAlignmentOptions.Center;
            qtyTmp.color = new Color(1f, 0.9f, 0.3f, 1f);

            // 4. Pick Button (Ambil 1)
            GameObject pickGo = new GameObject("PickButton");
            pickGo.transform.SetParent(cardGo.transform, false);
            RectTransform pickRect = pickGo.AddComponent<RectTransform>();
            pickRect.anchoredPosition = new Vector2(-48, -68);
            pickRect.sizeDelta = new Vector2(90, 36);
            Image pickImg = pickGo.AddComponent<Image>();
            pickImg.color = new Color(0.2f, 0.55f, 0.85f, 1f);
            pickGo.AddComponent<Button>();

            GameObject pickTxtGo = new GameObject("Text");
            pickTxtGo.transform.SetParent(pickGo.transform, false);
            RectTransform pTxtRect = pickTxtGo.AddComponent<RectTransform>();
            pTxtRect.sizeDelta = pickRect.sizeDelta;
            TextMeshProUGUI pTmp = pickTxtGo.AddComponent<TextMeshProUGUI>();
            pTmp.text = "Ambil 1";
            pTmp.fontSize = 14;
            pTmp.alignment = TextAlignmentOptions.Center;
            pTmp.color = Color.white;

            // 5. Deposit Button (Setor 1)
            GameObject depGo = new GameObject("DepositButton");
            depGo.transform.SetParent(cardGo.transform, false);
            RectTransform depRect = depGo.AddComponent<RectTransform>();
            depRect.anchoredPosition = new Vector2(48, -68);
            depRect.sizeDelta = new Vector2(90, 36);
            Image depImg = depGo.AddComponent<Image>();
            depImg.color = new Color(0.25f, 0.7f, 0.35f, 1f);
            depGo.AddComponent<Button>();

            GameObject depTxtGo = new GameObject("Text");
            depTxtGo.transform.SetParent(depGo.transform, false);
            RectTransform dTxtRect = depTxtGo.AddComponent<RectTransform>();
            dTxtRect.sizeDelta = depRect.sizeDelta;
            TextMeshProUGUI dTmp = depTxtGo.AddComponent<TextMeshProUGUI>();
            dTmp.text = "Setor 1";
            dTmp.fontSize = 14;
            dTmp.alignment = TextAlignmentOptions.Center;
            dTmp.color = Color.white;

            SiloItemCard card = cardGo.AddComponent<SiloItemCard>();
            card.AutoDiscoverChildReferences();

            return cardGo;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeUI()
        {
            if (FindObjectOfType<SiloStorage>() == null) return;
            if (FindObjectOfType<SiloUI>() != null) return;

            CreateRuntimeCanvasUI();
        }

        public static SiloUI CreateRuntimeCanvasUI()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("SiloCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            GameObject panelObj = new GameObject("SiloUIPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(860, 560);

            Image panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.12f, 0.18f, 0.96f);

            // Title
            GameObject titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(panelObj.transform, false);
            RectTransform tRect = titleGo.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0, 1);
            tRect.anchorMax = new Vector2(1, 1);
            tRect.pivot = new Vector2(0.5f, 1);
            tRect.anchoredPosition = new Vector2(0, -20);
            tRect.sizeDelta = new Vector2(0, 40);
            TextMeshProUGUI tTmp = titleGo.AddComponent<TextMeshProUGUI>();
            tTmp.text = "LUMBUNG PANEN (SILO)";
            tTmp.fontSize = 28;
            tTmp.fontStyle = FontStyles.Bold;
            tTmp.alignment = TextAlignmentOptions.Center;
            tTmp.color = Color.white;

            // Capacity
            GameObject capGo = new GameObject("CapacityText");
            capGo.transform.SetParent(panelObj.transform, false);
            RectTransform cRect = capGo.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 1);
            cRect.anchorMax = new Vector2(1, 1);
            cRect.pivot = new Vector2(0.5f, 1);
            cRect.anchoredPosition = new Vector2(0, -65);
            cRect.sizeDelta = new Vector2(0, 30);
            TextMeshProUGUI cTmp = capGo.AddComponent<TextMeshProUGUI>();
            cTmp.text = "Kapasitas: 0 / 999 unit";
            cTmp.fontSize = 16;
            cTmp.alignment = TextAlignmentOptions.Center;
            cTmp.color = new Color(1f, 0.85f, 0.3f, 1f);

            // Close Button
            GameObject closeGo = new GameObject("CloseButton");
            closeGo.transform.SetParent(panelObj.transform, false);
            RectTransform clRect = closeGo.AddComponent<RectTransform>();
            clRect.anchorMin = new Vector2(1, 1);
            clRect.anchorMax = new Vector2(1, 1);
            clRect.pivot = new Vector2(1, 1);
            clRect.anchoredPosition = new Vector2(-15, -15);
            clRect.sizeDelta = new Vector2(40, 40);
            Image clImg = closeGo.AddComponent<Image>();
            clImg.color = new Color(0.85f, 0.25f, 0.25f, 1f);
            closeGo.AddComponent<Button>();

            GameObject clTxtGo = new GameObject("Text");
            clTxtGo.transform.SetParent(closeGo.transform, false);
            RectTransform cltRect = clTxtGo.AddComponent<RectTransform>();
            cltRect.sizeDelta = clRect.sizeDelta;
            TextMeshProUGUI clTmp = clTxtGo.AddComponent<TextMeshProUGUI>();
            clTmp.text = "X";
            clTmp.fontSize = 20;
            clTmp.fontStyle = FontStyles.Bold;
            clTmp.alignment = TextAlignmentOptions.Center;
            clTmp.color = Color.white;

            // Scroll View
            GameObject scrollGo = new GameObject("ItemScrollView");
            scrollGo.transform.SetParent(panelObj.transform, false);
            RectTransform sRect = scrollGo.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 0);
            sRect.anchorMax = new Vector2(1, 1);
            sRect.offsetMin = new Vector2(25, 90);
            sRect.offsetMax = new Vector2(-25, -105);

            Image sBg = scrollGo.AddComponent<Image>();
            sBg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
            ScrollRect sr = scrollGo.AddComponent<ScrollRect>();
            sr.horizontal = true;
            sr.vertical = false;

            GameObject vpGo = new GameObject("Viewport");
            vpGo.transform.SetParent(scrollGo.transform, false);
            RectTransform vpRect = vpGo.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = new Vector2(-10, -10);
            vpGo.AddComponent<RectMask2D>();
            sr.viewport = vpRect;

            GameObject contGo = new GameObject("ItemContainer");
            contGo.transform.SetParent(vpGo.transform, false);
            RectTransform contRect = contGo.AddComponent<RectTransform>();
            contRect.anchorMin = new Vector2(0, 0);
            contRect.anchorMax = new Vector2(0, 1);
            contRect.pivot = new Vector2(0, 0.5f);

            HorizontalLayoutGroup hlg = contGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.padding = new RectOffset(15, 15, 15, 15);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            ContentSizeFitter csf = contGo.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = contRect;

            // Bottom Player Hand Status
            GameObject handGo = new GameObject("PlayerHandStatusText");
            handGo.transform.SetParent(panelObj.transform, false);
            RectTransform hRect = handGo.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0, 0);
            hRect.anchorMax = new Vector2(0.6f, 0);
            hRect.pivot = new Vector2(0, 0);
            hRect.anchoredPosition = new Vector2(30, 25);
            hRect.sizeDelta = new Vector2(0, 45);
            TextMeshProUGUI hTmp = handGo.AddComponent<TextMeshProUGUI>();
            hTmp.text = "Tangan Pemain: <i>Kosong</i>";
            hTmp.fontSize = 18;
            hTmp.alignment = TextAlignmentOptions.MidlineLeft;
            hTmp.color = Color.white;

            // Bottom Deposit Button
            GameObject depGo = new GameObject("DepositHeldItemButton");
            depGo.transform.SetParent(panelObj.transform, false);
            RectTransform dRect = depGo.AddComponent<RectTransform>();
            dRect.anchorMin = new Vector2(1, 0);
            dRect.anchorMax = new Vector2(1, 0);
            dRect.pivot = new Vector2(1, 0);
            dRect.anchoredPosition = new Vector2(-30, 25);
            dRect.sizeDelta = new Vector2(240, 45);
            Image dImg = depGo.AddComponent<Image>();
            dImg.color = new Color(0.2f, 0.65f, 0.35f, 1f);
            depGo.AddComponent<Button>();

            GameObject dTxtGo = new GameObject("Text");
            dTxtGo.transform.SetParent(depGo.transform, false);
            RectTransform dtRect = dTxtGo.AddComponent<RectTransform>();
            dtRect.sizeDelta = dRect.sizeDelta;
            TextMeshProUGUI dtTmp = dTxtGo.AddComponent<TextMeshProUGUI>();
            dtTmp.text = "Setor Item di Tangan";
            dtTmp.fontSize = 16;
            dtTmp.fontStyle = FontStyles.Bold;
            dtTmp.alignment = TextAlignmentOptions.Center;
            dtTmp.color = Color.white;

            // Feedback Text
            GameObject feedGo = new GameObject("FeedbackText");
            feedGo.transform.SetParent(panelObj.transform, false);
            RectTransform fRect = feedGo.AddComponent<RectTransform>();
            fRect.anchorMin = new Vector2(0, 0);
            fRect.anchorMax = new Vector2(1, 0);
            fRect.pivot = new Vector2(0.5f, 0);
            fRect.anchoredPosition = new Vector2(0, 72);
            fRect.sizeDelta = new Vector2(0, 25);
            TextMeshProUGUI fTmp = feedGo.AddComponent<TextMeshProUGUI>();
            fTmp.fontSize = 15;
            fTmp.alignment = TextAlignmentOptions.Center;
            feedGo.SetActive(false);

            SiloUI ui = panelObj.AddComponent<SiloUI>();
            ui.AutoDiscoverPanelReferences();
            panelObj.SetActive(false);

            return ui;
        }
    }
}


