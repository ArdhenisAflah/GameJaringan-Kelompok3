using System.Collections;
using UnityEngine;

public class FlyingBird : MonoBehaviour
{
    [Header("Kecepatan & Batas Horizontal")]
    [Tooltip("Kecepatan burung meluncur dari kiri ke kanan")]
    [SerializeField] private float speed = 3.5f;

    [Tooltip("Posisi X awal di luar layar kiri")]
    [SerializeField] private float startX = -13.0f;

    [Tooltip("Posisi X akhir di luar layar kanan")]
    [SerializeField] private float endX = 13.0f;

    [Header("Gerakan Naik Turun (Wave Bobbing)")]
    [Tooltip("Ketinggian ayunan naik turun")]
    [SerializeField] private float waveAmplitude = 0.4f;

    [Tooltip("Frekuensi atau kecepatan kepakan naik turun")]
    [SerializeField] private float waveFrequency = 4.0f;

    [Tooltip("Memberi kemiringan (tilt) alami saat burung naik atau turun")]
    [SerializeField] private bool enableTiltEffect = true;

    [Tooltip("Sudut kemiringan maksimal dalam derajat saat terbang normal")]
    [SerializeField] private float tiltAngle = 8.0f;

    [Header("Transisi Layar Turun (Fly Away Upward)")]
    [Tooltip("Kecepatan menanjak ke atas saat kamera mulai bergerak turun")]
    [SerializeField] private float climbSpeed = 2.5f;

    [Tooltip("Sudut kemiringan hidung burung mendongak ke atas saat menanjak")]
    [SerializeField] private float climbTiltAngle = 20.0f;

    [Tooltip("Batas ketinggian Y atas layar untuk menonaktifkan burung")]
    [SerializeField] private float topExitY = 9.0f;

    [Header("Rentang Ketinggian (Sumbu Y) - Mode Tunggal")]
    [SerializeField] private float minY = 1.0f;
    [SerializeField] private float maxY = 4.0f;

    [Header("Siklus Looping - Mode Tunggal")]
    [Tooltip("Centang jika burung berdiri sendiri tanpa spawner kawanan")]
    [SerializeField] private bool standaloneMode = true;
    [SerializeField] private float minRespawnDelay = 1.5f;
    [SerializeField] private float maxRespawnDelay = 4.0f;

    private float _currentBaseY;
    private float _waveTimer;
    private bool _isFlying = false;
    private bool _isWaitingRespawn = false;
    private bool _isEscapingUpward = false;
    private SpriteRenderer _spriteRenderer;
    private System.Action<FlyingBird> _onFlightComplete;

    public bool IsFlying => _isFlying;
    public bool IsEscapingUpward => _isEscapingUpward;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // Hanya jalan otomatis jika tidak dikelola oleh BirdFlockSpawner
        if (standaloneMode)
        {
            _isFlying = true;
            SpawnAtStart();
        }
    }

    /// <summary>
    /// Dipanggil oleh BirdFlockSpawner untuk meluncurkan burung dengan parameter dinamis
    /// </summary>
    public void Launch(Vector3 spawnPosition, float customSpeed, float customAmplitude, float customFrequency, float scale, System.Action<FlyingBird> onComplete = null)
    {
        standaloneMode = false;
        _isWaitingRespawn = false;
        _isEscapingUpward = false;
        _isFlying = true;
        _onFlightComplete = onComplete;

        speed = customSpeed;
        waveAmplitude = customAmplitude;
        waveFrequency = customFrequency;
        _currentBaseY = spawnPosition.y;
        _waveTimer = Random.Range(0f, Mathf.PI * 2f); // Offset fase gelombang acak

        transform.position = spawnPosition;
        transform.localScale = Vector3.one * scale;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;
        }

        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!_isFlying || _isWaitingRespawn) return;

        // Jika sedang mode transisi kamera: ketinggian bertambah naik terus menerus (menanjak ke atas)
        if (_isEscapingUpward)
        {
            _currentBaseY += climbSpeed * Time.deltaTime;
        }

        // 1. Gerakan horizontal maju ke kanan
        float currentX = transform.position.x + (speed * Time.deltaTime);

        // 2. Gerakan vertikal naik-turun menggunakan gelombang sinus (Mathf.Sin)
        _waveTimer += Time.deltaTime * waveFrequency;
        float waveOffset = Mathf.Sin(_waveTimer) * waveAmplitude;
        float currentY = _currentBaseY + waveOffset;

        transform.position = new Vector3(currentX, currentY, transform.position.z);

        // 3. Efek kemiringan (tilt)
        if (enableTiltEffect)
        {
            if (_isEscapingUpward)
            {
                // Mendongak menanjak ke atas secara mulus
                Quaternion targetRot = Quaternion.Euler(0f, 0f, climbTiltAngle);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * 6f);
            }
            else
            {
                // Gerakan naik-turun gelombang normal
                float verticalVelocity = Mathf.Cos(_waveTimer);
                float targetZRotation = verticalVelocity * tiltAngle;
                transform.rotation = Quaternion.Euler(0f, 0f, targetZRotation);
            }
        }

        // 4. Deteksi ketika burung sudah keluar dari batas kanan layar ATAU batas atas layar
        if (currentX >= endX || currentY >= topExitY)
        {
            if (standaloneMode && !_isEscapingUpward)
            {
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                _isFlying = false;
                _isEscapingUpward = false;
                gameObject.SetActive(false);
                _onFlightComplete?.Invoke(this);
            }
        }
    }

    private void SpawnAtStart()
    {
        _currentBaseY = Random.Range(minY, maxY);
        _waveTimer = Random.Range(0f, Mathf.PI * 2f);
        _isEscapingUpward = false;
        transform.position = new Vector3(startX, _currentBaseY, transform.position.z);

        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;
        }
    }

    private IEnumerator RespawnRoutine()
    {
        _isWaitingRespawn = true;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = false;
        }

        float waitTime = Random.Range(minRespawnDelay, maxRespawnDelay);
        yield return new WaitForSeconds(waitTime);

        if (_isFlying && !_isEscapingUpward)
        {
            SpawnAtStart();
            _isWaitingRespawn = false;
        }
    }

    /// <summary>
    /// Burung beralih mode: terbang menanjak ke atas keluar layar seiring kamera bergerak turun
    /// </summary>
    public void FlyAwayUpward()
    {
        if (!_isFlying || !gameObject.activeInHierarchy) return;

        _isEscapingUpward = true;
        speed *= 1.25f; // Sedikit lebih cepat saat menanjak keluar
        StopAllCoroutines();
    }

    /// <summary>
    /// Hentikan burung secara instan
    /// </summary>
    public void StopSpawning()
    {
        _isFlying = false;
        _isWaitingRespawn = false;
        _isEscapingUpward = false;
        StopAllCoroutines();

        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = false;
        }

        gameObject.SetActive(false);
    }

    public void ResumeFlying()
    {
        _isFlying = true;
        _isWaitingRespawn = false;
        _isEscapingUpward = false;
        gameObject.SetActive(true);
        SpawnAtStart();
    }
}
