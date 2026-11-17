using UnityEngine;
using EnemySystem.FSM;
using Farming;

namespace EnemySystem.FSM.States
{
    /// <summary>
    /// State pergerakan jalan musuh (Walk):
    /// Jika mendeteksi tanaman di sekitar, musuh akan berjalan menuju tanaman tersebut.
    /// Jika tidak ada tanaman, musuh akan berpatroli (wander) ke titik acak di ladang.
    /// </summary>
    public class EnemyWalkState : EnemyBaseState
    {
        private Vector3 _destination;
        private bool _isHeadingToCrop = false;
        private float _reScanTimer = 0f;
        private const float ReScanInterval = 0.5f;

        public Vector3 CurrentDestination => _destination;
        public bool IsHeadingToCrop => _isHeadingToCrop;

        public EnemyWalkState(Enemy enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

        public override void Enter()
        {
            _enemy.SetNetworkState(EnemyStateEnum.Walking);
            PickDestination();
        }

        private void PickDestination()
        {
            Plant target = _enemy.CurrentTargetCrop != null ? _enemy.CurrentTargetCrop : _enemy.FindNearestCrop();

            if (target != null && target.gameObject.activeInHierarchy)
            {
                _enemy.CurrentTargetCrop = target;
                _destination = target.transform.position;
                _isHeadingToCrop = true;
            }
            else
            {
                _enemy.CurrentTargetCrop = null;
                _destination = GenerateRandomWanderPoint();
                _isHeadingToCrop = false;
            }
        }

        protected virtual Vector3 GenerateRandomWanderPoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * _enemy.WanderRadius;
            Vector3 point = _enemy.transform.position + new Vector3(randomCircle.x, randomCircle.y, 0f);
            point.z = 0f;
            return point;
        }

        public override void Update(float deltaTime)
        {
            // 1. Jika sedang wander tanpa tanaman, lakukan re-scan berkala
            if (!_isHeadingToCrop)
            {
                _reScanTimer -= deltaTime;
                if (_reScanTimer <= 0f)
                {
                    _reScanTimer = ReScanInterval;
                    Plant crop = _enemy.FindNearestCrop();
                    if (crop != null)
                    {
                        _enemy.CurrentTargetCrop = crop;
                        _destination = crop.transform.position;
                        _isHeadingToCrop = true;
                    }
                }
            }
            else
            {
                // Jika sedang menuju tanaman, pastikan tanaman masih valid/belum dipanen
                if (_enemy.CurrentTargetCrop == null || !_enemy.CurrentTargetCrop.gameObject.activeInHierarchy)
                {
                    _enemy.CurrentTargetCrop = null;
                    _isHeadingToCrop = false;
                    PickDestination();
                    return;
                }
                else
                {
                    _destination = _enemy.CurrentTargetCrop.transform.position;
                }
            }

            // 2. Pergerakan maju menuju titik tujuan
            Vector3 currentPos = _enemy.transform.position;
            currentPos.z = 0f;
            _destination.z = 0f;

            Vector2 direction = (new Vector2(_destination.x, _destination.y) - (Vector2)currentPos);
            float distance = direction.magnitude;

            if (distance > 0.01f)
            {
                // Update arah hadap sprite
                _enemy.SetFacingDirection(direction.x < 0f);

                // Gerakkan posisi
                _enemy.transform.position = Vector3.MoveTowards(currentPos, _destination, _enemy.MoveSpeed * deltaTime);
            }

            // 3. Evaluasi apakah sudah tiba di tujuan
            if (_isHeadingToCrop)
            {
                if (distance <= _enemy.EatDistanceThreshold)
                {
                    _stateMachine.ChangeState(_enemy.EatCropState);
                }
            }
            else
            {
                if (distance <= 0.15f)
                {
                    _stateMachine.ChangeState(_enemy.IdleState);
                }
            }
        }

        public override void OnDrawGizmos()
        {
            Gizmos.color = _isHeadingToCrop ? Color.red : Color.cyan;
            Gizmos.DrawLine(_enemy.transform.position, _destination);
            Gizmos.DrawWireSphere(_destination, 0.2f);
        }
    }
}
