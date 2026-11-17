using UnityEngine;
using EnemySystem.FSM;

namespace EnemySystem.FSM.States
{
    /// <summary>
    /// State saat musuh terkena serangan/hit (flinch/stagger/recoil):
    /// Memberikan efek jeda stun singkat dan sedikit dorongan mundur (knockback) menjauhi penyerang,
    /// sebelum kemudian bertransisi ke FleeState (kabur) atau kembali ke WalkState.
    /// </summary>
    public class EnemyHitState : EnemyBaseState
    {
        private float _hitTimer;
        private Vector2 _knockbackVelocity;

        public EnemyHitState(Enemy enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

        public override void Enter()
        {
            _enemy.SetNetworkState(EnemyStateEnum.Hit);
            _hitTimer = _enemy.HitStunDuration;

            // Hitung arah dorongan (knockback) menjauhi sumber serangan
            Vector2 diff = (Vector2)_enemy.transform.position - (Vector2)_enemy.LastDamageSourcePosition;
            Vector2 knockbackDir = diff.sqrMagnitude < 0.001f ? Random.insideUnitCircle.normalized : diff.normalized;
            _knockbackVelocity = knockbackDir * _enemy.KnockbackForce;

            // Hadapkan musuh menghadap ke arah datangnya serangan
            _enemy.SetFacingDirection(diff.x > 0f);

            Debug.Log($"<color=orange>[Enemy FSM]</color> {_enemy.EnemyName} ('{_enemy.name}') memasuki HitState (Stun: {_enemy.HitStunDuration}s, Knockback: {_enemy.KnockbackForce}).");
        }

        public override void Update(float deltaTime)
        {
            // Terapkan sedikit dorongan knockback teredam
            if (_knockbackVelocity.sqrMagnitude > 0.01f)
            {
                _enemy.transform.position += (Vector3)(_knockbackVelocity * deltaTime);
                _knockbackVelocity = Vector2.Lerp(_knockbackVelocity, Vector2.zero, deltaTime * 10f);
            }

            _hitTimer -= deltaTime;
            if (_hitTimer <= 0f)
            {
                // Setelah jeda hit selesai, beralih ke FleeState untuk kabur menjauh
                if (_enemy.FleeState != null)
                {
                    _stateMachine.ChangeState(_enemy.FleeState);
                }
                else
                {
                    _stateMachine.ChangeState(_enemy.WalkState);
                }
            }
        }
    }
}
