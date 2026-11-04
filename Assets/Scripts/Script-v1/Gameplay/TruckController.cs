using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using SiloSystem;

namespace TruckOrder
{
    /// <summary>
    /// Mengelola perilaku kendaraan truk: state machine, gerak lurus antrean,
    /// timer sabar di loading spot, dan penggantian sprite saat pesanan terisi.
    /// </summary>
    public class TruckController : MonoBehaviour
    {
        [Header("Order Generation")]
        [Tooltip("Jenis item yang diminta oleh truk ini.")]
        [SerializeField] private HarvestType orderItemType = HarvestType.Padi;

        [Tooltip("Jumlah pesanan minimal (acak).")]
        [SerializeField] private int minAmount = 2;

        [Tooltip("Jumlah pesanan maksimal (acak).")]
        [SerializeField] private int maxAmount = 10;

        [Tooltip("Reward per unit yang diberikan jika pesanan selesai.")]
        [SerializeField] private int rewardPerUnit = 10;

        [Header("Movement & Steering Settings")]
        [Tooltip("Kecepatan gerak lurus truk (unit/detik).")]
        [SerializeField] private float moveSpeed = 3.5f;

        [Tooltip("Kecepatan rotasi kendaraan saat berbelok (derajat per detik).")]
        [SerializeField] private float turnSpeed = 270f;

        [Tooltip("Offset sudut arah depan sprite (default -90 derajat untuk sprite top-down menghadap atas).")]
        [SerializeField] private float spriteForwardOffset = -90f;

        [Tooltip("Toleransi jarak untuk beralih ke waypoint berikutnya saat tikungan.")]
        [SerializeField] private float waypointReachThreshold = 0.35f;

        [Tooltip("Toleransi jarak untuk mendeteksi tiba di target slot akhir.")]
        [SerializeField] private float arrivalDistanceThreshold = 0.05f;

        [Tooltip("Menampilkan garis raycast ke target waypoint di Scene View untuk debugging.")]
        [SerializeField] private bool showDebugHeadingRay = true;

        [Header("Timer Sabar (Patience Timer)")]
        [Tooltip("Waktu tunggu dalam menit saat truk berada di loading spot.")]
        [SerializeField] private float patienceMinutes = 2.0f;

        [Tooltip("Override waktu tunggu dalam detik untuk keperluan testing cepat (jika > 0).")]
        [SerializeField] private float testSecondsOverride = 0f;

        [Tooltip("Jika true, item yang sudah dimasukkan akan dikembalikan jika waktu habis. Default false (hangus).")]
        [SerializeField] private bool refundItemsOnFail = false;

        [Header("Sprites & Sorting")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Sorting layer untuk memastikan truk berada di atas layer tanah dan tanaman.")]
        [SerializeField] private string sortingLayerName = "Karakter";

        [Tooltip("Sorting order pada layer Karakter.")]
        [SerializeField] private int sortingOrder = 5;

        [Tooltip("Sprite saat truk masih kosong.")]
        [SerializeField] private Sprite emptyTruckSprite;

        [Tooltip("Sprite saat truk sudah terisi penuh.")]
        [SerializeField] private Sprite filledTruckSprite;

        [Header("Keberangkatan")]
        [Tooltip("Jeda waktu (detik) setelah order terpenuhi sebelum truk melaju pergi.")]
        [SerializeField] private float departureDelay = 1.0f;

        // State Machine
        public TruckState CurrentState { get; private set; } = TruckState.Arriving;
        public Order MyOrder { get; private set; }
        public float RemainingPatienceTime { get; private set; }
        public float TotalPatienceTime { get; private set; }
        public bool IsAtLoadingSpot { get; private set; }

        private Vector3 _targetPosition;
        private Vector3 _exitPosition;
        private TruckQueueManager _queueManager;
        private bool _hasNotifiedQueueLeave = false;

        // Path Navigation Waypoints
        private readonly List<Vector3> _currentPath = new List<Vector3>();
        private int _currentPathIndex = 0;
        private readonly List<Vector3> _exitPath = new List<Vector3>();
        private int _exitPathIndex = 0;

        public event Action<float, float> OnTimerTick; // remaining, total

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            ApplySortingSettings();
        }

        private void Start()
        {
            ApplySortingSettings();
            SetSprite(emptyTruckSprite);
            // Kunci posisi Z selalu 0
            transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
        }

        private void ApplySortingSettings()
        {
            if (spriteRenderer != null)
            {
                if (!string.IsNullOrEmpty(sortingLayerName))
                {
                    spriteRenderer.sortingLayerName = sortingLayerName;
                }
                spriteRenderer.sortingOrder = sortingOrder;
            }
        }

        /// <summary>
        /// Membuat pesanan acak untuk truk ini saat spawn.
        /// </summary>
        public void GenerateOrder()
        {
            int randomQty = UnityEngine.Random.Range(minAmount, maxAmount + 1);
            MyOrder = new Order(orderItemType, randomQty, rewardPerUnit);
        }

        public void Initialize(TruckQueueManager manager, Vector3 exitPos)
        {
            _queueManager = manager;
            _exitPosition = new Vector3(exitPos.x, exitPos.y, 0f);
        }

        /// <summary>
        /// Menugaskan serangkaian waypoint yang harus dilalui truk menuju slot antrean.
        /// </summary>
        public void SetPath(List<Vector3> path, bool isLoadingSpot)
        {
            _currentPath.Clear();
            if (path != null && path.Count > 0)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    _currentPath.Add(new Vector3(path[i].x, path[i].y, 0f));
                }
                _targetPosition = _currentPath[_currentPath.Count - 1];
            }
            else
            {
                _targetPosition = transform.position;
            }

            _currentPathIndex = 0;
            IsAtLoadingSpot = isLoadingSpot;
            CurrentState = TruckState.Arriving;
        }

        /// <summary>
        /// Menambahkan waypoint lanjutan saat antrean bergerak maju (mencegah teleportasi).
        /// </summary>
        public void ExtendPath(List<Vector3> additionalWaypoints, bool isLoadingSpot)
        {
            if (additionalWaypoints != null && additionalWaypoints.Count > 0)
            {
                bool wasFinished = (_currentPath.Count > 0 && _currentPathIndex >= _currentPath.Count - 1);

                for (int i = 0; i < additionalWaypoints.Count; i++)
                {
                    _currentPath.Add(new Vector3(additionalWaypoints[i].x, additionalWaypoints[i].y, 0f));
                }
                _targetPosition = _currentPath[_currentPath.Count - 1];

                if (wasFinished)
                {
                    _currentPathIndex++;
                }
            }

            IsAtLoadingSpot = isLoadingSpot;
            if (CurrentState == TruckState.Waiting)
            {
                CurrentState = TruckState.Arriving;
            }
        }

        /// <summary>
        /// Memperbarui jalur sisa saat antrean truk maju ke slot berikutnya.
        /// </summary>
        public void AdvancePath(List<Vector3> newRemainingPath, bool isLoadingSpot)
        {
            ExtendPath(newRemainingPath, isLoadingSpot);
        }

        /// <summary>
        /// Menugaskan serangkaian waypoint jalur keluar setelah loading spot.
        /// </summary>
        public void SetExitPath(List<Vector3> exitPath)
        {
            _exitPath.Clear();
            if (exitPath != null && exitPath.Count > 0)
            {
                for (int i = 0; i < exitPath.Count; i++)
                {
                    _exitPath.Add(new Vector3(exitPath[i].x, exitPath[i].y, 0f));
                }
                _exitPosition = _exitPath[_exitPath.Count - 1];
            }
            _exitPathIndex = 0;
        }

        /// <summary>
        /// Mengarahkan orientasi mobil secara instan ke arah titik tertentu (misal saat baru spawn).
        /// </summary>
        public void SnapRotationToHeading(Vector3 nextPoint)
        {
            Vector2 dir = (new Vector2(nextPoint.x, nextPoint.y) - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spriteForwardOffset;
                transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
            }
        }

        /// <summary>
        /// Menugaskan posisi slot target di antrean (kompatibilitas backward / direct move).
        /// </summary>
        public void AssignTargetSlot(Vector3 slotPos, bool isLoadingSpot)
        {
            _currentPath.Clear();
            _targetPosition = new Vector3(slotPos.x, slotPos.y, 0f);
            _currentPath.Add(_targetPosition);
            _currentPathIndex = 0;
            IsAtLoadingSpot = isLoadingSpot;
            CurrentState = TruckState.Arriving;
        }

        private void Update()
        {
            switch (CurrentState)
            {
                case TruckState.Arriving:
                    HandleMovementToTarget();
                    break;

                case TruckState.Waiting:
                    // Truk parkir diam menunggu giliran maju di antrean
                    break;

                case TruckState.Active:
                    HandlePatienceTimer();
                    break;

                case TruckState.Filled:
                    // Menunggu coroutine jeda sebelum berangkat
                    break;

                case TruckState.Departing:
                case TruckState.Escaping:
                    HandleMovementToExit();
                    break;
            }
        }

        private void HandleMovementToTarget()
        {
            Vector3 target;
            bool hasIntermediateWaypoints = false;

            if (_currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
            {
                target = _currentPath[_currentPathIndex];
                hasIntermediateWaypoints = (_currentPathIndex < _currentPath.Count - 1);
            }
            else
            {
                target = new Vector3(_targetPosition.x, _targetPosition.y, 0f);
            }

            Vector3 current = new Vector3(transform.position.x, transform.position.y, 0f);

            // 1. Perputaran Halus Menuju Waypoint (Smooth Vector Direction Rotation)
            Vector2 direction = (new Vector2(target.x, target.y) - (Vector2)current).normalized;
            if (direction.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + spriteForwardOffset;
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            // 2. Pergerakan Maju Menuju Target
            transform.position = Vector3.MoveTowards(current, target, moveSpeed * Time.deltaTime);

            // 3. Evaluasi Ketibaan
            float dist = Vector2.Distance(transform.position, target);
            if (hasIntermediateWaypoints)
            {
                // Beralih ke waypoint berikutnya saat memasuki radius tikungan (Dynamic Continuous Drive)
                if (dist <= waypointReachThreshold)
                {
                    _currentPathIndex++;
                }
            }
            else
            {
                // Titik akhir jalur antrean
                if (dist <= arrivalDistanceThreshold)
                {
                    transform.position = target;

                    if (IsAtLoadingSpot)
                    {
                        EnterActiveState();
                    }
                    else
                    {
                        CurrentState = TruckState.Waiting;
                    }
                }
            }
        }

        private void EnterActiveState()
        {
            CurrentState = TruckState.Active;

            // Hitung durasi timer sabar
            TotalPatienceTime = (testSecondsOverride > 0f) ? testSecondsOverride : (patienceMinutes * 60f);
            RemainingPatienceTime = TotalPatienceTime;

            // Server-Authoritative: Hanya Server atau mode offline yang mengaktifkan order baru
            bool isServer = InstanceFinder.IsServerStarted;
            bool isOffline = !InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted;

            if (isServer || isOffline)
            {
                int randomQty = UnityEngine.Random.Range(minAmount, maxAmount + 1);
                MyOrder = new Order(orderItemType, randomQty, rewardPerUnit);

                if (OrderSystem.Instance != null)
                {
                    OrderSystem.Instance.ServerActivateNewOrder(orderItemType, randomQty, TotalPatienceTime, rewardPerUnit, this);
                }
            }
        }

        private void HandlePatienceTimer()
        {
            if (OrderSystem.Instance != null && OrderSystem.Instance.HasActiveOrder)
            {
                RemainingPatienceTime = OrderSystem.Instance.RemainingPatienceTime;
                TotalPatienceTime = OrderSystem.Instance.TotalPatienceTime;
                OnTimerTick?.Invoke(RemainingPatienceTime, TotalPatienceTime);
            }
            else
            {
                RemainingPatienceTime -= Time.deltaTime;
                OnTimerTick?.Invoke(RemainingPatienceTime, TotalPatienceTime);

                if (RemainingPatienceTime <= 0f)
                {
                    RemainingPatienceTime = 0f;
                    NotifyOrderFailed();
                }
            }
        }

        /// <summary>
        /// Dipanggil saat pesanan berhasil diselesaikan.
        /// </summary>
        public void NotifyOrderCompleted()
        {
            if (CurrentState == TruckState.Filled || CurrentState == TruckState.Departing) return;

            CurrentState = TruckState.Filled;
            SetSprite(filledTruckSprite);

            StartCoroutine(DepartAfterDelayRoutine());
        }

        private IEnumerator DepartAfterDelayRoutine()
        {
            yield return new WaitForSeconds(departureDelay);

            CurrentState = TruckState.Departing;
            NotifyQueueToAdvance();
        }

        /// <summary>
        /// Dipanggil saat waktu sabar habis dan pesanan gagal.
        /// </summary>
        public void NotifyOrderFailed()
        {
            if (CurrentState == TruckState.Escaping) return;

            CurrentState = TruckState.Escaping;
            Debug.LogWarning($"[TruckController] Truk {name} kabur lurus keluar karena timer sabar habis!");

            // Jika refund diaktifkan, kembalikan item (jika ada logic refund)
            if (refundItemsOnFail && MyOrder != null && MyOrder.CurrentAmount > 0)
            {
                Debug.Log($"[TruckController] Refund {MyOrder.CurrentAmount} {MyOrder.ItemType} (refundItemsOnFail=true).");
            }

            NotifyQueueToAdvance();
        }

        private void NotifyQueueToAdvance()
        {
            if (!_hasNotifiedQueueLeave)
            {
                _hasNotifiedQueueLeave = true;
                if (OrderSystem.Instance != null && OrderSystem.Instance.ActiveTruck == this)
                {
                    OrderSystem.Instance.ClearActiveOrder();
                }
                if (_queueManager != null)
                {
                    _queueManager.OnActiveTruckLeft();
                }
            }
        }

        private void HandleMovementToExit()
        {
            Vector3 exitTarget;
            bool hasIntermediateWaypoints = false;

            if (_exitPath.Count > 0 && _exitPathIndex < _exitPath.Count)
            {
                exitTarget = _exitPath[_exitPathIndex];
                hasIntermediateWaypoints = (_exitPathIndex < _exitPath.Count - 1);
            }
            else
            {
                exitTarget = new Vector3(_exitPosition.x, _exitPosition.y, 0f);
            }

            Vector3 current = new Vector3(transform.position.x, transform.position.y, 0f);

            // 1. Perputaran Halus Menuju Exit (Smooth Vector Direction Rotation)
            Vector2 direction = (new Vector2(exitTarget.x, exitTarget.y) - (Vector2)current).normalized;
            if (direction.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + spriteForwardOffset;
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            // 2. Pergerakan Maju Menuju Exit
            transform.position = Vector3.MoveTowards(current, exitTarget, moveSpeed * Time.deltaTime);

            // 3. Evaluasi Ketibaan
            float dist = Vector2.Distance(transform.position, exitTarget);
            if (hasIntermediateWaypoints)
            {
                if (dist <= waypointReachThreshold)
                {
                    _exitPathIndex++;
                }
            }
            else
            {
                if (dist <= arrivalDistanceThreshold)
                {
                    // Tiba di titik destroy akhir
                    Destroy(gameObject);
                }
            }
        }

        private void SetSprite(Sprite s)
        {
            if (spriteRenderer != null && s != null)
            {
                spriteRenderer.sprite = s;
                ApplySortingSettings();
            }
        }

        public void SetSprites(Sprite empty, Sprite filled)
        {
            emptyTruckSprite = empty;
            filledTruckSprite = filled;
            if (CurrentState != TruckState.Filled && CurrentState != TruckState.Departing)
            {
                SetSprite(empty);
            }
        }

        public void SetTestSecondsOverride(float seconds)
        {
            testSecondsOverride = seconds;
        }

        private void OnDrawGizmos()
        {
            if (!showDebugHeadingRay) return;

            Vector3 target = Vector3.zero;
            bool hasTarget = false;

            if (CurrentState == TruckState.Arriving)
            {
                if (_currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
                {
                    target = _currentPath[_currentPathIndex];
                    hasTarget = true;
                }
                else if (_targetPosition != Vector3.zero)
                {
                    target = _targetPosition;
                    hasTarget = true;
                }
            }
            else if (CurrentState == TruckState.Departing || CurrentState == TruckState.Escaping)
            {
                if (_exitPath.Count > 0 && _exitPathIndex < _exitPath.Count)
                {
                    target = _exitPath[_exitPathIndex];
                    hasTarget = true;
                }
                else if (_exitPosition != Vector3.zero)
                {
                    target = _exitPosition;
                    hasTarget = true;
                }
            }

            if (hasTarget)
            {
                Gizmos.color = Color.green;
                // Moncong mobil dihitung dari arah transform.up
                Vector3 nose = transform.position + (transform.up * 0.8f);
                Gizmos.DrawLine(nose, target);
                Gizmos.DrawWireSphere(target, 0.15f);
            }
        }
    }
}
