using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TruckOrder
{
    /// <summary>
    /// Mengelola antrean berderet truk pesanan (Truck Queue Manager).
    /// Mengatur kemunculan truk di titik spawn, penempatan slot antrean,
    /// memajukan antrean saat truk terdepan pergi, serta spawn truk pengganti di belakang.
    /// </summary>
    public class TruckQueueManager : MonoBehaviour
    {
        public static TruckQueueManager Instance { get; private set; }

        [Header("Prefab & Sprites")]
        [Tooltip("Prefab Truk dengan komponen TruckController.")]
        [SerializeField] private GameObject truckPrefab;

        [Tooltip("Sprite truk kosong bawaan.")]
        [SerializeField] private Sprite defaultEmptySprite;

        [Tooltip("Sprite truk terisi bawaan.")]
        [SerializeField] private Sprite defaultFilledSprite;

        [Header("Titik Lintasan (Waypoints)")]
        [Tooltip("Titik awal spawn mobil.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Daftar waypoint lintasan kedatangan dari spawn sampai loading spot.")]
        [SerializeField] private List<Transform> arrivingWaypoints = new List<Transform>();

        [Tooltip("Titik loading spot tempat truk parkir dan menerima pesanan.")]
        [SerializeField] private Transform loadingSpot;

        [Tooltip("Daftar waypoint lintasan keberangkatan dari loading spot sampai titik exit.")]
        [SerializeField] private List<Transform> departingWaypoints = new List<Transform>();

        [Tooltip("Titik akhir keluar lintasan untuk destroy mobil.")]
        [SerializeField] private Transform exitPoint;

        [Tooltip("Slot antrean berderet. Index 0 adalah Loading Spot (terdepan), index 1 dan seterusnya di belakang.")]
        [SerializeField] private Transform[] queueSlots;

        [Header("Pengaturan Antrean")]
        [Tooltip("Jumlah maksimal slot antrean berderet.")]
        [SerializeField] private int maxQueueSlots = 3;

        [Tooltip("Jeda waktu antar spawn truk baru.")]
        [SerializeField] private float spawnInterval = 1.5f;

        [Tooltip("Jeda waktu awal sebelum truk pertama di-spawn saat game dimulai.")]
        [SerializeField] private float initialSpawnDelay = 0.5f;

        private readonly List<TruckController> _trucksInQueue = new List<TruckController>();
        private bool _isSpawning = false;

        public int CurrentQueueCount => _trucksInQueue.Count;
        public TruckController ActiveTruck => _trucksInQueue.Count > 0 ? _trucksInQueue[0] : null;
        public Transform LoadingSpot => loadingSpot != null ? loadingSpot : (queueSlots != null && queueSlots.Length > 0 ? queueSlots[0] : null);

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

            ResolveWaypointsIfNeeded();
        }

        private void OnValidate()
        {
            if (spawnPoint == null || arrivingWaypoints == null || arrivingWaypoints.Count == 0 || exitPoint == null)
            {
                ResolveWaypointsIfNeeded();
            }
        }

        private void Start()
        {
            StartCoroutine(InitialSpawnRoutine());
        }

        private IEnumerator InitialSpawnRoutine()
        {
            yield return new WaitForSeconds(initialSpawnDelay);

            // Isi antrean sampai batas maxQueueSlots saat mulai
            for (int i = 0; i < maxQueueSlots; i++)
            {
                SpawnTruck();
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        /// <summary>
        /// Otomatis memetakan waypoint dari GameObject 'Arah Jalan Mobil Pickup' di scene jika belum dipasang di Inspector.
        /// </summary>
        public void ResolveWaypointsIfNeeded()
        {
            if (spawnPoint != null && arrivingWaypoints != null && arrivingWaypoints.Count > 0 
                && departingWaypoints != null && departingWaypoints.Count > 0 && exitPoint != null 
                && queueSlots != null && queueSlots.Length > 0)
            {
                return;
            }

            GameObject roadParent = GameObject.Find("Arah Jalan Mobil Pickup");
            if (roadParent != null)
            {
                Transform road = roadParent.transform;
                int childCount = road.childCount;

                int spawnIdx = -1;
                int stopIdx = -1;
                int exitIdx = -1;

                for (int i = 0; i < childCount; i++)
                {
                    string n = road.GetChild(i).name.Trim().ToLowerInvariant();
                    if (spawnIdx == -1 && (n.Contains("mulai") || n.Contains("spawn") || n.Contains("instantiate")))
                    {
                        spawnIdx = i;
                    }
                    else if (n.Contains("stop") && (n.Contains("load") || n.Contains("order")))
                    {
                        stopIdx = i;
                    }
                    else if (n.Contains("berakhir") || n.Contains("destroy") || n.Contains("exit"))
                    {
                        exitIdx = i;
                    }
                }

                // Fallbacks jika nama spesifik tidak ditemukan
                if (spawnIdx == -1 && childCount > 0) spawnIdx = 0;
                if (stopIdx == -1 && childCount > 1) stopIdx = childCount / 2;
                if (exitIdx == -1 && childCount > 0) exitIdx = childCount - 1;

                if (spawnIdx != -1 && stopIdx != -1 && exitIdx != -1)
                {
                    spawnPoint = road.GetChild(spawnIdx);
                    loadingSpot = road.GetChild(stopIdx);
                    exitPoint = road.GetChild(exitIdx);

                    arrivingWaypoints.Clear();
                    for (int i = spawnIdx + 1; i <= stopIdx; i++)
                    {
                        arrivingWaypoints.Add(road.GetChild(i));
                    }

                    departingWaypoints.Clear();
                    for (int i = stopIdx + 1; i <= exitIdx; i++)
                    {
                        departingWaypoints.Add(road.GetChild(i));
                    }

                    List<Transform> slots = new List<Transform>();
                    for (int s = 0; s < maxQueueSlots; s++)
                    {
                        int arriveIdx = arrivingWaypoints.Count - 1 - s;
                        if (arriveIdx >= 0)
                        {
                            slots.Add(arrivingWaypoints[arriveIdx]);
                        }
                    }
                    queueSlots = slots.ToArray();

                    Debug.Log($"<color=cyan>[TruckQueueManager]</color> Berhasil memetakan {arrivingWaypoints.Count} arriving waypoints, {departingWaypoints.Count} departing waypoints, dan {queueSlots.Length} slot antrean dari '{roadParent.name}'.");
                }
            }
        }

        /// <summary>
        /// Men-spawn truk baru di titik spawn dan menugaskannya ke slot antrean paling belakang yang kosong.
        /// </summary>
        public TruckController SpawnTruck()
        {
            if (_trucksInQueue.Count >= maxQueueSlots) return null;

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : new Vector3(56.68f, 24.39f, 0f);
            spawnPos.z = 0f;

            Vector3 exitPos = exitPoint != null ? exitPoint.position : new Vector3(25.5f, -36.22f, 0f);
            exitPos.z = 0f;

            GameObject truckObj;
            if (truckPrefab != null)
            {
                truckObj = Instantiate(truckPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // Fallback runtime generator jika prefab belum di-assign di Inspector
                truckObj = CreateRuntimeTruckObject(spawnPos);
            }

            TruckController truck = truckObj.GetComponent<TruckController>();
            if (truck == null)
            {
                truck = truckObj.AddComponent<TruckController>();
            }

            if (defaultEmptySprite != null || defaultFilledSprite != null)
            {
                truck.SetSprites(defaultEmptySprite, defaultFilledSprite);
            }

            truck.Initialize(this, exitPos);

            // Pasang rute keluar jika tersedia
            if (departingWaypoints != null && departingWaypoints.Count > 0)
            {
                List<Vector3> exitPoints = new List<Vector3>();
                for (int i = 0; i < departingWaypoints.Count; i++)
                {
                    if (departingWaypoints[i] != null)
                    {
                        exitPoints.Add(departingWaypoints[i].position);
                    }
                }
                truck.SetExitPath(exitPoints);
            }

            int targetSlotIndex = _trucksInQueue.Count;
            _trucksInQueue.Add(truck);

            bool isLoadingSpot = (targetSlotIndex == 0);

            // Tentukan sliced arrival path menuju slot antrean
            if (arrivingWaypoints != null && arrivingWaypoints.Count > 0)
            {
                int targetWaypointIdx = Mathf.Max(0, arrivingWaypoints.Count - 1 - targetSlotIndex);
                List<Vector3> initialPath = new List<Vector3>();
                for (int i = 0; i <= targetWaypointIdx; i++)
                {
                    if (arrivingWaypoints[i] != null)
                    {
                        initialPath.Add(arrivingWaypoints[i].position);
                    }
                }

                // Arahkan orientasi truk seketika menghadap waypoint pertama
                if (initialPath.Count > 0)
                {
                    truck.SnapRotationToHeading(initialPath[0]);
                }

                truck.SetPath(initialPath, isLoadingSpot);
            }
            else
            {
                Vector3 targetPos = GetSlotPosition(targetSlotIndex);
                truck.SnapRotationToHeading(targetPos);
                truck.AssignTargetSlot(targetPos, isLoadingSpot);
            }

            return truck;
        }

        /// <summary>
        /// Dipanggil oleh TruckController saat truk aktif di loading spot berangkat pergi (Completed atau Failed).
        /// </summary>
        public void OnActiveTruckLeft()
        {
            TruckController departingTruck = null;
            if (_trucksInQueue.Count > 0)
            {
                departingTruck = _trucksInQueue[0];
                _trucksInQueue.RemoveAt(0);
            }

            // Pastikan rute keluar terpasang pada truk yang meninggalkan loading spot
            if (departingTruck != null && departingWaypoints != null && departingWaypoints.Count > 0)
            {
                List<Vector3> exitPoints = new List<Vector3>();
                for (int i = 0; i < departingWaypoints.Count; i++)
                {
                    if (departingWaypoints[i] != null)
                    {
                        exitPoints.Add(departingWaypoints[i].position);
                    }
                }
                departingTruck.SetExitPath(exitPoints);
            }

            // Majukan semua truk di belakangnya satu slot ke depan
            for (int i = 0; i < _trucksInQueue.Count; i++)
            {
                bool isNewLoadingSpot = (i == 0);

                if (arrivingWaypoints != null && arrivingWaypoints.Count > 0)
                {
                    int newTargetWpIdx = arrivingWaypoints.Count - 1 - i;
                    if (newTargetWpIdx >= 0 && newTargetWpIdx < arrivingWaypoints.Count && arrivingWaypoints[newTargetWpIdx] != null)
                    {
                        List<Vector3> additional = new List<Vector3> { arrivingWaypoints[newTargetWpIdx].position };
                        _trucksInQueue[i].ExtendPath(additional, isNewLoadingSpot);
                    }
                    else
                    {
                        Vector3 newSlotPos = GetSlotPosition(i);
                        _trucksInQueue[i].AssignTargetSlot(newSlotPos, isNewLoadingSpot);
                    }
                }
                else
                {
                    Vector3 newSlotPos = GetSlotPosition(i);
                    _trucksInQueue[i].AssignTargetSlot(newSlotPos, isNewLoadingSpot);
                }
            }

            // Jadwalkan spawn truk baru di belakang antrean untuk menjaga slot penuh
            if (!_isSpawning && _trucksInQueue.Count < maxQueueSlots)
            {
                StartCoroutine(SpawnNextRoutine());
            }
        }

        private IEnumerator SpawnNextRoutine()
        {
            _isSpawning = true;
            yield return new WaitForSeconds(spawnInterval);

            if (_trucksInQueue.Count < maxQueueSlots)
            {
                SpawnTruck();
            }
            _isSpawning = false;
        }

        public Vector3 GetSlotPosition(int slotIndex)
        {
            Vector3 pos;
            if (queueSlots != null && slotIndex >= 0 && slotIndex < queueSlots.Length && queueSlots[slotIndex] != null)
            {
                pos = queueSlots[slotIndex].position;
            }
            else if (arrivingWaypoints != null && arrivingWaypoints.Count > 0)
            {
                int arriveIdx = Mathf.Max(0, arrivingWaypoints.Count - 1 - slotIndex);
                pos = arrivingWaypoints[arriveIdx].position;
            }
            else
            {
                // Fallback kalkulasi koordinat berderet di sepanjang x = 5.35
                float baseY = -2.13f; // Loading spot Y
                float spacingY = 1.5f;
                pos = new Vector3(5.35f, baseY + (slotIndex * spacingY), 0f);
            }
            pos.z = 0f;
            return pos;
        }

        private GameObject CreateRuntimeTruckObject(Vector3 pos)
        {
            pos.z = 0f;
            GameObject obj = new GameObject("Truck_Runtime");
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.identity;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Truck";
            sr.sortingOrder = 5;

            // Load sprite dari mobilPickup-Sheet jika ada
            LoadDefaultSpritesIfNeeded(sr);

            BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 1.8f);
            col.isTrigger = false;

            return obj;
        }

        private void LoadDefaultSpritesIfNeeded(SpriteRenderer sr)
        {
            if (defaultEmptySprite != null && defaultFilledSprite != null)
            {
                if (sr != null) sr.sprite = defaultEmptySprite;
                return;
            }

#if UNITY_EDITOR
            string sheetPath = "Assets/SpriteSheet/mobilPickup-Sheet.png";
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite s)
                {
                    if (s.name.Contains("0") && defaultEmptySprite == null) defaultEmptySprite = s;
                    else if (s.name.Contains("1") && defaultFilledSprite == null) defaultFilledSprite = s;
                }
            }
            if (sr != null && defaultEmptySprite != null)
            {
                sr.sprite = defaultEmptySprite;
            }
#endif
        }

        private void OnDrawGizmos()
        {
            if (spawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
            }

            // Gambar garis lintasan kedatangan
            if (arrivingWaypoints != null && arrivingWaypoints.Count > 0)
            {
                Gizmos.color = Color.cyan;
                Vector3 prev = spawnPoint != null ? spawnPoint.position : arrivingWaypoints[0].position;
                for (int i = 0; i < arrivingWaypoints.Count; i++)
                {
                    if (arrivingWaypoints[i] == null) continue;
                    Gizmos.DrawLine(prev, arrivingWaypoints[i].position);
                    Gizmos.DrawWireSphere(arrivingWaypoints[i].position, 0.2f);
                    prev = arrivingWaypoints[i].position;
                }
            }

            // Gambar slot antrean
            if (queueSlots != null)
            {
                for (int i = 0; i < queueSlots.Length; i++)
                {
                    if (queueSlots[i] == null) continue;
                    Gizmos.color = (i == 0) ? Color.yellow : new Color(0f, 0.8f, 1f, 0.7f);
                    Gizmos.DrawWireCube(queueSlots[i].position, new Vector3(1.2f, 1.8f, 0f));
                }
            }

            // Gambar garis lintasan keberangkatan
            if (departingWaypoints != null && departingWaypoints.Count > 0)
            {
                Gizmos.color = Color.magenta;
                Vector3 prev = (loadingSpot != null)
                    ? loadingSpot.position
                    : (queueSlots != null && queueSlots.Length > 0 && queueSlots[0] != null ? queueSlots[0].position : departingWaypoints[0].position);

                for (int i = 0; i < departingWaypoints.Count; i++)
                {
                    if (departingWaypoints[i] == null) continue;
                    Gizmos.DrawLine(prev, departingWaypoints[i].position);
                    Gizmos.DrawWireSphere(departingWaypoints[i].position, 0.2f);
                    prev = departingWaypoints[i].position;
                }
            }

            if (exitPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(exitPoint.position, 0.4f);
            }
        }

        // ==========================================
        // RUNTIME AUTO-SETUP HOOK
        // ==========================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureTruckSystemInScene()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            CheckAndSetupScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            CheckAndSetupScene(scene);
        }

        private static void CheckAndSetupScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.name != "Gameplay") return;

            // 1. Pastikan OrderSystem ada
            if (OrderSystem.Instance == null && FindObjectOfType<OrderSystem>() == null)
            {
                GameObject gm = GameObject.Find("GameManager");
                if (gm != null)
                {
                    gm.AddComponent<OrderSystem>();
                }
                else
                {
                    GameObject osObj = new GameObject("OrderSystem");
                    osObj.AddComponent<OrderSystem>();
                }
                Debug.Log("<color=green>[TruckOrder]</color> Auto-created OrderSystem in Gameplay.");
            }

            // 2. Pastikan TruckQueueManager ada pada 'Arah Jalan Mobil Pickup'
            GameObject roadParent = GameObject.Find("Arah Jalan Mobil Pickup");
            TruckQueueManager qm = null;
            if (roadParent != null)
            {
                qm = roadParent.GetComponent<TruckQueueManager>();
                if (qm == null)
                {
                    qm = roadParent.AddComponent<TruckQueueManager>();
                    qm.ResolveWaypointsIfNeeded();
                    Debug.Log("<color=green>[TruckOrder]</color> Auto-attached TruckQueueManager to 'Arah Jalan Mobil Pickup'.");
                }
            }

            // 3. Pastikan TruckOrderBubbleUI Canvas UI ada
            if (TruckOrderBubbleUI.Instance == null && FindObjectOfType<TruckOrderBubbleUI>() == null)
            {
                TruckOrderBubbleUI.CreateRuntimeCanvasUI();
            }

            // 4. Pastikan TruckInteraction ada pada loading spot
            if (FindObjectOfType<TruckInteraction>() == null)
            {
                GameObject interactionObj = new GameObject("TruckInteraction");
                Vector3 loadingPos = (qm != null && qm.LoadingSpot != null)
                    ? qm.LoadingSpot.position
                    : new Vector3(5.35f, -2.13f, 0f);
                loadingPos.z = 0f;
                interactionObj.transform.position = loadingPos;
                interactionObj.AddComponent<TruckInteraction>();
                Debug.Log($"<color=green>[TruckOrder]</color> Auto-created TruckInteraction for loading spot at {loadingPos}.");
            }
        }
    }
}
