using UnityEngine;

/// <summary>
/// Kompatibilitas legacy: PlayerMovement kini mewarisi PlayerController.
/// Seluruh fitur movement fisik dan event dispatching telah dipusatkan pada PlayerController.
/// Prefab atau skrip lama yang mereferensikan PlayerMovement tetap berfungsi normal tanpa error.
/// </summary>
public class PlayerMovement : PlayerController
{
}