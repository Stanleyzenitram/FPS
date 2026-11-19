using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Menu de entrada. Se muestra en la escena "00 Menu Principal".</summary>
public class MenuController : MonoBehaviour
{
    private static readonly Color Accent = new Color(0.25f, 0.88f, 1f, 0.95f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.28f, 0.95f);

    /// <summary>Fuente opcional detectada en Assets/ShootingGallery/Fonts al generar.</summary>
    public Font uiFont;

    private GUIStyle Label(int size, TextAnchor anchor, Color color, bool bold = false)
    {
        return UIStyles.Label(size, anchor, color, bold, uiFont);
    }

    private GUIStyle Button(int size)
    {
        return UIStyles.Button(size, uiFont);
    }

    private void Start()
    {
        AudioListener.volume = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        float w = Screen.width, h = Screen.height;

        UIStyles.Fill(new Rect(0, 0, w, h), new Color(0.015f, 0.025f, 0.045f, 0.94f));
        UIStyles.Fill(new Rect(0, h * 0.07f, w, 2f), new Color(Gold.r, Gold.g, Gold.b, 0.5f));
        UIStyles.Fill(new Rect(0, h * 0.79f, w, 2f), new Color(Accent.r, Accent.g, Accent.b, 0.35f));

        int titleSize = Mathf.RoundToInt(Mathf.Min(w, h) * 0.085f);
        GUI.Label(new Rect(0, h * 0.12f, w, 84f), "SHOOTING GALLERY",
            Label(titleSize, TextAnchor.MiddleCenter, Gold, true));
        GUI.Label(new Rect(0, h * 0.24f, w, 40f), "FPS MILITAR  //  3 NIVELES  //  ARSENAL TÁCTICO",
            Label(21, TextAnchor.MiddleCenter, Accent, true));

        float bw = Mathf.Min(340f, w * 0.7f);
        float bx = (w - bw) * 0.5f;
        GUIStyle button = Button(22);

        if (GUI.Button(new Rect(bx, h * 0.35f, bw, 48f), "EMPEZAR PARTIDA", button))
        {
            GameSession.ResetRun();
            SceneManager.LoadScene("01 Galeria de disparos");
        }
        if (GUI.Button(new Rect(bx, h * 0.44f, bw, 48f), "SALIR", button))
        {
            Application.Quit();
        }

        float pw = Mathf.Min(760f, w * 0.88f), px = (w - pw) * 0.5f;
        float py = h * 0.57f, ph = 150f;
        UIStyles.Fill(new Rect(px, py, pw, ph), new Color(0.03f, 0.055f, 0.085f, 0.9f));
        UIStyles.Fill(new Rect(px, py, 4f, ph), Accent);

        GUIStyle small = Label(16, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.9f));
        GUI.Label(new Rect(px + 20f, py + 12f, pw - 40f, 26f),
            "FLECHAS / WASD  mover      RATÓN  apuntar      CLIC IZQ.  disparar", small);
        GUI.Label(new Rect(px + 20f, py + 42f, pw - 40f, 26f),
            "CLIC DER.  mira ADS      R  recargar      1 / 2  cambiar de arma", small);
        GUI.Label(new Rect(px + 20f, py + 72f, pw - 40f, 26f),
            "ESPACIO  saltar      ESC  soltar el cursor", small);
        GUI.Label(new Rect(px + 20f, py + 104f, pw - 40f, 26f),
            "Nivel 1: galería + campo     Nivel 2: bots     Nivel 3: jefe final",
            Label(15, TextAnchor.MiddleLeft, new Color(0.6f, 0.68f, 0.76f)));
    }
}
