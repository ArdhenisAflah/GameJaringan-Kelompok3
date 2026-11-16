using System;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using EnemySystem.FSM;
using EnemySystem.FSM.States;
using Farming;

namespace EnemySystem
{
    public enum EnemyStateEnum : byte
    {
        Idle = 0,
        Walking = 1,
        Eating = 2,
        Fleeing = 3,
        Dead = 4,
        Hit = 5
    }

    /// <summary>
    /// Base class abstrak untuk seluruh entitas musuh/hama di jaringan FishNet.
    /// Menyediakan arsitektur FSM (Finite State Machine), perilaku umum (Walk & Eat Crops),
    /// sistem deteksi tanaman, pengelolaan health/damage, dan event Observer pattern.
    /// Subclass konkret seperti Rat meng-extend class ini.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public abstract class Enemy : NetworkBehaviour, IDamageable
    {
        [Header("Pengaturan Identitas & Health")]
        [Tooltip("Nama jenis musuh (misal: Tikus, Gagak, Babi Hutan).")]
        [SerializeField] protected string enemyName = "Enemy";

        [Tooltip("Jumlah Health maksimal musuh.")]
        [SerializeField] protected int maxHealth = 3;

        [Header("Kecepatan & Pergerakan")]
        [Tooltip("Kecepatan jalan normal saat mencari tanaman atau berpatroli (unit/detik).")]
        [SerializeField] protected float moveSpeed = 2.0f;

        [Tooltip("Kecepatan lari kabur saat diserang pemain (unit/detik).")]
        [SerializeField] protected float fleeSpeed = 3.5f;

        [Tooltip("Radius acak wandering jika belum mendeteksi tanaman.")]
        [SerializeField] protected float wanderRadius = 6.0f;

        [Tooltip("Durasi minimal jeda diam di state Idle (detik).")]
        [SerializeField] protected float idleDurationMin = 0.8f;

        [Tooltip("Durasi maksimal jeda diam di state Idle (detik).")]
        [SerializeField] protected float idleDurationMax = 2.0f;

        [Header("Deteksi & Konsumsi Tanaman (Eat Crops)")]
        [Tooltip("Jarak radius sensor untuk mendeteksi tanaman terdekat.")]
        [SerializeField] protected float cropDetectionRadius = 6.0f;

        [Tooltip("Toleransi jarak ke tanaman untuk mulai memakannya.")]
        [SerializeField] protected float eatDistanceThreshold = 0.35f;

        [Tooltip("Waktu dalam detik yang dibutuhkan musuh untuk menghabiskan satu tanaman.")]
        [SerializeField] protected float eatDuration = 3.5f;

        [Tooltip("Interval antar gigitan saat makan (memicu flash putih pada tanaman).")]
        [SerializeField] protected float biteInterval = 0.6f;

        [Header("Reaksi Hit & Stun")]
        [Tooltip("Durasi jeda/stun saat musuh menerima serangan sebelum mulai kabur (detik).")]
        [SerializeField] protected float hitStunDuration = 0.35f;

        [Tooltip("Kekuatan dorongan knockback mundur saat menerima serangan.")]
        [SerializeField] protected float knockbackForce = 1.2f;

        [Header("Visual & Sorting")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected string sortingLayerName = "Karakter";
        [SerializeField] protected int sortingOrder = 5;

        // --- FishNet Network State Synchronization ---
        private readonly SyncVar<int> _currentHealth = new();
        private readonly SyncVar<EnemyStateEnum> _networkState = new();
        private readonly SyncVar<bool> _isFacingLeft = new();

        // --- Finite State Machine ---
        public EnemyStateMachine StateMachine { get; private set; }
        public EnemyIdleState IdleState { get; protected set; }
        public EnemyWalkState WalkState { get; protected set; }
        public EnemyEatCropState EatCropState { get; protected set; }
        public EnemyFleeState FleeState { get; protected set; }
        public EnemyHitState HitState { get; protected set; }

        // --- Context Target Data ---
        public Plant CurrentTargetCrop { get; set; }
        public Vector3 LastDamageSourcePosition { get; set; }

        // --- Observer Pattern Events (Technical VFX / Audio / UI Hooks) ---
        public event Action<int, int> OnHealthChanged; // (current, max)
        public event Action<int> OnDamaged; // (damageAmount) -> hook efek hit & white flash
        public event Action OnDeath; // hook efek musuh mati
        public event Action<Plant> OnCropBitten; // hook efek saat menggigit tanaman
        public event Action<Plant> OnCropEaten; // hook efek saat tanaman musnah

        public static event Action<Enemy> OnAnyEnemySpawned;
        public static event Action<Enemy> OnAnyEnemyDespawned;

        // Properties IDamageable
        public int CurrentHealth => _currentHealth.Value;
        public int MaxHealth => maxHealth;
        public bool IsDead => _currentHealth.Value <= 0;

        public string EnemyName => enemyName;
        public float MoveSpeed => moveSpeed;
        public float FleeSpeed => fleeSpeed;
        public float HitStunDuration => hitStunDuration;
        public float KnockbackForce => knockbackForce;
        public float CropDetectionRadius => cropDetectionRadius;
        public float EatDistanceThreshold => eatDistanceThreshold;
        public float EatDuration => eatDuration;
        public float BiteInterval => biteInterval;
        public float WanderRadius => wanderRadius;
        public float IdleDurationMin => idleDurationMin;
        public float IdleDurationMax => idleDurationMax;
        public EnemyStateEnum CurrentNetworkState => _networkState.Value;

        private Coroutine _hitFlashCoroutine;
        private Color _originalColor = Color.white;

        protected virtual void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            ApplySortingSettings();

            // Setup FSM
            StateMachine = new EnemyStateMachine();
            SetupStates();

            // Mendaftarkan callback SyncVar
            _currentHealth.OnChange += HandleHealthChanged;
            _networkState.OnChange += HandleNetworkStateChanged;
            _isFacingLeft.OnChange += HandleFacingDirectionChanged;
        }

        protected virtual void Start()
        {
            ApplySortingSettings();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Inisialisasi status di Server
            _currentHealth.Value = maxHealth;
            _networkState.Value = EnemyStateEnum.Idle;

            // Memulai FSM pada IdleState
            if (IdleState != null)
            {
                StateMachine.Initialize(IdleState);
            }

            OnAnyEnemySpawned?.Invoke(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            OnAnyEnemyDespawned?.Invoke(this);
        }

        protected virtual void OnDestroy()
        {
            _currentHealth.OnChange -= HandleHealthChanged;
            _networkState.OnChange -= HandleNetworkStateChanged;
            _isFacingLeft.OnChange -= HandleFacingDirectionChanged;
        }

        /// <summary>
        /// Inisialisasi state-state FSM.
        /// Subclass dapat meng-override method ini untuk menyuntikkan subclass state kustom.
        /// </summary>
        protected virtual void SetupStates()
        {
            IdleState = new EnemyIdleState(this, StateMachine);
            WalkState = new EnemyWalkState(this, StateMachine);
            EatCropState = new EnemyEatCropState(this, StateMachine);
            FleeState = new EnemyFleeState(this, StateMachine);
            HitState = new EnemyHitState(this, StateMachine);
        }

        protected virtual void Update()
        {
            // Server-Authoritative: FSM dan AI hanya dijalankan oleh Server (atau mode offline)
            bool isServer = IsServerStarted;
            bool isOffline = !IsServerStarted && !IsClientStarted;

            if (isServer || isOffline)
            {
                StateMachine?.Update(Time.deltaTime);
            }
        }

        /// <summary>
        /// Mengatur state jaringan untuk disinkronkan ke semua client.
        /// </summary>
        public void SetNetworkState(EnemyStateEnum newState)
        {
            if (IsServerStarted)
            {
                _networkState.Value = newState;
            }
        }

        /// <summary>
        /// Memperbarui arah hadap sprite dan menyinkronkannya ke client.
        /// </summary>
        public void SetFacingDirection(bool facingLeft)
        {
            if (IsServerStarted)
            {
                _isFacingLeft.Value = facingLeft;
            }
            ApplyFacingDirection(facingLeft);
        }

        private void ApplyFacingDirection(bool facingLeft)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = facingLeft;
            }
        }

        public void ApplySortingSettings()
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
        /// Mencari tanaman (Plant) terdekat di dalam radius deteksi.
        /// Dapat di-override oleh subclass untuk memprioritaskan jenis tanaman tertentu.
        /// </summary>
        public virtual Plant FindNearestCrop()
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, cropDetectionRadius);
            Plant nearestPlant = null;
            float minDistanceSqr = float.MaxValue;
            Vector2 currentPos = transform.position;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;

                Plant plant = colliders[i].GetComponent<Plant>();
                if (plant == null)
                {
                    plant = colliders[i].GetComponentInParent<Plant>();
                }

                if (plant != null && plant.gameObject.activeInHierarchy)
                {
                    float distSqr = ((Vector2)plant.transform.position - currentPos).sqrMagnitude;
                    if (distSqr < minDistanceSqr)
                    {
                        minDistanceSqr = distSqr;
                        nearestPlant = plant;
                    }
                }
            }

            return nearestPlant;
        }

        /// <summary>
        /// Implementasi IDamageable: Menerima serangan/damage dari pemain.
        /// Memancarkan observer events dan memicu efek visual putih (white flash).
        /// </summary>
        public virtual void TakeDamage(int amount, NetworkObject attacker = null)
        {
            // Validasi Server Authority
            if (!IsServerStarted && IsClientStarted) return;
            if (IsDead) return;

            int prevHealth = _currentHealth.Value;
            int newHealth = Mathf.Max(0, prevHealth - amount);
            _currentHealth.Value = newHealth;

            if (attacker != null)
            {
                LastDamageSourcePosition = attacker.transform.position;
            }
            else
            {
                LastDamageSourcePosition = transform.position + (Vector3)UnityEngine.Random.insideUnitCircle.normalized;
            }

            // Observer Event: Trigger OnDamaged
            Debug.Log($"<color=orange>[Enemy Observer]</color> {enemyName} ('{name}') menerima {amount} damage! Sisa HP: {newHealth}/{maxHealth}. (Hook VFX: Flash Putih).");
            OnDamaged?.Invoke(amount);

            // Memicu efek visual flash putih di seluruh klien
            if (IsServerStarted)
            {
                ObserversTriggerHitFlash();
            }
            else
            {
                StartHitFlashRoutine();
            }

            if (newHealth <= 0)
            {
                HandleDeath();
            }
            else
            {
                // Transisi ke HitState (stun/flinch/knockback) lalu FleeState
                if (HitState != null)
                {
                    StateMachine.ChangeState(HitState);
                }
                else if (FleeState != null && StateMachine.CurrentState != FleeState)
                {
                    StateMachine.ChangeState(FleeState);
                }
            }
        }

        [ObserversRpc(BufferLast = false)]
        private void ObserversTriggerHitFlash()
        {
            StartHitFlashRoutine();
        }

        private void StartHitFlashRoutine()
        {
            if (spriteRenderer == null) return;
            if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
            _hitFlashCoroutine = StartCoroutine(HitFlashWhiteRoutine(0.15f));
        }

        private IEnumerator HitFlashWhiteRoutine(float duration)
        {
            if (spriteRenderer != null)
            {
                _originalColor = Color.white;
                // White flash tint
                spriteRenderer.color = new Color(2.5f, 2.5f, 2.5f, 1f);
                yield return new WaitForSeconds(duration);
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = _originalColor;
                }
            }
            _hitFlashCoroutine = null;
        }

        protected virtual void HandleDeath()
        {
            Debug.Log($"<color=red>[Enemy Observer]</color> {enemyName} ('{name}') dikalahkan (HP 0)! Despawning...");
            SetNetworkState(EnemyStateEnum.Dead);
            OnDeath?.Invoke();

            if (IsServerStarted)
            {
                StartCoroutine(DespawnAfterDelay(0.1f));
            }
            else
            {
                Destroy(gameObject, 0.1f);
            }
        }

        private IEnumerator DespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (IsServerStarted && NetworkObject != null && NetworkObject.IsSpawned)
            {
                ServerManager.Despawn(gameObject);
            }
        }

        // --- Notification Callbacks for States ---
        public void NotifyCropBitten(Plant crop)
        {
            OnCropBitten?.Invoke(crop);
        }

        public void NotifyCropEaten(Plant crop)
        {
            OnCropEaten?.Invoke(crop);
        }

        // --- SyncVar Handlers ---
        private void HandleHealthChanged(int prev, int next, bool asServer)
        {
            OnHealthChanged?.Invoke(next, maxHealth);
        }

        private void HandleNetworkStateChanged(EnemyStateEnum prev, EnemyStateEnum next, bool asServer)
        {
            // Hook untuk client-side animation controller
            OnVisualStateUpdated(next);
        }

        private void HandleFacingDirectionChanged(bool prev, bool next, bool asServer)
        {
            ApplyFacingDirection(next);
        }

        /// <summary>
        /// Hook visual/animator di client saat state berubah.
        /// </summary>
        protected virtual void OnVisualStateUpdated(EnemyStateEnum state) { }

        protected virtual void OnDrawGizmosSelected()
        {
            // Visualisasi radius deteksi tanaman
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, cropDetectionRadius);

            // Visualisasi radius makan
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, eatDistanceThreshold);

            // Meneruskan gizmo ke FSM
            StateMachine?.OnDrawGizmos();
        }
    }
}
