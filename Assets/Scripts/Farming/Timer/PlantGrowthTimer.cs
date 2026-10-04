using UnityEngine;

namespace Farming.Timer
{
    /// <summary>
    /// Implementasi konkret dari IGrowthTimer untuk menghitung progres timer tanaman.
    /// Murni C# tanpa ketergantungan MonoBehaviour sehingga mudah diuji (unit testing) dan digunakan ulang.
    /// </summary>
    public class PlantGrowthTimer : IGrowthTimer
    {
        private float _targetDuration;
        private float _elapsedTime;
        private float _speedMultiplier = 1.0f;
        private bool _isPaused = false;

        public float TargetDuration => _targetDuration;
        public float ElapsedTime => _elapsedTime;
        public float RemainingTime => Mathf.Max(0f, _targetDuration - _elapsedTime);
        public float ProgressRatio => _targetDuration > 0f ? Mathf.Clamp01(_elapsedTime / _targetDuration) : 1.0f;

        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = Mathf.Max(0f, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        public bool IsFinished => _elapsedTime >= _targetDuration;

        /// <summary>
        /// Konstruktor default PlantGrowthTimer dengan durasi awal.
        /// </summary>
        /// <param name="initialDuration">Durasi waktu awal dalam detik.</param>
        public PlantGrowthTimer(float initialDuration = 10f)
        {
            ResetTimer(initialDuration);
        }

        public void Tick(float deltaTime)
        {
            if (_isPaused || IsFinished || _targetDuration <= 0f) return;

            _elapsedTime += deltaTime * _speedMultiplier;
            if (_elapsedTime > _targetDuration)
            {
                _elapsedTime = _targetDuration;
            }
        }

        public void ResetTimer(float newDuration)
        {
            _targetDuration = Mathf.Max(0.001f, newDuration);
            _elapsedTime = 0f;
        }

        public void SetProgressRatio(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            _elapsedTime = _targetDuration * ratio;
        }

        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }
    }
}
