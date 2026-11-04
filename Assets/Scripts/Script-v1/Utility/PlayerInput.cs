using System;
using UnityEngine;

namespace Utility
{
    /// <summary>
    /// Utility class dan komponen pembaca input pemain terpusat (Single Responsibility).
    /// Mengabstraksi pembacaan Input Unity (Keyboard/Joystick/Virtual) agar tidak tersebar
    /// di banyak skrip, memudahkan remap tombol, pengujian, serta penonaktifan input saat Pause/Cutscene.
    /// </summary>
    public class PlayerInput : MonoBehaviour
    {
        [Header("Status Input")]
        [Tooltip("Apakah input dari pemain sedang aktif diterima?")]
        [SerializeField] private bool inputEnabled = true;

        [Header("Mapping Tombol (Keycodes)")]
        [SerializeField] private KeyCode plantKey = KeyCode.E;
        [SerializeField] private KeyCode actionKey = KeyCode.Space;
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [SerializeField] private KeyCode toolEmptyKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode toolHoeKey = KeyCode.Alpha2;
        [SerializeField] private KeyCode toolWaterKey = KeyCode.Alpha3;

        [Header("Bypass Shortcut (Testing)")]
        [SerializeField] private KeyCode bypassDigKey = KeyCode.Backslash;
        [SerializeField] private KeyCode bypassWaterKey = KeyCode.RightBracket;
        [SerializeField] private KeyCode bypassGrassKey = KeyCode.LeftBracket;

        // Properties pembacaan input (frame-accurate)
        public Vector2 MoveInput { get; private set; }
        public bool IsMoving => MoveInput.sqrMagnitude > 0.01f;

        public bool IsPlantPressed { get; private set; }
        public bool IsActionPressed { get; private set; }
        public bool IsInteractPressed { get; private set; }

        public bool IsToolEmptyPressed { get; private set; }
        public bool IsToolHoePressed { get; private set; }
        public bool IsToolWaterPressed { get; private set; }

        public bool IsBypassDigPressed { get; private set; }
        public bool IsBypassWaterPressed { get; private set; }
        public bool IsBypassGrassPressed { get; private set; }

        public KeyCode PlantKey => plantKey;
        public KeyCode ActionKey => actionKey;
        public KeyCode InteractKey => interactKey;

        /// <summary>
        /// Mengaktifkan atau menonaktifkan pembacaan input pemain.
        /// Saat dinonaktifkan, semua state input langsung di-reset ke nol/false.
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                ResetInputs();
            }
        }

        private void ResetInputs()
        {
            MoveInput = Vector2.zero;
            IsPlantPressed = false;
            IsActionPressed = false;
            IsInteractPressed = false;
            IsToolEmptyPressed = false;
            IsToolHoePressed = false;
            IsToolWaterPressed = false;
            IsBypassDigPressed = false;
            IsBypassWaterPressed = false;
            IsBypassGrassPressed = false;
        }

        private void Update()
        {
            if (!inputEnabled) return;

            // 1. Pergerakan (Axis Raw agar kontrol snappy)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector2 rawMove = new Vector2(h, v);
            MoveInput = rawMove.sqrMagnitude > 1f ? rawMove.normalized : rawMove;

            // 2. Tombol Aksi
            IsPlantPressed = Input.GetKeyDown(plantKey);
            IsActionPressed = Input.GetKeyDown(actionKey);
            IsInteractPressed = Input.GetKeyDown(interactKey);

            // 3. Tombol Alat
            IsToolEmptyPressed = Input.GetKeyDown(toolEmptyKey);
            IsToolHoePressed = Input.GetKeyDown(toolHoeKey);
            IsToolWaterPressed = Input.GetKeyDown(toolWaterKey);

            // 4. Bypass Dev Keys
            IsBypassDigPressed = Input.GetKeyDown(bypassDigKey);
            IsBypassWaterPressed = Input.GetKeyDown(bypassWaterKey);
            IsBypassGrassPressed = Input.GetKeyDown(bypassGrassKey);
        }
    }
}
