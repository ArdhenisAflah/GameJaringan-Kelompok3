using System;
using UnityEngine;

namespace EnemySystem.FSM
{
    /// <summary>
    /// Pengelola Finite State Machine (FSM) musuh.
    /// Mengatur inisialisasi, transisi aman antar state, dan update loop di Server.
    /// </summary>
    public class EnemyStateMachine
    {
        public EnemyBaseState CurrentState { get; private set; }

        /// <summary>
        /// Observer event dipicu saat terjadi pergantian state (previousState, newState).
        /// Berguna untuk debugging atau sinkronisasi visual/animasi.
        /// </summary>
        public event Action<EnemyBaseState, EnemyBaseState> OnStateChanged;

        /// <summary>
        /// Menginisialisasi FSM dengan state awal.
        /// </summary>
        public void Initialize(EnemyBaseState startingState)
        {
            if (startingState == null)
            {
                Debug.LogWarning("[EnemyStateMachine] startingState tidak boleh null!");
                return;
            }

            CurrentState = startingState;
            CurrentState.Enter();
            OnStateChanged?.Invoke(null, CurrentState);
        }

        /// <summary>
        /// Melakukan transisi dari CurrentState ke newState.
        /// </summary>
        public void ChangeState(EnemyBaseState newState)
        {
            if (newState == null)
            {
                Debug.LogWarning("[EnemyStateMachine] newState tidak boleh null!");
                return;
            }

            if (CurrentState == newState)
            {
                return;
            }

            EnemyBaseState previousState = CurrentState;

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();

            OnStateChanged?.Invoke(previousState, CurrentState);
        }

        /// <summary>
        /// Mengeksekusi logic update dari state aktif.
        /// </summary>
        public void Update(float deltaTime)
        {
            CurrentState?.Update(deltaTime);
        }

        /// <summary>
        /// Meneruskan penggambaran Gizmo dari state aktif ke Scene view.
        /// </summary>
        public void OnDrawGizmos()
        {
            CurrentState?.OnDrawGizmos();
        }
    }
}
