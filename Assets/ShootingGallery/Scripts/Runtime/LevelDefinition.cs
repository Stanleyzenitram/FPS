using UnityEngine;

/// <summary>Configuracion de cada nivel. Se crea una instancia por escena.</summary>
[CreateAssetMenu(menuName = "Shooting Gallery/Level Definition", fileName = "Level")]
public class LevelDefinition : ScriptableObject
{
    public string displayName = "Nivel";
    public string sceneName = "";
    public string nextSceneName = "";
    public int startingAmmo = 14;
    public int startingReserve = 42;
    public int totalTargets = 0;
    public int totalBots = 0;
    public bool hasBoss = false;
}
