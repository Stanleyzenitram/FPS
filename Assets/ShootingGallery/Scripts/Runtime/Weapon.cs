using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Configuracion de un arma del arsenal (fusil o pistola).</summary>
[Serializable]
public class WeaponSlot
{
    public string displayName = "AR-L 5.56";
    public GameObject model;
    public Transform muzzle;

    [Header("Municion")]
    public int magazineSize = 14;
    public int magazine = 14;
    public int reserve = 42;
    public float reloadTime = 1.4f;

    [Header("Disparo")]
    public AudioClip fireClip;
    public float fireRate = 0.16f;
    public float spread = 0f;
    public float bulletSpeed = 130f;
    public float recoilKick = 0.07f;
    public float recoilPitch = 2.2f;

    [Header("Agarre en primera persona")]
    [Tooltip("Puntos del WeaponPivot donde caen las muñecas al empuñar este arma.")]
    public Vector3 gripRight = new Vector3(0.34f, -0.35f, 0.56f);
    public Vector3 gripLeft = new Vector3(0.16f, -0.28f, 0.78f);
    public bool pistol;
}

/// <summary>
/// Arsenal de primera persona: dispara con clic izquierdo, recarga con R,
/// cambia de arma con 1/2 y aplica sonido, fogonazo y recoil.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("Referencias")]
    public Camera aimCamera;
    public Transform weaponPivot;
    public GameObject bulletPrefab;
    public GameObject muzzleFlashPrefab;
    public AudioClip reloadClip;
    public AudioClip reloadInsertClip;
    public AudioSource sfxSource;

    [Header("Mira ADS")]
    public Vector3 adsOffset = new Vector3(-0.26f, 0.10f, 0.02f);

    [Header("Arsenal")]
    public WeaponSlot[] slots = new WeaponSlot[2];

    public static Weapon Instance { get; private set; }

    private int _current;
    private float _cooldown;
    private float _reloadTimer;
    private float _recoil;
    private float _lastKick;
    private bool _insertSfxPlayed;
    private Vector3 _pivotBasePosition;
    private Quaternion _pivotBaseRotation;

    public int CurrentIndex => _current;
    public WeaponSlot CurrentSlot => slots != null && _current >= 0 && _current < slots.Length
        ? slots[_current] : null;
    public string CurrentName => slots != null && slots.Length > 0 && slots[_current] != null
        ? slots[_current].displayName : "Sin arma";
    public int CurrentMagazine => slots != null && slots.Length > 0 && slots[_current] != null
        ? slots[_current].magazine : 0;
    public int CurrentReserve => slots != null && slots.Length > 0 && slots[_current] != null
        ? slots[_current].reserve : 0;
    public int CurrentMagazineSize => slots != null && slots.Length > 0 && slots[_current] != null
        ? slots[_current].magazineSize : 1;
    public bool IsReloading => _reloadTimer > 0f;
    public float ReloadProgress => IsReloading && slots[_current] != null
        ? 1f - (_reloadTimer / Mathf.Max(0.01f, slots[_current].reloadTime)) : 1f;

    // Fases de una recarga realista: bajar el arma, alcanzar el cargador,
    // extraerlo, insertarlo y asentar el arma antes de volver a disparar.
    public float ReloadDip => ReloadPulse(0.02f, 0.22f, 0.76f, 0.98f);
    public float ReloadReach => ReloadPulse(0.06f, 0.26f, 0.68f, 0.90f);
    public float ReloadPull => ReloadPulse(0.22f, 0.38f, 0.60f, 0.78f);
    public float ReloadInsert => ReloadPulse(0.58f, 0.72f, 0.84f, 0.96f);
    public float ReloadSettle => IsReloading
        ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, ReloadProgress)) : 1f;

    private float ReloadPulse(float inStart, float inEnd, float outStart, float outEnd)
    {
        if (!IsReloading) return 0f;
        float p = ReloadProgress;
        float enter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inStart, inEnd, p));
        float exit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outStart, outEnd, p));
        return enter * (1f - exit);
    }

    public int TotalAmmo
    {
        get
        {
            int total = 0;
            if (slots == null) return 0;
            foreach (WeaponSlot slot in slots)
                if (slot != null) total += slot.magazine + slot.reserve;
            return total;
        }
    }

    public bool HasAmmo => slots != null && slots.Length > 0 && slots[_current] != null
        && slots[_current].magazine > 0;

    private void Awake()
    {
        Instance = this;
        if (aimCamera == null) aimCamera = Camera.main;

        // Recordamos la pose del arma para aplicar el recoil sobre ella.
        Transform pivot = weaponPivot != null ? weaponPivot : transform;
        _pivotBasePosition = pivot.localPosition;
        _pivotBaseRotation = pivot.localRotation;

        if (slots == null) slots = new WeaponSlot[0];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].magazine = Mathf.Clamp(slots[i].magazine, 0, slots[i].magazineSize);
            if (slots[i].model != null) slots[i].model.SetActive(i == _current);
        }
    }

    /// <summary>El manager llama a esto al empezar cada nivel.</summary>
    public void SetupAmmo(int primaryMagazine, int primaryReserve,
        int secondaryMagazine = 15, int secondaryReserve = 45)
    {
        if (slots == null || slots.Length == 0) return;
        if (slots[0] != null)
        {
            slots[0].magazineSize = Mathf.Max(1, primaryMagazine);
            slots[0].magazine = Mathf.Max(1, primaryMagazine);
            slots[0].reserve = Mathf.Max(0, primaryReserve);
        }
        if (slots.Length > 1 && slots[1] != null)
        {
            slots[1].magazineSize = Mathf.Max(1, secondaryMagazine);
            slots[1].magazine = Mathf.Max(1, secondaryMagazine);
            slots[1].reserve = Mathf.Max(0, secondaryReserve);
        }
        // El resto del arsenal del muro conserva su tamano de cargador y recibe
        // tres cargadores de reserva; asi no se duplica la municion del nivel.
        for (int i = 2; i < slots.Length; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null) continue;
            slot.magazineSize = Mathf.Max(1, slot.magazineSize);
            slot.magazine = slot.magazineSize;
            slot.reserve = Mathf.Max(slot.reserve, slot.magazineSize * 3);
        }
        _current = 0;
        SelectWeapon(0);
        GameManager.Instance?.NotifyAmmoChanged();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _cooldown -= dt;
        _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 6.5f);

        bool aiming = FirstPersonPlayer.Instance != null && FirstPersonPlayer.Instance.IsAiming &&
                      (GameManager.Instance == null || !GameManager.Instance.IsGameOver);
        Vector3 ads = aiming ? adsOffset : Vector3.zero;

        Transform pivot = weaponPivot != null ? weaponPivot : transform;
        WeaponSlot slot = slots != null && slots.Length > 0 ? slots[_current] : null;
        float kick = slot != null ? slot.recoilKick : 0.06f;
        float pitch = slot != null ? slot.recoilPitch : 2f;

        // Recarga por fases: el arma baja, se gira para ver el cargador,
        // se extrae, se inserta y vuelve a su pose de combate.
        float dip = ReloadDip;
        float pull = ReloadPull;
        float insert = ReloadInsert;
        Vector3 reloadOffset = new Vector3(
            0.04f * dip + 0.02f * pull,
            -0.34f * dip - 0.06f * pull,
            -0.14f * dip);
        Quaternion reloadRotation = Quaternion.Euler(
            20f * dip + 10f * pull,
            16f * dip + 22f * pull,
            -32f * dip - 12f * pull + 6f * insert);

        Vector3 restPose = Vector3.Lerp(pivot.localPosition - Vector3.back * (_lastKick),
            _pivotBasePosition + ads + reloadOffset, 15f * dt);
        _lastKick = kick * _recoil;
        pivot.localPosition = restPose + Vector3.back * _lastKick;

        Quaternion targetRotation = _pivotBaseRotation *
            Quaternion.Euler(aiming ? 1.5f : 0f, 0f, 0f) * reloadRotation;
        Quaternion restRotation = Quaternion.Slerp(pivot.localRotation, targetRotation, 15f * dt);
        pivot.localRotation = restRotation * Quaternion.Euler(-pitch * _recoil, 0f, 0f);

        if (_reloadTimer > 0f)
        {
            _reloadTimer -= dt;
            if (!_insertSfxPlayed && IsReloading && ReloadProgress >= 0.72f && reloadInsertClip != null)
            {
                _insertSfxPlayed = true;
                PlaySfx(reloadInsertClip, 0.55f);
            }
            if (_reloadTimer <= 0f) FinishReload();
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && !GameManager.IsFinished)
        {
            if (kb.rKey.wasPressedThisFrame) StartReload();
            if (kb.digit1Key.wasPressedThisFrame) SelectWeapon(0);
            if (kb.digit2Key.wasPressedThisFrame) SelectWeapon(1);
            if (kb.digit3Key.wasPressedThisFrame) SelectWeapon(2);
            if (kb.digit4Key.wasPressedThisFrame) SelectWeapon(3);
            if (kb.digit5Key.wasPressedThisFrame) SelectWeapon(4);
            if (kb.digit6Key.wasPressedThisFrame) SelectWeapon(5);
            if (kb.digit7Key.wasPressedThisFrame) SelectWeapon(6);
            if (kb.digit8Key.wasPressedThisFrame) SelectWeapon(7);
            if (kb.digit9Key.wasPressedThisFrame) SelectWeapon(8);
            if (kb.digit0Key.wasPressedThisFrame) SelectWeapon(9);
            if (kb.qKey.wasPressedThisFrame) CycleWeapon(-1);
            if (kb.eKey.wasPressedThisFrame) CycleWeapon(1);
        }

        if (_reloadTimer > 0f) return;

        // Si el arma actual se quedo sin nada, pasa automaticamente a la que tenga balas.
        if (slot != null && slot.magazine <= 0 && slot.reserve <= 0)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == _current || slots[i] == null) continue;
                if (slots[i].magazine + slots[i].reserve > 0)
                {
                    SelectWeapon(i);
                    slot = slots[_current];
                    break;
                }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (GameManager.Instance == null || !GameManager.Instance.CanShoot) return;
        if (_cooldown > 0f) return;
        if (slot == null || slot.magazine <= 0)
        {
            StartReload();
            return;
        }

        Fire(slot);
    }

    public void SelectWeapon(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return;
        if (slots[index] == null) return;
        _current = index;
        _reloadTimer = 0f;
        _insertSfxPlayed = false;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].model != null)
                slots[i].model.SetActive(i == index);
        GameManager.Instance?.NotifyAmmoChanged();
    }

    /// <summary>Recorre el arsenal con Q/E, ignorando huecos y envolviendo.</summary>
    public void CycleWeapon(int delta)
    {
        if (slots == null || slots.Length == 0 || delta == 0) return;
        int step = delta > 0 ? 1 : -1;
        int index = _current;
        for (int attempt = 0; attempt < slots.Length; attempt++)
        {
            index = (index + step + slots.Length) % slots.Length;
            if (index != _current && slots[index] != null)
            {
                SelectWeapon(index);
                return;
            }
        }
    }

    public void StartReload()
    {
        if (_reloadTimer > 0f) return;
        WeaponSlot slot = slots != null && slots.Length > 0 ? slots[_current] : null;
        // Se permite recargar con el cargador lleno: asi R siempre muestra la
        // animacion de manos y cargador (no cambia la municion).
        if (slot == null || slot.reserve <= 0) return;

        _reloadTimer = slot.reloadTime;
        _insertSfxPlayed = false;
        PlaySfx(reloadClip, 0.7f);
    }

    private void FinishReload()
    {
        WeaponSlot slot = slots != null && slots.Length > 0 ? slots[_current] : null;
        if (slot == null) return;
        int needed = slot.magazineSize - slot.magazine;
        int taken = Mathf.Min(needed, slot.reserve);
        slot.magazine += taken;
        slot.reserve -= taken;
        _reloadTimer = 0f;
        GameManager.Instance?.NotifyAmmoChanged();
    }

    /// <summary>Sonido 2D directo al listener: evita que el disparo se pierda por attenuacion 3D.</summary>
    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
            return;
        }
        AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }

    private void Fire(WeaponSlot slot)
    {
        _cooldown = slot.fireRate;
        _recoil = 1f;
        slot.magazine--;

        if (bulletPrefab != null)
        {
            GameObject go = Instantiate(bulletPrefab, slot.muzzle != null ? slot.muzzle.position : transform.position,
                Quaternion.identity);
            Projectile bullet = go.GetComponent<Projectile>();
            if (bullet == null) bullet = go.AddComponent<Projectile>();

            Vector3 dir = aimCamera != null ? aimCamera.transform.forward : transform.forward;
            if (slot.spread > 0f)
            {
                dir = Quaternion.Euler(UnityEngine.Random.Range(-slot.spread, slot.spread),
                                       UnityEngine.Random.Range(-slot.spread, slot.spread),
                                       UnityEngine.Random.Range(-slot.spread, slot.spread)) * dir;
            }

            bullet.owner = Projectile.Owner.Player;
            bullet.speed = slot.bulletSpeed;
            bullet.shooter = transform.root;
            bullet.Launch(dir, transform.root);
        }

        Vector3 flashPos = slot.muzzle != null ? slot.muzzle.position : transform.position;
        PlaySfx(slot.fireClip, 0.95f);

        // Fogonazo visible (bocacha) + luz que ilumina la escena un instante.
        if (muzzleFlashPrefab != null)
        {
            Quaternion facing = aimCamera != null ? aimCamera.transform.rotation : transform.rotation;
            GameObject flash = Instantiate(muzzleFlashPrefab, flashPos, facing);
            flash.transform.localScale *= UnityEngine.Random.Range(0.85f, 1.15f);
            Destroy(flash, 0.075f);
        }
        else
        {
            GameObject flash = new GameObject("MuzzleFlash");
            flash.transform.position = flashPos;
            Light light = flash.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.45f);
            light.intensity = 4f;
            light.range = 4f;
            Destroy(flash, 0.06f);
        }

        FirstPersonPlayer.Instance?.AddRecoil(slot.recoilPitch);
        GameManager.Instance?.NotifyAmmoChanged();
    }
}
