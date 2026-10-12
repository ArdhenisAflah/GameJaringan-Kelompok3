using System;
using System.Collections;
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

        [Header("Movement Settings")]
        [Tooltip("Kecepatan gerak lurus truk (unit/detik).")]
        [SerializeField] private float moveSpeed = 3.5f;

        [Tooltip("Toleransi jarak untuk mendeteksi tiba di target slot.")]
        [SerializeField] private float arrivalDistanceThreshold = 0.05f;

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
        /// Menugaskan posisi slot target di antrean.
        /// </summary>
        public void AssignTargetSlot(Vector3 slotPos, bool isLoadingSpot)
        {
            _targetPosition = new Vector3(slotPos.x, slotPos.y, 0f);
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
            Vector3 target = new Vector3(_targetPosition.x, _targetPosition.y, 0f);
            Vector3 current = new Vector3(transform.position.x, transform.position.y, 0f);
            transform.position = Vector3.MoveTowards(current, target, moveSpeed * Time.deltaTime);

            if (Vector2.Distance(transform.position, target) <= arrivalDistanceThreshold)
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
            Vector3 exitTarget = new Vector3(_exitPosition.x, _exitPosition.y, 0f);
            Vector3 current = new Vector3(transform.position.x, transform.position.y, 0f);
            transform.position = Vector3.MoveTowards(current, exitTarget, moveSpeed * Time.deltaTime);

            if (Vector2.Distance(transform.position, exitTarget) <= arrivalDistanceThreshold)
            {
                // Tiba di titik destroy
                Destroy(gameObject);
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
    }
}
