using System;
using UnityEngine;
using FishNet.Object;
using TMPro;

namespace SiloSystem
{
    /// <summary>
    /// Komponen untuk mendeteksi kehadiran pemain di dekat Silo menggunakan Collider2D (Trigger),
    /// serta mengikat input tombol 'O' untuk membuka/menutup interaksi Silo pada pemain lokal (IsOwner).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SiloInteractionTrigger : MonoBehaviour
    {
        [Header("Pengaturan Input")]
        [Tooltip("Tombol untuk membuka/menutup Silo.")]
        [SerializeField] private KeyCode interactKey = KeyCode.O;

        [Header("Jangkauan & Deteksi")]
        [Tooltip("Jarak maksimal interaksi sebagai safety fallback selain collider trigger.")]
        [SerializeField] private float maxInteractionDistance = 2.5f;

        [Header("Indikator Visual")]
        [Tooltip("Objek petunjuk visual (misal teks '[O] Buka Silo') yang muncul saat pemain mendekat.")]
        [SerializeField] private GameObject interactionPromptObj;
        [SerializeField] private TextMeshPro promptText;

        [Header("Referensi Storage")]
        [SerializeField] private SiloStorage siloStorage;

        private bool _isLocalPlayerInRange = false;
        private Transform _localPlayerTransform;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;
        public bool IsLocalPlayerInRange => _isLocalPlayerInRange;

        public event Action OnSiloOpened;
        public event Action OnSiloClosed;
        public event Action<bool> OnPlayerInRangeChanged;

        private void Awake()
        {
            if (siloStorage == null)
            {
                siloStorage = GetComponent<SiloStorage>();
            }

            SetupInteractionPrompt();
        }

        private void SetupInteractionPrompt()
        {
            // Jika prompt belum dipasang di Inspector, buat indikator teks TextMeshPro world-space secara otomatis
            if (interactionPromptObj == null)
            {
                interactionPromptObj = new GameObject("InteractionPrompt");
                interactionPromptObj.transform.SetParent(transform, false);
                interactionPromptObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);

                promptText = interactionPromptObj.AddComponent<TextMeshPro>();
                promptText.text = "[O] Buka Silo";
                promptText.fontSize = 4f;
                promptText.alignment = TextAlignmentOptions.Center;
                promptText.color = new Color(1f, 0.95f, 0.4f, 1f); // Kuning hangat
                promptText.sortingOrder = 50;

                // Pastikan prompt tidak aktif saat awal
                interactionPromptObj.SetActive(false);
            }
            else if (promptText == null)
            {
                promptText = interactionPromptObj.GetComponentInChildren<TextMeshPro>();
            }
        }

        private void Update()
        {
            // 1. Jika transform pemain lokal belum terikat (misal collider belum trigger), cari pemain lokal
            if (_localPlayerTransform == null)
            {
                TryFindLocalPlayer();
            }

            // 2. Evaluasi jarak pemain lokal ke Silo
            if (_localPlayerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, _localPlayerTransform.position);
                if (dist <= maxInteractionDistance && !_isLocalPlayerInRange)
                {
                    SetPlayerInRange(true, _localPlayerTransform);
                }
                else if (dist > maxInteractionDistance && _isLocalPlayerInRange)
                {
                    SetPlayerInRange(false, null);
                }
            }

            // 3. Input 'O' hanya dideteksi jika pemain lokal berada di jangkauan Silo
            if (_isLocalPlayerInRange)
            {
                if (Input.GetKeyDown(interactKey))
                {
                    ToggleSilo();
                }
            }
        }

        private void TryFindLocalPlayer()
        {
            // Cari player yang dimiliki oleh client lokal (IsOwner)
            var players = FindObjectsOfType<PlayerMovement>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].IsOwner)
                {
                    _localPlayerTransform = players[i].transform;
                    break;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Periksa apakah objek yang memasuki area adalah Player lokal (IsOwner)
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                SetPlayerInRange(true, netObj.transform);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                SetPlayerInRange(false, null);
            }
        }

        private void SetPlayerInRange(bool inRange, Transform playerTransform)
        {
            if (_isLocalPlayerInRange == inRange) return;

            _isLocalPlayerInRange = inRange;
            _localPlayerTransform = inRange ? playerTransform : null;

            if (interactionPromptObj != null)
            {
                interactionPromptObj.SetActive(inRange);
                UpdatePromptText();
            }

            // Jika pemain menjauh saat Silo terbuka, otomatis tutup Silo
            if (!inRange && _isOpen)
            {
                CloseSilo();
            }

            OnPlayerInRangeChanged?.Invoke(inRange);
            Debug.Log($"[SiloInteraction] Pemain lokal {(inRange ? "memasuki" : "meninggalkan")} area Silo.");
        }

        public void ToggleSilo()
        {
            if (_isOpen)
            {
                CloseSilo();
            }
            else
            {
                OpenSilo();
            }
        }

        public void OpenSilo()
        {
            _isOpen = true;
            UpdatePromptText();
            OnSiloOpened?.Invoke();

            int totalStok = siloStorage != null ? siloStorage.GetTotalItemCount() : 0;
            Debug.Log($"<color=yellow>[Silo]</color> Silo Terbuka! Total hasil panen tersimpan: {totalStok} unit. Tekan '{interactKey}' untuk menutup.");
        }

        public void CloseSilo()
        {
            _isOpen = false;
            UpdatePromptText();
            OnSiloClosed?.Invoke();

            Debug.Log($"<color=yellow>[Silo]</color> Silo Ditutup.");
        }

        private void UpdatePromptText()
        {
            if (promptText != null)
            {
                promptText.text = _isOpen ? $"[{interactKey}] Tutup Silo" : $"[{interactKey}] Buka Silo";
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Visualisasi jangkauan trigger interaksi di Scene Editor
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, maxInteractionDistance);
        }
    }
}
