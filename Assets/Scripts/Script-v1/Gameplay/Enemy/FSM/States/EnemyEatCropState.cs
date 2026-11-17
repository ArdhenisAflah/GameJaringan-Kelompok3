using UnityEngine;
using EnemySystem.FSM;
using Farming;

namespace EnemySystem.FSM.States
{
    /// <summary>
    /// State saat musuh sedang memakan tanaman (Eat Crops):
    /// Melakukan channeling gigitan berkala yang memicu flash putih pada SpriteRenderer tanaman
    /// serta observer event, sebelum akhirnya menghabiskan dan mendespawn tanaman tersebut.
    /// </summary>
    public class EnemyEatCropState : EnemyBaseState
    {
        private float _eatTimer;
        private float _biteTimer;

        public EnemyEatCropState(Enemy enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

        public override void Enter()
        {
            if (_enemy.CurrentTargetCrop == null || !_enemy.CurrentTargetCrop.gameObject.activeInHierarchy)
            {
                _stateMachine.ChangeState(_enemy.WalkState);
                return;
            }

            _enemy.SetNetworkState(EnemyStateEnum.Eating);
            _eatTimer = _enemy.EatDuration;
            _biteTimer = 0.1f; // Gigitan pertama dipicu segera setelah tiba di tanaman

            // Hadapkan musuh ke arah tanaman
            Vector3 diff = _enemy.CurrentTargetCrop.transform.position - _enemy.transform.position;
            _enemy.SetFacingDirection(diff.x < 0f);

            Debug.Log($"<color=magenta>[Enemy FSM]</color> {_enemy.EnemyName} mulai memakan tanaman '{_enemy.CurrentTargetCrop.PlantName}' (durasi: {_enemy.EatDuration}s).");
        }

        public override void Update(float deltaTime)
        {
            // 1. Validasi keberadaan tanaman target (bisa saja dipanen pemain di tengah-tengah makan)
            if (_enemy.CurrentTargetCrop == null || !_enemy.CurrentTargetCrop.gameObject.activeInHierarchy)
            {
                Debug.Log($"<color=orange>[Enemy FSM]</color> Tanaman target hilang/dipanen pemain saat sedang dimakan. {_enemy.EnemyName} kembali berjalan.");
                _enemy.CurrentTargetCrop = null;
                _stateMachine.ChangeState(_enemy.WalkState);
                return;
            }

            // 2. Efek gigitan berkala (Pulse White Flash pada crop)
            _biteTimer -= deltaTime;
            if (_biteTimer <= 0f)
            {
                _biteTimer = _enemy.BiteInterval;

                // Memicu efek putih pada sprite tanaman dan observer event
                _enemy.CurrentTargetCrop.TriggerBiteFlash(0.15f);
                _enemy.NotifyCropBitten(_enemy.CurrentTargetCrop);

                Debug.Log($"<color=yellow>[Enemy Observer]</color> {_enemy.EnemyName} menggigit '{_enemy.CurrentTargetCrop.PlantName}'! (Flash Putih Sprite Crop & Event OnCropBitten).");
            }

            // 3. Evaluasi durasi makan selesai
            _eatTimer -= deltaTime;
            if (_eatTimer <= 0f)
            {
                Plant consumedCrop = _enemy.CurrentTargetCrop;
                _enemy.CurrentTargetCrop = null;

                // Observer event: tanaman habis dimakan
                _enemy.NotifyCropEaten(consumedCrop);

                // Despawn tanaman secara otoritatif di server
                consumedCrop.ServerConsumeByEnemy();

                Debug.Log($"<color=green>[Enemy FSM]</color> {_enemy.EnemyName} selesai memakan tanaman. Beralih ke state Idle.");
                _stateMachine.ChangeState(_enemy.IdleState);
            }
        }

        public override void Exit()
        {
            // Reset state saat keluar
        }
    }
}
