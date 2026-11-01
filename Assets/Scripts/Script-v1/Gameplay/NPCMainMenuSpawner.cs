using System.Collections.Generic;
using UnityEngine;

public class NPCMainMenuSpawner : MonoBehaviour
{
    [Header("Mode Prefab (Spawn Karakter Baru)")]
    [Tooltip("Prefab karakter NPC (harus memiliki script NPCMainMenuCharacter, Animator, SpriteRenderer)")]
    [SerializeField] private GameObject characterPrefab;

    [Tooltip("Jumlah karakter yang di-spawn")]
    [SerializeField] private int spawnCount = 2;

    [Header("Posisi Spawn di Luar Layar")]
    [Tooltip("Koordinat X spawn di luar layar kiri")]
    [SerializeField] private float spawnLeftX = -11.5f;

    [Tooltip("Koordinat X spawn di luar layar kanan")]
    [SerializeField] private float spawnRightX = 11.5f;

    [Tooltip("Rentang Y acak saat spawn di luar layar")]
    [SerializeField] private Vector2 spawnYRange = new Vector2(-12.0f, -10.5f);

    [Header("Mode Scene (Karakter yang Sudah Ditaruh di Scene)")]
    [Tooltip("Daftar karakter yang sudah diletakkan manual di luar layar di Scene")]
    [SerializeField] private List<NPCMainMenuCharacter> sceneCharacters = new List<NPCMainMenuCharacter>();

    [Header("Referensi Zona Title (Otomatis Diberikan ke Karakter)")]
    [SerializeField] private Transform titleTransform;
    [SerializeField] private Collider2D titleCollider;

    private readonly List<NPCMainMenuCharacter> _spawnedCharacters = new List<NPCMainMenuCharacter>();
    private bool _hasSpawned = false;

    /// <summary>
    /// Memunculkan karakter di luar layar dan mengaktifkannya untuk mulai berjalan ke lahan
    /// Hubungkan fungsi ini ke event onMainMenuReady di MainMenu atau onAllButtonsShown di MainMenuButtonsAnimator
    /// </summary>
    [ContextMenu("Spawn & Activate Characters")]
    public void SpawnAndActivateCharacters()
    {
        if (_hasSpawned) return;
        _hasSpawned = true;

        // 1. Aktifkan karakter yang ditaruh langsung di Scene jika ada
        foreach (var character in sceneCharacters)
        {
            if (character != null)
            {
                character.gameObject.SetActive(true);
                character.ActivateCharacter();
            }
        }

        // 2. Spawn karakter dari Prefab jika ada prefab yang di-assign
        if (characterPrefab != null && spawnCount > 0)
        {
            for (int i = 0; i < spawnCount; i++)
            {
                // Bergantian spawn dari kiri dan kanan layar
                bool spawnOnLeft = (i % 2 == 0);
                float posX = spawnOnLeft ? spawnLeftX : spawnRightX;
                float posY = Random.Range(spawnYRange.x, spawnYRange.y);
                Vector3 spawnPos = new Vector3(posX, posY, 0f);

                GameObject charObj = Instantiate(characterPrefab, spawnPos, Quaternion.identity);
                charObj.name = $"NPC_MainMenu_{i + 1}";

                NPCMainMenuCharacter npc = charObj.GetComponent<NPCMainMenuCharacter>();
                if (npc != null)
                {
                    _spawnedCharacters.Add(npc);
                    npc.ActivateCharacter();
                }
            }
        }
    }

    /// <summary>
    /// Menghentikan aktivitas seluruh karakter jika scene keluar/berpindah
    /// </summary>
    public void DeactivateAllCharacters()
    {
        foreach (var npc in _spawnedCharacters)
        {
            if (npc != null) npc.DeactivateCharacter();
        }

        foreach (var npc in sceneCharacters)
        {
            if (npc != null) npc.DeactivateCharacter();
        }
    }
}
