using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Farming;
using EnemySystem;

/// <summary>
/// State enum untuk Finite State Machine sederhana pada Curut (Tikus ladang).
/// </summary>
public enum CurutState : byte
{
    Run = 0,        // Berlari memburu tanaman atau berpatroli di ladang
    AttackCrop = 1, // Berhenti di petak tanaman dan memakannya hingga habis
    Hit = 2,        // Reaksi terkejut/stun sesaat saat diserang pemain
    Flee = 3        // Panik dan berlari menjauhi pemain setelah terkena hit
}

/// <summary>
/// Finite State Machine sederhana untuk musuh Curut (Tikus sawah/ladang).
/// Memiliki dua perilaku inti:
/// 1. Run: Berlari mencari tanaman (Plant) terdekat dengan animasi frame curutRun-Sheet.
/// 2. Attack Crops: Menyerang dan memakan tanaman padi hingga habis, memicu efek visual flash putih.
/// 
/// Mendukung arsitektur ganda:
/// - Otoritatif di server FishNet multiplayer (NetworkBehaviour).
/// - Berfungsi langsung saat diuji secara offline/singleplayer di Unity Editor.
/// </summary>
[DisallowMultipleComponent]
public class CurutFSM : NetworkBehaviour, IDamageable, IWiggleTriggerSource
{
    [Header("Status & FSM")]
    [Tooltip("Status FSM Curut saat ini.")]
    [SerializeField] private CurutState currentState = CurutState.Run;

    [Header("Identitas & Health")]
    [SerializeField] private string curutName = "Curut";
    [SerializeField] private int maxHealth = 2;
    private int _localHealth;

    [Header("Kecepatan & Pergerakan (Run)")]
    [Tooltip("Kecepatan lari curut saat mencari atau menuju tanaman (unit/detik).")]
    [SerializeField] private float runSpeed = 2.8f;

    [Tooltip("Kecepatan lari panik saat kabur setelah terkena hit (unit/detik).")]
    [SerializeField] private float fleeSpeed = 4.2f;

    [Tooltip("Radius sensor pencarian tanaman terdekat.")]
    [SerializeField] private float detectionRadius = 8.0f;

    [Tooltip("Radius wandering acak jika belum ada tanaman di ladang.")]
    [SerializeField] private float wanderRadius = 6.0f;

    [Tooltip("Toleransi jarak ke tanaman untuk memulai serangan/makan.")]
    [SerializeField] private float attackDistance = 0.35f;

    [Header("Serangan Tanaman (Attack Crops)")]
    [Tooltip("Durasi waktu yang dibutuhkan curut untuk menghabiskan satu tanaman (detik).")]
    [SerializeField] private float attackDuration = 2.8f;

    [Tooltip("Jeda interval antar gigitan (memicu flash putih pada tanaman).")]
    [SerializeField] private float biteInterval = 0.5f;

    [Header("Reaksi Hit")]
    [Tooltip("Durasi stun/jeda saat curut terkena serangan pemain (detik).")]
    [SerializeField] private float hitStunDuration = 0.3f;

    [Tooltip("Kekuatan dorongan knockback saat terkena serangan.")]
    [SerializeField] private float knockbackForce = 1.2f;

    [Header("Sprite & Animasi (curutRun-Sheet)")]
    [Tooltip("Komponen SpriteRenderer curut. Jika kosong, akan dicari otomatis.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Daftar frame sprite lari dari curutRun-Sheet (curutRun-Sheet_0 s/d 3).")]
    [SerializeField] private Sprite[] runSprites;

    [Tooltip("Kecepatan animasi sprite lari (frame per second).")]
    [SerializeField] private float animationFps = 10f;

    [Tooltip("Sorting layer untuk sprite curut (default: Karakter).")]
    [SerializeField] private string sortingLayerName = "Karakter";

    [Tooltip("Order in layer sprite curut.")]
    [SerializeField] private int sortingOrder = 6;

    [Tooltip("Animator opsional jika menggunakan Animator Controller.")]
    [SerializeField] private Animator animator;

    // --- FishNet SyncVars ---
    private readonly SyncVar<CurutState> _networkState = new();
    private readonly SyncVar<int> _currentHealth = new();
    private readonly SyncVar<bool> _isFacingLeft = new();

    // --- State Timers & References ---
    private Plant _targetCrop;
    private Vector3 _currentDestination;
    private bool _hasTargetCrop;
    private float _rescanTimer;
    private float _attackTimer;
    private float _biteTimer;
    private float _hitStunTimer;
    private float _fleeTimer;
    private Vector3 _lastAttackerPos;

    // --- Sprite Cycling Timer ---
    private float _spriteAnimTimer;
    private int _currentSpriteFrame;
    private Coroutine _hitFlashCoroutine;
    private Color _originalColor = Color.white;

    #region Properties & Interfaces

    public CurutState CurrentState => currentState;
    public Plant TargetCrop => _targetCrop;

    // IDamageable
    public int CurrentHealth => IsServerStarted ? _currentHealth.Value : _localHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0;

    // IWiggleTriggerSource (Tanaman otomatis bergoyang saat curut melintas)
    public TriggerEntityType EntityType => TriggerEntityType.Animal;
    public Vector2 Velocity
    {
        get
        {
            if (currentState == CurutState.Run)
            {
                Vector3 dir = (_currentDestination - transform.position).normalized;
                return new Vector2(dir.x, dir.y) * runSpeed;
            }
            if (currentState == CurutState.Flee)
            {
                Vector3 dir = (transform.position - _lastAttackerPos).normalized;
                return new Vector2(dir.x, dir.y) * fleeSpeed;
            }
            return Vector2.zero;
        }
    }

    #endregion

    #region Unity Lifecycle & Init

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        ApplySorting();

        _localHealth = maxHealth;

        // Registrasi SyncVar callback untuk client FishNet
        _networkState.OnChange += HandleNetworkStateChanged;
        _isFacingLeft.OnChange += HandleFacingDirectionChanged;
        _currentHealth.OnChange += HandleHealthChanged;
    }

    private void OnDestroy()
    {
        _networkState.OnChange -= HandleNetworkStateChanged;
        _isFacingLeft.OnChange -= HandleFacingDirectionChanged;
        _currentHealth.OnChange -= HandleHealthChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentHealth.Value = maxHealth;
        _networkState.Value = CurutState.Run;
    }

    private void Start()
    {
        ApplySorting();
        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
        }

        // Mulai pada state Run
        TransitionToState(CurutState.Run);
    }

    private void ApplySorting()
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

    #endregion

    #region Update & FSM Execution

    private void Update()
    {
        // 1. Eksekusi FSM Logic (Server Authority atau Mode Offline)
        bool canProcessAI = IsServerStarted || (!IsServerStarted && !IsClientStarted);
        if (canProcessAI && !IsDead)
        {
            UpdateFSM(Time.deltaTime);
        }

        // 2. Eksekusi Animasi Visual (Berjalan di semua client & offline)
        UpdateVisualAnimation(Time.deltaTime);
    }

    private void UpdateFSM(float dt)
    {
        switch (currentState)
        {
            case CurutState.Run:
                UpdateRunState(dt);
                break;

            case CurutState.AttackCrop:
                UpdateAttackCropState(dt);
                break;

            case CurutState.Hit:
                UpdateHitState(dt);
                break;

            case CurutState.Flee:
                UpdateFleeState(dt);
                break;
        }
    }

    #endregion

    #region State: Run (Buru Tanaman / Wander)

    private void EnterRunState()
    {
        SetStateAndSync(CurutState.Run);
        _rescanTimer = 0f;
        PickNextRunTarget();
    }

    private void UpdateRunState(float dt)
    {
        // 1. Validasi / Re-scan Tanaman
        if (_hasTargetCrop)
        {
            // Jika tanaman target musnah atau sudah dipanen pemain
            if (_targetCrop == null || !_targetCrop.gameObject.activeInHierarchy)
            {
                _targetCrop = null;
                _hasTargetCrop = false;
                PickNextRunTarget();
                return;
            }
            _currentDestination = _targetCrop.transform.position;
        }
        else
        {
            // Rescan berkala untuk mendeteksi apakah ada benih/tanaman baru
            _rescanTimer -= dt;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = 0.5f;
                Plant nearest = FindNearestActiveCrop();
                if (nearest != null)
                {
                    _targetCrop = nearest;
                    _hasTargetCrop = true;
                    _currentDestination = nearest.transform.position;
                }
            }
        }

        // 2. Pergerakan Menuju Target
        Vector3 curPos = transform.position;
        curPos.z = 0f;
        _currentDestination.z = 0f;

        Vector2 moveDir = (new Vector2(_currentDestination.x, _currentDestination.y) - (Vector2)curPos);
        float distance = moveDir.magnitude;

        if (distance > 0.02f)
        {
            // Update arah hadap sprite
            SetFacingDirection(moveDir.x < 0f);

            // Gerakkan curut
            transform.position = Vector3.MoveTowards(curPos, _currentDestination, runSpeed * dt);
        }

        // 3. Evaluasi Jangkauan Serang / Titik Tiba
        if (_hasTargetCrop)
        {
            if (distance <= attackDistance)
            {
                TransitionToState(CurutState.AttackCrop);
            }
        }
        else
        {
            // Jika tiba di titik wander acak, cari titik baru
            if (distance <= 0.2f)
            {
                PickNextRunTarget();
            }
        }
    }

    private void PickNextRunTarget()
    {
        Plant crop = FindNearestActiveCrop();
        if (crop != null)
        {
            _targetCrop = crop;
            _hasTargetCrop = true;
            _currentDestination = crop.transform.position;
            _currentDestination.z = 0f;
        }
        else
        {
            _targetCrop = null;
            _hasTargetCrop = false;
            // Titik acak wander
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * wanderRadius;
            _currentDestination = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
            _currentDestination.z = 0f;
        }
    }

    /// <summary>
    /// Mencari tanaman aktif terdekat di dalam radius deteksi.
    /// </summary>
    public Plant FindNearestActiveCrop()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius);
        Plant closestPlant = null;
        float minDstSqr = float.MaxValue;
        Vector2 pos = transform.position;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            Plant plant = hits[i].GetComponent<Plant>() ?? hits[i].GetComponentInParent<Plant>();
            if (plant != null && plant.gameObject.activeInHierarchy)
            {
                float dSqr = ((Vector2)plant.transform.position - pos).sqrMagnitude;
                if (dSqr < minDstSqr)
                {
                    minDstSqr = dSqr;
                    closestPlant = plant;
                }
            }
        }

        return closestPlant;
    }

    #endregion

    #region State: Attack Crop (Serang & Makan Tanaman)

    private void EnterAttackCropState()
    {
        if (_targetCrop == null || !_targetCrop.gameObject.activeInHierarchy)
        {
            TransitionToState(CurutState.Run);
            return;
        }

        SetStateAndSync(CurutState.AttackCrop);
        _attackTimer = attackDuration;
        _biteTimer = 0.05f; // Gigitan pertama seketika setelah tiba

        // Hadapkan curut ke arah tanaman
        Vector3 diff = _targetCrop.transform.position - transform.position;
        SetFacingDirection(diff.x < 0f);

        Debug.Log($"<color=magenta>[CurutFSM]</color> Curut mulai menyerang & memakan tanaman '{_targetCrop.PlantName}'!");
    }

    private void UpdateAttackCropState(float dt)
    {
        // 1. Validasi tanaman (bisa saja dipanen pemain di tengah-tengah makan)
        if (_targetCrop == null || !_targetCrop.gameObject.activeInHierarchy)
        {
            Debug.Log("<color=orange>[CurutFSM]</color> Tanaman target hilang/dipanen pemain saat diserang. Curut kembali berlari.");
            _targetCrop = null;
            _hasTargetCrop = false;
            TransitionToState(CurutState.Run);
            return;
        }

        // 2. Gigitan berkala (Memicu White Flash & Efek Kerusakan)
        _biteTimer -= dt;
        if (_biteTimer <= 0f)
        {
            _biteTimer = biteInterval;

            // Flash putih pada sprite tanaman
            _targetCrop.TriggerBiteFlash(0.15f);

            Debug.Log($"<color=yellow>[Curut Attack]</color> Curut menggigit '{_targetCrop.PlantName}'! (Flash Putih)");
        }

        // 3. Durasi serangan selesai -> Tanaman habis dimakan
        _attackTimer -= dt;
        if (_attackTimer <= 0f)
        {
            Plant finishedCrop = _targetCrop;
            _targetCrop = null;
            _hasTargetCrop = false;

            Debug.Log($"<color=red>[Curut Attack]</color> Tanaman '{finishedCrop.PlantName}' telah habis dimakan oleh curut!");

            // Despawn di server atau destroy lokal
            bool consumedInServer = finishedCrop.ServerConsumeByEnemy();
            if (!consumedInServer && !IsServerStarted && !IsClientStarted)
            {
                Destroy(finishedCrop.gameObject);
            }

            // Kembali berburu tanaman berikutnya
            TransitionToState(CurutState.Run);
        }
    }

    #endregion

    #region State: Hit & Flee (Reaksi Terkena Serangan)

    private void EnterHitState()
    {
        SetStateAndSync(CurutState.Hit);
        _hitStunTimer = hitStunDuration;

        // Knockback mundur menjauhi penyerang
        Vector3 knockbackDir = (transform.position - _lastAttackerPos).normalized;
        if (knockbackDir.sqrMagnitude < 0.01f)
        {
            knockbackDir = UnityEngine.Random.insideUnitCircle.normalized;
        }

        transform.position += knockbackDir * (knockbackForce * 0.15f);
        SetFacingDirection(knockbackDir.x > 0f);
    }

    private void UpdateHitState(float dt)
    {
        _hitStunTimer -= dt;
        if (_hitStunTimer <= 0f)
        {
            // Setelah stun selesai, beralih ke state Flee untuk kabur sesaat
            TransitionToState(CurutState.Flee);
        }
    }

    private void EnterFleeState()
    {
        SetStateAndSync(CurutState.Flee);
        _fleeTimer = 2.0f; // Kabur selama 2 detik
    }

    private void UpdateFleeState(float dt)
    {
        // Berlari menjauhi posisi penyerang terakhir
        Vector3 fleeDir = (transform.position - _lastAttackerPos).normalized;
        if (fleeDir.sqrMagnitude < 0.01f) fleeDir = Vector3.right;

        SetFacingDirection(fleeDir.x < 0f);
        transform.position += fleeDir * (fleeSpeed * dt);

        _fleeTimer -= dt;
        if (_fleeTimer <= 0f)
        {
            // Tenang kembali dan lanjutkan mencari tanaman
            TransitionToState(CurutState.Run);
        }
    }

    #endregion

    #region State Transition & Sync

    public void TransitionToState(CurutState nextState)
    {
        currentState = nextState;

        switch (nextState)
        {
            case CurutState.Run:
                EnterRunState();
                break;
            case CurutState.AttackCrop:
                EnterAttackCropState();
                break;
            case CurutState.Hit:
                EnterHitState();
                break;
            case CurutState.Flee:
                EnterFleeState();
                break;
        }
    }

    private void SetStateAndSync(CurutState state)
    {
        currentState = state;
        if (IsServerStarted)
        {
            _networkState.Value = state;
        }
    }

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

    #endregion

    #region Visual Animations (curutRun-Sheet & Animator)

    private void UpdateVisualAnimation(float dt)
    {
        // 1. Integrasi Animator jika terpasang
        if (animator != null)
        {
            animator.SetBool("IsRunning", currentState == CurutState.Run || currentState == CurutState.Flee);
            animator.SetBool("IsAttacking", currentState == CurutState.AttackCrop);
        }

        // 2. Animasi Frame Sprite Bawaan (Hanya aktif jika tidak menggunakan Animator Controller)
        bool hasAnimatorController = animator != null && animator.runtimeAnimatorController != null;
        if (!hasAnimatorController && runSprites != null && runSprites.Length > 0 && spriteRenderer != null)
        {
            if (currentState == CurutState.Run || currentState == CurutState.Flee)
            {
                _spriteAnimTimer += dt;
                float frameDuration = 1f / Mathf.Max(1f, animationFps);

                if (_spriteAnimTimer >= frameDuration)
                {
                    _spriteAnimTimer -= frameDuration;
                    _currentSpriteFrame = (_currentSpriteFrame + 1) % runSprites.Length;

                    if (runSprites[_currentSpriteFrame] != null)
                    {
                        spriteRenderer.sprite = runSprites[_currentSpriteFrame];
                    }
                }
            }
            else if (currentState == CurutState.AttackCrop)
            {
                // Saat menyerang, lakukan getaran/wobble halus pada frame pertama
                if (runSprites.Length > 0 && runSprites[0] != null)
                {
                    spriteRenderer.sprite = runSprites[0];
                }
            }
        }
    }

    #endregion

    #region IDamageable Implementation

    public void TakeDamage(int amount, NetworkObject attacker = null)
    {
        if (IsDead) return;

        // Otoritas Server di multiplayer
        if (!IsServerStarted && IsClientStarted) return;

        int newHp;
        if (IsServerStarted)
        {
            newHp = Mathf.Max(0, _currentHealth.Value - amount);
            _currentHealth.Value = newHp;
        }
        else
        {
            newHp = Mathf.Max(0, _localHealth - amount);
            _localHealth = newHp;
        }

        if (attacker != null)
        {
            _lastAttackerPos = attacker.transform.position;
        }
        else
        {
            _lastAttackerPos = transform.position + (Vector3)UnityEngine.Random.insideUnitCircle.normalized;
        }

        Debug.Log($"<color=orange>[Curut Hit]</color> {curutName} menerima {amount} damage! Sisa HP: {newHp}/{maxHealth}.");

        // Flash visual putih saat curut terkena hit
        if (IsServerStarted)
        {
            ObserversTriggerHitFlash();
        }
        else
        {
            StartHitFlash();
        }

        if (newHp <= 0)
        {
            HandleDeath();
        }
        else
        {
            TransitionToState(CurutState.Hit);
        }
    }

    [ObserversRpc(BufferLast = false)]
    private void ObserversTriggerHitFlash()
    {
        StartHitFlash();
    }

    private void StartHitFlash()
    {
        if (spriteRenderer == null) return;
        if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
        _hitFlashCoroutine = StartCoroutine(HitFlashRoutine(0.15f));
    }

    private IEnumerator HitFlashRoutine(float dur)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(2.5f, 2.5f, 2.5f, 1f);
            yield return new WaitForSeconds(dur);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = _originalColor;
            }
        }
        _hitFlashCoroutine = null;
    }

    private void HandleDeath()
    {
        Debug.Log($"<color=red>[Curut Death]</color> {curutName} telah dikalahkan (HP 0)! Despawning...");
        if (IsServerStarted && NetworkObject != null && NetworkObject.IsSpawned)
        {
            ServerManager.Despawn(gameObject);
        }
        else
        {
            Destroy(gameObject, 0.1f);
        }
    }

    #endregion

    #region SyncVar Callbacks

    private void HandleNetworkStateChanged(CurutState prev, CurutState next, bool asServer)
    {
        currentState = next;
    }

    private void HandleFacingDirectionChanged(bool prev, bool next, bool asServer)
    {
        ApplyFacingDirection(next);
    }

    private void HandleHealthChanged(int prev, int next, bool asServer)
    {
        _localHealth = next;
    }

    #endregion

    #region Editor Gizmos

    private void OnDrawGizmosSelected()
    {
        // Visualisasi radius deteksi tanaman
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Visualisasi jangkauan serang
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        // Garis tujuan saat ini
        if (currentState == CurutState.Run)
        {
            Gizmos.color = _hasTargetCrop ? Color.green : Color.cyan;
            Gizmos.DrawLine(transform.position, _currentDestination);
            Gizmos.DrawWireSphere(_currentDestination, 0.2f);
        }
    }

    #endregion
}

/// <summary>
/// Alias komponen Curut untuk kemudahan pencarian di Unity Inspector.
/// </summary>
public class Curut : CurutFSM
{
}
