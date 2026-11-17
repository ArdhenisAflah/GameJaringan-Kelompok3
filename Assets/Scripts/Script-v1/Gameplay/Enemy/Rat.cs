using UnityEngine;
using EnemySystem.FSM;
using EnemySystem.FSM.States;

namespace EnemySystem
{
    /// <summary>
    /// Kelas konkret untuk musuh Tikus (Rat) yang meng-extend base class Enemy.
    /// Memiliki perilaku umum Walk dan Eat Crops dengan karakteristik khusus:
    /// pergerakan lincah, kemampuan mendeteksi padi/tanaman, dan integrasi animasi tikus.
    /// </summary>
    [DisallowMultipleComponent]
    public class Rat : Enemy
    {
        [Header("Pengaturan Khusus Tikus")]
        [Tooltip("Komponen Animator untuk animasi sprite tikus (opsional).")]
        [SerializeField] private Animator ratAnimator;

        [Tooltip("Nama parameter boolean di Animator untuk berjalan.")]
        [SerializeField] private string animParamWalking = "IsWalking";

        [Tooltip("Nama parameter boolean di Animator untuk memakan tanaman.")]
        [SerializeField] private string animParamEating = "IsEating";

        [Tooltip("Nama parameter boolean di Animator untuk kabur.")]
        [SerializeField] private string animParamFleeing = "IsFleeing";

        [Tooltip("Nama parameter trigger di Animator saat terkena hit.")]
        [SerializeField] private string animParamHit = "Hit";

        [Tooltip("Nama parameter trigger di Animator saat mati.")]
        [SerializeField] private string animParamDie = "Die";

        protected override void Awake()
        {
            // Konfigurasi nilai bawaan karakteristik tikus
            enemyName = "Rat";
            if (maxHealth <= 0) maxHealth = 2;
            if (moveSpeed <= 0f) moveSpeed = 2.4f;
            if (fleeSpeed <= 0f) fleeSpeed = 4.2f;
            if (cropDetectionRadius <= 0f) cropDetectionRadius = 7.0f;
            if (eatDuration <= 0f) eatDuration = 3.0f;
            if (biteInterval <= 0f) biteInterval = 0.5f;

            if (ratAnimator == null)
            {
                ratAnimator = GetComponentInChildren<Animator>();
            }

            base.Awake();
        }

        /// <summary>
        /// Inisialisasi state FSM untuk Tikus.
        /// Tikus menggunakan reusable states bawaan dengan tuning parameter gerak lincah.
        /// Subclass dapat memodifikasi state tertentu di sini jika diperlukan.
        /// </summary>
        protected override void SetupStates()
        {
            base.SetupStates();
        }

        /// <summary>
        /// Callback saat SyncVar NetworkState diperbarui dari Server.
        /// Mengatur transisi animasi visual tikus di sisi client.
        /// </summary>
        protected override void OnVisualStateUpdated(EnemyStateEnum state)
        {
            base.OnVisualStateUpdated(state);

            if (ratAnimator == null) return;

            // Reset boolean anim states
            SetAnimBoolSafe(animParamWalking, false);
            SetAnimBoolSafe(animParamEating, false);
            SetAnimBoolSafe(animParamFleeing, false);

            switch (state)
            {
                case EnemyStateEnum.Idle:
                    // Kembali ke idle anim
                    break;

                case EnemyStateEnum.Walking:
                    SetAnimBoolSafe(animParamWalking, true);
                    break;

                case EnemyStateEnum.Eating:
                    SetAnimBoolSafe(animParamEating, true);
                    break;

                case EnemyStateEnum.Fleeing:
                    SetAnimBoolSafe(animParamFleeing, true);
                    break;

                case EnemyStateEnum.Dead:
                    SetAnimTriggerSafe(animParamDie);
                    break;

                case EnemyStateEnum.Hit:
                    SetAnimTriggerSafe(animParamHit);
                    break;
            }
        }

        public override void TakeDamage(int amount, FishNet.Object.NetworkObject attacker = null)
        {
            base.TakeDamage(amount, attacker);

            if (ratAnimator != null && !IsDead)
            {
                SetAnimTriggerSafe(animParamHit);
            }
        }

        protected override void HandleDeath()
        {
            if (ratAnimator != null)
            {
                SetAnimTriggerSafe(animParamDie);
            }

            base.HandleDeath();
        }

        private void SetAnimBoolSafe(string paramName, bool val)
        {
            if (ratAnimator != null && !string.IsNullOrEmpty(paramName))
            {
                ratAnimator.SetBool(paramName, val);
            }
        }

        private void SetAnimTriggerSafe(string paramName)
        {
            if (ratAnimator != null && !string.IsNullOrEmpty(paramName))
            {
                ratAnimator.SetTrigger(paramName);
            }
        }
    }
}
