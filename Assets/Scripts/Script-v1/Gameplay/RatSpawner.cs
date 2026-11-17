using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Object;

/// <summary>
/// Komponen Spawner untuk memunculkan Tikus / Curut secara berkala di area ladang.
/// Mendukung multi-titik kemunculan (spawn points), batas maksimum tikus aktif,
/// serta kompatibilitas penuh dengan server authority FishNet dan mode pengujian offline.
/// </summary>
public class RatSpawner : MonoBehaviour
{
    [Header("Konfigurasi Prefab")]
    [Tooltip("Prefab Curut / Tikus yang akan di-spawn (harus memiliki komponen CurutFSM atau NetworkObject).")]
    [SerializeField] private GameObject ratPrefab;

    [Header("Waktu & Interval Spawning")]
    [Tooltip("Apakah spawner langsung aktif saat permainan dimulai?")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("Jeda waktu sebelum tikus pertama muncul (detik).")]
    [SerializeField] private float initialDelay = 2.0f;

    [Tooltip("Interval minimal kemunculan tikus berikutnya (detik).")]
    [SerializeField] private float minSpawnInterval = 6.0f;

    [Tooltip("Interval maksimal kemunculan tikus berikutnya (detik).")]
    [SerializeField] private float maxSpawnInterval = 12.0f;

    [Header("Batas Populasi")]
    [Tooltip("Jumlah maksimum tikus yang boleh aktif secara bersamaan di lapangan.")]
    [SerializeField] private int maxActiveRats = 5;

    [Header("Titik Kemunculan (Spawn Points)")]
    [Tooltip("Daftar titik kemunculan spesifik (misal: lubang tikus, semak, batas ladang).")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Area Lingkaran (Jika Spawn Points Kosong)")]
    [Tooltip("Radius minimal spawn di sekitar spawner jika spawn points tidak diisi.")]
    [SerializeField] private float minSpawnRadius = 3.0f;

    [Tooltip("Radius maksimal spawn di sekitar spawner jika spawn points tidak diisi.")]
    [SerializeField] private float maxSpawnRadius = 8.0f;

    private readonly List<GameObject> _activeRats = new List<GameObject>();
    private Coroutine _spawnRoutine;
    private bool _isSpawning;

    public int ActiveRatCount
    {
        get
        {
            CleanDeadRats();
            return _activeRats.Count;
        }
    }

    public bool IsSpawning => _isSpawning;

    private void Start()
    {
        if (autoStart)
        {
            StartSpawning();
        }
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    /// <summary>
    /// Memulai rutinitas kemunculan tikus berkala.
    /// </summary>
    public void StartSpawning()
    {
        if (_isSpawning) return;
        _isSpawning = true;
        _spawnRoutine = StartCoroutine(SpawnLoop());
    }

    /// <summary>
    /// Menghentikan rutinitas kemunculan tikus.
    /// </summary>
    public void StopSpawning()
    {
        _isSpawning = false;
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        while (_isSpawning)
        {
            // Hanya Server yang berhak memunculkan entitas di multiplayer
            bool isServer = InstanceFinder.IsServerStarted;
            bool isOffline = !InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted;

            if (isServer || isOffline)
            {
                CleanDeadRats();

                if (_activeRats.Count < maxActiveRats)
                {
                    SpawnSingleRat();
                }
            }

            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    /// <summary>
    /// Memunculkan satu tikus pada posisi acak yang valid.
    /// </summary>
    [ContextMenu("Spawn Rat Now")]
    public GameObject SpawnSingleRat()
    {
        if (ratPrefab == null)
        {
            Debug.LogWarning("<color=orange>[RatSpawner]</color> ratPrefab belum di-assign di Inspector!");
            return null;
        }

        CleanDeadRats();
        if (_activeRats.Count >= maxActiveRats)
        {
            Debug.Log($"<color=orange>[RatSpawner]</color> Batas populasi tikus aktif ({maxActiveRats}) sudah tercapai.");
            return null;
        }

        Vector3 spawnPosition = CalculateSpawnPosition();
        spawnPosition.z = 0f; // Menghindari clipping pada kamera 2D

        GameObject ratInstance = Instantiate(ratPrefab, spawnPosition, Quaternion.identity);
        ratInstance.name = $"Curut_{_activeRats.Count + 1}";

        // Integrasi FishNet Server Spawning jika berjalan di server multiplayer
        if (InstanceFinder.IsServerStarted)
        {
            NetworkObject nob = ratInstance.GetComponent<NetworkObject>();
            if (nob != null)
            {
                InstanceFinder.ServerManager.Spawn(ratInstance);
            }
        }

        _activeRats.Add(ratInstance);
        Debug.Log($"<color=green>[RatSpawner]</color> Berhasil memunculkan '{ratInstance.name}' pada koordinat {spawnPosition}. (Aktif: {_activeRats.Count}/{maxActiveRats})");

        return ratInstance;
    }

    /// <summary>
    /// Menghitung posisi spawn berdasarkan daftar spawn points atau radius acak di sekitar spawner.
    /// </summary>
    private Vector3 CalculateSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Filter titik spawn yang valid
            List<Transform> validPoints = new List<Transform>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    validPoints.Add(spawnPoints[i]);
                }
            }

            if (validPoints.Count > 0)
            {
                int randomIndex = Random.Range(0, validPoints.Count);
                return validPoints[randomIndex].position;
            }
        }

        // Fallback: area lingkaran acak di sekitar posisi spawner
        float randomAngle = Random.Range(0f, Mathf.PI * 2f);
        float randomDist = Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector3 offset = new Vector3(Mathf.Cos(randomAngle) * randomDist, Mathf.Sin(randomAngle) * randomDist, 0f);

        return transform.position + offset;
    }

    /// <summary>
    /// Membersihkan referensi tikus yang sudah mati atau di-destroy dari list aktif.
    /// </summary>
    private void CleanDeadRats()
    {
        for (int i = _activeRats.Count - 1; i >= 0; i--)
        {
            if (_activeRats[i] == null || !_activeRats[i].activeInHierarchy)
            {
                _activeRats.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Menghapus semua tikus aktif yang dimunculkan oleh spawner ini.
    /// </summary>
    [ContextMenu("Clear All Active Rats")]
    public void ClearAllActiveRats()
    {
        CleanDeadRats();
        for (int i = _activeRats.Count - 1; i >= 0; i--)
        {
            if (_activeRats[i] != null)
            {
                if (InstanceFinder.IsServerStarted)
                {
                    NetworkObject nob = _activeRats[i].GetComponent<NetworkObject>();
                    if (nob != null && nob.IsSpawned)
                    {
                        InstanceFinder.ServerManager.Despawn(_activeRats[i]);
                    }
                    else
                    {
                        Destroy(_activeRats[i]);
                    }
                }
                else
                {
                    Destroy(_activeRats[i]);
                }
            }
        }
        _activeRats.Clear();
        Debug.Log("<color=yellow>[RatSpawner]</color> Seluruh tikus aktif telah dibersihkan.");
    }

    #region Editor Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);

        // Visualisasi titik spawn
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    Gizmos.DrawWireSphere(spawnPoints[i].position, 0.4f);
                    Gizmos.DrawLine(transform.position, spawnPoints[i].position);
                }
            }
        }
        else
        {
            // Visualisasi area radius spawn
            Gizmos.DrawWireSphere(transform.position, minSpawnRadius);
            Gizmos.DrawWireSphere(transform.position, maxSpawnRadius);
        }
    }

    #endregion
}

/// <summary>
/// Alias komponen CurutSpawner untuk kemudahan pencarian di Unity Inspector.
/// </summary>
public class CurutSpawner : RatSpawner
{
}
