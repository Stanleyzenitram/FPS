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
    private const string ArmsModel = "Assets/fp-arms/source/arms.fbx";
    private const string ArmsColor = "Assets/fp-arms/textures/armColor.png";
    private const string ArmsNormal = "Assets/fp-arms/textures/armNormal.png";
    private const string ArmsRoughness = "Assets/fp-arms/textures/armRoughness.png";
    private const string ArmsOcclusion = "Assets/fp-arms/textures/armAO.png";
    private const string BulletPlayerModel = "Assets/Low Poly AR Weapon Pack 3/Prefabs/Bullets/Bullet_A.prefab";
    private const string BulletEnemyModel = "Assets/Low Poly AR Weapon Pack 3/Prefabs/Bullets/Bullet_E.prefab";
    private const string TempleRoot = "Assets/Sun_Temple/";
    private const string TemplePrefabsRoot = TempleRoot + "Prefabs/";

    private static Material _floor, _wall, _cover, _outdoor, _exit, _targetMat, _bulletMat,
        _enemyBulletMat, _weaponMat, _flashMat, _trailMat, _weaponArMat, _weaponM9Mat,
        _sleeveMat, _gloveMat, _armsMat, _sectionA, _sectionB, _sectionC;
    private static GameObject _playerPrefab, _targetPrefab, _botPrefab, _bossPrefab,
        _bulletPrefab, _enemyBulletPrefab, _managerPrefab, _muzzleFlashPrefab;
    private static AudioClip _gun, _hit, _hurt, _win, _lose, _reload;
    private static Texture2D _arTexture, _m9Texture, _soldierTexture,
        _armsColor, _armsNormal, _armsRoughness, _armsOcclusion;
    private static Font _hudFont;
    private static Texture2D _healthIcon, _ammoIcon, _hitmarkerIcon;
    private static GameObject _soldierAsset, _armsAsset, _bulletModel, _enemyBulletModel;
    private static GameObject _pavement, _solidWall, _fullWall, _decoColumn, _doorFrame,
        _tower, _crate, _barrel, _stone, _bench, _torch;
    private static Avatar _soldierAvatar;
    private static RuntimeAnimatorController _soldierController;

    [MenuItem("Shooting Gallery/Generar proyecto (escenas + prefabs)")]
    public static void BuildAll()
    {
        EnsureFolders();
        LoadAssets();
        CreateMaterials();
        FixTempleMaterials();
        CreateSoldierAnimator();
        CreatePrefabs();

        LevelDefinition gallery = CreateLevel("01 Galeria", "01 Galería de disparos",
            "01 Galeria de disparos", "02 Bots", 14, 42, 12, 5, false);
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
        string[] folders =
        {
            "Art", "Art/UI", "Audio", "Materials", "Prefabs", "Scenes", "Levels", "Animations", "Fonts"
        };
        foreach (string folder in folders)
        {
            string[] parts = folder.Split('/');
            string parent = Root;
            foreach (string part in parts)
            {
                string path = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, part);
                parent = path;
            }
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
        _soldierTexture = ConfigureTexture(
            "Assets/LowPolySoldiers_demo/models/Materials/demo_soldier_512.tga");

        // Manos proporcionadas por el usuario: color sRGB, normal y mapas lineales.
        _armsColor = ConfigureTexture(ArmsColor);
        _armsNormal = ConfigureNormalTexture(ArmsNormal);
        _armsRoughness = ConfigureLinearTexture(ArmsRoughness);
        _armsOcclusion = ConfigureLinearTexture(ArmsOcclusion);
        _armsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsModel);
        _bulletModel = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPlayerModel);
        _enemyBulletModel = AssetDatabase.LoadAssetAtPath<GameObject>(BulletEnemyModel);

        // Modulos del mapa Sun_Temple usados en las tres escenas.
        _pavement = TempleAsset("Modules_Structs/Struct_Pavement_A.prefab");
        _solidWall = TempleAsset("Modules_Temple/Bld_TempleWall_Solid_Mid_A.prefab");
        _fullWall = TempleAsset("Modules_Structs/Struct_Fullwall_Mid_A.prefab");
        _decoColumn = TempleAsset("Modules_Common/Bld_Deco_Column_A.prefab");
        _doorFrame = TempleAsset("Modules_Temple/Bld_Temple_DoorFrame_A.prefab");
        _tower = TempleAsset("Buildings_Prefabbed/Bld_TempleTower_A.prefab");
        _crate = TempleAsset("Props/Prop_Crate_A.prefab");
        _barrel = TempleAsset("Props/Prop_Barrel_A.prefab");
        _stone = TempleAsset("Natural/Prop_Stone_Med_A.prefab");
        _bench = TempleAsset("Props/Prop_StoneBench_A.prefab");
        _torch = TempleAsset("Props/Prop_FireTorch_A.prefab");

        // Assets de HUD opcionales: basta con dejarlos en estas carpetas y regenerar.
        _hudFont = LoadFirstAsset<Font>(Root + "/Fonts");
        _healthIcon = LoadUiTexture("health", "vida", "salud");
        _ammoIcon = LoadUiTexture("ammo", "municion", "bullet");
        _hitmarkerIcon = LoadUiTexture("hitmarker", "impacto", "hit");

        _soldierAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPrefab);
        Object[] modelAssets = AssetDatabase.LoadAllAssetsAtPath(SoldierModel);
        foreach (Object asset in modelAssets)
        {
            if (asset is Avatar avatar) { _soldierAvatar = avatar; break; }
        }
        Debug.Log($"[ShootingGallery] Soldado personal={( _soldierAsset != null)} " +
                  $"manos={(_armsAsset != null)} avatar={(_soldierAvatar != null)} " +
                  $"sonido={(_gun != null)} recarga={(_reload != null)} " +
                  $"balas={(_bulletModel != null && _enemyBulletModel != null)} " +
                  $"temple={(_pavement != null && _solidWall != null && _tower != null)} " +
                  $"texturas={( _arTexture != null && _m9Texture != null)} " +
                  $"hud={( _hudFont != null ? "fuente" : "por defecto")}/" +
                  $"{((_healthIcon != null ? 1 : 0) + (_ammoIcon != null ? 1 : 0) + (_hitmarkerIcon != null ? 1 : 0))}iconos");
    }

    private static GameObject TempleAsset(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(TemplePrefabsRoot + relativePath);
    }

    /// <summary>Devuelve el primer asset de un tipo dentro de una carpeta.</summary>
    private static T LoadFirstAsset<T>(string folder) where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    /// <summary>Busca un icono de UI por palabra clave en su nombre de archivo.</summary>
    private static Texture2D LoadUiTexture(params string[] keywords)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Art/UI" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            foreach (string keyword in keywords)
            {
                if (name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }
        return null;
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

    /// <summary>Importa el normal map como datos, no como color.</summary>
    private static Texture2D ConfigureNormalTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = importer.textureType != TextureImporterType.NormalMap ||
                           importer.sRGBTexture || importer.maxTextureSize < 2048;
            if (changed)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Importa roughness/AO como datos lineales.</summary>
    private static Texture2D ConfigureLinearTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = importer.textureType != TextureImporterType.Default ||
                           importer.sRGBTexture || importer.maxTextureSize < 2048;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
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
        _sectionA = MakeMaterial("Section_Alfa", new Color(0.16f, 0.34f, 0.50f));
        _sectionB = MakeMaterial("Section_Bravo", new Color(0.44f, 0.30f, 0.16f));
        _sectionC = MakeMaterial("Section_Charlie", new Color(0.20f, 0.42f, 0.28f));
        _targetMat = MakeTexturedMaterial("Target_Bullseye", Color.white, targetTex);
        _bulletMat = MakeMaterial("Bullet", new Color(1f, 0.85f, 0.25f));
        _enemyBulletMat = MakeMaterial("EnemyBullet", new Color(1f, 0.25f, 0.15f));
        _weaponMat = MakeMaterial("WeaponFallback", new Color(0.18f, 0.19f, 0.21f));
        _weaponArMat = MakeWeaponMaterial("Weapon_AR_Textured", _arTexture);
        _weaponM9Mat = MakeWeaponMaterial("Weapon_M9_Textured", _m9Texture);
        _sleeveMat = MakeTexturedMaterial("Arms_Sleeve", Color.white, _soldierTexture);
        _gloveMat = MakeMaterial("Arms_Glove", new Color(0.07f, 0.08f, 0.07f));
        _armsMat = MakeArmsMaterial();
        _flashMat = MakeFlashMaterial();
        _trailMat = MakeTrailMaterial();
    }

    /// <summary>
    /// Los materiales del asset Sun_Temple vinieron actualizados a URP Simple Lit
    /// pero sin el mapa de color: por eso se veia todo blanco. Recuperamos la
    /// textura de albedo que correspondia a cada material original.
    /// </summary>
    private static void FixTempleMaterials()
    {
        string matFolder = TempleRoot + "Content/Meshes/Main/Materials/";
        string texFolder = TempleRoot + "Content/Textures/";
        string[] pairs =
        {
            "Pavement_A|pavementA_tiling_A.tiff",
            "Bricks_Plain|bricks_plain_A.tif",
            "Bricks_Solid|bricks_solid_A.tiff",
            "Roof_Clay|roof_clay_A.tif",
            "Plaster_Trims|plaster_trim_A.tif",
            "Wood_Glazed_Trim|wood_glazed_trim_A.tiff",
            "Wood_Glazed_Tiling|wood_tiling_A.tif",
            "Wood_Glazed_Tiling_Rotten|wood_tiling_rotten_A.tif",
            "Temple_Trims|templetrims_A.tiff",
            "Metal_Plain_Trim|metal_plain_trim_A.tiff",
            "PropStones|prop_stones_A.tif"
        };

        int fixedCount = 0;
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split('|');
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matFolder + parts[0] + ".mat");
            Texture2D tex = ConfigureTexture(texFolder + parts[1]);
            if (mat == null || tex == null) continue;

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(mat);
            fixedCount++;
        }

        // HLP_Red es un material de ayuda del asset: no tiene textura, le damos color.
        Material red = AssetDatabase.LoadAssetAtPath<Material>(matFolder + "HLP_Red.mat");
        if (red != null)
        {
            Color marker = new Color(0.62f, 0.12f, 0.10f, 1f);
            if (red.HasProperty("_BaseColor")) red.SetColor("_BaseColor", marker);
            if (red.HasProperty("_Color")) red.SetColor("_Color", marker);
            EditorUtility.SetDirty(red);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ShootingGallery] Materiales Sun_Temple con textura: {fixedCount}/{pairs.Length}");
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

    /// <summary>Material PBR de las manos importadas con color, normal, AO y roughness.</summary>
    private static Material MakeArmsMaterial()
    {
        Material mat = MakeMaterial("Arms_Imported", Color.white);
        if (_armsColor != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _armsColor);
            mat.mainTexture = _armsColor;
        }
        if (_armsNormal != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", _armsNormal);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (_armsOcclusion != null && mat.HasProperty("_OcclusionMap"))
        {
            mat.SetTexture("_OcclusionMap", _armsOcclusion);
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_OCCLUSIONMAP");
        }
        if (_armsRoughness != null && mat.HasProperty("_MetallicGlossMap"))
        {
            mat.SetTexture("_MetallicGlossMap", _armsRoughness);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
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

    /// <summary>Busca un hueso por nombre en cualquier profundidad del modelo.</summary>
    private static Transform FindDeepChild(GameObject root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private static Transform[] CollectFingerBones(Transform wrist)
    {
        if (wrist == null) return new Transform[0];
        System.Collections.Generic.List<Transform> bones = new System.Collections.Generic.List<Transform>();
        string[] keys = { "finger", "thumb", "index", "middle", "ring", "pinky" };
        foreach (Transform bone in wrist.GetComponentsInChildren<Transform>(true))
        {
            if (bone == wrist) continue;
            string name = bone.name.ToLowerInvariant();
            foreach (string key in keys)
            {
                if (name.Contains(key)) { bones.Add(bone); break; }
            }
        }
        return bones.ToArray();
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

    /// <summary>
    /// Crea un proyectil con el modelo importado como hijo visual. El GameObject
    /// raiz sigue llevando Projectile y TrailRenderer, por lo que los impactos
    /// barridos no cambian: solo cambia la malla que se ve.
    /// </summary>
    private static GameObject CreateBullet(GameObject model, Material fallback,
        float modelScale, float trailTime, float trailWidth, string name)
    {
        GameObject root = new GameObject(name);

        if (model != null)
        {
            GameObject visual = PrefabUtility.InstantiatePrefab(model) as GameObject;
            visual.name = "BulletModel";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * modelScale;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer.sharedMaterial == null) renderer.sharedMaterial = fallback;
            }
        }
        else
        {
            GameObject fallbackBullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallbackBullet.name = "BulletModel";
            fallbackBullet.transform.SetParent(root.transform, false);
            fallbackBullet.transform.localScale = Vector3.one * modelScale * 0.12f;
            SetMaterial(fallbackBullet, fallback);
            Object.DestroyImmediate(fallbackBullet.GetComponent<Collider>());
        }

        SetupBulletTrail(root, trailTime, trailWidth);
        return root;
    }

    private static void CreatePrefabs()
    {
        // --- Bala del jugador (malla importada en vez de una esfera)
        GameObject bullet = CreateBullet(_bulletModel, _bulletMat, 8f, 0.2f, 0.065f, "Bullet_Player");
        Projectile pb = bullet.AddComponent<Projectile>();
        pb.owner = Projectile.Owner.Player;
        pb.speed = 95f;
        pb.damage = 1;
        _bulletPrefab = SavePrefab(bullet, "Bullet_Player");

        // --- Bala enemiga (otro modelo del paquete, mas grande y lenta)
        GameObject eBullet = CreateBullet(_enemyBulletModel, _enemyBulletMat, 10f, 0.28f, 0.1f, "Bullet_Enemy");
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
        gm.hudFont = _hudFont;
        gm.healthIcon = _healthIcon;
        gm.ammoIcon = _ammoIcon;
        gm.hitmarkerIcon = _hitmarkerIcon;
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

    /// <summary>Parte de brazo sin collider ni sombras para que nunca estorbe al disparo.</summary>
    private static GameObject CreateArmPart(string name, PrimitiveType type, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        SetMaterial(go, mat);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
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

        // --- Manos importadas (modelo del usuario), con brazos procedurales de respaldo.
        Transform rightForearm = null, rightHand = null, leftForearm = null, leftHand = null;
        Transform handsRoot = null, leftArmBone = null, leftElbowBone = null, leftWristBone = null;
        Transform rightArmBone = null, rightElbowBone = null, rightWristBone = null;

        if (_armsAsset != null)
        {
            GameObject handsModel = PrefabUtility.InstantiatePrefab(_armsAsset) as GameObject;
            handsModel.name = "FirstPersonHands";
            handsModel.transform.SetParent(pivot.transform, false);
            handsModel.transform.localPosition = new Vector3(0.30f, -0.30f, 0.20f);
            handsModel.transform.localRotation = Quaternion.identity;
            handsModel.transform.localScale = Vector3.one;
            ApplyWeaponMaterial(handsModel, _armsMat);
            foreach (Renderer renderer in handsModel.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            foreach (Collider collider in handsModel.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            handsRoot = handsModel.transform;
            leftArmBone = FindDeepChild(handsModel, "L_arm");
            leftElbowBone = FindDeepChild(handsModel, "L_elbow");
            leftWristBone = FindDeepChild(handsModel, "L_wrist");
            rightArmBone = FindDeepChild(handsModel, "R_arm");
            rightElbowBone = FindDeepChild(handsModel, "R_elbow");
            rightWristBone = FindDeepChild(handsModel, "R_wrist");
        }
        else
        {
            GameObject rightForearmGo = CreateArmPart(
                "Arm_Right_Forearm", PrimitiveType.Capsule, _sleeveMat, pivot.transform);
            GameObject rightHandGo = CreateArmPart(
                "Arm_Right_Hand", PrimitiveType.Sphere, _gloveMat, pivot.transform);
            GameObject leftForearmGo = CreateArmPart(
                "Arm_Left_Forearm", PrimitiveType.Capsule, _sleeveMat, pivot.transform);
            GameObject leftHandGo = CreateArmPart(
                "Arm_Left_Hand", PrimitiveType.Sphere, _gloveMat, pivot.transform);
            rightHandGo.transform.localScale = Vector3.one * 0.12f;
            leftHandGo.transform.localScale = Vector3.one * 0.11f;
            rightForearm = rightForearmGo.transform;
            rightHand = rightHandGo.transform;
            leftForearm = leftForearmGo.transform;
            leftHand = leftHandGo.transform;
        }

        GameObject reloadMag = CreateArmPart(
            "Reload_Magazine", PrimitiveType.Cube, _weaponMat, pivot.transform);
        reloadMag.transform.localScale = new Vector3(0.075f, 0.16f, 0.055f);
        reloadMag.SetActive(false);

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

        FirstPersonArms arms = pivot.AddComponent<FirstPersonArms>();
        arms.weapon = wp;
        arms.rightForearm = rightForearm;
        arms.rightHand = rightHand;
        arms.leftForearm = leftForearm;
        arms.leftHand = leftHand;
        arms.magazineProp = reloadMag;
        arms.handsRoot = handsRoot;
        arms.leftArmBone = leftArmBone;
        arms.leftElbowBone = leftElbowBone;
        arms.leftWristBone = leftWristBone;
        arms.rightArmBone = rightArmBone;
        arms.rightElbowBone = rightElbowBone;
        arms.rightWristBone = rightWristBone;
        arms.leftFingerBones = CollectFingerBones(leftWristBone);
        arms.rightFingerBones = CollectFingerBones(rightWristBone);

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

    /// <summary>Caja que solo colisiona: la vista la dan los prefabs del templo.</summary>
    private static GameObject MakeCollider(string name, Vector3 pos, Vector3 scale)
    {
        GameObject go = MakeBox(name, pos, scale, _wall);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        return go;
    }

    /// <summary>Panel decorativo que no debe detener balas: ya hay una pared detras.</summary>
    private static GameObject MakeVisualBox(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = MakeBox(name, pos, scale, mat);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        return go;
    }

    private static void AddLight(Vector3 position, float intensity)
    {
        GameObject lightGo = new GameObject("Luz");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = new Color(1f, 0.91f, 0.76f);
        lightGo.transform.position = position;
        lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        RenderSettings.ambientLight = new Color(0.48f, 0.46f, 0.40f);
    }

    /// <summary>Instancia un modulo de Sun_Temple solo como decoracion.</summary>
    private static GameObject PlaceTemple(GameObject prefab, string name, Vector3 pos,
        float yaw = 0f, Vector3? scale = null)
    {
        if (prefab == null) return null;
        GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        go.name = name;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (scale.HasValue) go.transform.localScale = scale.Value;
        foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        return go;
    }

    /// <summary>
    /// Marco de la salida: gira 90 grados para quedar en el muro lateral y
    /// recupera colision en jambas y dintel. El hueco central sigue pasable.
    /// </summary>
    private static void PlaceExitDoor()
    {
        const float x = 14.9f;
        const float z = -1f;
        GameObject door = PlaceTemple(_doorFrame, "Marco_Salida_Temple",
            new Vector3(x, 0f, z), 90f, new Vector3(1f, 0.75f, 1.8f));
        if (door == null) return;

        GameObject jambSur = MakeCollider("Col_Marco_Salida_Jamb_Sur",
            new Vector3(x, 2.5f, z - 1.4f), new Vector3(0.62f, 5f, 0.4f));
        GameObject jambNorte = MakeCollider("Col_Marco_Salida_Jamb_Norte",
            new Vector3(x, 2.5f, z + 1.4f), new Vector3(0.62f, 5f, 0.4f));
        GameObject dintel = MakeCollider("Col_Marco_Salida_Dintel",
            new Vector3(x, 5.45f, z), new Vector3(0.62f, 0.5f, 3.2f));

        jambSur.transform.SetParent(door.transform, true);
        jambNorte.transform.SetParent(door.transform, true);
        dintel.transform.SetParent(door.transform, true);
    }

    private static void TilePavement(float minX, float maxX, float minZ, float maxZ, string prefix)
    {
        if (_pavement == null) return;
        int columns = Mathf.Max(1, Mathf.RoundToInt((maxX - minX) / 10f));
        int rows = Mathf.Max(1, Mathf.RoundToInt((maxZ - minZ) / 10f));
        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                Vector3 pos = new Vector3(minX + 5f + x * 10f, -0.01f, minZ + 5f + z * 10f);
                PlaceTemple(_pavement, $"{prefix}_{x}_{z}", pos);
            }
        }
    }

    /// <summary>Muro decorativo del templo; las colisiones las ponen los MakeCollider.</summary>
    private static void PlaceWall(string name, GameObject prefab, Vector3 pos, float yaw, Vector3 scale)
    {
        PlaceTemple(prefab, name, pos, yaw, scale);
    }

    /// <summary>Cobertura importada con su colision invisible del tamano de juego.</summary>
    private static void PlaceCrateCover(string name, Vector3 pos, Vector3 size, float yaw = 0f)
    {
        MakeCollider("Col_" + name, new Vector3(pos.x, size.y * 0.5f, pos.z), size);
        PlaceTemple(_crate, name + "_Caja", new Vector3(pos.x - size.x * 0.22f, 0f, pos.z + size.z * 0.18f),
            yaw, new Vector3(size.x * 0.58f, size.y, size.z * 0.62f));
        PlaceTemple(_barrel, name + "_Barril", new Vector3(pos.x + size.x * 0.24f, 0f, pos.z - size.z * 0.20f),
            yaw + 35f, new Vector3(size.x * 0.7f, size.y * 0.95f, size.z * 0.7f));
    }

    private static void PlaceTempleDecor(Vector3 center, float halfWidth, float halfDepth)
    {
        PlaceTemple(_stone, "Piedra_Deco", new Vector3(center.x - halfWidth * 0.7f, 0f, center.z - halfDepth * 0.6f),
            24f, Vector3.one * 1.2f);
        PlaceTemple(_stone, "Piedra_Deco_2", new Vector3(center.x + halfWidth * 0.7f, 0f, center.z + halfDepth * 0.5f),
            -18f, Vector3.one * 0.9f);
        PlaceTemple(_bench, "Banco_Deco", new Vector3(center.x - halfWidth * 0.5f, 0f, center.z + halfDepth * 0.45f), 90f);
        PlaceTemple(_torch, "Antorcha_Deco", new Vector3(center.x + halfWidth * 0.6f, 0f, center.z - halfDepth * 0.5f));
    }

    /// <summary>Recinto cerrado usado por los niveles 2 y 3.</summary>
    private static void BuildArena()
    {
        TilePavement(-22f, 22f, -27f, 51f, "Arena_Pavimento");

        MakeCollider("Col_Suelo", new Vector3(0f, -0.5f, 12f), new Vector3(46f, 1f, 80f));
        MakeCollider("Col_Pared_izquierda", new Vector3(-22f, 3f, 12f), new Vector3(1f, 6f, 80f));
        MakeCollider("Col_Pared_derecha", new Vector3(22f, 3f, 12f), new Vector3(1f, 6f, 80f));
        MakeCollider("Col_Pared_fondo", new Vector3(0f, 3f, 51f), new Vector3(45f, 6f, 1f));
        MakeCollider("Col_Pared_inicio", new Vector3(0f, 3f, -27f), new Vector3(45f, 6f, 1f));

        Vector3 wallScale = new Vector3(1f, 0.5f, 0.5f);
        for (float x = -22f; x <= 18f; x += 10f)
        {
            PlaceWall("Muro_Temple_Fondo_" + x, _solidWall, new Vector3(x, 0f, 51f), 0f, wallScale);
            PlaceWall("Muro_Temple_Inicio_" + x, _solidWall, new Vector3(x, 0f, -27f), 0f, wallScale);
        }
        for (float z = -17f; z <= 53f; z += 10f)
        {
            PlaceWall("Muro_Temple_Izq_" + z, _solidWall, new Vector3(-22f, 0f, z), 90f, wallScale);
            PlaceWall("Muro_Temple_Der_" + z, _solidWall, new Vector3(22f, 0f, z), 90f, wallScale);
        }

        float[] columnZ = { -12f, 0f, 14f, 28f, 42f };
        foreach (float z in columnZ)
        {
            PlaceTemple(_decoColumn, "Columna_Izq_" + z, new Vector3(-19.5f, 0f, z), 0f, Vector3.one * 0.62f);
            PlaceTemple(_decoColumn, "Columna_Der_" + z, new Vector3(19.5f, 0f, z), 0f, Vector3.one * 0.62f);
        }
        PlaceTemple(_tower, "Torre_Temple_Izq", new Vector3(-30f, 0f, 48f), 25f, Vector3.one * 0.45f);
        PlaceTemple(_tower, "Torre_Temple_Der", new Vector3(30f, 0f, 48f), -25f, Vector3.one * 0.45f);
        PlaceTempleDecor(Vector3.zero, 22f, 39f);
        AddLight(Vector3.zero, 1.15f);
    }

    /// <summary>
    /// Nivel 1: sala de tiro del templo con salida lateral al campo exterior.
    /// </summary>
    private static void BuildGalleryWorld()
    {
        TilePavement(-15f, 15f, -16f, 16f, "Galeria_Pavimento");
        TilePavement(14f, 70f, -40f, 40f, "Exterior_Pavimento");

        // --- Colisiones (invisibles) exactamente donde estaban los muros viejos
        MakeCollider("Col_Suelo_Galeria", new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 32f));
        MakeBox("Techo_Galeria", new Vector3(0f, 6.2f, 0f), new Vector3(30f, 0.4f, 32f), _wall);
        MakeCollider("Col_Pared_Entrada", new Vector3(0f, 3f, -16f), new Vector3(30f, 6f, 1f));
        MakeCollider("Col_Pared_Dianas", new Vector3(0f, 3f, 16f), new Vector3(30f, 6f, 1f));
        MakeCollider("Col_Pared_Izquierda", new Vector3(-15f, 3f, 0f), new Vector3(1f, 6f, 32f));
        MakeCollider("Col_Pared_Derecha_A", new Vector3(15f, 3f, -10f), new Vector3(1f, 6f, 12f));
        MakeCollider("Col_Pared_Derecha_B", new Vector3(15f, 3f, 9f), new Vector3(1f, 6f, 14f));
        MakeBox("Suelo_Salida", new Vector3(16f, -0.48f, -1f), new Vector3(3f, 0.06f, 6f), _exit);

        MakeCollider("Col_Suelo_Exterior", new Vector3(42f, -0.5f, 0f), new Vector3(56f, 1f, 80f));
        MakeCollider("Col_Cierre_Sur", new Vector3(15f, 3f, -28f), new Vector3(1f, 6f, 24f));
        MakeCollider("Col_Cierre_Norte", new Vector3(15f, 3f, 28f), new Vector3(1f, 6f, 24f));
        MakeCollider("Col_Valla_Fondo", new Vector3(70f, 3f, 0f), new Vector3(1f, 6f, 80f));
        MakeCollider("Col_Valla_Sur", new Vector3(42f, 3f, -40f), new Vector3(56f, 6f, 1f));
        MakeCollider("Col_Valla_Norte", new Vector3(42f, 3f, 40f), new Vector3(56f, 6f, 1f));

        // --- Muros y columnas visibles del templo
        Vector3 wallScale = new Vector3(1f, 0.5f, 0.5f);
        for (float x = -15f; x <= 5f; x += 10f)
        {
            PlaceWall("Muro_Dianas_" + x, _solidWall, new Vector3(x, 0f, 16f), 0f, wallScale);
            PlaceWall("Muro_Entrada_" + x, _solidWall, new Vector3(x, 0f, -16f), 0f, wallScale);
        }
        for (float z = -16f; z <= 14f; z += 10f)
            PlaceWall("Muro_Izq_" + z, _solidWall, new Vector3(-15f, 0f, z), 90f, wallScale);
        for (float z = -14f; z <= -4f; z += 10f)
            PlaceWall("Muro_Der_A_" + z, _solidWall, new Vector3(15f, 0f, z), 90f, wallScale);
        for (float z = 12f; z <= 22f; z += 10f)
            PlaceWall("Muro_Der_B_" + z, _solidWall, new Vector3(15f, 0f, z), 90f, wallScale);

        PlaceExitDoor();
        PlaceTemple(_decoColumn, "Columna_Sala_Izq", new Vector3(-12f, 0f, -12f), 0f, Vector3.one * 0.62f);
        PlaceTemple(_decoColumn, "Columna_Sala_Der", new Vector3(12f, 0f, -12f), 0f, Vector3.one * 0.62f);
        PlaceTemple(_decoColumn, "Columna_Dianas_Izq", new Vector3(-12f, 0f, 13f), 0f, Vector3.one * 0.62f);
        PlaceTemple(_decoColumn, "Columna_Dianas_Der", new Vector3(12f, 0f, 13f), 0f, Vector3.one * 0.62f);

        // Cierres laterales del campo: SOLO los tramos que tenian colision.
        // El hueco de la salida (x=15, z=-4..2) debe quedar libre o no se ve
        // la galeria ni los soldados desde dentro.
        for (float z = -16f; z >= -36f; z -= 10f)
            PlaceWall("Cierre_Temple_Sur_" + z, _solidWall, new Vector3(15f, 0f, z), 90f, wallScale);
        for (float z = 26f; z <= 46f; z += 10f)
            PlaceWall("Cierre_Temple_Norte_" + z, _solidWall, new Vector3(15f, 0f, z), 90f, wallScale);
        for (float z = -30f; z <= 40f; z += 10f)
            PlaceWall("Valla_Temple_Fondo_" + z, _solidWall, new Vector3(70f, 0f, z), 90f, wallScale);
        for (float x = 22f; x <= 62f; x += 10f)
        {
            PlaceWall("Valla_Temple_Sur_" + x, _solidWall, new Vector3(x, 0f, -40f), 0f, wallScale);
            PlaceWall("Valla_Temple_Norte_" + x, _solidWall, new Vector3(x, 0f, 40f), 0f, wallScale);
        }
        PlaceTemple(_tower, "Torre_Exterior_A", new Vector3(66f, 0f, -34f), -35f, Vector3.one * 0.42f);
        PlaceTemple(_tower, "Torre_Exterior_B", new Vector3(66f, 0f, 34f), 35f, Vector3.one * 0.42f);

        PlaceCrateCover("Cobertura_Ext_1", new Vector3(28f, 0f, 9f), new Vector3(4f, 2f, 3f), 12f);
        PlaceCrateCover("Cobertura_Ext_2", new Vector3(38f, 0f, -10f), new Vector3(5f, 2f, 3f), -20f);
        PlaceCrateCover("Cobertura_Ext_3", new Vector3(52f, 0f, 6f), new Vector3(4f, 2f, 4f), 35f);
        PlaceTempleDecor(new Vector3(42f, 0f, 0f), 28f, 40f);
        AddLight(Vector3.zero, 1.2f);
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

    /// <summary>
    /// Galeria tipo circuito: tres secciones (A, B y C) con panel propio,
    /// separadores de piedra y cuatro dianas por seccion a dos alturas.
    /// Las dianas van delante del modulo de muro, que tiene 1,54 m de grosor.
    /// </summary>
    private static void BuildGalleryTargets()
    {
        float[] centers = { -9f, 0f, 9f };
        Material[] materials = { _sectionA, _sectionB, _sectionC };

        for (int section = 0; section < centers.Length; section++)
        {
            float x = centers[section];
            char letter = (char)('A' + section);

            MakeVisualBox("Panel_Seccion_" + letter,
                new Vector3(x, 3.1f, 14.38f), new Vector3(7f, 5.2f, 0.12f), materials[section]);
            MakeVisualBox("Linea_Seccion_" + letter,
                new Vector3(x, 0.04f, 8f), new Vector3(6.4f, 0.06f, 0.14f), materials[section]);
            PlaceTemple(_decoColumn, "Separador_Seccion_" + letter,
                new Vector3(x - 4.5f, 0f, 13.8f), 0f, Vector3.one * 0.5f);

            for (int i = 0; i < 4; i++)
            {
                float dx = (i % 2 == 0) ? -2.2f : 2.2f;
                float y = (i < 2) ? 2.1f : 3.5f;
                GameObject diana = Spawn(_targetPrefab,
                    new Vector3(x + dx, y, 14.15f), 180f);
                AdjustStand(diana);
                diana.GetComponent<Target>().points =
                    100 + section * 25 + (i >= 2 ? 25 : 0);
            }
        }

        PlaceTemple(_decoColumn, "Separador_Seccion_D",
            new Vector3(13.5f, 0f, 13.8f), 0f, Vector3.one * 0.5f);
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
        gm.requireTargetsForVictory = false;
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
        MenuController menu = new GameObject("MenuController").AddComponent<MenuController>();
        menu.uiFont = _hudFont;
        SaveCurrentScene("00 Menu Principal");
    }

    private static void CreateGalleryScene(LevelDefinition def)
    {
        NewScene("01 Galeria de disparos");
        BuildGalleryWorld();
        Spawn(_playerPrefab, new Vector3(0f, 0f, -11f));

        // Dianas en tres secciones separadas, delante del muro decorativo.
        BuildGalleryTargets();

        // Enemigos esparcidos por el campo exterior (se ven al salir por la derecha).
        Vector3[] outside =
        {
            new Vector3(24f, 0f, -12f), new Vector3(31f, 0f, 7f),
            new Vector3(40f, 0f, -19f), new Vector3(49f, 0f, 14f),
            new Vector3(58f, 0f, -4f)
        };
        foreach (Vector3 p in outside) ConfigureOutdoorBot(Spawn(_botPrefab, p, 180f));

        PlaceCrateCover("Cobertura_Sala_1", new Vector3(-11f, 0f, 2f), new Vector3(3f, 1.6f, 2f), 15f);
        PlaceCrateCover("Cobertura_Sala_2", new Vector3(11f, 0f, 2f), new Vector3(3f, 1.6f, 2f), -15f);
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

        PlaceCrateCover("Caja_1", new Vector3(-6f, 0f, 4f), new Vector3(4f, 2f, 3f), 10f);
        PlaceCrateCover("Caja_2", new Vector3(7f, 0f, 12f), new Vector3(4f, 2f, 3f), -20f);
        PlaceCrateCover("Caja_3", new Vector3(-2f, 0f, 22f), new Vector3(5f, 2f, 3f), 30f);
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
        PlaceCrateCover("Cobertura_1", new Vector3(-9f, 0f, 4f), new Vector3(4f, 2f, 3f), -12f);
        PlaceCrateCover("Cobertura_2", new Vector3(9f, 0f, 4f), new Vector3(4f, 2f, 3f), 12f);
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
            FirstPersonArms[] arms = Object.FindObjectsByType<FirstPersonArms>();
            MenuController[] menus = Object.FindObjectsByType<MenuController>();
            BotAI[] bots = Object.FindObjectsByType<BotAI>();

            GameObject exitDoor = GameObject.Find("Marco_Salida_Temple");
            int doorColliders = exitDoor == null
                ? 0 : exitDoor.GetComponentsInChildren<Collider>(true).Length;
            int dianasVisibles = 0;
            foreach (Target t in targets)
                if (t.type == Target.TargetType.Diana && t.transform.position.z < 14.5f)
                    dianasVisibles++;

            int templeMaterials = 0, templeTextured = 0;
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(
                         FindObjectsSortMode.None))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || !material.shader.name.Contains("Simple Lit")) continue;
                    templeMaterials++;
                    Texture baseMap = material.HasProperty("_BaseMap")
                        ? material.GetTexture("_BaseMap") : null;
                    if (baseMap == null) baseMap = material.mainTexture;
                    if (baseMap != null) templeTextured++;
                }
            }

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
                      $"players={players.Length} weapons={weapons.Length} brazos={arms.Length} menus={menus.Length} " +
                      $"bots={bots.Length} soldados={withSoldier} anim={withAnimator} " +
                      $"acotados={bounded} detectanSalida={detecting} armados={ranged} " +
                      $"puertaCol={doorColliders} dianasVisibles={dianasVisibles} " +
                      $"templeTexturas={templeTextured}/{templeMaterials}");
            foreach (Weapon w in weapons)
            {
                Debug.Log($"[VALIDATE]   arma: bala={(w.bulletPrefab != null)} " +
                          $"modeloBala={(w.bulletPrefab != null && w.bulletPrefab.transform.Find("BulletModel") != null)} " +
                          $"fogonazo={(w.muzzleFlashPrefab != null)} recarga={(w.reloadClip != null)} " +
                          $"audio2D={(w.sfxSource != null)} pivote={(w.weaponPivot != null)} " +
                          $"camara={(w.aimCamera != null)} slots={(w.slots == null ? 0 : w.slots.Length)}");
                if (w.slots != null)
                    foreach (WeaponSlot slot in w.slots)
                        Debug.Log($"[VALIDATE]     {slot.displayName}: modelo={(slot.model != null)} " +
                                  $"bocacha={(slot.muzzle != null)} sonido={(slot.fireClip != null)} " +
                                  $"cargador={slot.magazineSize} reserva={slot.reserve}");
            }
            foreach (FirstPersonArms a in arms)
            {
                Debug.Log($"[VALIDATE]   brazos: modelo={(a.handsRoot != null)} " +
                          $"huesos={(a.leftArmBone != null && a.rightArmBone != null)} " +
                          $"procedurales={(a.rightForearm != null && a.leftForearm != null)} " +
                          $"dedos={((a.leftFingerBones == null ? 0 : a.leftFingerBones.Length) + (a.rightFingerBones == null ? 0 : a.rightFingerBones.Length))} " +
                          $"cargador={(a.magazineProp != null)} arma={(a.weapon != null)}");
            }
            foreach (GameManager gm in managers)
            {
                Debug.Log($"[VALIDATE]   nivel: cargador={gm.startingAmmo} reserva={gm.startingReserve} " +
                          $"dianas={gm.totalTargets} bots={gm.totalBots} boss={gm.hasBoss} " +
                          $"exigeDianas={gm.requireTargetsForVictory} " +
                          $"hudFuente={(gm.hudFont != null)} iconos=" +
                          $"{((gm.healthIcon != null ? 1 : 0) + (gm.ammoIcon != null ? 1 : 0) + (gm.hitmarkerIcon != null ? 1 : 0))} " +
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
