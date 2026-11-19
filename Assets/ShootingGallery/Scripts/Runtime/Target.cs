using UnityEngine;

/// <summary>Cualquier cosa que se puede destruir a tiros: diana, bot o jefe.</summary>
public class Target : MonoBehaviour
{
    public enum TargetType { Diana, Bot, Boss }

    [Header("Que es este objetivo")]
    public TargetType type = TargetType.Diana;
    public int points = 100;
    public int hp = 1;
    public AudioClip hitClip;

    /// <summary>Recibe un impacto. Devuelve true si el objetivo ha muerto.</summary>
    public bool TakeHit(int damage)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return false;

        hp -= damage;
        if (hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, transform.position, 0.5f);

        if (hp > 0) return false;

        GameManager.Instance?.RegisterKill(this);
        Destroy(gameObject);
        return true;
    }
}
