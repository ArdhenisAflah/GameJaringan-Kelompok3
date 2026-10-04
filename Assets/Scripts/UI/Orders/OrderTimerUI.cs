using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Farming.Timer;

namespace UI.Orders
{
    /// <summary>
    /// Component UI untuk menangani timer pesanan (Order Timer).
    /// Menggunakan aset Order Box, Order Timer (Base), dan Order Timer (Fill).
    /// Mendukung kustomisasi mode transisi warna (Kontinu sejak awal, Instan/Step, atau Threshold Lerp),
    /// serta efek kedip (blink) ketika waktu pesanan hampir habis.
    /// </summary>
    public class OrderTimerUI : MonoBehaviour
    {
        [Header("Aset Visual Pesanan (Required Assets)")]
        [Tooltip("Asset Image untuk latar belakang kotak pesanan (Order Box).")]
        [SerializeField] private Image orderBoxImage;

        [Tooltip("Asset Image untuk latar belakang/track bar timer (Order Timer Base).")]
        [SerializeField] private Image orderTimerBaseImage;

        [Tooltip("Asset Image untuk isi bar timer (Order Timer Fill). Pastikan Image Type di-set ke 'Filled'.")]
        [SerializeField] private Image orderTimerFillImage;

        [Header("Tampilan Teks Waktu (Opsional)")]
        [Tooltip("Teks komponen TextMeshProUGUI untuk menampilkan angka sisa waktu (misal '01:30' atau '15s').")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Tooltip("Format teks waktu: MM:SS (Menit:Detik) atau SecondsOnly (Detik murni).")]
        [SerializeField] private TimeFormatType timeFormat = TimeFormatType.MinutesAndSeconds;

        [Tooltip("Tampilkan desimal/milidetik jika sisa waktu di bawah 10 detik.")]
        [SerializeField] private bool showDecimalsUnderTenSeconds = false;

        [Header("Pengaturan Durasi & Auto Update")]
        [Tooltip("Durasi default timer pesanan (dalam detik).")]
        [SerializeField] private float defaultDuration = 60f;

        [Tooltip("Jika true, timer akan berjalan otomatis di Update(). Jika false, timer di-tick secara manual.")]
        [SerializeField] private bool autoUpdate = true;

        [Header("Warna & Efek Visual")]
        [Tooltip("Ganti warna Order Timer Fill secara dinamis berdasarkan sisa waktu.")]
        [SerializeField] private bool useColorGradient = true;

        [Tooltip("Pilih mode transisi warna: Continuous (Sejak awal), InstantStep (Langsung ganti), ThresholdLerp, atau CustomGradientAsset.")]
        [SerializeField] private ColorTransitionMode transitionMode = ColorTransitionMode.Continuous;

        [Tooltip("Warna saat sisa waktu masih banyak (Normal/Awal).")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.8f, 0.2f, 1.0f);

        [Tooltip("Warna saat sisa waktu berada di tingkat sedang (Warning).")]
        [SerializeField] private Color warningColor = new Color(1.0f, 0.8f, 0.2f, 1.0f);

        [Tooltip("Warna saat sisa waktu kritis (Critical).")]
        [SerializeField] private Color criticalColor = new Color(0.9f, 0.2f, 0.2f, 1.0f);

        [Tooltip("Gradient Kustom Unity jika transitionMode di-set ke CustomGradientAsset.")]
        [SerializeField] private Gradient customGradient;

        [Tooltip("Batas rasio waktu untuk masuk ke kondisi Warning (0.0 - 1.0). Default = 0.5 (50%).")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float warningThreshold = 0.5f;

        [Tooltip("Batas rasio waktu untuk masuk ke kondisi Kritis (0.0 - 1.0). Default = 0.2 (20%).")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float criticalThreshold = 0.2f;

        [Tooltip("Aktifkan efek kedip (blink) saat sisa waktu berada di kondisi kritis.")]
        [SerializeField] private bool enableCriticalBlink = true;

        [Tooltip("Kecepatan kedipan saat kondisi kritis.")]
        [SerializeField] private float blinkSpeed = 5.0f;

        // Enum Mode Transisi Warna
        public enum ColorTransitionMode
        {
            Continuous,          // Gradasi halus secara kontinyu dari detik pertama (100% -> 0%)
            InstantStep,         // Langsung berubah warna tanpa gradasi saat melewati threshold
            ThresholdLerp,       // Transisi Lerp halus antar batas threshold
            CustomGradientAsset  // Menggunakan Unity Gradient Picker di Inspector
        }

        // Enum Format Waktu
        public enum TimeFormatType
        {
            MinutesAndSeconds,     // Format 01:30
            SecondsOnly,            // Format 90s
            MinutesSecondsOnlyText  // Format 1m 30s
        }

        // Event Callback
        public event Action OnTimerExpired;
        public event Action<float, float> OnTimerUpdated; // (remainingTime, duration)

        // Internal State
        private float _totalDuration;
        private float _remainingTime;
        private bool _isRunning;
        private bool _isPaused;
        private IGrowthTimer _boundModularTimer;

        public float TotalDuration => _totalDuration;
        public float RemainingTime => Mathf.Max(0f, _remainingTime);
        public float ProgressRatio => _totalDuration > 0f ? Mathf.Clamp01(_remainingTime / _totalDuration) : 0f;
        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        public bool IsExpired => _remainingTime <= 0f;

        // Encapsulated Property Accessors untuk Asset UI
        public Image OrderBoxImage => orderBoxImage;
        public Image OrderTimerBaseImage => orderTimerBaseImage;
        public Image OrderTimerFillImage => orderTimerFillImage;

        private void Awake()
        {
            ValidateFillImageType();
        }

        private void OnValidate()
        {
            ValidateFillImageType();
        }

        /// <summary>
        /// Memastikan orderTimerFillImage menggunakan Image Type Filled agar bar bisa menyusut/mengisi.
        /// </summary>
        private void ValidateFillImageType()
        {
            if (orderTimerFillImage != null && orderTimerFillImage.type != Image.Type.Filled)
            {
                orderTimerFillImage.type = Image.Type.Filled;
                orderTimerFillImage.fillMethod = Image.FillMethod.Horizontal;
                orderTimerFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        private void Update()
        {
            if (!autoUpdate) return;

            if (_boundModularTimer != null)
            {
                // Sinkronisasi dengan IGrowthTimer jika di-bind
                SetTimerProgress(_boundModularTimer.RemainingTime, _boundModularTimer.TargetDuration);
            }
            else if (_isRunning && !_isPaused)
            {
                Tick(Time.deltaTime);
            }

            // Animasi Kedip (Blink) saat Kritis
            HandleBlinkEffect();
        }

        /// <summary>
        /// Memulai timer pesanan dari durasi awal (detik).
        /// </summary>
        /// <param name="duration">Durasi total pesanan dalam detik.</param>
        public void StartTimer(float duration)
        {
            _totalDuration = Mathf.Max(0.001f, duration);
            _remainingTime = _totalDuration;
            _isRunning = true;
            _isPaused = false;
            _boundModularTimer = null;

            UpdateUIVisuals();
        }

        /// <summary>
        /// Mengatur posisi sisa waktu & durasi total secara manual (contoh: sinkronisasi dari server/network).
        /// </summary>
        public void SetTimerProgress(float remainingTime, float totalDuration)
        {
            _totalDuration = Mathf.Max(0.001f, totalDuration);
            _remainingTime = Mathf.Clamp(remainingTime, 0f, _totalDuration);
            _isRunning = _remainingTime > 0f;

            UpdateUIVisuals();

            if (_remainingTime <= 0f && _isRunning)
            {
                _isRunning = false;
                OnTimerExpired?.Invoke();
            }
        }

        /// <summary>
        /// Mengikat timer ini dengan sistem timer modular IGrowthTimer.
        /// </summary>
        public void BindModularTimer(IGrowthTimer timer)
        {
            _boundModularTimer = timer;
            if (timer != null)
            {
                SetTimerProgress(timer.RemainingTime, timer.TargetDuration);
            }
        }

        /// <summary>
        /// Memproses waktu mundur per frame.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isRunning || _isPaused) return;

            _remainingTime -= deltaTime;
            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                _isRunning = false;
                UpdateUIVisuals();
                OnTimerExpired?.Invoke();
            }
            else
            {
                UpdateUIVisuals();
                OnTimerUpdated?.Invoke(_remainingTime, _totalDuration);
            }
        }

        /// <summary>
        /// Menghentikan sementara timer pesanan.
        /// </summary>
        public void PauseTimer()
        {
            _isPaused = true;
        }

        /// <summary>
        /// Melanjutkan timer pesanan.
        /// </summary>
        public void ResumeTimer()
        {
            _isPaused = false;
        }

        /// <summary>
        /// Menghentikan dan mereset timer.
        /// </summary>
        public void StopTimer()
        {
            _isRunning = false;
            _isPaused = false;
            _remainingTime = 0f;
            UpdateUIVisuals();
        }

        /// <summary>
        /// Mengatur sprite aset secara dinamis dari kode jika belum diatur melalui Inspector.
        /// </summary>
        public void SetupAssets(Sprite orderBoxSprite, Sprite timerBaseSprite, Sprite timerFillSprite)
        {
            if (orderBoxImage != null && orderBoxSprite != null)
                orderBoxImage.sprite = orderBoxSprite;

            if (orderTimerBaseImage != null && timerBaseSprite != null)
                orderTimerBaseImage.sprite = timerBaseSprite;

            if (orderTimerFillImage != null && timerFillSprite != null)
                orderTimerFillImage.sprite = timerFillSprite;
        }

        /// <summary>
        /// Memperbarui elemen visual (fill amount, warna bar, dan teks timer).
        /// </summary>
        private void UpdateUIVisuals()
        {
            float ratio = ProgressRatio;

            // 1. Update Fill Amount pada Order Timer Fill
            if (orderTimerFillImage != null)
            {
                orderTimerFillImage.fillAmount = ratio;

                // Update Warna Fill berdasarkan sisa waktu
                if (useColorGradient)
                {
                    orderTimerFillImage.color = GetColorForRatio(ratio);
                }
            }

            // 2. Update Teks Timer
            if (timerText != null)
            {
                timerText.text = FormatTimeString(_remainingTime);
            }
        }

        /// <summary>
        /// Menghitung warna bar berdasarkan rasio waktu dan mode transisi yang dipilih.
        /// </summary>
        private Color GetColorForRatio(float ratio)
        {
            switch (transitionMode)
            {
                case ColorTransitionMode.Continuous:
                    // Gradasi kontinyu responsif sejak detik pertama (1.0 -> 0.5 -> 0.0)
                    if (ratio >= 0.5f)
                    {
                        float t = (ratio - 0.5f) / 0.5f;
                        return Color.Lerp(warningColor, normalColor, t);
                    }
                    else
                    {
                        float t = ratio / 0.5f;
                        return Color.Lerp(criticalColor, warningColor, t);
                    }

                case ColorTransitionMode.InstantStep:
                    // Berubah warna secara instan tanpa jeda samar saat melewati threshold
                    if (ratio > warningThreshold) return normalColor;
                    if (ratio > criticalThreshold) return warningColor;
                    return criticalColor;

                case ColorTransitionMode.CustomGradientAsset:
                    // Menggunakan Gradient Picker bawaan Unity jika terpasang
                    if (customGradient != null) return customGradient.Evaluate(ratio);
                    return Color.Lerp(criticalColor, normalColor, ratio);

                case ColorTransitionMode.ThresholdLerp:
                default:
                    if (ratio > warningThreshold)
                    {
                        return normalColor;
                    }
                    else if (ratio > criticalThreshold)
                    {
                        float t = (ratio - criticalThreshold) / (warningThreshold - criticalThreshold);
                        return Color.Lerp(warningColor, normalColor, t);
                    }
                    else
                    {
                        float t = ratio / criticalThreshold;
                        return Color.Lerp(criticalColor, warningColor, t);
                    }
            }
        }

        /// <summary>
        /// Menangani efek kedipan lembut saat waktu pesanan masuk ke tahap kritis.
        /// </summary>
        private void HandleBlinkEffect()
        {
            if (!enableCriticalBlink || orderTimerFillImage == null || !_isRunning || _isPaused) return;

            float ratio = ProgressRatio;
            if (ratio <= criticalThreshold && ratio > 0f)
            {
                float alpha = (Mathf.Sin(Time.time * blinkSpeed) + 1.0f) * 0.5f;
                Color baseColor = GetColorForRatio(ratio);
                orderTimerFillImage.color = Color.Lerp(baseColor, Color.white, alpha * 0.5f);
            }
        }

        /// <summary>
        /// Format string detik menjadi format menit dan detik yang mudah dibaca.
        /// </summary>
        private string FormatTimeString(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);

            if (showDecimalsUnderTenSeconds && seconds < 10f)
            {
                return $"{seconds:F1}s";
            }

            int mins = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);

            switch (timeFormat)
            {
                case TimeFormatType.SecondsOnly:
                    return $"{Mathf.CeilToInt(seconds)}s";

                case TimeFormatType.MinutesSecondsOnlyText:
                    if (mins > 0) return $"{mins}m {secs}s";
                    return $"{secs}s";

                case TimeFormatType.MinutesAndSeconds:
                default:
                    return $"{mins:00}:{secs:00}";
            }
        }
    }
}
