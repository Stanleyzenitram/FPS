using UnityEngine;

/// <summary>Datos que sobreviven entre niveles (puntuacion y vidas).</summary>
public static class GameSession
{
    public static int Score;
    public static int Lives = 3;

    public static void ResetRun()
    {
        Score = 0;
        Lives = 3;
    }
}
