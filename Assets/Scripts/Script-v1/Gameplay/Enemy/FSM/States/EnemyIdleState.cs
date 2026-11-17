using UnityEngine;
using EnemySystem.FSM;
using Farming;

namespace EnemySystem.FSM.States
{
    /// <summary>
    /// State saat musuh diam sejenak di tempat sebelum melanjutkan perjalanan atau mendeteksi tanaman.
    /// </summary>
    public class EnemyIdleState : EnemyBaseState
    {
        private float _idleTimer;

        public EnemyIdleState(Enemy enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

        public override void Enter()
        {
            _enemy.SetNetworkState(EnemyStateEnum.Idle);
            _idleTimer = Random.Range(_enemy.IdleDurationMin, _enemy.IdleDurationMax);
        }

        public override void Update(float deltaTime)
        {
            // 1. Periksa apakah ada tanaman terdekat di sekitar
            Plant crop = _enemy.FindNearestCrop();
            if (crop != null)
            {
                _enemy.CurrentTargetCrop = crop;
                _stateMachine.ChangeState(_enemy.WalkState);
                return;
            }

            // 2. Hitung mundur jeda diam
            _idleTimer -= deltaTime;
            if (_idleTimer <= 0f)
            {
                _stateMachine.ChangeState(_enemy.WalkState);
            }
        }
    }
}
