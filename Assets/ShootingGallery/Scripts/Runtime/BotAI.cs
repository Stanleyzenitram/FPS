using UnityEngine;

/// <summary>Bot con el soldado personalizado: persigue al jugador y quita vida al chocar.</summary>
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

    private Transform _player;
    private Animator _animator;
    private float _hitTimer;

    protected virtual void Start()
    {
        _player = FirstPersonPlayer.Instance != null ? FirstPersonPlayer.Instance.transform : null;
        _animator = GetComponentInChildren<Animator>();
    }

    protected virtual void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_player == null) return;

        _hitTimer -= Time.deltaTime;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        bool moving = distance > stopDistance;

        if (moving)
        {
            Vector3 next = transform.position + toPlayer.normalized * speed * Time.deltaTime;
            if (useBounds)
            {
                next.x = Mathf.Clamp(next.x, minXZ.x, maxXZ.x);
                next.z = Mathf.Clamp(next.z, minXZ.y, maxXZ.y);
            }
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(toPlayer), 6f * Time.deltaTime);
        }
        else if (_hitTimer <= 0f)
        {
            _hitTimer = contactCooldown;
            FirstPersonPlayer.Instance?.TakeDamage(contactDamage);
        }

        if (_animator != null) _animator.SetFloat("Speed", moving ? 1f : 0f);
    }

    /// <summary>Dispara la animacion de disparo del modelo de soldado.</summary>
    protected void PlayShootAnimation()
    {
        if (_animator != null) _animator.SetTrigger("Shoot");
    }
}
