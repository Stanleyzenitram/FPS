using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Marcador, vidas, arsenal, condiciones de victoria/derrota y HUD.</summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static bool IsFinished => Instance != null && Instance.IsGameOver;

    [Header("Configuracion del nivel (la rellena el generador)")]
    public string levelDisplayName = "Nivel";
    public string nextSceneName = "";
    public int startingAmmo = 14;
    public int startingReserve = 42;
    public int totalTargets = 0;
    public int totalBots = 0;
    public bool hasBoss = false;

    [Header("Sonidos")]
    public AudioClip hitClip;
    public AudioClip hurtClip;
    public AudioClip winClip;
    public AudioClip loseClip;

    private int _score, _lives, _targetsLeft, _botsLeft;
    private bool _bossAlive, _gameOver, _won, _outOfAmmoGrace;
    private float _ammoGrace = -1f, _damageFlash, _messageTimer, _hitMarker, _popupTimer;
    private string _message = "", _popup = "";
    private Vector3 _popupWorld;
    private Weapon _weapon;

    public bool IsGameOver => _gameOver;
    public bool CanShoot => !_gameOver && WeaponRef != null && WeaponRef.HasAmmo;

    private Weapon WeaponRef
    {
        get
        {
            if (_weapon == null) _weapon = FindAnyObjectByType<Weapon>();
            return _weapon;
        }
    }

    private void Awake()
    {
        Instance = this;
        _score = GameSession.Score;
        _lives = GameSession.Lives;
        _targetsLeft = totalTargets;
        _botsLeft = totalBots;
        _bossAlive = hasBoss;
    }

    private void Start()
    {
        // El arma recibe la municion de este nivel una vez que todos los Awake terminaron.
        if (WeaponRef != null)
            WeaponRef.SetupAmmo(startingAmmo, startingReserve);
    }

    private void Update()
    {
        if (_gameOver) return;

        _damageFlash = Mathf.Max(0f, _damageFlash - Time.deltaTime * 1.8f);
        _messageTimer -= Time.deltaTime;
        _hitMarker -= Time.deltaTime;
        _popupTimer -= Time.deltaTime;

        // Margen para que la ultima bala llegue a su destino antes de declarar derrota.
        if (_ammoGrace >= 0f)
        {
            _ammoGrace -= Time.deltaTime;
            if (_ammoGrace <= 0f && Projectile.ActiveCount == 0 && !_won)
                Lose("Sin munición");
        }
    }

    /// <summary>El arma avisa cuando dispara, recarga o cambia de arma.</summary>
    public void NotifyAmmoChanged()
    {
        if (_gameOver || WeaponRef == null) return;

        if (WeaponRef.TotalAmmo <= 0)
        {
            if (!_outOfAmmoGrace)
            {
                _outOfAmmoGrace = true;
                _ammoGrace = 2.5f;
                ShowMessage("Sin munición", 2.5f);
            }
        }
        else
        {
            _outOfAmmoGrace = false;
            _ammoGrace = -1f;
        }
    }

    public void TakeDamage(int amount)
    {
        if (_gameOver) return;
        _lives -= amount;
        GameSession.Lives = _lives;
        _damageFlash = 1f;
        if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, Vector3.zero, 0.8f);
        if (_lives <= 0) Lose("Te han eliminado");
    }

    public void RegisterKill(Target target)
    {
        if (_gameOver) return;

        _score += target.points;
        GameSession.Score = _score;
        _hitMarker = 0.18f;
        _popup = "+" + target.points;
        _popupWorld = target.transform.position;
        _popupTimer = 0.9f;
        if (target.type == Target.TargetType.Diana && hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, target.transform.position, 0.5f);

        switch (target.type)
        {
            case Target.TargetType.Diana: _targetsLeft--; break;
            case Target.TargetType.Bot: _botsLeft--; break;
            case Target.TargetType.Boss: _bossAlive = false; break;
        }

        if (_targetsLeft <= 0 && _botsLeft <= 0 && !_bossAlive) Win();
    }

    private void Win()
    {
        if (_gameOver) return;
        _gameOver = true;
        _won = true;
        _score += _lives * 100;
        GameSession.Score = _score;
        if (winClip != null) AudioSource.PlayClipAtPoint(winClip, Vector3.zero, 0.8f);
        ShowMessage("¡NIVEL COMPLETADO!", 3f);
    }

    private void Lose(string reason)
    {
        if (_gameOver) return;
        _gameOver = true;
        _won = false;
        if (loseClip != null) AudioSource.PlayClipAtPoint(loseClip, Vector3.zero, 0.8f);
        ShowMessage(reason, 3f);
    }

    private void ShowMessage(string text, float seconds)
    {
        _message = text;
        _messageTimer = seconds;
    }

    public void NextLevel()
    {
        if (string.IsNullOrEmpty(nextSceneName)) { RestartRun(); return; }
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneName);
    }

    public void RestartRun()
    {
        GameSession.ResetRun();
        Time.timeScale = 1f;
        SceneManager.LoadScene("01 Galeria de disparos");
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("00 Menu Principal");
    }

    private void OnGUI()
    {
        float w = Screen.width, h = Screen.height;
        Color previous = GUI.color;

        DrawStatusPanel();
        DrawArsenalPanel(w, h);

        // Punto de mira.
        float cx = w * 0.5f, cy = h * 0.5f;
        GUI.color = _hitMarker > 0f ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(cx - 1f, cy - 9f, 2f, 18f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - 9f, cy - 1f, 18f, 2f), Texture2D.whiteTexture);
        GUI.color = previous;

        // "+puntos" flotante sobre el objetivo.
        if (_popupTimer > 0f && FirstPersonPlayer.Instance != null &&
            FirstPersonPlayer.Instance.viewCamera != null)
        {
            Vector3 screen = FirstPersonPlayer.Instance.viewCamera.WorldToScreenPoint(_popupWorld);
            if (screen.z > 0f)
            {
                GUI.Label(new Rect(screen.x - 60f, h - screen.y - 50f, 120f, 30f), _popup,
                    UIStyles.Label(22, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.25f), true));
            }
        }

        if (_messageTimer > 0f && !_gameOver)
            GUI.Label(new Rect(0, h * 0.16f, w, 50), _message,
                UIStyles.Label(30, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f), true));

        if (_damageFlash > 0f)
        {
            GUI.color = new Color(1f, 0f, 0f, _damageFlash * 0.28f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        if (_gameOver) DrawEndPanel(w, h);
    }

    private void DrawStatusPanel()
    {
        GUIStyle hud = UIStyles.Label(18, TextAnchor.MiddleLeft, Color.white, true);
        GUIStyle hearts = UIStyles.Label(19, TextAnchor.MiddleLeft, new Color(1f, 0.42f, 0.42f), true);
        GUI.Box(new Rect(14, 14, 336, 176), GUIContent.none);
        GUI.Label(new Rect(26, 20, 310, 26), "NIVEL: " + levelDisplayName, hud);
        GUI.Label(new Rect(26, 46, 310, 26), "PUNTUACIÓN: " + _score, hud);
        GUI.Label(new Rect(26, 72, 310, 26), "VIDA: " + Mathf.Max(0, _lives) + " / 3", hearts);
        Color saved = GUI.color;
        GUI.color = new Color(1f, 0.35f, 0.35f);
        GUI.DrawTexture(new Rect(26, 98, 250f * Mathf.Clamp01(_lives / 3f), 6f), Texture2D.whiteTexture);
        GUI.color = saved;
        string objetivo = "OBJETIVOS: ";
        if (hasBoss) objetivo += _bossAlive ? "JEFE VIVO" : "JEFE ABATIDO";
        else objetivo += (_targetsLeft + _botsLeft) + " restantes";
        GUI.Label(new Rect(26, 124, 310, 26), objetivo, hud);
        GUI.Label(new Rect(26, 150, 310, 24), "R: recargar   1/2: cambiar arma",
            UIStyles.Label(13, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f)));
    }

    private void DrawArsenalPanel(float w, float h)
    {
        float panelW = 330f, panelH = 150f;
        float x = w - panelW - 14f, y = h - panelH - 14f;
        GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none);

        GUI.Label(new Rect(x + 12, y + 8, panelW - 24, 24), "ARSENAL",
            UIStyles.Label(17, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.3f), true));

        Weapon weapon = WeaponRef;
        if (weapon == null || weapon.slots == null || weapon.slots.Length == 0)
        {
            GUI.Label(new Rect(x + 12, y + 36, panelW - 24, 24), "Sin arma",
                UIStyles.Label(15, TextAnchor.MiddleLeft, Color.white));
            return;
        }

        for (int i = 0; i < weapon.slots.Length; i++)
        {
            WeaponSlot slot = weapon.slots[i];
            if (slot == null) continue;
            bool active = i == weapon.CurrentIndex;
            string line = (i + 1) + ". " + (slot.displayName ?? "Arma") +
                          "   " + slot.magazine + "/" + slot.reserve;
            GUI.Label(new Rect(x + 12, y + 34 + i * 24, panelW - 24, 24), line,
                UIStyles.Label(15, TextAnchor.MiddleLeft,
                    active ? new Color(0.55f, 1f, 0.65f) : new Color(0.68f, 0.72f, 0.78f), active));
        }

        GUI.Label(new Rect(x + 12, y + 86, panelW - 24, 24),
            "BALAS RESTANTES: " + weapon.TotalAmmo,
            UIStyles.Label(16, TextAnchor.MiddleLeft, Color.white, true));
        GUI.Label(new Rect(x + 12, y + 110, panelW - 24, 22),
            weapon.IsReloading ? "RECARGANDO..." : "CARGADOR: " + weapon.CurrentMagazine + "/" + weapon.CurrentMagazineSize,
            UIStyles.Label(14, TextAnchor.MiddleLeft,
                weapon.IsReloading ? new Color(1f, 0.7f, 0.25f) : new Color(0.8f, 0.85f, 0.9f), weapon.IsReloading));

        if (weapon.IsReloading)
        {
            Color saved = GUI.color;
            GUI.color = new Color(1f, 0.7f, 0.2f);
            GUI.DrawTexture(new Rect(x + 12, y + 136, (panelW - 24) * weapon.ReloadProgress, 4f),
                Texture2D.whiteTexture);
            GUI.color = saved;
        }
    }

    private void DrawEndPanel(float w, float h)
    {
        GUI.Box(new Rect(0, 0, w, h), GUIContent.none);
        float pw = Mathf.Min(520f, w * 0.85f), ph = 380f;
        float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;
        GUI.Box(new Rect(px, py, pw, ph), GUIContent.none);

        Color color = _won ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
        GUI.Label(new Rect(px, py + 24, pw, 56), _won ? "VICTORIA" : "DERROTA",
            UIStyles.Label(44, TextAnchor.MiddleCenter, color, true));
        GUI.Label(new Rect(px, py + 96, pw, 34), "PUNTUACIÓN FINAL: " + _score,
            UIStyles.Label(26, TextAnchor.MiddleCenter, Color.white, true));
        GUI.Label(new Rect(px, py + 134, pw, 30), "VIDAS RESTANTES: " + Mathf.Max(0, _lives),
            UIStyles.Label(20, TextAnchor.MiddleCenter, Color.white));
        GUI.Label(new Rect(px, py + 168, pw, 30), _message,
            UIStyles.Label(18, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f)));

        GUIStyle button = UIStyles.Button(20);
        float bw = pw - 80f, bx = px + 40f;
        if (_won && !string.IsNullOrEmpty(nextSceneName))
        {
            if (GUI.Button(new Rect(bx, py + 212, bw, 44), "SIGUIENTE NIVEL", button)) NextLevel();
            if (GUI.Button(new Rect(bx, py + 264, bw, 44), "VOLVER A JUGAR", button)) RestartRun();
        }
        else
        {
            if (GUI.Button(new Rect(bx, py + 212, bw, 44), "VOLVER A JUGAR", button)) RestartRun();
        }
        if (GUI.Button(new Rect(bx, py + 316, bw, 44), "MENÚ PRINCIPAL", button)) GoToMenu();
    }
}
