using System;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

namespace UI
{
    public class MainMenuButtonsAnimator : MonoBehaviour
    {
        [Header("Tombol Main Menu (RectTransform)")]
        [Tooltip("Tombol Lobby (masuk pertama)")]
        [SerializeField] private RectTransform lobbyButton;

        [Tooltip("Tombol Setting (masuk kedua)")]
        [SerializeField] private RectTransform settingButton;

        [Tooltip("Tombol Exit (masuk ketiga)")]
        [SerializeField] private RectTransform exitButton;

        [Header("Pengaturan Animasi Meluncur (Slide-In)")]
        [Tooltip("Durasi animasi per tombol saat meluncur")]
        [SerializeField] private float slideDuration = 0.6f;

        [Tooltip("Jeda waktu antar tombol (stagger delay)")]
        [SerializeField] private float staggerDelay = 0.15f;

        [Tooltip("Jarak offset tombol saat berada di luar layar sebelah kiri")]
        [SerializeField] private float offscreenLeftOffset = 1000f;

        [Tooltip("Gaya kurva animasi saat masuk (default: OutBack memberi efek membal elastis)")]
        [SerializeField] private Ease enterEase = Ease.OutBack;

        [Tooltip("Gaya kurva animasi saat keluar")]
        [SerializeField] private Ease exitEase = Ease.InBack;

        [Header("Events")]
        [Tooltip("Event yang terpanggil otomatis saat seluruh tombol selesai meluncur ke tempatnya (Fase 4 trigger)")]
        public UnityEvent onAllButtonsShown;

        [Tooltip("Event yang terpanggil saat seluruh tombol selesai disembunyikan")]
        public UnityEvent onAllButtonsHidden;

        // C# Action events untuk subscribe via kode
        public event Action OnButtonsShown;
        public event Action OnButtonsHidden;

        // Menyimpan posisi target asli yang sudah diatur di Editor
        private Vector2 _lobbyTargetPos;
        private Vector2 _settingTargetPos;
        private Vector2 _exitTargetPos;

        private Sequence _buttonsSequence;
        private bool _isInitialized = false;

        private void Awake()
        {
            RecordTargetPositions();
        }

        private void Start()
        {
            // Atur posisi awal ketiga tombol berada di luar layar sebelah kiri
            SetButtonsOffscreenInstant();
        }

        /// <summary>
        /// Mencatat posisi target tombol dari layout yang sudah dibuat di Editor
        /// </summary>
        private void RecordTargetPositions()
        {
            if (_isInitialized) return;

            if (lobbyButton != null)
                _lobbyTargetPos = lobbyButton.anchoredPosition;

            if (settingButton != null)
                _settingTargetPos = settingButton.anchoredPosition;

            if (exitButton != null)
                _exitTargetPos = exitButton.anchoredPosition;

            _isInitialized = true;
        }

        /// <summary>
        /// Menempatkan tombol langsung ke luar layar sebelah kiri tanpa animasi
        /// </summary>
        public void SetButtonsOffscreenInstant()
        {
            RecordTargetPositions();

            if (lobbyButton != null)
            {
                lobbyButton.anchoredPosition = new Vector2(_lobbyTargetPos.x - offscreenLeftOffset, _lobbyTargetPos.y);
                lobbyButton.gameObject.SetActive(false);
            }

            if (settingButton != null)
            {
                settingButton.anchoredPosition = new Vector2(_settingTargetPos.x - offscreenLeftOffset, _settingTargetPos.y);
                settingButton.gameObject.SetActive(false);
            }

            if (exitButton != null)
            {
                exitButton.anchoredPosition = new Vector2(_exitTargetPos.x - offscreenLeftOffset, _exitTargetPos.y);
                exitButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Menggerakkan ketiga tombol masuk ke posisi target dengan jeda (stagger) bergantian
        /// Dapat dipanggil langsung dari UnityEvent pada MainMenu (onCameraReachedTarget)
        /// </summary>
        [ContextMenu("Show Menu (Test)")]
        public void ShowMenu()
        {
            RecordTargetPositions();

            _buttonsSequence?.Kill();
            _buttonsSequence = DOTween.Sequence();

            // 1. Pastikan semua tombol aktif
            if (lobbyButton != null) lobbyButton.gameObject.SetActive(true);
            if (settingButton != null) settingButton.gameObject.SetActive(true);
            if (exitButton != null) exitButton.gameObject.SetActive(true);

            // 2. Tombol Lobby meluncur lebih dulu
            if (lobbyButton != null)
            {
                _buttonsSequence.Append(lobbyButton.DOAnchorPos(_lobbyTargetPos, slideDuration).SetEase(enterEase));
            }

            // 3. Tombol Setting meluncur disusul dengan jeda (stagger)
            if (settingButton != null)
            {
                _buttonsSequence.Insert(staggerDelay, settingButton.DOAnchorPos(_settingTargetPos, slideDuration).SetEase(enterEase));
            }

            // 4. Tombol Exit meluncur terakhir dengan jeda (stagger * 2)
            if (exitButton != null)
            {
                _buttonsSequence.Insert(staggerDelay * 2f, exitButton.DOAnchorPos(_exitTargetPos, slideDuration).SetEase(enterEase));
            }

            // 5. Callback saat animasi seluruh tombol selesai
            _buttonsSequence.OnComplete(() =>
            {
                Debug.Log("[MainMenuButtonsAnimator] Semua tombol menu telah tampil!");
                onAllButtonsShown?.Invoke();
                OnButtonsShown?.Invoke();
            });
        }

        /// <summary>
        /// Menggerakkan tombol keluar ke sisi kiri layar (berguna saat membuka Settings / berpindah menu)
        /// </summary>
        [ContextMenu("Hide Menu (Test)")]
        public void HideMenu()
        {
            RecordTargetPositions();

            _buttonsSequence?.Kill();
            _buttonsSequence = DOTween.Sequence();

            float hideDuration = slideDuration * 0.75f;

            // Tombol Exit keluar dulu, lalu Setting, lalu Lobby (urutan mundur)
            if (exitButton != null)
            {
                Vector2 offscreenPos = new Vector2(_exitTargetPos.x - offscreenLeftOffset, _exitTargetPos.y);
                _buttonsSequence.Append(exitButton.DOAnchorPos(offscreenPos, hideDuration).SetEase(exitEase));
            }

            if (settingButton != null)
            {
                Vector2 offscreenPos = new Vector2(_settingTargetPos.x - offscreenLeftOffset, _settingTargetPos.y);
                _buttonsSequence.Insert(staggerDelay, settingButton.DOAnchorPos(offscreenPos, hideDuration).SetEase(exitEase));
            }

            if (lobbyButton != null)
            {
                Vector2 offscreenPos = new Vector2(_lobbyTargetPos.x - offscreenLeftOffset, _lobbyTargetPos.y);
                _buttonsSequence.Insert(staggerDelay * 2f, lobbyButton.DOAnchorPos(offscreenPos, hideDuration).SetEase(exitEase));
            }

            _buttonsSequence.OnComplete(() =>
            {
                if (lobbyButton != null) lobbyButton.gameObject.SetActive(false);
                if (settingButton != null) settingButton.gameObject.SetActive(false);
                if (exitButton != null) exitButton.gameObject.SetActive(false);

                onAllButtonsHidden?.Invoke();
                OnButtonsHidden?.Invoke();
            });
        }

        private void OnDestroy()
        {
            _buttonsSequence?.Kill();
        }
    }
}
