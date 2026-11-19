using System.Collections.Generic;
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
    /// <summary>Si es false, las dianas son bonus y el nivel termina al eliminar enemigos.</summary>
    public bool requireTargetsForVictory = false;

    [Header("Sonidos")]
    public AudioClip hitClip;
    public AudioClip hurtClip;
    public AudioClip winClip;
    public AudioClip loseClip;
    public AudioSource sfxSource;

    [Header("HUD opcional (lo detecta el generador)")]
    public Font hudFont;
    public Texture2D healthIcon;
    public Texture2D ammoIcon;
    public Texture2D hitmarkerIcon;

    private sealed class KillFeedEntry
    {
        public string text;
        public Color color;
        public float time;
    }

    private readonly List<KillFeedEntry> _killFeed = new List<KillFeedEntry>();

    private int _score, _lives, _targetsLeft, _botsLeft;
    private bool _bossAlive, _gameOver, _won, _outOfAmmoGrace;
    private float _ammoGrace = -1f, _damageFlash, _messageTimer, _hitMarker, _popupTimer, _alertTimer;
    private string _message = "", _popup = "";
    private Vector3 _popupWorld;
    private Weapon _weapon;

    private static readonly Color Accent = new Color(0.25f, 0.88f, 1f, 0.95f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.28f, 0.95f);
    private static readonly Color Danger = new Color(1f, 0.32f, 0.26f, 0.95f);
    private static readonly Color Panel = new Color(0.015f, 0.03f, 0.05f, 0.84f);
    private static readonly Color White = new Color(0.96f, 0.98f, 1f, 0.98f);
    private static readonly Color Muted = new Color(0.62f, 0.71f, 0.8f, 0.95f);
    private static readonly Color Dim = new Color(0.24f, 0.31f, 0.38f, 0.9f);

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
        UpdateKillFeed();
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

    /// <summary>La pared de armas avisa de que el jugador ha cambiado de arsenal.</summary>
    public void NotifyWeaponSwitch(string text, float seconds)
    {
        if (_gameOver) return;
        ShowMessage(text, seconds);
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

        string feedText = target.type == Target.TargetType.Boss ? "JEFE ABATIDO" :
                          target.type == Target.TargetType.Bot ? "SOLDADO ELIMINADO" :
                          "DIANA DESTRUIDA";
        AddKillFeed(feedText + "  +" + target.points,
            target.type == Target.TargetType.Diana ? Gold : Accent);

        switch (target.type)
        {
            case Target.TargetType.Diana: _targetsLeft--; break;
            case Target.TargetType.Bot: _botsLeft--; break;
            case Target.TargetType.Boss: _bossAlive = false; break;
        }

        bool objetivos = _botsLeft <= 0 && (!hasBoss || !_bossAlive) &&
                         (!requireTargetsForVictory || _targetsLeft <= 0);
        if (objetivos) Win();
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

    private void AddKillFeed(string text, Color color)
    {
        _killFeed.Insert(0, new KillFeedEntry { text = text, color = color, time = 3.4f });
        if (_killFeed.Count > 4) _killFeed.RemoveAt(_killFeed.Count - 1);
    }

    private void UpdateKillFeed()
    {
        for (int i = _killFeed.Count - 1; i >= 0; i--)
        {
            KillFeedEntry entry = _killFeed[i];
            entry.time -= Time.deltaTime;
            if (entry.time <= 0f) _killFeed.RemoveAt(i);
            else _killFeed[i] = entry;
        }
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

        // Cada nivel empieza con la barra de vida llena; la puntuacion se conserva.
        GameSession.Lives = GameSession.MaxLives;
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

        DrawCompass(w);
        DrawStatusPanel();
        DrawKillFeed(w);
        DrawHealthPanel(h);
        DrawAmmoPanel(w, h);
        DrawCrosshair(w, h);

        if (_popupTimer > 0f && FirstPersonPlayer.Instance != null &&
            FirstPersonPlayer.Instance.viewCamera != null)
        {
            Vector3 screen = FirstPersonPlayer.Instance.viewCamera.WorldToScreenPoint(_popupWorld);
            if (screen.z > 0f)
            {
                GUI.Label(new Rect(screen.x - S(60f), h - screen.y - S(50f), S(120f), S(30f)), _popup,
                    HudLabel(22, TextAnchor.MiddleCenter, Gold, true));
            }
        }

        if (_messageTimer > 0f && !_gameOver) DrawBanner(w);

        if (_damageFlash > 0f)
            UIStyles.Fill(new Rect(0, 0, w, h), new Color(1f, 0f, 0f, _damageFlash * 0.22f));

        if (_gameOver) DrawEndPanel(w, h);
    }

    private float UiScale => Mathf.Clamp(
        Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.72f, 1.25f);

    private float S(float value) => value * UiScale;

    private GUIStyle HudLabel(int size, TextAnchor anchor, Color color, bool bold = false)
    {
        return UIStyles.Label(Mathf.RoundToInt(S(size)), anchor, color, bold, hudFont);
    }

    /// <summary>Marco angular típico de una HUD militar, hecho con rectangulos.</summary>
    private void DrawHudFrame(Rect rect, Color accent)
    {
        UIStyles.Fill(rect, Panel);
        float t = S(2f), corner = S(18f);
        Color soft = new Color(accent.r, accent.g, accent.b, 0.25f);
        UIStyles.Fill(new Rect(rect.x, rect.y, rect.width, t), soft);
        UIStyles.Fill(new Rect(rect.x, rect.yMax - t, rect.width, t), soft);
        UIStyles.Fill(new Rect(rect.x, rect.y, corner, t), accent);
        UIStyles.Fill(new Rect(rect.xMax - corner, rect.y, corner, t), accent);
        UIStyles.Fill(new Rect(rect.x, rect.yMax - t, corner, t), accent);
        UIStyles.Fill(new Rect(rect.xMax - corner, rect.yMax - t, corner, t), accent);
        UIStyles.Fill(new Rect(rect.x, rect.y, S(2f), rect.height),
            new Color(accent.r, accent.g, accent.b, 0.42f));
    }

    private static string Cardinal(float degrees)
    {
        string[] names = { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };
        int index = Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) / 45f) % 8;
        return names[index];
    }

    private static float NormalizeAngle(float degrees) => Mathf.Repeat(degrees, 360f);

    private void DrawCrosshair(float w, float h)
    {
        bool aiming = FirstPersonPlayer.Instance != null && FirstPersonPlayer.Instance.IsAiming;
        bool hit = _hitMarker > 0f;
        float cx = Mathf.Round(w * 0.5f), cy = Mathf.Round(h * 0.5f);

        Color outline = new Color(0f, 0f, 0f, 0.75f);
        Color inner = hit ? new Color(1f, 0.35f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.95f);
        float dot = S(2f), dotOutline = S(4f);

        // Al apuntar con la mira del arma, la cruz se reduce a un punto central.
        if (aiming)
        {
            UIStyles.Fill(new Rect(cx - dotOutline * 0.5f, cy - dotOutline * 0.5f,
                dotOutline, dotOutline), outline);
            UIStyles.Fill(new Rect(cx - dot * 0.5f, cy - dot * 0.5f, dot, dot), inner);
        }
        else
        {
            float gap = S(6f), len = S(6f), thickness = S(2f);
            DrawCrossLine(new Rect(cx - thickness * 0.5f, cy - gap - len, thickness, len), inner, outline);
            DrawCrossLine(new Rect(cx - thickness * 0.5f, cy + gap, thickness, len), inner, outline);
            DrawCrossLine(new Rect(cx - gap - len, cy - thickness * 0.5f, len, thickness), inner, outline);
            DrawCrossLine(new Rect(cx + gap, cy - thickness * 0.5f, len, thickness), inner, outline);

            UIStyles.Fill(new Rect(cx - dotOutline * 0.5f, cy - dotOutline * 0.5f,
                dotOutline, dotOutline), outline);
            UIStyles.Fill(new Rect(cx - dot * 0.5f, cy - dot * 0.5f, dot, dot), inner);
        }

        if (hit) DrawHitMarker(cx, cy, aiming ? 0.8f : 1f, inner);
    }

    private static void DrawCrossLine(Rect rect, Color inner, Color outline)
    {
        UIStyles.Fill(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), outline);
        UIStyles.Fill(rect, inner);
    }

    /// <summary>Confirmacion de impacto: X táctica que no sustituye a la mira clásica.</summary>
    private void DrawHitMarker(float cx, float cy, float factor, Color color)
    {
        if (hitmarkerIcon != null)
        {
            float size = S(44f) * factor;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size),
                hitmarkerIcon, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
            return;
        }

        Color outline = new Color(0f, 0f, 0f, 0.85f);
        float pixel = Mathf.Max(2f, S(3f) * factor);
        int[] dx = { -1, 1, -1, 1 };
        int[] dy = { -1, -1, 1, 1 };
        for (int arm = 0; arm < 4; arm++)
        {
            for (int i = 0; i < 4; i++)
            {
                float offset = (S(11f) + i * S(4f)) * factor;
                float x = cx + dx[arm] * offset;
                float y = cy + dy[arm] * offset;
                UIStyles.Fill(new Rect(x - pixel, y - pixel, pixel * 2f, pixel * 2f), outline);
                UIStyles.Fill(new Rect(x - pixel * 0.5f, y - pixel * 0.5f, pixel, pixel), color);
            }
        }
    }

    private void DrawBanner(float w)
    {
        float alpha = Mathf.Clamp01(_messageTimer * 2.5f);
        float bw = Mathf.Min(S(560f), w * 0.82f), bh = S(58f);
        float bx = (w - bw) * 0.5f, by = Screen.height * 0.15f;
        Color accent = _alertTimer > 0f ? Danger : Gold;
        float edge = S(5f);

        UIStyles.Fill(new Rect(bx, by, bw, bh), new Color(0.02f, 0.04f, 0.07f, 0.88f * alpha));
        UIStyles.Fill(new Rect(bx, by, edge, bh), new Color(accent.r, accent.g, accent.b, alpha));
        UIStyles.Fill(new Rect(bx + bw - edge, by, edge, bh), new Color(accent.r, accent.g, accent.b, alpha));
        UIStyles.Fill(new Rect(bx + edge, by, S(26f), S(3f)), new Color(accent.r, accent.g, accent.b, alpha));

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(bx, by, bw, bh), _message,
            HudLabel(25, TextAnchor.MiddleCenter, accent, true));
        GUI.color = previous;
    }

    private void DrawStatusPanel()
    {
        float pw = S(360f), ph = S(104f), x = S(16f), y = S(16f);
        DrawHudFrame(new Rect(x, y, pw, ph), Accent);

        GUI.Label(new Rect(x + S(14f), y + S(8f), pw - S(28f), S(18f)),
            "OPERADOR // " + (levelDisplayName ?? "Nivel").ToUpperInvariant(),
            HudLabel(11, TextAnchor.MiddleLeft, Muted, true));
        GUI.Label(new Rect(x + S(14f), y + S(28f), S(190f), S(40f)),
            _score.ToString("000000"), HudLabel(26, TextAnchor.MiddleLeft, Gold, true));
        GUI.Label(new Rect(x + pw - S(112f), y + S(37f), S(96f), S(18f)),
            "PUNTOS", HudLabel(10, TextAnchor.MiddleRight, Muted));

        UIStyles.Fill(new Rect(x + S(14f), y + S(70f), pw - S(28f), S(1f)),
            new Color(1f, 1f, 1f, 0.10f));
        GUI.Label(new Rect(x + S(14f), y + S(76f), pw - S(28f), S(20f)),
            "OBJETIVO  //  " + ObjectiveText(), HudLabel(13, TextAnchor.MiddleLeft, Accent, true));
    }

    private string ObjectiveText()
    {
        int enemies = Mathf.Max(0, _botsLeft) + (hasBoss && _bossAlive ? 1 : 0);
        string text = enemies.ToString("00") + " ENEMIGOS";
        if (_targetsLeft > 0) text += "  /  " + _targetsLeft.ToString("00") + " DIANAS BONO";
        return text;
    }

    private void DrawCompass(float w)
    {
        float cw = Mathf.Min(S(470f), Mathf.Max(S(240f), w - S(760f)));
        float ch = S(54f), x = (w - cw) * 0.5f, y = S(16f);
        DrawHudFrame(new Rect(x, y, cw, ch), new Color(Accent.r, Accent.g, Accent.b, 0.7f));

        FirstPersonPlayer player = FirstPersonPlayer.Instance;
        float heading = player != null ? NormalizeAngle(player.transform.eulerAngles.y) : 0f;
        float center = x + cw * 0.5f;
        float lineY = y + ch - S(15f);
        float pixelsPerDegree = (cw * 0.5f - S(14f)) / 90f;

        for (int delta = -90; delta <= 90; delta += 15)
        {
            float tickX = center + delta * pixelsPerDegree;
            float angle = NormalizeAngle(heading + delta);
            bool major = delta % 45 == 0;
            float tickH = S(major ? 10f : 5f);
            UIStyles.Fill(new Rect(tickX - S(major ? 1f : 0.5f), lineY - tickH,
                S(major ? 2f : 1f), tickH), major ? White : Dim);
            if (major)
            {
                GUI.Label(new Rect(tickX - S(18f), lineY - S(29f), S(36f), S(15f)),
                    Cardinal(angle), HudLabel(11, TextAnchor.MiddleCenter, Muted, true));
            }
        }

        UIStyles.Fill(new Rect(center - S(1f), lineY - S(14f), S(2f), S(14f)), Gold);
        UIStyles.Fill(new Rect(center - S(5f), y + ch - S(4f), S(10f), S(3f)), Gold);
        GUI.Label(new Rect(center - S(52f), y + S(4f), S(104f), S(26f)),
            Mathf.RoundToInt(heading).ToString("000") + "°  " + Cardinal(heading),
            HudLabel(16, TextAnchor.MiddleCenter, White, true));
    }

    private void DrawKillFeed(float w)
    {
        float pw = S(322f), row = S(27f), ph = S(22f) + row * 3f;
        float x = w - pw - S(16f), y = S(16f);
        DrawHudFrame(new Rect(x, y, pw, ph), Danger);

        GUI.Label(new Rect(x + S(12f), y + S(5f), pw - S(24f), S(16f)),
            "REGISTRO DE BAJAS", HudLabel(10, TextAnchor.MiddleLeft, Muted, true));

        for (int i = 0; i < 3; i++)
        {
            float ry = y + S(22f) + i * row;
            UIStyles.Fill(new Rect(x + S(12f), ry + S(4f), S(34f), S(2f)),
                new Color(1f, 1f, 1f, 0.09f));
            if (i >= _killFeed.Count) continue;

            KillFeedEntry entry = _killFeed[i];
            float alpha = Mathf.Clamp01(entry.time * 1.8f);
            UIStyles.Fill(new Rect(x + S(12f), ry + S(3f), S(3f), row - S(9f)),
                new Color(entry.color.r, entry.color.g, entry.color.b, alpha));

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(x + S(22f), ry, pw - S(34f), row - S(4f)),
                entry.text, HudLabel(12, TextAnchor.MiddleLeft, entry.color, true));
            GUI.color = previous;
        }
    }

    private void DrawHealthPanel(float h)
    {
        int lives = Mathf.Max(0, _lives);
        Color accent = lives <= 1 ? Danger : Accent;
        float pw = S(330f), ph = S(124f), x = S(18f), y = h - ph - S(18f);
        DrawHudFrame(new Rect(x, y, pw, ph), accent);

        Rect icon = new Rect(x + S(14f), y + S(38f), S(46f), S(46f));
        if (healthIcon != null)
            GUI.DrawTexture(icon, healthIcon, ScaleMode.ScaleToFit, true);
        else
            DrawPlusIcon(icon, new Color(accent.r, accent.g, accent.b, 0.9f));

        GUI.Label(new Rect(x + S(74f), y + S(15f), S(160f), S(18f)),
            "ESTADO / VIDA", HudLabel(11, TextAnchor.MiddleLeft, Muted, true));
        GUI.Label(new Rect(x + S(74f), y + S(32f), S(120f), S(46f)),
            lives.ToString("00"), HudLabel(38, TextAnchor.MiddleLeft, accent, true));
        GUI.Label(new Rect(x + S(154f), y + S(52f), S(56f), S(22f)),
            "/ 05", HudLabel(14, TextAnchor.MiddleLeft, Muted));

        float barX = x + S(74f), barY = y + S(86f), segW = S(42f), gap = S(5f), barH = S(9f);
        for (int i = 0; i < GameSession.MaxLives; i++)
        {
            Color c = i < lives
                ? new Color(accent.r, accent.g, accent.b, 0.95f)
                : new Color(1f, 1f, 1f, 0.10f);
            UIStyles.Fill(new Rect(barX + i * (segW + gap), barY, segW, barH), c);
            UIStyles.Fill(new Rect(barX + i * (segW + gap), barY, segW, S(2f)),
                new Color(1f, 1f, 1f, 0.18f));
        }

        GUI.Label(new Rect(x + pw - S(92f), y + S(84f), S(78f), S(18f)),
            lives <= 1 ? "CRÍTICO" : "ESTABLE", HudLabel(10, TextAnchor.MiddleRight, accent, true));
    }

    private void DrawAmmoPanel(float w, float h)
    {
        float pw = S(460f), ph = S(174f), x = w - pw - S(18f), y = h - ph - S(18f);
        Weapon weapon = WeaponRef;
        if (weapon == null || weapon.slots == null || weapon.slots.Length == 0)
        {
            DrawHudFrame(new Rect(x, y, pw, ph), Gold);
            GUI.Label(new Rect(x + S(16f), y + S(50f), pw - S(32f), S(30f)),
                "SIN ARMA", HudLabel(20, TextAnchor.MiddleCenter, Danger, true));
            return;
        }

        int mag = weapon.CurrentMagazine;
        int size = Mathf.Max(1, weapon.CurrentMagazineSize);
        int lowThreshold = Mathf.Max(1, Mathf.CeilToInt(size * 0.25f));
        bool low = mag <= lowThreshold && !weapon.IsReloading;
        Color accent = low ? Danger : Gold;
        DrawHudFrame(new Rect(x, y, pw, ph), accent);

        GUI.Label(new Rect(x + S(16f), y + S(11f), pw - S(32f), S(18f)),
            "ARSENAL // " + (weapon.CurrentName ?? "Arma").ToUpperInvariant(),
            HudLabel(11, TextAnchor.MiddleLeft, Muted, true));

        Rect icon = new Rect(x + S(16f), y + S(46f), S(42f), S(42f));
        if (ammoIcon != null)
            GUI.DrawTexture(icon, ammoIcon, ScaleMode.ScaleToFit, true);
        else
            DrawAmmoIcon(icon, new Color(accent.r, accent.g, accent.b, 0.9f));

        GUI.Label(new Rect(x + S(70f), y + S(38f), S(130f), S(56f)),
            mag.ToString("00"), HudLabel(46, TextAnchor.MiddleLeft, low ? Danger : White, true));
        GUI.Label(new Rect(x + S(202f), y + S(50f), S(96f), S(34f)),
            "/ " + weapon.CurrentReserve.ToString("000"), HudLabel(20, TextAnchor.MiddleLeft, Muted, true));

        float chipW = S(60f), chipH = S(44f), chipGap = S(6f);
        float chipX = x + pw - S(16f) - chipW * 2f - chipGap;
        // Con mas de dos armas los chips muestran la activa y la siguiente.
        int current = weapon.CurrentIndex;
        int next = weapon.slots.Length > 1 ? (current + 1) % weapon.slots.Length : current;
        int[] shown = { current, next };
        for (int chipIndex = 0; chipIndex < shown.Length; chipIndex++)
        {
            int i = shown[chipIndex];
            if (chipIndex == 1 && i == shown[0]) continue;
            WeaponSlot slot = i >= 0 && i < weapon.slots.Length ? weapon.slots[i] : null;
            if (slot == null) continue;
            bool active = i == current;
            Rect chip = new Rect(chipX + chipIndex * (chipW + chipGap), y + S(42f), chipW, chipH);
            Color chipAccent = active ? accent : Dim;
            UIStyles.Fill(chip, active
                ? new Color(accent.r, accent.g, accent.b, 0.20f)
                : new Color(1f, 1f, 1f, 0.05f));
            UIStyles.Fill(new Rect(chip.x, chip.y, chipW, S(2f)), chipAccent);
            GUI.Label(new Rect(chip.x + S(5f), chip.y + S(3f), chip.width - S(10f), S(16f)),
                (i + 1).ToString("00"), HudLabel(10, TextAnchor.MiddleLeft, chipAccent, true));
            GUI.Label(new Rect(chip.x + S(5f), chip.y + S(19f), chip.width - S(10f), S(20f)),
                slot.magazine.ToString("00"), HudLabel(14, TextAnchor.MiddleLeft,
                    active ? White : Muted, true));
        }

        GUI.Label(new Rect(x + S(16f), y + S(101f), S(210f), S(18f)),
            "TOTAL  " + weapon.TotalAmmo + " BALAS", HudLabel(11, TextAnchor.MiddleLeft, Muted));

        string state = weapon.IsReloading ? "RECARGANDO..." :
            (FirstPersonPlayer.Instance != null && FirstPersonPlayer.Instance.IsAiming
                ? "MIRA ACTIVA" : "CARGADOR " + mag + "/" + size);
        GUI.Label(new Rect(x + S(16f), y + S(142f), S(240f), S(18f)),
            state, HudLabel(11, TextAnchor.MiddleLeft, weapon.IsReloading ? Gold : Accent, true));

        float barX = x + S(16f), barY = y + S(132f), barW = pw - S(32f);
        UIStyles.Fill(new Rect(barX, barY, barW, S(5f)), new Color(1f, 1f, 1f, 0.10f));
        float fill = weapon.IsReloading
            ? weapon.ReloadProgress
            : Mathf.Clamp01(mag / (float)size);
        UIStyles.Fill(new Rect(barX, barY, barW * fill, S(5f)),
            weapon.IsReloading ? Gold : accent);

        if (low && Mathf.Repeat(Time.unscaledTime, 1f) < 0.62f)
        {
            GUI.Label(new Rect(x + pw - S(180f), y + S(140f), S(166f), S(20f)),
                "MUNICIÓN BAJA", HudLabel(11, TextAnchor.MiddleRight, Danger, true));
        }
    }

    private static void DrawPlusIcon(Rect r, Color color)
    {
        UIStyles.Fill(r, new Color(1f, 1f, 1f, 0.06f));
        UIStyles.Fill(new Rect(r.x + r.width * 0.38f, r.y + r.height * 0.12f,
            r.width * 0.24f, r.height * 0.76f), color);
        UIStyles.Fill(new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.38f,
            r.width * 0.76f, r.height * 0.24f), color);
    }

    private static void DrawAmmoIcon(Rect r, Color color)
    {
        UIStyles.Fill(r, new Color(1f, 1f, 1f, 0.05f));
        UIStyles.Fill(new Rect(r.x + r.width * 0.34f, r.y + r.height * 0.20f,
            r.width * 0.32f, r.height * 0.64f), color);
        UIStyles.Fill(new Rect(r.x + r.width * 0.26f, r.y + r.height * 0.08f,
            r.width * 0.48f, r.height * 0.16f), color);
    }

    private void DrawEndPanel(float w, float h)
    {
        UIStyles.Fill(new Rect(0, 0, w, h), new Color(0.01f, 0.02f, 0.04f, 0.86f));
        float pw = Mathf.Min(S(540f), w * 0.86f), ph = S(390f);
        float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;
        Color result = _won ? Accent : Danger;
        float edge = S(5f);

        UIStyles.Fill(new Rect(px, py, pw, ph), new Color(0.03f, 0.05f, 0.08f, 0.96f));
        UIStyles.Fill(new Rect(px, py, pw, edge), result);
        UIStyles.Fill(new Rect(px, py + ph - edge, pw, edge), result);

        GUI.Label(new Rect(px, py + S(26f), pw, S(58f)), _won ? "VICTORIA" : "DERROTA",
            HudLabel(46, TextAnchor.MiddleCenter, result, true));
        GUI.Label(new Rect(px, py + S(100f), pw, S(34f)), "PUNTUACIÓN FINAL  " + _score,
            HudLabel(25, TextAnchor.MiddleCenter, White, true));
        GUI.Label(new Rect(px, py + S(138f), pw, S(30f)), "VIDAS RESTANTES  " + Mathf.Max(0, _lives),
            HudLabel(19, TextAnchor.MiddleCenter, White));
        GUI.Label(new Rect(px, py + S(172f), pw, S(30f)), _message,
            HudLabel(18, TextAnchor.MiddleCenter, Gold));

        GUIStyle button = UIStyles.Button(20, hudFont);
        float bw = pw - S(80f), bx = px + S(40f), bh = S(44f);
        if (_won && !string.IsNullOrEmpty(nextSceneName))
        {
            if (GUI.Button(new Rect(bx, py + S(216f), bw, bh), "SIGUIENTE NIVEL", button)) NextLevel();
            if (GUI.Button(new Rect(bx, py + S(268f), bw, bh), "VOLVER A JUGAR", button)) RestartRun();
        }
        else
        {
            if (GUI.Button(new Rect(bx, py + S(216f), bw, bh), "VOLVER A JUGAR", button)) RestartRun();
        }
        if (GUI.Button(new Rect(bx, py + S(320f), bw, bh), "MENÚ PRINCIPAL", button)) GoToMenu();
    }
}
