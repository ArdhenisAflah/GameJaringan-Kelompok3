using System;
using System.Collections;
using UnityEngine;

namespace Farming
{
    /// <summary>
    /// Komponen modular independen (Single Responsibility) pada tanaman untuk menghasilkan
    /// efek animasi goyangan (wiggle / sway) saat dilewati oleh pemain atau entitas lain.
    /// Bekerja murni melalui Unity Physics2D Trigger (OnTriggerEnter2D) dan animasi prosedural
    /// spring damping tanpa bergantung pada Animator Controller.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlantWiggleAnimation : MonoBehaviour
    {
        [Header("Target Visual")]
        [Tooltip("Transform yang akan digoyang/dirotasikan. Jika null, menggunakan transform GameObject ini.")]
        [SerializeField] private Transform visualTransform;

        [Tooltip("SpriteRenderer tanaman (opsional, untuk query visual jika diperlukan).")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Filter Entitas")]
        [Tooltip("LayerMask entitas yang diizinkan memicu goyangan (default layer Pemain).")]
        [SerializeField] private LayerMask triggerLayers = 1 << 8; // Layer 8 = Pemain

        [Tooltip("Tag opsional entitas pemicu.")]
        [SerializeField] private string targetTag = "Player";

        [Tooltip("Jika true, entitas yang mengimplementasikan IWiggleTriggerSource akan selalu memicu goyangan.")]
        [SerializeField] private bool allowTriggerSourceInterface = true;

        [Header("Pengaturan Animasi Wiggle")]
        [Tooltip("Sudut rotasi Z maksimal saat tanaman pertama kali tersenggol (derajat).")]
        [SerializeField] private float maxWiggleAngle = 12f;

        [Tooltip("Durasi animasi goyangan sampai kembali diam (detik).")]
        [SerializeField] private float wiggleDuration = 0.35f;

        [Tooltip("Frekuensi osilasi goyangan (kecepatan bolak-balik).")]
        [SerializeField] private float wiggleFrequency = 22f;

        [Tooltip("Tingkat peredaman (damping rate). Semakin besar, semakin cepat goyangan tenang.")]
        [SerializeField] private float damping = 6f;

        [Tooltip("Jeda waktu minimum antar pemicu goyangan untuk mencegah spam jittering.")]
        [SerializeField] private float retriggerCooldown = 0.15f;

        [Header("Squash & Stretch (Opsional)")]
        [Tooltip("Memberikan efek kompresi skala halus saat tersenggol.")]
        [SerializeField] private bool enableSquashAndStretch = true;
        [SerializeField] private float squashAmount = 0.08f;

        // Events untuk ekstensibilitas (SFX, partikel daun rontok, dsb)
        public event Action<TriggerEntityType, Collider2D> OnWiggleTriggered;

        private Coroutine _wiggleCoroutine;
        private Quaternion _initialRotation;
        private Vector3 _initialScale;
        private float _lastWiggleTime = -999f;

        private void Awake()
        {
            if (visualTransform == null)
            {
                visualTransform = transform;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _initialRotation = visualTransform.localRotation;
            _initialScale = visualTransform.localScale;
        }

        private void OnDisable()
        {
            ResetVisualState();
        }

        private void ResetVisualState()
        {
            if (_wiggleCoroutine != null)
            {
                StopCoroutine(_wiggleCoroutine);
                _wiggleCoroutine = null;
            }

            if (visualTransform != null)
            {
                visualTransform.localRotation = _initialRotation;
                visualTransform.localScale = _initialScale;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryTriggerWiggle(other);
        }

        /// <summary>
        /// Mengevaluasi objek yang bersentuhan dengan trigger dan memulai animasi goyangan jika valid.
        /// </summary>
        public bool TryTriggerWiggle(Collider2D other)
        {
            if (other == null) return false;
            if (Time.time < _lastWiggleTime + retriggerCooldown) return false;

            if (!IsValidTriggerEntity(other, out TriggerEntityType entityType, out Vector2 velocity))
            {
                return false;
            }

            _lastWiggleTime = Time.time;
            OnWiggleTriggered?.Invoke(entityType, other);

            // Tentukan arah senggolan (push direction)
            float pushDir = DeterminePushDirection(other, velocity);

            if (_wiggleCoroutine != null)
            {
                StopCoroutine(_wiggleCoroutine);
            }
            _wiggleCoroutine = StartCoroutine(WiggleRoutine(pushDir));
            return true;
        }

        private bool IsValidTriggerEntity(Collider2D col, out TriggerEntityType entityType, out Vector2 velocity)
        {
            entityType = TriggerEntityType.Other;
            velocity = Vector2.zero;

            if (col == null) return false;

            // 1. Cek interface modular IWiggleTriggerSource jika diimplementasikan
            if (allowTriggerSourceInterface)
            {
                if (col.TryGetComponent<IWiggleTriggerSource>(out var source) ||
                    (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<IWiggleTriggerSource>(out source)))
                {
                    entityType = source.EntityType;
                    velocity = source.Velocity;
                    return true;
                }
            }

            // 2. Cek komponen Player (PlayerMovement)
            if (col.TryGetComponent<PlayerMovement>(out _) ||
                (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)))
            {
                entityType = TriggerEntityType.Player;
                if (col.attachedRigidbody != null) velocity = col.attachedRigidbody.velocity;
                return true;
            }

            // 3. Cek Tag "Player"
            if (col.CompareTag(targetTag) || (col.attachedRigidbody != null && col.attachedRigidbody.CompareTag(targetTag)))
            {
                entityType = TriggerEntityType.Player;
                if (col.attachedRigidbody != null) velocity = col.attachedRigidbody.velocity;
                return true;
            }

            // 4. Cek LayerMask
            if (((1 << col.gameObject.layer) & triggerLayers.value) != 0)
            {
                entityType = (col.gameObject.layer == 8) ? TriggerEntityType.Player : TriggerEntityType.Other;
                if (col.attachedRigidbody != null) velocity = col.attachedRigidbody.velocity;
                return true;
            }

            return false;
        }

        private float DeterminePushDirection(Collider2D other, Vector2 velocity)
        {
            // Jika entitas memiliki kecepatan horizontal yang signifikan, ikuti arah geraknya
            if (Mathf.Abs(velocity.x) > 0.05f)
            {
                return Mathf.Sign(velocity.x);
            }

            // Fallback: bandingkan posisi X tanaman dengan entitas
            float diffX = transform.position.x - other.transform.position.x;
            if (Mathf.Abs(diffX) > 0.02f)
            {
                return Mathf.Sign(diffX);
            }

            // Arah acak jika tepat di tengah
            return UnityEngine.Random.value > 0.5f ? 1f : -1f;
        }

        private IEnumerator WiggleRoutine(float pushDirection)
        {
            float elapsed = 0f;

            while (elapsed < wiggleDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / wiggleDuration);

                // Damped harmonic oscillation: e^(-damping * t) * cos(frequency * t)
                float decay = Mathf.Exp(-damping * progress);
                float oscillation = Mathf.Cos(elapsed * wiggleFrequency);
                float currentAngle = -pushDirection * maxWiggleAngle * decay * oscillation;

                if (visualTransform != null)
                {
                    visualTransform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

                    if (enableSquashAndStretch)
                    {
                        float squash = Mathf.Sin(elapsed * wiggleFrequency * 0.5f) * squashAmount * decay;
                        visualTransform.localScale = new Vector3(
                            _initialScale.x * (1f + squash),
                            _initialScale.y * (1f - squash),
                            _initialScale.z
                        );
                    }
                }

                yield return null;
            }

            // Kembalikan rotasi dan skala ke posisi awal
            if (visualTransform != null)
            {
                visualTransform.localRotation = _initialRotation;
                visualTransform.localScale = _initialScale;
            }

            _wiggleCoroutine = null;
        }
    }
}
