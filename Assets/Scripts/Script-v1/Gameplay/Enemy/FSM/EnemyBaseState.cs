using UnityEngine;

namespace EnemySystem.FSM
{
    /// <summary>
    /// Base class abstrak untuk setiap state di Finite State Machine (FSM) musuh.
    /// Memungkinkan implementasi modular, reusable, dan extensible untuk berbagai jenis perilaku musuh.
    /// </summary>
    public abstract class EnemyBaseState
    {
        protected readonly Enemy _enemy;
        protected readonly EnemyStateMachine _stateMachine;

        public Enemy EnemyContext => _enemy;
        public EnemyStateMachine StateMachine => _stateMachine;

        public EnemyBaseState(Enemy enemy, EnemyStateMachine stateMachine)
        {
            _enemy = enemy;
            _stateMachine = stateMachine;
        }

        /// <summary>
        /// Dipanggil tepat saat state ini mulai aktif.
        /// </summary>
        public virtual void Enter() { }

        /// <summary>
        /// Dipanggil setiap frame di Server saat state ini aktif.
        /// </summary>
        /// <param name="deltaTime">Waktu antar frame (Time.deltaTime).</param>
        public virtual void Update(float deltaTime) { }

        /// <summary>
        /// Dipanggil tepat sebelum berpindah ke state berikutnya untuk proses pembersihan.
        /// </summary>
        public virtual void Exit() { }

        /// <summary>
        /// Opsional: Menggambar gizmo visualisasi state di Scene view editor.
        /// </summary>
        public virtual void OnDrawGizmos() { }
    }
}
