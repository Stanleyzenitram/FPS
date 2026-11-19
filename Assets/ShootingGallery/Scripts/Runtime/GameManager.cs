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
    public AudioSource sfxSource;

    private int _score, _lives, _targetsLeft, _botsLeft;
    private bool _bossAlive, _gameOver, _won, _outOfAmmoGrace;
    private float _ammoGrace = -1f, _damageFlash, _messageTimer, _hitMarker, _popupTimer, _alertTimer;
    private string _message = "", _popup = "";
    private Vector3 _popupWorld;
    private Weapon _weapon;

    private static readonly Color Accent = new Color(0.25f, 0.88f, 1f, 0.95f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.28f, 0.95f);
    private static readonly Color Danger = new Color(1f, 0.32f, 0.26f, 0.95f);
    private static readonly Color Panel = new Color(0.025f, 0.045f, 0.07f, 0.84f);

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
        if (WeaponRef != null)
            WeaponRef.SetupAmmo(startingAmmo, startingReserve);
    }

    private void Update()
    {
        if (_gameOver) return;

        _damageFlash = Mathf.Max(0f, _damageFlash - Time.deltaTime * 1.8f);
        _messageTimer -= Time.deltaTime;
        _alertTimer -= Time.deltaTime;
        _hitMarker -= Time.deltaTime;
        _popupTimer -= Time.deltaTime;

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

    /// <summary>Un soldado avisa cuando descubre al jugador al salir de la galeria.</summary>
    public void OnEnemiesDetected()
    {
        if (_gameOver || _alertTimer > 0f) return;
        _alertTimer = 3.2f;
        ShowMessage("ENEMIGOS DETECTADOS", 3.2f);
    }

    public void TakeDamage(int amount)
    {
        if (_gameOver) return;
        _lives -= amount;
        GameSession.Lives = _lives;
        _damageFlash = 1f;
        PlaySfx(hurtClip, 0.9f);
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
        if (target.type == Target.TargetType.Diana) PlaySfx(hitClip, 0.8f);

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
        PlaySfx(winClip, 1f);
        ShowMessage("¡NIVEL COMPLETADO!", 3f);
    }

    private void Lose(string reason)
    {
        if (_gameOver) return;
        _gameOver = true;
        _won = false;
        PlaySfx(loseClip, 1f);
        ShowMessage(reason, 3f);
    }

    private void ShowMessage(string text, float seconds)
    {
        _message = text;
        _messageTimer = seconds;
    }

    /// <summary>Reproduce un efecto en 2D para que siempre se escuche cerca del listener.</summary>
    public void PlaySfx(AudioClip clip, float volume = 0.8f)
    {
        if (clip == null) return;
        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
            return;
        }
        AudioSource.PlayClipAtPoint(clip, Vector3.zero, volume);
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

        DrawStatusPanel();
        DrawArsenalPanel(w, h);
        DrawCrosshair(w, h);

        if (_popupTimer > 0f && FirstPersonPlayer.Instance != null &&
            FirstPersonPlayer.Instance.viewCamera != null)
        {
            Vector3 screen = FirstPersonPlayer.Instance.viewCamera.WorldToScreenPoint(_popupWorld);
            if (screen.z > 0f)
            {
                GUI.Label(new Rect(screen.x - 60f, h - screen.y - 50f, 120f, 30f), _popup,
                    UIStyles.Label(22, TextAnchor.MiddleCenter, Gold, true));
            }
        }

        if (_messageTimer > 0f && !_gameOver) DrawBanner(w);

        if (_damageFlash > 0f)
            UIStyles.Fill(new Rect(0, 0, w, h), new Color(1f, 0f, 0f, _damageFlash * 0.22f));

        if (_gameOver) DrawEndPanel(w, h);
    }

    private static void DrawCrosshair(float w, float h)
    {
        bool aiming = FirstPersonPlayer.Instance != null && FirstPersonPlayer.Instance.IsAiming;
        bool hit = GameManager.Instance != null && GameManager.Instance._hitMarker > 0f;
        float cx = Mathf.Round(w * 0.5f), cy = Mathf.Round(h * 0.5f);

        Color outline = new Color(0f, 0f, 0f, 0.75f);
        Color inner = hit ? new Color(1f, 0.35f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.95f);

        // Al apuntar con la mira del arma, la cruz se reduce a un punto central.
        if (aiming)
        {
            UIStyles.Fill(new Rect(cx - 2f, cy - 2f, 4f, 4f), outline);
            UIStyles.Fill(new Rect(cx - 1f, cy - 1f, 2f, 2f), inner);
            return;
        }

        const float gap = 6f, len = 6f, thickness = 2f;
        DrawCrossLine(new Rect(cx - thickness * 0.5f, cy - gap - len, thickness, len), inner, outline);
        DrawCrossLine(new Rect(cx - thickness * 0.5f, cy + gap, thickness, len), inner, outline);
        DrawCrossLine(new Rect(cx - gap - len, cy - thickness * 0.5f, len, thickness), inner, outline);
        DrawCrossLine(new Rect(cx + gap, cy - thickness * 0.5f, len, thickness), inner, outline);

        UIStyles.Fill(new Rect(cx - 2f, cy - 2f, 4f, 4f), outline);
        UIStyles.Fill(new Rect(cx - 1f, cy - 1f, 2f, 2f), inner);
    }

    private static void DrawCrossLine(Rect rect, Color inner, Color outline)
    {
        UIStyles.Fill(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), outline);
        UIStyles.Fill(rect, inner);
    }

    private void DrawBanner(float w)
    {
        float alpha = Mathf.Clamp01(_messageTimer * 2.5f);
        float bw = Mathf.Min(540f, w * 0.82f), bh = 58f;
        float bx = (w - bw) * 0.5f, by = Screen.height * 0.15f;
        Color accent = _alertTimer > 0f ? Danger : Gold;

        UIStyles.Fill(new Rect(bx, by, bw, bh), new Color(0.02f, 0.04f, 0.07f, 0.88f * alpha));
        UIStyles.Fill(new Rect(bx, by, 5f, bh), new Color(accent.r, accent.g, accent.b, alpha));
        UIStyles.Fill(new Rect(bx + bw - 5f, by, 5f, bh), new Color(accent.r, accent.g, accent.b, alpha));

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(bx, by, bw, bh), _message,
            UIStyles.Label(28, TextAnchor.MiddleCenter, accent, true));
        GUI.color = previous;
    }

    private void DrawStatusPanel()
    {
        float x = 14f, y = 14f, pw = 352f, ph = 170f;
        int lives = Mathf.Max(0, _lives);

        UIStyles.Fill(new Rect(x, y, pw, ph), Panel);
        UIStyles.Fill(new Rect(x, y, 4f, ph), Accent);
        UIStyles.Fill(new Rect(x, y + ph - 3f, pw, 3f), new Color(Accent.r, Accent.g, Accent.b, 0.35f));

        GUIStyle label = UIStyles.Label(17, TextAnchor.MiddleLeft, Color.white, true);
        GUIStyle small = UIStyles.Label(13, TextAnchor.MiddleLeft, new Color(0.72f, 0.8f, 0.88f));
        GUI.Label(new Rect(x + 16f, y + 12f, pw - 30f, 24f),
            "NIVEL  //  " + levelDisplayName.ToUpperInvariant(), label);
        GUI.Label(new Rect(x + 16f, y + 38f, pw - 30f, 24f),
            "PUNTUACIÓN  " + _score, UIStyles.Label(16, TextAnchor.MiddleLeft, Gold, true));
        GUI.Label(new Rect(x + 16f, y + 64f, pw - 30f, 24f),
            "VIDA  " + lives + " / 3", UIStyles.Label(18, TextAnchor.MiddleLeft,
                lives <= 1 ? Danger : Accent, true));

        for (int i = 0; i < 3; i++)
        {
            Color c = i < lives ? (lives <= 1 ? Danger : Accent) : new Color(1f, 1f, 1f, 0.12f);
            UIStyles.Fill(new Rect(x + 16f + i * 82f, y + 92f, 74f, 7f), c);
        }

        string objetivo = hasBoss
            ? (_bossAlive ? "JEFE VIVO" : "JEFE ABATIDO")
            : (_targetsLeft + _botsLeft) + " objetivos restantes";
        GUI.Label(new Rect(x + 16f, y + 108f, pw - 30f, 24f), "OBJETIVO  //  " + objetivo, small);
        GUI.Label(new Rect(x + 16f, y + 138f, pw - 30f, 22f),
            "R recargar   1/2 arma   CLIC DERECHO mira", small);
    }

    private void DrawArsenalPanel(float w, float h)
    {
        float pw = 344f, ph = 176f;
        float x = w - pw - 14f, y = h - ph - 14f;

        UIStyles.Fill(new Rect(x, y, pw, ph), Panel);
        UIStyles.Fill(new Rect(x + pw - 4f, y, 4f, ph), Gold);
        UIStyles.Fill(new Rect(x, y, pw, 3f), new Color(Gold.r, Gold.g, Gold.b, 0.35f));

        GUI.Label(new Rect(x + 14f, y + 10f, pw - 28f, 24f), "ARSENAL  //  02 ARMAS",
            UIStyles.Label(16, TextAnchor.MiddleLeft, Gold, true));

        Weapon weapon = WeaponRef;
        if (weapon == null || weapon.slots == null || weapon.slots.Length == 0)
        {
            GUI.Label(new Rect(x + 14f, y + 42f, pw - 28f, 24f), "SIN ARMA",
                UIStyles.Label(15, TextAnchor.MiddleLeft, Color.white));
            return;
        }

        for (int i = 0; i < weapon.slots.Length; i++)
        {
            WeaponSlot slot = weapon.slots[i];
            if (slot == null) continue;
            bool active = i == weapon.CurrentIndex;
            float rowY = y + 38f + i * 26f;
            if (active)
                UIStyles.Fill(new Rect(x + 10f, rowY + 3f, 3f, 20f), Accent);
            string line = (i + 1) + ". " + (slot.displayName ?? "Arma") +
                          "    " + slot.magazine + "/" + slot.reserve;
            GUI.Label(new Rect(x + 22f, rowY, pw - 36f, 24f), line,
                UIStyles.Label(15, TextAnchor.MiddleLeft,
                    active ? Accent : new Color(0.65f, 0.7f, 0.76f), active));
        }

        GUI.Label(new Rect(x + 14f, y + 94f, pw - 28f, 40f),
            weapon.CurrentMagazine.ToString("00") + "  /  " + weapon.CurrentReserve,
            UIStyles.Label(32, TextAnchor.MiddleLeft, Color.white, true));
        GUI.Label(new Rect(x + 14f, y + 132f, pw - 28f, 20f),
            "BALAS RESTANTES  " + weapon.TotalAmmo,
            UIStyles.Label(13, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.9f)));

        string state = weapon.IsReloading
            ? "RECARGANDO..."
            : (FirstPersonPlayer.Instance != null && FirstPersonPlayer.Instance.IsAiming
                ? "MIRA ACTIVA" : "CARGADOR " + weapon.CurrentMagazine + "/" + weapon.CurrentMagazineSize);
        GUI.Label(new Rect(x + 14f, y + 150f, pw - 28f, 20f), state,
            UIStyles.Label(12, TextAnchor.MiddleLeft,
                weapon.IsReloading ? Gold : Accent, weapon.IsReloading));

        if (weapon.IsReloading)
            UIStyles.Fill(new Rect(x + 14f, y + 171f, (pw - 28f) * weapon.ReloadProgress, 3f), Gold);
    }

    private void DrawEndPanel(float w, float h)
    {
        UIStyles.Fill(new Rect(0, 0, w, h), new Color(0.01f, 0.02f, 0.04f, 0.86f));
        float pw = Mathf.Min(540f, w * 0.86f), ph = 390f;
        float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;
        Color result = _won ? Accent : Danger;

        UIStyles.Fill(new Rect(px, py, pw, ph), new Color(0.03f, 0.05f, 0.08f, 0.96f));
        UIStyles.Fill(new Rect(px, py, pw, 5f), result);
        UIStyles.Fill(new Rect(px, py + ph - 5f, pw, 5f), result);

        GUI.Label(new Rect(px, py + 26f, pw, 58f), _won ? "VICTORIA" : "DERROTA",
            UIStyles.Label(46, TextAnchor.MiddleCenter, result, true));
        GUI.Label(new Rect(px, py + 100f, pw, 34f), "PUNTUACIÓN FINAL  " + _score,
            UIStyles.Label(25, TextAnchor.MiddleCenter, Color.white, true));
        GUI.Label(new Rect(px, py + 138f, pw, 30f), "VIDAS RESTANTES  " + Mathf.Max(0, _lives),
            UIStyles.Label(19, TextAnchor.MiddleCenter, Color.white));
        GUI.Label(new Rect(px, py + 172f, pw, 30f), _message,
            UIStyles.Label(18, TextAnchor.MiddleCenter, Gold));

        GUIStyle button = UIStyles.Button(20);
        float bw = pw - 80f, bx = px + 40f;
        if (_won && !string.IsNullOrEmpty(nextSceneName))
        {
            if (GUI.Button(new Rect(bx, py + 216f, bw, 44f), "SIGUIENTE NIVEL", button)) NextLevel();
            if (GUI.Button(new Rect(bx, py + 268f, bw, 44f), "VOLVER A JUGAR", button)) RestartRun();
        }
        else
        {
            if (GUI.Button(new Rect(bx, py + 216f, bw, 44f), "VOLVER A JUGAR", button)) RestartRun();
        }
        if (GUI.Button(new Rect(bx, py + 320f, bw, 44f), "MENÚ PRINCIPAL", button)) GoToMenu();
    }
}
