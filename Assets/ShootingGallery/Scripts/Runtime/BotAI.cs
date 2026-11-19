using UnityEngine;

/// <summary>
/// Soldado enemigo: puede esperar a que el jugador salga de la galeria,
/// perseguirlo y disparar balas visibles desde larga distancia.
/// </summary>
public class BotAI : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 2.8f;
    public float stopDistance = 1.5f;
    public int contactDamage = 1;
    public float contactCooldown = 1.2f;

    [Header("Zona de patrulla (evita que entren en la galeria)")]
    public bool useBounds;
    public Vector2 minXZ = new Vector2(-100f, -100f);
    public Vector2 maxXZ = new Vector2(100f, 100f);

    [Header("Deteccion")]
    public bool requiresExitDetection;
    public float activationX = 15.5f;

    [Header("Disparo a distancia")]
    public GameObject enemyBulletPrefab;
    public Transform muzzle;
    public float fireRange = 30f;
    public float fireInterval = 2.1f;
    public float fireSpread = 3f;
    public float bulletSpeed = 42f;

    [Header("Sonido de disparo")]
    public AudioClip fireClip;
    public AudioSource sfxSource;

    private Transform _player;
    private Animator _animator;
    private CharacterController _controller;
    private float _hitTimer;
    private float _fireTimer;
    private float _avoidTimer;
    private float _avoidSign = 1f;
    private bool _detected;

    protected virtual void Start()
    {
        _player = FirstPersonPlayer.Instance != null ? FirstPersonPlayer.Instance.transform : null;
        _animator = GetComponentInChildren<Animator>();
        _controller = GetComponent<CharacterController>();
        _detected = !requiresExitDetection;
        _fireTimer = Random.Range(0.4f, Mathf.Max(0.5f, fireInterval));

        if (_detected) GameManager.Instance?.OnEnemiesDetected();
    }

    protected virtual void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_player == null) return;

        _hitTimer -= Time.deltaTime;
        CheckDetection();

        if (!_detected)
        {
            if (_animator != null) _animator.SetFloat("Speed", 0f);
            return;
        }

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        bool moving = distance > stopDistance;

        if (moving)
        {
            Vector3 direction = toPlayer.normalized;
            _avoidTimer -= Time.deltaTime;
            if (_avoidTimer > 0f)
                direction = Quaternion.Euler(0f, 55f * _avoidSign, 0f) * direction;

            Vector3 delta = direction * speed * Time.deltaTime;
            Vector3 next = transform.position + delta;
            if (useBounds)
            {
                next.x = Mathf.Clamp(next.x, minXZ.x, maxXZ.x);
                next.z = Mathf.Clamp(next.z, minXZ.y, maxXZ.y);
                delta = next - transform.position;
            }

            if (_controller != null)
            {
                // Move() respeta los colliders: los bots ya no atraviesan
                // cajas, muros ni coberturas. El pequeño empuje hacia abajo
                // los mantiene pegados al suelo.
                _controller.Move(delta + Vector3.down * 0.5f * Time.deltaTime);
                if ((_controller.collisionFlags & CollisionFlags.Sides) != 0 && _avoidTimer <= 0f)
                {
                    _avoidTimer = 0.7f;
                    _avoidSign = -_avoidSign;
                }
            }
            else
            {
                transform.position = next;
            }
        }
        else if (_hitTimer <= 0f)
        {
            _hitTimer = contactCooldown;
            FirstPersonPlayer.Instance?.TakeDamage(contactDamage);
        }

        // Mirar al jugador tambien cuando esta quieto: asi la bocacha apunta bien.
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(toPlayer), 7f * Time.deltaTime);
        }

        UpdateRangedAttack(distance);

        if (_animator != null) _animator.SetFloat("Speed", moving ? 1f : 0f);
    }

    private void CheckDetection()
    {
        if (_detected || !requiresExitDetection) return;
        if (_player.position.x >= activationX)
        {
            _detected = true;
            GameManager.Instance?.OnEnemiesDetected();
        }
    }

    /// <summary>Dispara al jugador si esta a alcance y no hay muro de por medio.</summary>
    protected virtual void UpdateRangedAttack(float distance)
    {
        if (enemyBulletPrefab == null || distance > fireRange) return;

        _fireTimer -= Time.deltaTime;
        if (_fireTimer > 0f) return;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
        Vector3 target = _player.position + Vector3.up * 1f;
        if (!HasLineOfSight(origin, target))
        {
            _fireTimer = 0.2f;
            return;
        }

        _fireTimer = Mathf.Max(0.35f, fireInterval + Random.Range(-0.25f, 0.25f));
        PlayShootAnimation();
        PlayFireSound();

        Vector3 aim = (target - origin).normalized;
        if (fireSpread > 0f)
        {
            aim = Quaternion.Euler(UnityEngine.Random.Range(-fireSpread, fireSpread),
                UnityEngine.Random.Range(-fireSpread, fireSpread), 0f) * aim;
        }

        GameObject go = Instantiate(enemyBulletPrefab, origin, Quaternion.LookRotation(aim));
        Projectile bullet = go.GetComponent<Projectile>();
        if (bullet == null) bullet = go.AddComponent<Projectile>();
        bullet.owner = Projectile.Owner.Enemy;
        bullet.speed = bulletSpeed;
        bullet.damage = 1;
        bullet.shooter = transform;
        bullet.Launch(aim, transform);
    }

    /// <summary>Primero obstruction que encuentre: muro (false) o jugador (true).</summary>
    private bool HasLineOfSight(Vector3 origin, Vector3 target)
    {
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance, ~0,
            QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            if (_player != null && hit.transform.IsChildOf(_player)) return true;
            return false;
        }
        return true;
    }

    /// <summary>Dispara la animacion de disparo del modelo de soldado.</summary>
    protected void PlayShootAnimation()
    {
        if (_animator != null) _animator.SetTrigger("Shoot");
    }

    /// <summary>
    /// El disparo enemigo se oye incluso a distancia: la fuente es 2D/3D
    /// suave para que no se pierda con el volumen del arma del jugador.
    /// </summary>
    private void PlayFireSound()
    {
        if (fireClip == null) return;
        if (sfxSource != null)
        {
            sfxSource.pitch = Random.Range(0.92f, 1.08f);
            sfxSource.PlayOneShot(fireClip, 0.85f);
            return;
        }
        GameManager.Instance?.PlaySfx(fireClip, 0.6f);
    }
}
