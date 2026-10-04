using UnityEngine;
using FishNet.Object;

namespace CameraSystem
{
    /// <summary>
    /// Komponen NetworkBehaviour pada prefab pemain yang secara otomatis mendaftarkan
    /// transform pemain lokal (IsOwner) ke sistem kamera utama.
    /// Memastikan kamera independen untuk setiap client dan hanya fokus pada karakter pemiliknya.
    /// </summary>
    public class PlayerCameraBinder : NetworkBehaviour
    {
        public override void OnStartClient()
        {
            base.OnStartClient();

            // Hanya karakter milik client lokal (IsOwner) yang mengambil alih fokus kamera
            if (IsOwner)
            {
                PlayerCameraFollow.AssignTarget(transform, snapImmediately: true);
            }
        }

        private void OnDestroy()
        {
            // Lepas referensi kamera jika karakter ini yang sedang diikuti saat hancur / disconnect
            if (IsOwner && PlayerCameraFollow.Instance != null && PlayerCameraFollow.Instance.Target == transform)
            {
                PlayerCameraFollow.Instance.ClearTarget();
            }
        }
    }
}
