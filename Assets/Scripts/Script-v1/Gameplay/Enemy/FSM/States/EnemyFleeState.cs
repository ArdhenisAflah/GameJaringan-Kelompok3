using UnityEngine;
using EnemySystem.FSM;

namespace EnemySystem.FSM.States
{
    /// <summary>
    /// State saat musuh panik dan kabur (Flee) setelah menerima serangan dari pemain.
    /// Berlari cepat menjauhi posisi penyerang selama durasi tertentu.
    /// </summary>
    public class EnemyFleeState : EnemyBaseState
    {
        private Vector2 _fleeDirection;
        private float _fleeTimer;
        private const float FleeDuration = 2.0f;

        public EnemyFleeState(Enemy enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

        public override void Enter()
        {
            _enemy.SetNetworkState(EnemyStateEnum.Fleeing);
            _fleeTimer = FleeDuration;

            // Hitung vektor arah menjauhi posisi sumber damage
            Vector2 diff = (Vector2)_enemy.transform.position - (Vector2)_enemy.LastDamageSourcePosition;
            if (diff.sqrMagnitude < 0.001f)
            {
                _fleeDirection = Random.insideUnitCircle.normalized;
            }
            else
            {
                _fleeDirection = diff.normalized;
            }

            _enemy.SetFacingDirection(_fleeDirection.x < 0f);
            Debug.Log($"<color=red>[Enemy FSM]</color> {_enemy.EnemyName} panik dan kabur menjauhi penyerang!");
        }

        public override void Update(float deltaTime)
        {
            // Pergerakan lari menjauh
            Vector3 currentPos = _enemy.transform.position;
            Vector3 moveDelta = (Vector3)(_fleeDirection * (_enemy.FleeSpeed * deltaTime));
            _enemy.transform.position = currentPos + moveDelta;

            _enemy.SetFacingDirection(_fleeDirection.x < 0f);

            _fleeTimer -= deltaTime;
            if (_fleeTimer <= 0f)
            {
                // Setelah selesai kabur, kembali berpatroli / mencari tanaman
                _stateMachine.ChangeState(_enemy.WalkState);
            }
        }
    }
}
