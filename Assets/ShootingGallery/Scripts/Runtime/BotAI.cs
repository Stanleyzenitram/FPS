using UnityEngine;

/// <summary>Bot simple: persigue al jugador y le quita una vida al chocar.</summary>
public class BotAI : MonoBehaviour
{
    public float speed = 2.8f;
    public float stopDistance = 1.5f;
    public int contactDamage = 1;
    public float contactCooldown = 1.2f;

    private Transform _player;
    private float _hitTimer;

    protected virtual void Start()
    {
        _player = FirstPersonPlayer.Instance != null ? FirstPersonPlayer.Instance.transform : null;
    }

    protected virtual void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_player == null) return;

        _hitTimer -= Time.deltaTime;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (distance > stopDistance)
        {
            Vector3 step = toPlayer.normalized * speed * Time.deltaTime;
            transform.position += step;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(toPlayer), 6f * Time.deltaTime);
        }
        else if (_hitTimer <= 0f)
        {
            _hitTimer = contactCooldown;
            FirstPersonPlayer.Instance?.TakeDamage(contactDamage);
        }
    }
}
