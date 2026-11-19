using UnityEngine;

/// <summary>
/// Arma expuesta en la pared de la galeria. Al recibir un disparo cambia el
/// arsenal del jugador. No es un Target: no suma puntos ni cuenta para ganar.
/// </summary>
public class WeaponWallSwitch : MonoBehaviour
{
    [Header("Arma que entrega")]
    [Tooltip("Indice del slot del arsenal: 0-9 = AR del paquete, 10 = pistola M9")]
    public int weaponIndex = 0;
    public string weaponLabel = "AR-L 5.56";
    public AudioClip confirmClip;

    /// <summary>Llamado por Projectile cuando una bala del jugador impacta aqui.</summary>
    public void Activate()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        Weapon weapon = Weapon.Instance != null ? Weapon.Instance : FindAnyObjectByType<Weapon>();
        if (weapon == null || weapon.slots == null ||
            weaponIndex < 0 || weaponIndex >= weapon.slots.Length ||
            weapon.slots[weaponIndex] == null) return;

        if (weapon.CurrentIndex == weaponIndex)
        {
            GameManager.Instance?.NotifyWeaponSwitch("YA LLEVAS " + weaponLabel, 1.2f);
            return;
        }

        weapon.SelectWeapon(weaponIndex);
        GameManager.Instance?.NotifyWeaponSwitch("ARMA: " + weaponLabel, 1.8f);
        if (confirmClip != null) GameManager.Instance?.PlaySfx(confirmClip, 0.85f);
    }
}
