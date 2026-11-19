using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera todo el contenido del juego dentro del proyecto del alumno:
/// materiales, prefabs, animaciones, definiciones de nivel y las 4 escenas.
/// Menu: Shooting Gallery > Generar proyecto.
/// Tambien se puede llamar desde batch mode con -executeMethod ProjectBuilder.BuildAll
/// </summary>
public static class ProjectBuilder
{
    private const string Root = "Assets/ShootingGallery";
    private const string SoldierModel = "Assets/LowPolySoldiers_demo/models/Soldier_demo.FBX";
    private const string SoldierPrefab = "Assets/LowPolySoldiers_demo/Soldier_demo_Prefab.prefab";
    private const string SoldierAnims = "Assets/LowPolySoldiers_demo/animation/";

    private static Material _floor, _wall, _cover, _outdoor, _exit, _targetMat, _bulletMat,
        _enemyBulletMat, _weaponMat, _flashMat, _trailMat, _weaponArMat, _weaponM9Mat;
    private static GameObject _playerPrefab, _targetPrefab, _botPrefab, _bossPrefab,
        _bulletPrefab, _enemyBulletPrefab, _managerPrefab, _muzzleFlashPrefab;
    private static AudioClip _gun, _hit, _hurt, _win, _lose, _reload;
    private static Texture2D _arTexture, _m9Texture;
    private static GameObject _soldierAsset;
    private static Avatar _soldierAvatar;
    private static RuntimeAnimatorController _soldierController;

    [MenuItem("Shooting Gallery/Generar proyecto (escenas + prefabs)")]
    public static void BuildAll()
    {
        EnsureFolders();
        LoadAssets();
        CreateMaterials();
        CreateSoldierAnimator();
        CreatePrefabs();

        LevelDefinition gallery = CreateLevel("01 Galeria", "01 Galería de disparos",
            "01 Galeria de disparos", "02 Bots", 14, 42, 10, 5, false);
        LevelDefinition bots = CreateLevel("02 Bots", "02 Modo contra bots",
            "02 Bots", "03 Jefe Final", 20, 60, 2, 5, false);
        LevelDefinition boss = CreateLevel("03 Jefe", "03 Jefe final",
            "03 Jefe Final", "", 24, 72, 2, 3, true);

        CreateMenuScene();
        CreateGalleryScene(gallery);
        CreateBotsScene(bots);
        CreateBossScene(boss);
        SetBuildSettings();
        Validate();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ShootingGallery] Generacion completada: 4 escenas, soldados, arsenal y fogonazos listos.");
    }

    // ------------------------------------------------------------ carpetas ---
    private static void EnsureFolders()
    {
        string[] folders = { "Art", "Audio", "Materials", "Prefabs", "Scenes", "Levels", "Animations" };
        foreach (string folder in folders)
        {
            string path = Root + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Root, folder);
        }
    }

    private static void LoadAssets()
    {
        _gun = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/FSX/Gunfire.wav");
        _hit = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/hit.wav");
        _hurt = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/hurt.wav");
        _win = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/win.wav");
        _lose = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/lose.wav");
        _reload = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/reload.wav");

        // Los clips deben estar precargados o el primer disparo puede salir sin sonido.
        ConfigureAudio("Assets/Audio/FSX/Gunfire.wav");
        ConfigureAudio(Root + "/Audio/hit.wav");
        ConfigureAudio(Root + "/Audio/hurt.wav");
        ConfigureAudio(Root + "/Audio/win.wav");
        ConfigureAudio(Root + "/Audio/lose.wav");
        ConfigureAudio(Root + "/Audio/reload.wav");

        _arTexture = ConfigureTexture("Assets/Low Poly AR Weapon Pack 3/Texture/Low Poly Weapon.png");
        _m9Texture = ConfigureTexture("Assets/Objects/Handgun/Tex_0009_1.jpg");

        _soldierAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPrefab);
        Object[] modelAssets = AssetDatabase.LoadAllAssetsAtPath(SoldierModel);
        foreach (Object asset in modelAssets)
        {
            if (asset is Avatar avatar) { _soldierAvatar = avatar; break; }
        }
        Debug.Log($"[ShootingGallery] Soldado personal={( _soldierAsset != null)} " +
                  $"avatar={(_soldierAvatar != null)} sonido={(_gun != null)} recarga={(_reload != null)} " +
                  $"texturas={( _arTexture != null && _m9Texture != null)}");
    }

    /// <summary>Fuerza DecompressOnLoad + precarga para que los efectos suenen siempre.</summary>
    private static void ConfigureAudio(string path)
    {
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }

    /// <summary>Asegura que la textura del arma se importe como color sRGB.</summary>
    private static Texture2D ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = importer.textureType != TextureImporterType.Default ||
                           !importer.sRGBTexture || importer.maxTextureSize < 2048;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ----------------------------------------------------------- materiales ---
    private static Material MakeMaterial(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material MakeTexturedMaterial(string name, Color color, Texture2D texture)
    {
        Material mat = MakeMaterial(name, color);
        if (texture != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            mat.mainTexture = texture;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material MakeFlashMaterial()
    {
        string path = Root + "/Materials/MuzzleFlash.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/muzzle_flash.png");
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void CreateMaterials()
    {
        Texture2D targetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/target.png");

        _floor = MakeMaterial("Floor", new Color(0.16f, 0.17f, 0.19f));
        _wall = MakeMaterial("Wall", new Color(0.26f, 0.27f, 0.30f));
        _cover = MakeMaterial("Cover", new Color(0.34f, 0.33f, 0.28f));
        _outdoor = MakeMaterial("Outdoor", new Color(0.22f, 0.30f, 0.20f));
        _exit = MakeMaterial("Exit", new Color(0.18f, 0.75f, 0.32f));
        _targetMat = MakeTexturedMaterial("Target_Bullseye", Color.white, targetTex);
        _bulletMat = MakeMaterial("Bullet", new Color(1f, 0.85f, 0.25f));
        _enemyBulletMat = MakeMaterial("EnemyBullet", new Color(1f, 0.25f, 0.15f));
        _weaponMat = MakeMaterial("WeaponFallback", new Color(0.18f, 0.19f, 0.21f));
        _weaponArMat = MakeWeaponMaterial("Weapon_AR_Textured", _arTexture);
        _weaponM9Mat = MakeWeaponMaterial("Weapon_M9_Textured", _m9Texture);
        _flashMat = MakeFlashMaterial();
        _trailMat = MakeTrailMaterial();
    }

    /// <summary>Material PBR sencillo con el atlas de cada arma.</summary>
    private static Material MakeWeaponMaterial(string name, Texture2D texture)
    {
        Material mat = MakeTexturedMaterial(name, Color.white, texture);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.2f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Material con colores de vertice para que la estela se vea dorada/roja.</summary>
    private static Material MakeTrailMaterial()
    {
        string path = Root + "/Materials/BulletTrail.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ----------------------------------------------------------- animaciones ---
    private static AnimationClip LoadClip(string file)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SoldierAnims + file + ".FBX");
        if (clip != null) return clip;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SoldierAnims + file + ".FBX");
        foreach (Object asset in assets)
            if (asset is AnimationClip animationClip) return animationClip;
        return null;
    }

    private static void CreateSoldierAnimator()
    {
        string path = Root + "/Animations/Soldier.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = sm.AddState("Idle", new Vector3(240f, 60f, 0f));
        AnimatorState run = sm.AddState("Run", new Vector3(240f, -40f, 0f));
        AnimatorState shoot = sm.AddState("Shoot", new Vector3(480f, 120f, 0f));

        idle.motion = LoadClip("demo_combat_idle");
        run.motion = LoadClip("demo_combat_run");
        shoot.motion = LoadClip("demo_combat_shoot");
        sm.defaultState = idle;

        AnimatorStateTransition toRun = idle.AddTransition(run);
        toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        toRun.duration = 0.12f;
        AnimatorStateTransition toIdle = run.AddTransition(idle);
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        toIdle.duration = 0.12f;
        AnimatorStateTransition shootTransition = sm.AddAnyStateTransition(shoot);
        shootTransition.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
        shootTransition.duration = 0.05f;
        AnimatorStateTransition backToIdle = shoot.AddTransition(idle);
        backToIdle.hasExitTime = true;
        backToIdle.exitTime = 0.85f;

        _soldierController = controller;
        Debug.Log($"[ShootingGallery] Animaciones: idle={idle.motion != null} run={run.motion != null} " +
                  $"shoot={shoot.motion != null}");
    }

    /// <summary>Escala y centra un modelo importado dentro de su padre.</summary>
    private static void FitModel(GameObject model, float targetSize, bool useMaxDimension = false)
    {
        model.transform.localPosition = Vector3.zero;
        Bounds bounds = BoundsOf(model);
        float size = useMaxDimension
            ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
            : bounds.size.y;
        if (size > 0.001f) model.transform.localScale *= targetSize / size;

        bounds = BoundsOf(model);
        Transform parent = model.transform.parent;
        Vector3 pivot = parent != null ? parent.position : Vector3.zero;
        Vector3 worldDelta = new Vector3(pivot.x - bounds.center.x,
                                         pivot.y - bounds.min.y,
                                         pivot.z - bounds.center.z);
        Vector3 localDelta = parent != null ? parent.InverseTransformVector(worldDelta) : worldDelta;
        model.transform.localPosition += localDelta;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // ------------------------------------------------------------- prefabs ---
    private static GameObject SavePrefab(GameObject go, string name)
    {
        string path = Root + "/Prefabs/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            AssetDatabase.DeleteAsset(path);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void SetMaterial(GameObject go, Material mat)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    /// <summary>Sustituye los materiales grises del modelo por el material con textura.</summary>
    private static void ApplyWeaponMaterial(GameObject weapon, Material mat)
    {
        if (weapon == null || mat == null) return;
        foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            renderer.sharedMaterials = slots;
        }
    }

    private static void SetupBulletTrail(GameObject bullet, float time, float width)
    {
        TrailRenderer trail = bullet.AddComponent<TrailRenderer>();
        trail.sharedMaterial = _trailMat;
        trail.time = time;
        trail.startWidth = width;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.alignment = LineAlignment.View;
        trail.emitting = false;
    }

    private static void CreatePrefabs()
    {
        // --- Bala del jugador
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "Bullet_Player";
        bullet.transform.localScale = Vector3.one * 0.13f;
        SetMaterial(bullet, _bulletMat);
        Object.DestroyImmediate(bullet.GetComponent<Collider>());
        SetupBulletTrail(bullet, 0.2f, 0.065f);
        Projectile pb = bullet.AddComponent<Projectile>();
        pb.owner = Projectile.Owner.Player;
        pb.speed = 95f;
        pb.damage = 1;
        _bulletPrefab = SavePrefab(bullet, "Bullet_Player");

        // --- Bala enemiga
        GameObject eBullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eBullet.name = "Bullet_Enemy";
        eBullet.transform.localScale = Vector3.one * 0.22f;
        SetMaterial(eBullet, _enemyBulletMat);
        Object.DestroyImmediate(eBullet.GetComponent<Collider>());
        SetupBulletTrail(eBullet, 0.28f, 0.1f);
        Projectile pe = eBullet.AddComponent<Projectile>();
        pe.owner = Projectile.Owner.Enemy;
        pe.speed = 30f;
        pe.damage = 1;
        _enemyBulletPrefab = SavePrefab(eBullet, "Bullet_Enemy");

        // --- Fogonazo (quad con textura + luz)
        GameObject flash = new GameObject("MuzzleFlash");
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Flash";
        quad.transform.SetParent(flash.transform, false);
        quad.transform.localScale = Vector3.one * 0.45f;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        SetMaterial(quad, _flashMat);
        Light flashLight = flash.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.82f, 0.42f);
        flashLight.intensity = 6f;
        flashLight.range = 5f;
        _muzzleFlashPrefab = SavePrefab(flash, "MuzzleFlash");

        // --- Diana
        GameObject diana = new GameObject("Target_Diana");
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Disc";
        disc.transform.SetParent(diana.transform, false);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        disc.transform.localScale = new Vector3(1.5f, 0.12f, 1.5f);
        SetMaterial(disc, _targetMat);
        GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stand.name = "Stand";
        stand.transform.SetParent(diana.transform, false);
        stand.transform.localPosition = new Vector3(0f, -1.05f, 0f);
        stand.transform.localScale = new Vector3(0.08f, 1.05f, 0.08f);
        SetMaterial(stand, _wall);
        Object.DestroyImmediate(stand.GetComponent<Collider>());
        Target td = diana.AddComponent<Target>();
        td.type = Target.TargetType.Diana;
        td.points = 100;
        td.hp = 1;
        td.hitClip = _hit;
        _targetPrefab = SavePrefab(diana, "Target_Diana");

        // --- Enemigos con el modelo de soldado personal del alumno
        _botPrefab = CreateCharacterPrefab("Bot_Soldier", 1.8f, Target.TargetType.Bot, 250, 2, false);
        _bossPrefab = CreateCharacterPrefab("Boss_Soldier", 2.5f, Target.TargetType.Boss, 1500, 12, true);

        CreatePlayerPrefab();

        GameObject manager = new GameObject("GameManager");
        AudioSource managerSfx = manager.AddComponent<AudioSource>();
        managerSfx.playOnAwake = false;
        managerSfx.spatialBlend = 0f;
        managerSfx.volume = 1f;
        GameManager gm = manager.AddComponent<GameManager>();
        gm.hitClip = _hit;
        gm.hurtClip = _hurt;
        gm.winClip = _win;
        gm.loseClip = _lose;
        gm.sfxSource = managerSfx;
        _managerPrefab = SavePrefab(manager, "GameManager");
    }

    private static GameObject CreateCharacterPrefab(string name, float height,
        Target.TargetType type, int points, int hp, bool isBoss)
    {
        GameObject root = new GameObject(name);
        GameObject model = null;

        if (_soldierAsset != null)
        {
            model = PrefabUtility.InstantiatePrefab(_soldierAsset) as GameObject;
            if (model != null)
            {
                model.name = "Soldier_Model";
                model.transform.SetParent(root.transform, false);
                FitModel(model, height);

                Animator animator = model.GetComponentInChildren<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                if (animator.avatar == null && _soldierAvatar != null) animator.avatar = _soldierAvatar;
                animator.runtimeAnimatorController = _soldierController;
                animator.applyRootMotion = false;
            }
        }

        if (model == null)
        {
            // Respaldo por si se borra la carpeta de assets personales.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body_Fallback";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            body.transform.localScale = new Vector3(height * 0.35f, height * 0.5f, height * 0.35f);
        }

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, height * 0.5f, 0f);
        collider.height = height;
        collider.radius = Mathf.Max(0.25f, height * 0.22f);

        Target target = root.AddComponent<Target>();
        target.type = type;
        target.points = points;
        target.hp = hp;
        target.hitClip = _hit;

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, height * 0.72f, height * 0.36f);

        if (isBoss)
        {
            BossAI boss = root.AddComponent<BossAI>();
            boss.muzzle = muzzle.transform;
            boss.enemyBulletPrefab = _enemyBulletPrefab;
            boss.speed = 2f;
            boss.stopDistance = 4f;
        }
        else
        {
            BotAI bot = root.AddComponent<BotAI>();
            bot.muzzle = muzzle.transform;
            bot.enemyBulletPrefab = _enemyBulletPrefab;
            bot.fireRange = 30f;
            bot.fireInterval = 2.2f;
            bot.bulletSpeed = 30f;
            bot.fireSpread = 3.5f;
        }

        return SavePrefab(root, name);
    }

    private static GameObject CreateCubeWeapon(string name, Vector3 scale)
    {
        GameObject weapon = new GameObject(name);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(weapon.transform, false);
        body.transform.localScale = scale;
        SetMaterial(body, _weaponMat);
        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.transform.SetParent(weapon.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 0f, scale.z * 0.6f);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localScale = new Vector3(scale.x * 0.4f, scale.z * 0.5f, scale.x * 0.4f);
        SetMaterial(barrel, _weaponMat);
        return weapon;
    }

    private static void CreatePlayerPrefab()
    {
        GameObject root = new GameObject("Player_FPS");

        CharacterController cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        GameObject camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 76f;
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();

        GameObject pivot = new GameObject("WeaponPivot");
        pivot.transform.SetParent(camGo.transform, false);

        // --- Arma principal: AR del paquete importado.
        GameObject arPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Low Poly AR Weapon Pack 3/Prefabs/Weapons/AR_L.prefab");
        GameObject rifle;
        if (arPrefab != null)
        {
            rifle = PrefabUtility.InstantiatePrefab(arPrefab) as GameObject;
            rifle.transform.SetParent(pivot.transform, false);
            rifle.transform.localPosition = new Vector3(0.30f, -0.30f, 0.55f);
            rifle.transform.localRotation = Quaternion.Euler(0f, -6f, 0f);
            rifle.transform.localScale = Vector3.one * 0.55f;
            ApplyWeaponMaterial(rifle, _weaponArMat);
        }
        else
        {
            rifle = CreateCubeWeapon("WeaponFallback_AR", new Vector3(0.12f, 0.14f, 0.6f));
            rifle.transform.SetParent(pivot.transform, false);
            rifle.transform.localPosition = new Vector3(0.30f, -0.30f, 0.55f);
        }

        // --- Arma secundaria: pistola M9 del proyecto (o cubos de respaldo).
        GameObject m9Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Objects/Handgun/M9.FBX");
        GameObject pistol;
        if (m9Prefab != null)
        {
            pistol = PrefabUtility.InstantiatePrefab(m9Prefab) as GameObject;
            pistol.transform.SetParent(pivot.transform, false);
            // El FBX trae rotation (270,0,0): sin conservarla la pistola mira al suelo.
            Quaternion m9BaseRotation = pistol.transform.localRotation;
            FitModel(pistol, 0.42f, true);
            pistol.transform.localPosition = new Vector3(0.30f, -0.28f, 0.55f);
            pistol.transform.localRotation = Quaternion.Euler(0f, -8f, 0f) * m9BaseRotation;
            ApplyWeaponMaterial(pistol, _weaponM9Mat);
        }
        else
        {
            pistol = CreateCubeWeapon("WeaponFallback_M9", new Vector3(0.09f, 0.12f, 0.28f));
            pistol.transform.SetParent(pivot.transform, false);
            pistol.transform.localPosition = new Vector3(0.30f, -0.28f, 0.55f);
        }

        GameObject muzzleAr = new GameObject("Muzzle_AR");
        muzzleAr.transform.SetParent(pivot.transform, false);
        muzzleAr.transform.localPosition = new Vector3(0.28f, -0.25f, 1.0f);
        GameObject muzzlePistol = new GameObject("Muzzle_M9");
        muzzlePistol.transform.SetParent(pivot.transform, false);
        muzzlePistol.transform.localPosition = new Vector3(0.30f, -0.26f, 0.82f);

        AudioSource playerSfx = root.AddComponent<AudioSource>();
        playerSfx.playOnAwake = false;
        playerSfx.spatialBlend = 0f;
        playerSfx.volume = 1f;

        FirstPersonPlayer fpp = root.AddComponent<FirstPersonPlayer>();
        fpp.viewCamera = cam;

        Weapon wp = root.AddComponent<Weapon>();
        wp.aimCamera = cam;
        wp.weaponPivot = pivot.transform;
        wp.bulletPrefab = _bulletPrefab;
        wp.muzzleFlashPrefab = _muzzleFlashPrefab;
        wp.reloadClip = _reload;
        wp.sfxSource = playerSfx;
        wp.slots = new[]
        {
            new WeaponSlot
            {
                displayName = "AR-L 5.56", model = rifle, muzzle = muzzleAr.transform,
                magazineSize = 14, magazine = 14, reserve = 42, reloadTime = 1.5f,
                fireClip = _gun, fireRate = 0.15f, bulletSpeed = 95f,
                recoilKick = 0.085f, recoilPitch = 2.2f
            },
            new WeaponSlot
            {
                displayName = "M9 9mm", model = pistol, muzzle = muzzlePistol.transform,
                magazineSize = 8, magazine = 8, reserve = 16, reloadTime = 1.1f,
                fireClip = _gun, fireRate = 0.28f, bulletSpeed = 80f,
                recoilKick = 0.11f, recoilPitch = 3.4f
            }
        };

        _playerPrefab = SavePrefab(root, "Player_FPS");
    }

    // -------------------------------------------------------------- niveles ---
    private static LevelDefinition CreateLevel(string file, string display, string scene,
        string next, int ammo, int reserve, int targets, int bots, bool boss)
    {
        string path = Root + "/Levels/" + file + ".asset";
        LevelDefinition def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        def.displayName = display;
        def.sceneName = scene;
        def.nextSceneName = next;
        def.startingAmmo = ammo;
        def.startingReserve = reserve;
        def.totalTargets = targets;
        def.totalBots = bots;
        def.hasBoss = boss;
        EditorUtility.SetDirty(def);
        return def;
    }

    // ---------------------------------------------------------------- escenas ---
    private static GameObject MakeBox(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        SetMaterial(go, mat);
        return go;
    }

    private static void AddLight(Vector3 position, float intensity)
    {
        GameObject lightGo = new GameObject("Luz");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        lightGo.transform.position = position;
        lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
    }

    /// <summary>Recinto cerrado usado por los niveles 2 y 3.</summary>
    private static void BuildArena()
    {
        MakeBox("Suelo", new Vector3(0f, -0.5f, 12f), new Vector3(46f, 1f, 80f), _floor);
        MakeBox("Pared_izquierda", new Vector3(-22f, 3f, 12f), new Vector3(1f, 6f, 80f), _wall);
        MakeBox("Pared_derecha", new Vector3(22f, 3f, 12f), new Vector3(1f, 6f, 80f), _wall);
        MakeBox("Pared_fondo", new Vector3(0f, 3f, 51f), new Vector3(45f, 6f, 1f), _wall);
        MakeBox("Pared_inicio", new Vector3(0f, 3f, -27f), new Vector3(45f, 6f, 1f), _wall);
        AddLight(Vector3.zero, 1.15f);
        RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);
    }

    /// <summary>
    /// Nivel 1: sala de tiro cerrada con una salida lateral al campo exterior,
    /// donde los enemigos estan repartidos.
    /// </summary>
    private static void BuildGalleryWorld()
    {
        // --- Sala interior
        MakeBox("Suelo_Galeria", new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 32f), _floor);
        MakeBox("Techo_Galeria", new Vector3(0f, 6.2f, 0f), new Vector3(30f, 0.4f, 32f), _wall);
        MakeBox("Pared_Entrada", new Vector3(0f, 3f, -16f), new Vector3(30f, 6f, 1f), _wall);
        MakeBox("Pared_Dianas", new Vector3(0f, 3f, 16f), new Vector3(30f, 6f, 1f), _wall);
        MakeBox("Pared_Izquierda", new Vector3(-15f, 3f, 0f), new Vector3(1f, 6f, 32f), _wall);
        // Pared derecha con hueco de salida entre z=-4 y z=2.
        MakeBox("Pared_Derecha_A", new Vector3(15f, 3f, -10f), new Vector3(1f, 6f, 12f), _wall);
        MakeBox("Pared_Derecha_B", new Vector3(15f, 3f, 9f), new Vector3(1f, 6f, 14f), _wall);
        MakeBox("Marco_Salida", new Vector3(15f, 5.8f, -1f), new Vector3(1.3f, 0.5f, 6.6f), _exit);
        MakeBox("Suelo_Salida", new Vector3(16f, -0.48f, -1f), new Vector3(3f, 0.06f, 6f), _exit);

        // --- Campo exterior
        MakeBox("Suelo_Exterior", new Vector3(42f, -0.5f, 0f), new Vector3(56f, 1f, 80f), _outdoor);
        MakeBox("Cierre_Sur", new Vector3(15f, 3f, -28f), new Vector3(1f, 6f, 24f), _wall);
        MakeBox("Cierre_Norte", new Vector3(15f, 3f, 28f), new Vector3(1f, 6f, 24f), _wall);
        MakeBox("Valla_Fondo", new Vector3(70f, 3f, 0f), new Vector3(1f, 6f, 80f), _wall);
        MakeBox("Valla_Sur", new Vector3(42f, 3f, -40f), new Vector3(56f, 6f, 1f), _wall);
        MakeBox("Valla_Norte", new Vector3(42f, 3f, 40f), new Vector3(56f, 6f, 1f), _wall);

        MakeBox("Cobertura_Ext_1", new Vector3(28f, 1f, 9f), new Vector3(4f, 2f, 3f), _cover);
        MakeBox("Cobertura_Ext_2", new Vector3(38f, 1f, -10f), new Vector3(5f, 2f, 3f), _cover);
        MakeBox("Cobertura_Ext_3", new Vector3(52f, 1f, 6f), new Vector3(4f, 2f, 4f), _cover);

        AddLight(Vector3.zero, 1.2f);
        RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);
    }

    private static GameObject Spawn(GameObject prefab, Vector3 position, float yaw = 0f)
    {
        GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
    }

    private static void ConfigureOutdoorBot(GameObject bot)
    {
        BotAI ai = bot.GetComponent<BotAI>();
        if (ai == null) return;
        ai.useBounds = true;
        ai.minXZ = new Vector2(15.5f, -39f);
        ai.maxXZ = new Vector2(69f, 39f);

        // En el nivel 1 permanecen en silencio hasta que el jugador cruza la salida.
        ai.requiresExitDetection = true;
        ai.activationX = 15.5f;
        ai.fireRange = 36f;
        ai.fireInterval = 2.4f;
        ai.bulletSpeed = 30f;
    }

    /// <summary>Ajusta el soporte para que la diana no quede flotando.</summary>
    private static void AdjustStand(GameObject diana)
    {
        Transform stand = diana.transform.Find("Stand");
        if (stand == null) return;
        float height = diana.transform.position.y + 0.2f;
        stand.localScale = new Vector3(0.08f, height * 0.5f, 0.08f);
        stand.localPosition = new Vector3(0f, -height * 0.5f, 0f);
    }

    private static void SetupManager(LevelDefinition def)
    {
        GameObject go = PrefabUtility.InstantiatePrefab(_managerPrefab) as GameObject;
        GameManager gm = go.GetComponent<GameManager>();
        gm.levelDisplayName = def.displayName;
        gm.nextSceneName = def.nextSceneName;
        gm.startingAmmo = def.startingAmmo;
        gm.startingReserve = def.startingReserve;
        gm.totalTargets = def.totalTargets;
        gm.totalBots = def.totalBots;
        gm.hasBoss = def.hasBoss;
    }

    private static Scene NewScene(string name)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = name;
        return scene;
    }

    private static void SaveCurrentScene(string name)
    {
        string path = Root + "/Scenes/" + name + ".unity";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
        Debug.Log("[ShootingGallery] Escena guardada: " + path);
    }

    private static void CreateMenuScene()
    {
        NewScene("00 Menu Principal");
        MakeBox("Fondo", new Vector3(0f, 2f, 8f), new Vector3(60f, 20f, 1f), _wall);
        AddLight(Vector3.zero, 1f);
        GameObject camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 2f, -6f);
        new GameObject("MenuController").AddComponent<MenuController>();
        SaveCurrentScene("00 Menu Principal");
    }

    private static void CreateGalleryScene(LevelDefinition def)
    {
        NewScene("01 Galeria de disparos");
        BuildGalleryWorld();
        Spawn(_playerPrefab, new Vector3(0f, 0f, -11f));

        // Dianas en la pared del fondo de la sala.
        for (int i = 0; i < 10; i++)
        {
            int row = i / 5, col = i % 5;
            float x = -10f + col * 5f;
            float y = 2.1f + (col % 2) * 0.8f + row * 0.7f;
            GameObject diana = Spawn(_targetPrefab, new Vector3(x, y, 15.25f), 180f);
            AdjustStand(diana);
            diana.GetComponent<Target>().points = (col == 2) ? 150 : 100;
        }

        // Enemigos esparcidos por el campo exterior (se ven al salir por la derecha).
        Vector3[] outside =
        {
            new Vector3(24f, 0f, -12f), new Vector3(31f, 0f, 7f),
            new Vector3(40f, 0f, -19f), new Vector3(49f, 0f, 14f),
            new Vector3(58f, 0f, -4f)
        };
        foreach (Vector3 p in outside) ConfigureOutdoorBot(Spawn(_botPrefab, p, 180f));

        MakeBox("Cobertura_Sala_1", new Vector3(-11f, 0.8f, 2f), new Vector3(3f, 1.6f, 2f), _cover);
        MakeBox("Cobertura_Sala_2", new Vector3(11f, 0.8f, 2f), new Vector3(3f, 1.6f, 2f), _cover);
        SetupManager(def);
        SaveCurrentScene("01 Galeria de disparos");
    }

    private static void CreateBotsScene(LevelDefinition def)
    {
        NewScene("02 Bots");
        BuildArena();
        Spawn(_playerPrefab, new Vector3(0f, 0f, -16f));

        Vector3[] botPositions = {
            new Vector3(-10f, 0f, 8f), new Vector3(9f, 0f, 10f),
            new Vector3(-5f, 0f, 18f), new Vector3(6f, 0f, 22f),
            new Vector3(0f, 0f, 28f)
        };
        foreach (Vector3 p in botPositions) Spawn(_botPrefab, p, 180f);

        AdjustStand(Spawn(_targetPrefab, new Vector3(-12f, 2.6f, 30f), 180f));
        AdjustStand(Spawn(_targetPrefab, new Vector3(12f, 2.6f, 30f), 180f));

        MakeBox("Caja_1", new Vector3(-6f, 1f, 4f), new Vector3(4f, 2f, 3f), _cover);
        MakeBox("Caja_2", new Vector3(7f, 1f, 12f), new Vector3(4f, 2f, 3f), _cover);
        MakeBox("Caja_3", new Vector3(-2f, 1f, 22f), new Vector3(5f, 2f, 3f), _cover);
        SetupManager(def);
        SaveCurrentScene("02 Bots");
    }

    private static void CreateBossScene(LevelDefinition def)
    {
        NewScene("03 Jefe Final");
        BuildArena();
        Spawn(_playerPrefab, new Vector3(0f, 0f, -16f));
        Spawn(_bossPrefab, new Vector3(0f, 0f, 26f), 180f);
        Spawn(_botPrefab, new Vector3(-10f, 0f, 12f), 180f);
        Spawn(_botPrefab, new Vector3(10f, 0f, 12f), 180f);
        Spawn(_botPrefab, new Vector3(0f, 0f, 8f), 180f);
        AdjustStand(Spawn(_targetPrefab, new Vector3(-13f, 2.8f, 32f), 180f));
        AdjustStand(Spawn(_targetPrefab, new Vector3(13f, 2.8f, 32f), 180f));
        MakeBox("Cobertura_1", new Vector3(-9f, 1f, 4f), new Vector3(4f, 2f, 3f), _cover);
        MakeBox("Cobertura_2", new Vector3(9f, 1f, 4f), new Vector3(4f, 2f, 3f), _cover);
        SetupManager(def);
        SaveCurrentScene("03 Jefe Final");
    }

    /// <summary>Genera el ejecutable de Windows para subir a itch.io.</summary>
    [MenuItem("Shooting Gallery/Build Windows")]
    public static void BuildWindows()
    {
        string folder = "Builds/Windows";
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string[] names = { "00 Menu Principal", "01 Galeria de disparos", "02 Bots", "03 Jefe Final" };
        string[] scenes = new string[names.Length];
        for (int i = 0; i < names.Length; i++) scenes[i] = Root + "/Scenes/" + names[i] + ".unity";

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = folder + "/FPS.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[BUILD] resultado={report.summary.result} errores={report.summary.totalErrors} " +
                  $"warnings={report.summary.totalWarnings} tamano={report.summary.totalSize}");
    }

    /// <summary>Comprueba que las escenas tienen los componentes y referencias esperadas.</summary>
    [MenuItem("Shooting Gallery/Validar proyecto")]
    public static void Validate()
    {
        string[] names = { "00 Menu Principal", "01 Galeria de disparos", "02 Bots", "03 Jefe Final" };
        foreach (string name in names)
        {
            EditorSceneManager.OpenScene(Root + "/Scenes/" + name + ".unity", OpenSceneMode.Single);
            GameManager[] managers = Object.FindObjectsByType<GameManager>();
            Target[] targets = Object.FindObjectsByType<Target>();
            FirstPersonPlayer[] players = Object.FindObjectsByType<FirstPersonPlayer>();
            Weapon[] weapons = Object.FindObjectsByType<Weapon>();
            MenuController[] menus = Object.FindObjectsByType<MenuController>();
            BotAI[] bots = Object.FindObjectsByType<BotAI>();

            int withSoldier = 0, withAnimator = 0, bounded = 0, detecting = 0, ranged = 0;
            foreach (BotAI bot in bots)
            {
                if (bot.transform.Find("Soldier_Model") != null) withSoldier++;
                if (bot.GetComponentInChildren<Animator>() != null) withAnimator++;
                if (bot.useBounds) bounded++;
                if (bot.requiresExitDetection) detecting++;
                if (bot.enemyBulletPrefab != null && bot.muzzle != null) ranged++;
            }

            Debug.Log($"[VALIDATE] {name}: GM={managers.Length} targets={targets.Length} " +
                      $"players={players.Length} weapons={weapons.Length} menus={menus.Length} " +
                      $"bots={bots.Length} soldados={withSoldier} anim={withAnimator} " +
                      $"acotados={bounded} detectanSalida={detecting} armados={ranged}");
            foreach (Weapon w in weapons)
            {
                Debug.Log($"[VALIDATE]   arma: bala={(w.bulletPrefab != null)} " +
                          $"fogonazo={(w.muzzleFlashPrefab != null)} recarga={(w.reloadClip != null)} " +
                          $"audio2D={(w.sfxSource != null)} pivote={(w.weaponPivot != null)} " +
                          $"camara={(w.aimCamera != null)} slots={(w.slots == null ? 0 : w.slots.Length)}");
                if (w.slots != null)
                    foreach (WeaponSlot slot in w.slots)
                        Debug.Log($"[VALIDATE]     {slot.displayName}: modelo={(slot.model != null)} " +
                                  $"bocacha={(slot.muzzle != null)} sonido={(slot.fireClip != null)} " +
                                  $"cargador={slot.magazineSize} reserva={slot.reserve}");
            }
            foreach (GameManager gm in managers)
            {
                Debug.Log($"[VALIDATE]   nivel: cargador={gm.startingAmmo} reserva={gm.startingReserve} " +
                          $"dianas={gm.totalTargets} bots={gm.totalBots} boss={gm.hasBoss} " +
                          $"siguiente='{gm.nextSceneName}'");
            }
        }
    }

    private static void SetBuildSettings()
    {
        string[] names = { "00 Menu Principal", "01 Galeria de disparos", "02 Bots", "03 Jefe Final" };
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            scenes[i] = new EditorBuildSettingsScene(
                Root + "/Scenes/" + names[i] + ".unity", true);
        }
        EditorBuildSettings.scenes = scenes;
        Debug.Log("[ShootingGallery] Build Settings actualizado con " + scenes.Length + " escenas.");
    }
}
