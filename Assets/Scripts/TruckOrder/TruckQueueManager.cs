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

        [Tooltip("Slot antrean berderet. Index 0 adalah Loading Spot (terdepan), index 1 dan seterusnya di belakang.")]
        [SerializeField] private Transform[] queueSlots;

        [Tooltip("Titik akhir keluar lintasan untuk destroy mobil.")]
        [SerializeField] private Transform exitPoint;

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
            if (spawnPoint != null && queueSlots != null && queueSlots.Length > 0 && exitPoint != null) return;

            GameObject roadParent = GameObject.Find("Arah Jalan Mobil Pickup");
            if (roadParent != null)
            {
                Transform spawn = roadParent.transform.Find(" mulai (instantiate mobil )");
                Transform slot0 = roadParent.transform.Find("stop (load order ke sistem order)");
                Transform slot2 = roadParent.transform.Find("jalan ke sini - 1");
                Transform exit = roadParent.transform.Find("berakhir stop (destroy disini)");

                if (spawnPoint == null && spawn != null) spawnPoint = spawn;
                if (exitPoint == null && exit != null) exitPoint = exit;

                if (queueSlots == null || queueSlots.Length == 0)
                {
                    List<Transform> slots = new List<Transform>();
                    if (slot0 != null) slots.Add(slot0); // Loading spot

                    // Buat / cari slot 1 di tengah antara slot 0 dan slot 2
                    Transform slot1 = roadParent.transform.Find("queue_slot_1");
                    if (slot1 == null && slot0 != null && slot2 != null)
                    {
                        GameObject s1Obj = new GameObject("queue_slot_1");
                        s1Obj.transform.SetParent(roadParent.transform, false);
                        s1Obj.transform.position = Vector3.Lerp(slot0.position, slot2.position, 0.5f);
                        slot1 = s1Obj.transform;
                    }
                    if (slot1 != null) slots.Add(slot1);
                    if (slot2 != null) slots.Add(slot2);

                    queueSlots = slots.ToArray();
                }

                Debug.Log($"<color=cyan>[TruckQueueManager]</color> Berhasil memetakan {queueSlots.Length} slot antrean dan titik lintasan dari '{roadParent.name}'.");
            }
        }

        /// <summary>
        /// Men-spawn truk baru di titik spawn dan menugaskannya ke slot antrean paling belakang yang kosong.
        /// </summary>
        public TruckController SpawnTruck()
        {
            if (_trucksInQueue.Count >= maxQueueSlots) return null;

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : new Vector3(5.35f, 5.61f, 0f);
            spawnPos.z = 0f;

            Vector3 exitPos = exitPoint != null ? exitPoint.position : new Vector3(5.35f, -12.34f, 0f);
            exitPos.z = 0f;

            GameObject truckObj;
            if (truckPrefab != null)
            {
                // Rotasi 180 derajat agar kabin menghadap ke bawah (searah lintasan sumbu -Y)
                truckObj = Instantiate(truckPrefab, spawnPos, Quaternion.Euler(0f, 0f, 180f));
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

            int targetSlotIndex = _trucksInQueue.Count;
            _trucksInQueue.Add(truck);

            Vector3 targetPos = GetSlotPosition(targetSlotIndex);
            targetPos.z = 0f;
            bool isLoadingSpot = (targetSlotIndex == 0);
            truck.AssignTargetSlot(targetPos, isLoadingSpot);

            return truck;
        }

        /// <summary>
        /// Dipanggil oleh TruckController saat truk aktif di loading spot berangkat pergi (Completed atau Failed).
        /// </summary>
        public void OnActiveTruckLeft()
        {
            if (_trucksInQueue.Count > 0)
            {
                // Hapus truk terdepan dari daftar antrean
                _trucksInQueue.RemoveAt(0);
            }

            // Majukan semua truk di belakangnya satu slot ke depan
            for (int i = 0; i < _trucksInQueue.Count; i++)
            {
                Vector3 newSlotPos = GetSlotPosition(i);
                newSlotPos.z = 0f;
                bool isNewLoadingSpot = (i == 0);
                _trucksInQueue[i].AssignTargetSlot(newSlotPos, isNewLoadingSpot);
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
            obj.transform.rotation = Quaternion.Euler(0f, 0f, 180f);

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Karakter";
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

            if (queueSlots != null)
            {
                for (int i = 0; i < queueSlots.Length; i++)
                {
                    if (queueSlots[i] == null) continue;
                    Gizmos.color = (i == 0) ? Color.yellow : Color.cyan;
                    Gizmos.DrawWireCube(queueSlots[i].position, new Vector3(1.2f, 1.8f, 0f));
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
            if (scene.name != "SampleScene") return;

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
                Debug.Log("<color=green>[TruckOrder]</color> Auto-created OrderSystem in SampleScene.");
            }

            // 2. Pastikan TruckQueueManager ada pada 'Arah Jalan Mobil Pickup'
            GameObject roadParent = GameObject.Find("Arah Jalan Mobil Pickup");
            if (roadParent != null && roadParent.GetComponent<TruckQueueManager>() == null)
            {
                var qm = roadParent.AddComponent<TruckQueueManager>();
                qm.ResolveWaypointsIfNeeded();
                Debug.Log("<color=green>[TruckOrder]</color> Auto-attached TruckQueueManager to 'Arah Jalan Mobil Pickup'.");
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
                interactionObj.transform.position = new Vector3(5.35f, -2.13f, 0f);
                interactionObj.AddComponent<TruckInteraction>();
                Debug.Log("<color=green>[TruckOrder]</color> Auto-created TruckInteraction for loading spot.");
            }
        }
    }
}
