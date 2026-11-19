using UnityEngine;

/// <summary>Bala que avanza por el mundo. Detecta impactos con un Raycast barrido
/// (no necesita Rigidbody y no atraviesa objetos aunque vaya rapido).</summary>
public class Projectile : MonoBehaviour
{
    public enum Owner { Player, Enemy }

    public Owner owner = Owner.Player;
    public float speed = 90f;
    public int damage = 1;
    public float life = 4f;
    public Transform shooter;

    private Vector3 _dir;
    private bool _launched;
    private static int _active;

    public static int ActiveCount => _active;

    private void OnEnable() { _active++; }
    private void OnDisable() { _active = Mathf.Max(0, _active - 1); }

    public void Launch(Vector3 direction, Transform shooterTransform)
    {
        _dir = direction.normalized;
        shooter = shooterTransform;
        _launched = true;
        transform.forward = _dir;
    }

    private void Update()
    {
        if (!_launched) return;

        float step = speed * Time.deltaTime;
        Vector3 from = transform.position;
        Vector3 to = from + _dir * step;

        if (Physics.Raycast(from, _dir, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
        {
            // No nos disparamos a nosotros mismos.
            if (shooter == null || !hit.transform.IsChildOf(shooter))
            {
                HandleHit(hit);
                return;
            }
        }

        transform.position = to;
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }

    private void HandleHit(RaycastHit hit)
    {
        Target target = hit.collider.GetComponentInParent<Target>();
        if (target != null)
        {
            target.TakeHit(damage);
            Destroy(gameObject);
            return;
        }

        if (owner == Owner.Enemy)
        {
            FirstPersonPlayer player = hit.collider.GetComponentInParent<FirstPersonPlayer>();
            if (player != null)
            {
                player.TakeDamage(1);
                Destroy(gameObject);
                return;
            }
        }

        // Pared, suelo, caja... la bala se queda aqui.
        Destroy(gameObject);
    }
}
