using UnityEngine;

/// <summary>Datos que sobreviven entre niveles (puntuacion y vidas).</summary>
public static class GameSession
{
    /// <summary>Vidas con las que empieza cada partida y cada nivel.</summary>
    public const int MaxLives = 5;

    public static int Score;
    public static int Lives = MaxLives;

    public static void ResetRun()
    {
        Score = 0;
        Lives = MaxLives;
    }
}
