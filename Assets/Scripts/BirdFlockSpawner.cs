using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdFlockSpawner : MonoBehaviour
{
    [Header("Prefab / Template Burung")]
    [Tooltip("Prefab atau GameObject burung (harus memiliki FlyingBird, Animator, dan SpriteRenderer)")]
    [SerializeField] private GameObject birdPrefab;

    [Tooltip("Jumlah burung awal yang disiapkan di pool memori")]
    [SerializeField] private int poolSize = 8;

    [Header("Jumlah Burung per Kawanan (Flock Size)")]
    [Tooltip("Jumlah minimum burung dalam satu kawanan")]
    [SerializeField] private int minBirdsPerFlock = 2;

    [Tooltip("Jumlah maksimum burung dalam satu kawanan")]
    [SerializeField] private int maxBirdsPerFlock = 5;

    [Header("Jeda Waktu Antar Kawanan")]
    [Tooltip("Jeda waktu minimum sebelum kawanan berikutnya muncul (detik)")]
    [SerializeField] private float minIntervalBetweenFlocks = 3.0f;

    [Tooltip("Jeda waktu maksimum sebelum kawanan berikutnya muncul (detik)")]
    [SerializeField] private float maxIntervalBetweenFlocks = 6.5f;

    [Header("Variasi Kecepatan & Skala")]
    [Tooltip("Kecepatan terbang minimum")]
    [SerializeField] private float minSpeed = 2.8f;

    [Tooltip("Kecepatan terbang maksimum")]
    [SerializeField] private float maxSpeed = 4.2f;

    [Tooltip("Ukuran burung terkecil (efek terbang lebih jauh/di latar belakang)")]
    [SerializeField] private float minScale = 0.75f;

    [Tooltip("Ukuran burung terbesar (efek terbang lebih dekat)")]
    [SerializeField] private float maxScale = 1.15f;

    [Header("Variasi Gerakan Naik Turun (Wave Bobbing)")]
    [SerializeField] private float minAmplitude = 0.25f;
    [SerializeField] private float maxAmplitude = 0.5f;
    [SerializeField] private float minFrequency = 3.0f;
    [SerializeField] private float maxFrequency = 5.0f;

    [Header("Area Muncul & Hilang")]
    [SerializeField] private float startX = -13.0f;
    [SerializeField] private float endX = 13.0f;
    [SerializeField] private float minY = 1.0f;
    [SerializeField] private float maxY = 4.5f;

    [Header("Sebaran Formasi Kawanan")]
    [Tooltip("Jarak sebar horizontal acak antar burung di belakang pemimpin")]
    [SerializeField] private float flockSpreadX = 2.5f;

    [Tooltip("Jarak sebar vertikal acak antar burung dalam kawanan")]
    [SerializeField] private float flockSpreadY = 1.2f;

    private readonly List<FlyingBird> _birdPool = new List<FlyingBird>();
    private bool _isSpawning = true;
    private Coroutine _spawnerRoutine;

    private void Awake()
    {
        InitializePool();
    }

    private void Start()
    {
        if (birdPrefab != null)
        {
            _spawnerRoutine = StartCoroutine(FlockSpawnerRoutine());
        }
        else
        {
            Debug.LogWarning("[BirdFlockSpawner] birdPrefab belum di-assign di Inspector!");
        }
    }

    /// <summary>
    /// Menyiapkan pool objek burung di awal agar bebas sampah memori (GC)
    /// </summary>
    private void InitializePool()
    {
        if (birdPrefab == null) return;

        for (int i = 0; i < poolSize; i++)
        {
            GameObject birdObj = Instantiate(birdPrefab, transform);
            birdObj.name = $"FlockBird_{i + 1}";
            FlyingBird birdScript = birdObj.GetComponent<FlyingBird>();
            if (birdScript == null)
            {
                birdScript = birdObj.AddComponent<FlyingBird>();
            }

            birdObj.SetActive(false);
            _birdPool.Add(birdScript);
        }
    }

    private FlyingBird GetAvailableBird()
    {
        foreach (var bird in _birdPool)
        {
            if (bird != null && !bird.gameObject.activeInHierarchy)
            {
                return bird;
            }
        }

        // Jika kebutuhan kawanan melebihi ukuran pool, buat instance tambahan secara otomatis
        GameObject newBird = Instantiate(birdPrefab, transform);
        newBird.name = $"FlockBird_{_birdPool.Count + 1}";
        FlyingBird script = newBird.GetComponent<FlyingBird>() ?? newBird.AddComponent<FlyingBird>();
        newBird.SetActive(false);
        _birdPool.Add(script);
        return script;
    }

    private IEnumerator FlockSpawnerRoutine()
    {
        // Jeda sesaat sebelum kawanan pertama melintas
        yield return new WaitForSeconds(0.8f);

        while (_isSpawning)
        {
            SpawnFlock();

            float waitDelay = Random.Range(minIntervalBetweenFlocks, maxIntervalBetweenFlocks);
            yield return new WaitForSeconds(waitDelay);
        }
    }

    /// <summary>
    /// Memunculkan satu kawanan burung dengan formasi dan variasi acak yang organik
    /// </summary>
    [ContextMenu("Spawn Flock (Test)")]
    public void SpawnFlock()
    {
        if (!_isSpawning || birdPrefab == null) return;

        int birdCount = Random.Range(minBirdsPerFlock, maxBirdsPerFlock + 1);
        float baseFlockY = Random.Range(minY, maxY);
        float baseFlockSpeed = Random.Range(minSpeed, maxSpeed);

        for (int i = 0; i < birdCount; i++)
        {
            FlyingBird bird = GetAvailableBird();
            if (bird == null) continue;

            // Burung pertama (pemimpin) di depan, burung pengikut menyebar di belakang kiri
            float offsetX = i == 0 ? 0f : -Random.Range(0.6f, flockSpreadX * (i * 0.7f));
            float offsetY = i == 0 ? 0f : Random.Range(-flockSpreadY, flockSpreadY);

            Vector3 spawnPos = new Vector3(startX + offsetX, Mathf.Clamp(baseFlockY + offsetY, minY, maxY), transform.position.z);

            // Variasi organik per individu burung agar tidak monoton
            float individualSpeed = baseFlockSpeed + Random.Range(-0.35f, 0.35f);
            float individualScale = Random.Range(minScale, maxScale);
            float individualAmp = Random.Range(minAmplitude, maxAmplitude);
            float individualFreq = Random.Range(minFrequency, maxFrequency);

            bird.Launch(spawnPos, individualSpeed, individualAmp, individualFreq, individualScale);
        }
    }

    /// <summary>
    /// Menghentikan spawning kawanan baru dan membuat burung yang sedang aktif di layar menanjak ke atas keluar layar
    /// </summary>
    public void StopSpawning(bool flyAway = true)
    {
        _isSpawning = false;

        if (_spawnerRoutine != null)
        {
            StopCoroutine(_spawnerRoutine);
        }

        if (flyAway)
        {
            // Burung yang sedang aktif di layar dibiarkan lanjut terbang sambil menanjak ke atas keluar layar
            foreach (var bird in _birdPool)
            {
                if (bird != null && bird.gameObject.activeInHierarchy)
                {
                    bird.FlyAwayUpward();
                }
            }
        }
        else
        {
            // Matikan seketika jika diminta
            foreach (var bird in _birdPool)
            {
                if (bird != null)
                {
                    bird.StopSpawning();
                }
            }
        }
    }

    /// <summary>
    /// Memulai kembali spawning jika kembali ke layar intro
    /// </summary>
    public void ResumeSpawning()
    {
        _isSpawning = true;
        _spawnerRoutine = StartCoroutine(FlockSpawnerRoutine());
    }
}
