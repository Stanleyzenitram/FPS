using UnityEngine;

/// <summary>Jefe final: mas lento, mucha vida, dispara balas enemigas y hace dano al contacto.</summary>
public class BossAI : BotAI
{
    public GameObject enemyBulletPrefab;
    public Transform muzzle;
    public float fireInterval = 1.5f;
    public float bulletSpeed = 30f;

    private float _fireTimer;

    protected override void Start()
    {
        base.Start();
        _fireTimer = fireInterval;
        speed = 2.0f;
        stopDistance = 4f;
    }

    protected override void Update()
    {
        base.Update();

        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (FirstPersonPlayer.Instance == null) return;

        _fireTimer -= Time.deltaTime;
        if (_fireTimer > 0f) return;
        _fireTimer = fireInterval;

        Transform player = FirstPersonPlayer.Instance.transform;
        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
        Vector3 aim = (player.position + Vector3.up * 1f - origin).normalized;

        if (enemyBulletPrefab == null) return;
        GameObject go = Instantiate(enemyBulletPrefab, origin, Quaternion.identity);
        Projectile bullet = go.GetComponent<Projectile>();
        if (bullet == null) bullet = go.AddComponent<Projectile>();
        bullet.owner = Projectile.Owner.Enemy;
        bullet.speed = bulletSpeed;
        bullet.shooter = transform;
        bullet.Launch(aim, transform);
    }
}
