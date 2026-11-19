using UnityEngine;

/// <summary>Bala que avanza por el mundo. Detecta impactos con un Raycast barrido
/// (no necesita Rigidbody y no atraviesa objetos aunque vaya rapido).</summary>
public class Projectile : MonoBehaviour
{
    public enum Owner { Player, Enemy }

    public Owner owner = Owner.Player;
    public float speed = 115f;
    public int damage = 1;
    public float life = 4f;
    public Transform shooter;

    private Vector3 _dir;
    private bool _launched;
    private static int _active;
    private static Material _trailMaterial;

    public static int ActiveCount => _active;

    private void OnEnable() { _active++; }
    private void OnDisable() { _active = Mathf.Max(0, _active - 1); }

    public void Launch(Vector3 direction, Transform shooterTransform)
    {
        _dir = direction.normalized;
        shooter = shooterTransform;
        _launched = true;
        transform.forward = _dir;
        SetupTrail();
    }

    /// <summary>Estela luminosa para que se vea la trayectoria de la bala.</summary>
    private void SetupTrail()
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();

        if (trail.sharedMaterial == null)
        {
            if (_trailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                _trailMaterial = new Material(shader);
            }
            trail.sharedMaterial = _trailMaterial;
        }

        Color color = owner == Owner.Enemy
            ? new Color(1f, 0.25f, 0.12f, 0.95f)
            : new Color(1f, 0.85f, 0.25f, 0.95f);


        trail.time = owner == Owner.Enemy ? 0.28f : 0.2f;
        trail.startWidth = owner == Owner.Enemy ? 0.1f : 0.065f;
        trail.endWidth = 0f;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.minVertexDistance = 0.05f;
        trail.alignment = LineAlignment.View;
        trail.emitting = true;
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
        // La pared de armas no es un objetivo: cambia el arsenal y no puntua.
        if (owner == Owner.Player)
        {
            WeaponWallSwitch weaponWall = hit.collider.GetComponentInParent<WeaponWallSwitch>();
            if (weaponWall != null)
            {
                weaponWall.Activate();
                Destroy(gameObject);
                return;
            }
        }

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
