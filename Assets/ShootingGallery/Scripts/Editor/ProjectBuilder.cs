using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera todo el contenido del juego dentro del proyecto del alumno:
/// materiales, prefabs, definiciones de nivel y las 4 escenas.
/// Menu: Shooting Gallery > Generar proyecto.
/// Tambien se puede llamar desde batch mode con -executeMethod ProjectBuilder.BuildAll
/// </summary>
public static class ProjectBuilder
{
    private const string Root = "Assets/ShootingGallery";

    private static Material _floor, _wall, _cover, _targetMat, _botMat, _bossMat,
        _bulletMat, _enemyBulletMat, _weaponMat;
    private static GameObject _playerPrefab, _targetPrefab, _botPrefab, _bossPrefab,
        _bulletPrefab, _enemyBulletPrefab, _managerPrefab;
    private static AudioClip _gun, _hit, _hurt, _win, _lose;

    [MenuItem("Shooting Gallery/Generar proyecto (escenas + prefabs)")]
    public static void BuildAll()
    {
        EnsureFolders();
        LoadAssets();
        CreateMaterials();
        CreatePrefabs();
        LevelDefinition gallery = CreateLevel("01 Galeria", "01 Galería de disparos",
            "01 Galeria de disparos", "02 Bots", 14, 10, 0, false);
        LevelDefinition bots = CreateLevel("02 Bots", "02 Modo contra bots",
            "02 Bots", "03 Jefe Final", 22, 2, 5, false);
        LevelDefinition boss = CreateLevel("03 Jefe", "03 Jefe final",
            "03 Jefe Final", "", 30, 2, 3, true);

        CreateMenuScene();
        CreateGalleryScene(gallery);
        CreateBotsScene(bots);
        CreateBossScene(boss);
        SetBuildSettings();
        Validate();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ShootingGallery] Generacion completada: 4 escenas, prefabs y materiales listos.");
    }

    // ------------------------------------------------------------ carpetas ---
    private static void EnsureFolders()
    {
        string[] folders = { "Art", "Audio", "Materials", "Prefabs", "Scenes", "Levels" };
        foreach (string folder in folders)
        {
            string path = Root + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Root, folder);
        }
    }

    private static void LoadAssets()
    {
        _targetMat = null; // se crea en CreateMaterials a partir de la textura
        _gun = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/FSX/Gunfire.wav");
        _hit = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/hit.wav");
        _hurt = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/hurt.wav");
        _win = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/win.wav");
        _lose = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/lose.wav");
        Debug.Log("[ShootingGallery] Gunfire.wav encontrado: " + (_gun != null));
    }

    // ----------------------------------------------------------- materiales ---
    private static Material MakeMaterial(string name, Color color, Texture2D texture = null)
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
        if (texture != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            mat.mainTexture = texture;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void CreateMaterials()
    {
        Texture2D targetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/target.png");
        Texture2D botTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/enemy_x.png");
        Texture2D bossTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/boss_x.png");

        _floor = MakeMaterial("Floor", new Color(0.16f, 0.17f, 0.19f));
        _wall = MakeMaterial("Wall", new Color(0.26f, 0.27f, 0.30f));
        _cover = MakeMaterial("Cover", new Color(0.34f, 0.33f, 0.28f));
        _targetMat = MakeMaterial("Target_Bullseye", Color.white, targetTex);
        _botMat = MakeMaterial("Target_BotX", Color.white, botTex);
        _bossMat = MakeMaterial("Target_BossX", Color.white, bossTex);
        _bulletMat = MakeMaterial("Bullet", new Color(1f, 0.85f, 0.25f));
        _enemyBulletMat = MakeMaterial("EnemyBullet", new Color(1f, 0.25f, 0.15f));
        _weaponMat = MakeMaterial("WeaponFallback", new Color(0.18f, 0.19f, 0.21f));
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

    private static void CreatePrefabs()
    {
        // --- Bala del jugador
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "Bullet_Player";
        bullet.transform.localScale = Vector3.one * 0.13f;
        SetMaterial(bullet, _bulletMat);
        Object.DestroyImmediate(bullet.GetComponent<Collider>());
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
        Projectile pe = eBullet.AddComponent<Projectile>();
        pe.owner = Projectile.Owner.Enemy;
        pe.speed = 30f;
        pe.damage = 1;
        _enemyBulletPrefab = SavePrefab(eBullet, "Bullet_Enemy");

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

        // --- Bot (placeholder X)
        _botPrefab = CreateCharacterPrefab("Bot_X", _botMat, 1f,
            Target.TargetType.Bot, 250, 2, false);

        // --- Jefe (placeholder X grande)
        _bossPrefab = CreateCharacterPrefab("Boss_X", _bossMat, 1.9f,
            Target.TargetType.Boss, 1500, 12, true);

        // --- Jugador
        CreatePlayerPrefab();

        // --- GameManager
        GameObject manager = new GameObject("GameManager");
        manager.AddComponent<GameManager>();
        GameManager gm = manager.GetComponent<GameManager>();
        gm.hitClip = _hit;
        gm.hurtClip = _hurt;
        gm.winClip = _win;
        gm.loseClip = _lose;
        _managerPrefab = SavePrefab(manager, "GameManager");
    }

    private static GameObject CreateCharacterPrefab(string name, Material mat, float scale,
        Target.TargetType type, int points, int hp, bool isBoss)
    {
        GameObject root = new GameObject(name);
        root.transform.localScale = Vector3.one * scale;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        SetMaterial(body, mat);

        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = "Mark_X";
        mark.transform.SetParent(root.transform, false);
        mark.transform.localPosition = new Vector3(0f, 1.15f, 0.45f);
        mark.transform.localScale = new Vector3(0.9f, 0.9f, 0.06f);
        SetMaterial(mark, mat);
        Object.DestroyImmediate(mark.GetComponent<Collider>());

        Target target = root.AddComponent<Target>();
        target.type = type;
        target.points = points;
        target.hp = hp;
        target.hitClip = _hit;

        if (isBoss)
        {
            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 1.6f, 0.6f);
            BossAI boss = root.AddComponent<BossAI>();
            boss.muzzle = muzzle.transform;
            boss.enemyBulletPrefab = _enemyBulletPrefab;
        }
        else
        {
            root.AddComponent<BotAI>();
        }

        return SavePrefab(root, name);
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

        // Modelo de arma: usa el AR del paquete importado si existe; si no, cubos.
        GameObject weapon = null;
        GameObject arPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Low Poly AR Weapon Pack 3/Prefabs/Weapons/AR_L.prefab");
        if (arPrefab != null)
        {
            weapon = PrefabUtility.InstantiatePrefab(arPrefab) as GameObject;
            weapon.transform.SetParent(camGo.transform, false);
            weapon.transform.localPosition = new Vector3(0.30f, -0.30f, 0.55f);
            weapon.transform.localRotation = Quaternion.Euler(0f, -6f, 0f);
            weapon.transform.localScale = Vector3.one * 0.55f;
        }
        else
        {
            weapon = new GameObject("WeaponFallback");
            weapon.transform.SetParent(camGo.transform, false);
            weapon.transform.localPosition = new Vector3(0.3f, -0.3f, 0.6f);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(weapon.transform, false);
            body.transform.localScale = new Vector3(0.12f, 0.14f, 0.6f);
            SetMaterial(body, _weaponMat);
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.transform.SetParent(weapon.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, 0.42f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f);
            SetMaterial(barrel, _weaponMat);
        }

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(camGo.transform, false);
        muzzle.transform.localPosition = new Vector3(0.28f, -0.25f, 1f);

        FirstPersonPlayer fpp = root.AddComponent<FirstPersonPlayer>();
        fpp.viewCamera = cam;
        Weapon wp = root.AddComponent<Weapon>();
        wp.aimCamera = cam;
        wp.muzzle = muzzle.transform;
        wp.bulletPrefab = _bulletPrefab;
        wp.fireClip = _gun;

        _playerPrefab = SavePrefab(root, "Player_FPS");
    }

    // -------------------------------------------------------------- niveles ---
    private static LevelDefinition CreateLevel(string file, string display, string scene,
        string next, int ammo, int targets, int bots, bool boss)
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

    private static void BuildRoom()
    {
        MakeBox("Suelo", new Vector3(0f, -0.5f, 12f), new Vector3(46f, 1f, 80f), _floor);
        MakeBox("Pared_izquierda", new Vector3(-22f, 3f, 12f), new Vector3(1f, 6f, 80f), _wall);
        MakeBox("Pared_derecha", new Vector3(22f, 3f, 12f), new Vector3(1f, 6f, 80f), _wall);
        MakeBox("Pared_fondo", new Vector3(0f, 3f, 51f), new Vector3(45f, 6f, 1f), _wall);
        MakeBox("Pared_inicio", new Vector3(0f, 3f, -27f), new Vector3(45f, 6f, 1f), _wall);

        GameObject lightGo = new GameObject("Luz_principal");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

        RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);
    }

    private static GameObject Spawn(GameObject prefab, Vector3 position, float yaw = 0f)
    {
        GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
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
        GameObject lightGo = new GameObject("Luz");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightGo.transform.rotation = Quaternion.Euler(40f, -20f, 0f);
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
        BuildRoom();
        Spawn(_playerPrefab, new Vector3(0f, 0f, -16f));

        for (int i = 0; i < 10; i++)
        {
            int row = i / 5, col = i % 5;
            float x = -8f + col * 4f;
            float z = 8f + row * 8f;
            float y = 2.2f + (col % 2) * 0.7f + row * 0.5f;
            GameObject diana = Spawn(_targetPrefab, new Vector3(x, y, z), 180f);
            AdjustStand(diana);
            diana.GetComponent<Target>().points = (col == 2) ? 150 : 100;
        }
        MakeBox("Cobertura_1", new Vector3(-11f, 0.8f, 4f), new Vector3(3f, 1.6f, 2f), _cover);
        MakeBox("Cobertura_2", new Vector3(11f, 0.8f, 4f), new Vector3(3f, 1.6f, 2f), _cover);
        SetupManager(def);
        SaveCurrentScene("01 Galeria de disparos");
    }

    private static void CreateBotsScene(LevelDefinition def)
    {
        NewScene("02 Bots");
        BuildRoom();
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
        BuildRoom();
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
            GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None);
            Target[] targets = Object.FindObjectsByType<Target>(FindObjectsSortMode.None);
            FirstPersonPlayer[] players = Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None);
            Weapon[] weapons = Object.FindObjectsByType<Weapon>(FindObjectsSortMode.None);
            MenuController[] menus = Object.FindObjectsByType<MenuController>(FindObjectsSortMode.None);

            Debug.Log($"[VALIDATE] {name}: GM={managers.Length} targets={targets.Length} " +
                      $"players={players.Length} weapons={weapons.Length} menus={menus.Length}");
            foreach (Weapon w in weapons)
            {
                Debug.Log($"[VALIDATE]   arma: bala={(w.bulletPrefab != null)} " +
                          $"sonido={(w.fireClip != null)} muzzle={(w.muzzle != null)} " +
                          $"camara={(w.aimCamera != null)}");
            }
            foreach (GameManager gm in managers)
            {
                Debug.Log($"[VALIDATE]   nivel: mun={gm.startingAmmo} dianas={gm.totalTargets} " +
                          $"bots={gm.totalBots} boss={gm.hasBoss} siguiente='{gm.nextSceneName}'");
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
