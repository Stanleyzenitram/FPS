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
        fireRange = 42f;
        fireInterval = 1.35f;
        fireSpread = 2f;
        bulletSpeed = 38f;
        requiresExitDetection = false;

        base.Start();
    }
}
