using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Menu de entrada. Se muestra en la escena "00 Menu Principal".</summary>
public class MenuController : MonoBehaviour
{
    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        float w = Screen.width, h = Screen.height;
        GUIStyle title = UIStyles.Label(Mathf.RoundToInt(Mathf.Min(w, h) * 0.09f),
            TextAnchor.MiddleCenter, new Color(0.95f, 0.78f, 0.25f), true);
        GUIStyle subtitle = UIStyles.Label(22, TextAnchor.MiddleCenter, Color.white);
        GUIStyle small = UIStyles.Label(16, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.85f));
        GUIStyle button = UIStyles.Button(22);

        GUI.Label(new Rect(0, h * 0.12f, w, 80), "SHOOTING GALLERY", title);
        GUI.Label(new Rect(0, h * 0.24f, w, 40), "FPS MILITAR - 3 NIVELES", subtitle);

        float bw = Mathf.Min(320f, w * 0.6f);
        float bx = (w - bw) * 0.5f;

        if (GUI.Button(new Rect(bx, h * 0.38f, bw, 46), "EMPEZAR PARTIDA", button))
        {
            GameSession.ResetRun();
            SceneManager.LoadScene("01 Galeria de disparos");
        }

        if (GUI.Button(new Rect(bx, h * 0.48f, bw, 46), "SALIR", button))
        {
            Application.Quit();
        }

        GUI.Label(new Rect(0, h * 0.62f, w, 28),
            "Flechas / WASD: mover     Ratón: apuntar     Clic izquierdo: disparar", small);
        GUI.Label(new Rect(0, h * 0.67f, w, 28),
            "Nivel 1: galería de tiro   Nivel 2: bots   Nivel 3: jefe final", small);
        GUI.Label(new Rect(0, h * 0.72f, w, 28),
            "Al agotar la munición o perder las 3 vidas, la partida termina.", small);
    }
}
