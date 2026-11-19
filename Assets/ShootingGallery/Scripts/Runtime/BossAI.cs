using UnityEngine;

/// <summary>Jefe final: mismo comportamiento de soldado, pero mas lento,
/// con mas vida (en Target) y un alcance de disparo mayor.</summary>
public class BossAI : BotAI
{
    protected override void Start()
    {
        speed = 2f;
        stopDistance = 4f;
        contactDamage = 1;
        // El templo final mide 108 m de fondo: el jefe debe abrir fuego
        // mucho antes de llegar cuerpo a cuerpo.
        fireRange = 90f;
        fireInterval = 1.35f;
        fireSpread = 2f;
        bulletSpeed = 38f;
        requiresExitDetection = false;

        base.Start();
    }
}
