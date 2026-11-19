using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Arma de primera persona. Dispara con el boton izquierdo del raton.</summary>
public class Weapon : MonoBehaviour
{
    [Header("Referencias")]
    public Camera aimCamera;
    public Transform muzzle;
    public GameObject bulletPrefab;
    public AudioClip fireClip;

    [Header("Ajustes")]
    public float fireRate = 0.16f;
    public float spread = 0f;
    public float bulletSpeed = 95f;

    private float _cooldown;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (muzzle == null) muzzle = transform;
    }

    private void Update()
    {
        _cooldown -= Time.deltaTime;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (GameManager.Instance == null || !GameManager.Instance.CanShoot) return;
        if (_cooldown > 0f) return;

        Fire();
    }

    private void Fire()
    {
        _cooldown = fireRate;

        if (bulletPrefab != null)
        {
            GameObject go = Instantiate(bulletPrefab, muzzle.position, Quaternion.identity);
            Projectile bullet = go.GetComponent<Projectile>();
            if (bullet == null) bullet = go.AddComponent<Projectile>();

            Vector3 dir = aimCamera != null ? aimCamera.transform.forward : transform.forward;
            if (spread > 0f)
            {
                dir = Quaternion.Euler(Random.Range(-spread, spread),
                                       Random.Range(-spread, spread),
                                       Random.Range(-spread, spread)) * dir;
            }

            bullet.owner = Projectile.Owner.Player;
            bullet.speed = bulletSpeed;
            bullet.shooter = transform.root;
            bullet.Launch(dir, transform.root);
        }

        if (fireClip != null)
            AudioSource.PlayClipAtPoint(fireClip, muzzle.position, 0.7f);

        // Fogazo rapido (luz temporal, se destruye solo).
        GameObject flash = new GameObject("MuzzleFlash");
        flash.transform.position = muzzle.position;
        Light light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.45f);
        light.intensity = 4f;
        light.range = 4f;
        Destroy(flash, 0.06f);

        GameManager.Instance.ConsumeAmmo();
    }
}
