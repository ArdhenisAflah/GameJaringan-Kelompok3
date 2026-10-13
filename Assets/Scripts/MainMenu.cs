using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

public class MainMenu : MonoBehaviour
{
    public enum MenuState
    {
        IntroFade,          // Fase 1: Logo & Press Any Key fade in
        WaitingForInput,    // Fase 1: Press Any Key berkedip, menunggu input pemain
        Transitioning,      // Fase 2: Kamera pan down, Intro UI meluncur ke atas
        MainMenuView        // Fase 3 & 4: Tombol menu aktif, karakter spawn
    }

    [Header("Pengaturan Scene")]
    [Tooltip("Ketik nama scene lobby kamu secara persis (huruf besar/kecil berpengaruh)")]
    public string namaSceneLobby = "Lobby";

    [Header("Fase 1: Elemen UI Intro")]
    [Tooltip("Container utama untuk Logo dan Teks (agar bisa digeser ke atas bersamaan saat transisi)")]
    [SerializeField] private RectTransform introContainer;

    [Tooltip("CanvasGroup dari elemen Logo game")]
    [SerializeField] private CanvasGroup logoCanvasGroup;

    [Tooltip("CanvasGroup dari elemen Teks 'Press Any Key to Start'")]
    [SerializeField] private CanvasGroup pressKeyCanvasGroup;

    [Tooltip("Referensi TextMeshPro (opsional, jika ingin mengubah teks via script)")]
    [SerializeField] private TextMeshProUGUI pressKeyText;

    [Header("Durasi & Easing Animasi Intro")]
    [SerializeField] private float logoFadeDuration = 2.0f;
    [SerializeField] private float textFadeInDuration = 0.8f;
    [SerializeField] private float textBlinkDuration = 1.0f;
    [SerializeField] private float textMinAlpha = 0.2f;
    [SerializeField] private float textMaxAlpha = 1.0f;

    [Header("Fase 2: Kamera 2D & Transisi Layar")]
    [Tooltip("Kamera utama yang akan digerakkan (jika kosong, otomatis menggunakan Camera.main)")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Target Transform tujuan kamera (misal GameObject kosong di area lahan bawah). Jika diisi, kamera akan menuju koordinat Y objek ini.")]
    [SerializeField] private Transform cameraTarget;

    [Tooltip("Koordinat Y target lahan di bawah (digunakan jika slot cameraTarget di atas kosong)")]
    [SerializeField] private float targetYCoordinate = 0f;

    [Tooltip("Durasi pergerakan kamera turun (smooth pan down)")]
    [SerializeField] private float cameraPanDuration = 2.5f;

    [Tooltip("Kurva pergerakan kamera (halus di awal dan akhir)")]
    [SerializeField] private Ease cameraPanEase = Ease.InOutCubic;

    [Tooltip("Jarak container intro meluncur ke atas layar")]
    [SerializeField] private float introSlideUpDistance = 1200.0f;

    [Tooltip("Spawner kawanan burung di langit (opsional, akan otomatis dihentikan saat kamera turun)")]
    [SerializeField] private BirdFlockSpawner birdFlockSpawner;

    [Tooltip("Objek burung tunggal di langit (opsional fallback jika tidak memakai spawner)")]
    [SerializeField] private FlyingBird flyingBird;

    [Header("Event Selesai Transisi Kamera")]
    [Tooltip("Event Unity yang terpanggil secara otomatis ketika kamera sudah benar-benar berhenti di posisi tujuan")]
    public UnityEngine.Events.UnityEvent onCameraReachedTarget;

    // Event C# murni jika ada script lain yang ingin subscribe via kode
    public event Action OnCameraArrived;

    [Header("Fase 3: Tombol Menu Utama")]
    [Tooltip("Animator tombol utama dengan efek stagger (Lobby, Setting, Exit)")]
    [SerializeField] private UI.MainMenuButtonsAnimator buttonsAnimator;

    [Tooltip("Container tombol utama (jika menggunakan satu panel container tunggal)")]
    [SerializeField] private RectTransform mainButtonsContainer;
    [SerializeField] private float buttonsSlideInDuration = 1.0f;
    [SerializeField] private Ease buttonsSlideEase = Ease.OutBack;

    [Header("Fase 4: Aktivitas Karakter (Hooks)")]
    [Tooltip("Event/Hook saat menu utama siap memunculkan karakter")]
    public UnityEngine.Events.UnityEvent onMainMenuReady;

    private MenuState _currentState = MenuState.IntroFade;
    private Tween _blinkTween;
    private Sequence _introSequence;
    private Sequence _transitionSequence;
    private Vector2 _buttonsOriginalPos;

    public MenuState CurrentState => _currentState;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Simpan posisi awal tombol menu jika container sudah di-assign
        if (mainButtonsContainer != null)
        {
            _buttonsOriginalPos = mainButtonsContainer.anchoredPosition;
        }
    }

    private void Start()
    {
        SetupInitialState();
        PlayIntroSequence();
    }

    private void Update()
    {
        // Fase 2: Deteksi jika pemain menekan tombol keyboard apa saja atau klik mouse
        if (_currentState == MenuState.WaitingForInput)
        {
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
            {
                TriggerTransition();
            }
        }
    }

    /// <summary>
    /// Mengatur alpha dan posisi awal elemen UI sebelum animasi dimulai
    /// </summary>
    private void SetupInitialState()
    {
        if (logoCanvasGroup != null)
        {
            logoCanvasGroup.alpha = 0f;
        }

        if (pressKeyCanvasGroup != null)
        {
            pressKeyCanvasGroup.gameObject.SetActive(true);
            pressKeyCanvasGroup.alpha = 0f;
        }

        // Sembunyikan tombol menu di luar layar kiri jika container di-assign
        if (mainButtonsContainer != null)
        {
            mainButtonsContainer.anchoredPosition = new Vector2(-Screen.width, _buttonsOriginalPos.y);
            mainButtonsContainer.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Fase 1: Memainkan fade-in Logo selama 2 detik, lalu fade-in teks dan berkedip
    /// </summary>
    public void PlayIntroSequence()
    {
        _currentState = MenuState.IntroFade;

        // Bersihkan animasi sebelumnya jika ada
        _introSequence?.Kill();
        _blinkTween?.Kill();

        _introSequence = DOTween.Sequence();

        // 1. Logo muncul perlahan (alpha 0 ke 1) selama 2 detik
        if (logoCanvasGroup != null)
        {
            _introSequence.Append(logoCanvasGroup.DOFade(1f, logoFadeDuration).SetEase(Ease.InOutSine));
        }
        else
        {
            _introSequence.AppendInterval(0.5f);
        }

        // 2. Setelah Logo selesai fade-in, Teks 'Press Any Key' muncul perlahan (alpha 0 ke 1)
        if (pressKeyCanvasGroup != null)
        {
            _introSequence.Append(pressKeyCanvasGroup.DOFade(textMaxAlpha, textFadeInDuration).SetEase(Ease.InOutSine));
        }

        // 3. Setelah teks muncul, ubah state ke WaitingForInput dan jalankan kedipan
        _introSequence.OnComplete(() =>
        {
            _currentState = MenuState.WaitingForInput;
            StartBlinkingText();
        });
    }

    /// <summary>
    /// Animasi berkedip halus secara looping untuk teks 'Press Any Key to Start'
    /// </summary>
    private void StartBlinkingText()
    {
        if (pressKeyCanvasGroup == null) return;

        _blinkTween?.Kill();
        // Berkedip bolak-balik antara max alpha dan min alpha secara halus
        _blinkTween = pressKeyCanvasGroup.DOFade(textMinAlpha, textBlinkDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// Fase 2: Memulai transisi pan down kamera saat input ditekan
    /// </summary>
    public void TriggerTransition()
    {
        if (_currentState != MenuState.WaitingForInput) return;

        _currentState = MenuState.Transitioning;

        // 1. Langsung matikan / hilangkan teks 'Press Any Key' seketika
        _blinkTween?.Kill();
        if (pressKeyCanvasGroup != null)
        {
            pressKeyCanvasGroup.alpha = 0f;
            pressKeyCanvasGroup.gameObject.SetActive(false);
        }

        // Hentikan aktivitas burung di langit jika ada (burung yang sedang ada akan terbang menanjak ke atas)
        if (birdFlockSpawner != null)
        {
            birdFlockSpawner.StopSpawning(flyAway: true);
        }
        if (flyingBird != null)
        {
            flyingBird.FlyAwayUpward();
        }

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence();

        // 2. Logo / Intro Container meluncur ke atas keluar dari layar
        if (introContainer != null)
        {
            Vector2 targetPos = new Vector2(introContainer.anchoredPosition.x, introContainer.anchoredPosition.y + introSlideUpDistance);
            _transitionSequence.Join(introContainer.DOAnchorPos(targetPos, cameraPanDuration).SetEase(cameraPanEase));
        }
        else if (logoCanvasGroup != null)
        {
            _transitionSequence.Join(logoCanvasGroup.DOFade(0f, 0.5f));
        }

        // 3. Gerakkan Main Camera secara perlahan (smooth pan down) pada sumbu Y
        if (mainCamera != null)
        {
            // Ambil koordinat Y tujuan: dari cameraTarget (jika ada) atau targetYCoordinate
            float destinationY = cameraTarget != null ? cameraTarget.position.y : targetYCoordinate;

            _transitionSequence.Join(mainCamera.transform.DOMoveY(destinationY, cameraPanDuration).SetEase(cameraPanEase));
        }

        // 4. Panggil event/fungsi publik secara otomatis ketika kamera sudah benar-benar berhenti
        _transitionSequence.OnComplete(OnCameraReachedDestination);
    }

    /// <summary>
    /// Fungsi public yang otomatis terpanggil saat kamera benar-benar berhenti di posisi tujuan
    /// </summary>
    public void OnCameraReachedDestination()
    {
        Debug.Log("[MainMenu] Kamera telah berhenti di posisi tujuan lahan!");

        // Trigger event Inspector dan C# Action
        onCameraReachedTarget?.Invoke();
        OnCameraArrived?.Invoke();

        // Lanjutkan ke Fase 3: Tampilkan tombol menu utama
        EnterMainMenuPhase();
    }

    /// <summary>
    /// Fase 3 & 4: Tombol menu muncul dan memicu aktivitas karakter
    /// </summary>
    private void EnterMainMenuPhase()
    {
        _currentState = MenuState.MainMenuView;

        // Fase 3: Animasi slide-in tombol menu utama dari luar kiri layar
        if (buttonsAnimator != null)
        {
            buttonsAnimator.ShowMenu();
        }
        else if (mainButtonsContainer != null)
        {
            mainButtonsContainer.gameObject.SetActive(true);
            mainButtonsContainer.DOAnchorPos(_buttonsOriginalPos, buttonsSlideInDuration).SetEase(buttonsSlideEase);
        }

        // Fase 4: Trigger hook untuk aktivitas karakter
        onMainMenuReady?.Invoke();
    }

    // ==========================================
    // Navigasi Scene (Mempertahankan fungsi tombol UI)
    // ==========================================

    public void MoveToLobby()
    {
        Debug.Log("Berpindah ke scene: " + namaSceneLobby);
        SceneManager.LoadScene(namaSceneLobby);
    }

    public void QuitGame()
    {
        Debug.Log("Game ditutup!");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    private void OnDestroy()
    {
        // Bersihkan semua DOTween agar tidak memory leak
        _introSequence?.Kill();
        _blinkTween?.Kill();
        _transitionSequence?.Kill();
    }
}