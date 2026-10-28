#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Unity.AI.Navigation; // Package: com.unity.ai.navigation (AI Navigation)

/// <summary>
/// One-click setup: Tools > Bankshot Arena > Build Demo Scene.
/// Builds the arena, bakes the NavMesh, creates the player, enemy + projectile prefabs,
/// armor pickups, spawner and HUD, then saves everything under Assets/BankshotArena/Generated.
/// Uses only primitives, so no art assets are needed.
/// </summary>
public static class BankshotSceneBuilder
{
    const string Root = "Assets/BankshotArena";
    const string Gen = Root + "/Generated";
    const int IgnoreRaycastLayer = 2; // built-in layer: keeps the player out of its own bullets' raycasts

    [MenuItem("Tools/Bankshot Arena/Build Demo Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(Root, "Generated");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var defaultCam = GameObject.Find("Main Camera");
        if (defaultCam != null) Object.DestroyImmediate(defaultCam);

        // ---------- Materials ----------
        Material floorMat  = MakeMat("Floor",      new Color(0.32f, 0.34f, 0.38f));
        Material wallMat   = MakeMat("Wall",       new Color(0.18f, 0.22f, 0.30f));
        Material deflMat   = MakeMat("Deflector",  new Color(0.20f, 0.45f, 0.55f));
        Material enemyMat  = MakeMat("Enemy",      new Color(0.85f, 0.20f, 0.20f));
        Material visorMat  = MakeMat("Visor",      new Color(0.05f, 0.05f, 0.05f));
        Material boltMat   = MakeMat("Bolt",       new Color(1.00f, 0.55f, 0.10f));
        Material armorMat  = MakeMat("ArmorCrystal", new Color(0.30f, 0.80f, 1.00f));
        Material gunMat    = MakeMat("Gun",        new Color(0.12f, 0.12f, 0.14f));
        Material trailMat  = new Material(Shader.Find("Sprites/Default"));
        SaveAsset(trailMat, Gen + "/M_Trail.mat");

        // ---------- Arena ----------
        var arena = new GameObject("Arena").transform;
        const float half = 35f;
        Box("Floor", arena, new Vector3(0, -0.5f, 0), new Vector3(half * 2, 1, half * 2), 0, floorMat);
        Box("Wall_N", arena, new Vector3(0, 2.5f,  half), new Vector3(half * 2, 5, 1), 0, wallMat);
        Box("Wall_S", arena, new Vector3(0, 2.5f, -half), new Vector3(half * 2, 5, 1), 0, wallMat);
        Box("Wall_E", arena, new Vector3( half, 2.5f, 0), new Vector3(1, 5, half * 2), 0, wallMat);
        Box("Wall_W", arena, new Vector3(-half, 2.5f, 0), new Vector3(1, 5, half * 2), 0, wallMat);
        Box("CenterPillar", arena, new Vector3(0, 2f, 0), new Vector3(3, 4, 3), 45f, wallMat);

        // Ring of angled "deflector" walls — the bank-shot geometry the whole twist is about.
        const int deflectors = 8;
        for (int i = 0; i < deflectors; i++)
        {
            float a = i * Mathf.PI * 2f / deflectors;
            Vector3 p = GameMath.PointOnRing(Vector3.zero, 13f, a) + Vector3.up * 1.5f;
            float yaw = -a * Mathf.Rad2Deg + 45f; // 45° off tangent so shots glance around corners
            Box("Deflector_" + i, arena, p, new Vector3(6f, 3f, 0.6f), yaw, deflMat);
        }

        // Low cover between the ring and the outer wall.
        for (int i = 0; i < 4; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 0.5f;
            Vector3 p = GameMath.PointOnRing(Vector3.zero, 22f, a) + Vector3.up * 0.6f;
            Box("Cover_" + i, arena, p, new Vector3(4f, 1.2f, 1.2f), -a * Mathf.Rad2Deg, wallMat);
        }

        // ---------- NavMesh (bake before adding player/pickups so they don't carve it) ----------
        var navGO = new GameObject("NavMesh");
        var surface = navGO.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.BuildNavMesh();
        if (surface.navMeshData != null) SaveAsset(surface.navMeshData, Gen + "/NavMesh.asset");

        // ---------- Prefabs ----------
        EnemyProjectile boltPrefab = BuildBoltPrefab(boltMat, trailMat);
        EnemyAI enemyPrefab = BuildEnemyPrefab(enemyMat, visorMat, boltPrefab);

        // ---------- Game manager ----------
        new GameObject("GameManager").AddComponent<GameManager>();

        // ---------- Player ----------
        var playerGO = new GameObject("Player");
        playerGO.transform.position = new Vector3(0f, 0.1f, -8f);
        var cc = playerGO.AddComponent<CharacterController>();
        cc.center = new Vector3(0, 1, 0);
        cc.height = 2f;
        cc.radius = 0.4f;

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(playerGO.transform, false);
        pivot.localPosition = new Vector3(0, 1.6f, 0);

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(pivot, false);
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 75f;
        cam.nearClipPlane = 0.05f;
        camGO.AddComponent<AudioListener>();

        var gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gun.name = "Gun";
        Object.DestroyImmediate(gun.GetComponent<Collider>());
        gun.transform.SetParent(camGO.transform, false);
        gun.transform.localPosition = new Vector3(0.28f, -0.25f, 0.55f);
        gun.transform.localScale = new Vector3(0.09f, 0.12f, 0.55f);
        gun.GetComponent<Renderer>().sharedMaterial = gunMat;
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(camGO.transform, false);
        muzzle.localPosition = new Vector3(0.28f, -0.22f, 0.85f);

        var controller = playerGO.AddComponent<PlayerController>();
        controller.cameraPivot = pivot;
        var armor = playerGO.AddComponent<TemporaryArmor>();
        var health = playerGO.AddComponent<PlayerHealth>();
        health.armor = armor;

        var tracers = new GameObject("Tracers").AddComponent<TracerPool>();
        var weapon = playerGO.AddComponent<RicochetWeapon>();
        weapon.aimCamera = cam;
        weapon.muzzle = muzzle;
        weapon.tracers = tracers;
        weapon.armor = armor;

        SetLayerRecursive(playerGO, IgnoreRaycastLayer);

        // ---------- Armor pickups ----------
        float[] pickupAngles = { 90f, 210f, 330f };
        foreach (float deg in pickupAngles)
        {
            Vector3 p = GameMath.PointOnRing(Vector3.zero, 18.5f, deg * Mathf.Deg2Rad) + Vector3.up * 1.1f;
            BuildPickup(p, armorMat);
        }

        // ---------- Spawner ----------
        var spawnerGO = new GameObject("WaveSpawner");
        var spawner = spawnerGO.AddComponent<WaveSpawner>();
        spawner.enemyPrefab = enemyPrefab;
        spawner.arenaCenter = spawnerGO.transform;
        spawner.spawnRadius = 27f;

        // ---------- HUD ----------
        BuildHUD(health, armor, weapon, spawner);

        // ---------- Save ----------
        string scenePath = Root + "/BankshotArena.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AddSceneToBuildSettings(scenePath);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = playerGO;
        Debug.Log("[Bankshot] Scene built at " + scenePath + ". Press Play.");
    }

    // ===================================================================
    // Prefabs
    // ===================================================================

    static EnemyProjectile BuildBoltPrefab(Material mat, Material trailMat)
    {
        var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bolt.name = "EnemyBolt";
        Object.DestroyImmediate(bolt.GetComponent<Collider>()); // it does its own raycasts
        bolt.transform.localScale = Vector3.one * 0.3f;
        bolt.GetComponent<Renderer>().sharedMaterial = mat;
        bolt.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var trail = bolt.AddComponent<TrailRenderer>();
        trail.sharedMaterial = trailMat;
        trail.time = 0.15f;
        trail.startWidth = 0.25f;
        trail.endWidth = 0f;
        trail.startColor = new Color(1f, 0.6f, 0.1f, 1f);
        trail.endColor = new Color(1f, 0.3f, 0f, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;

        bolt.AddComponent<EnemyProjectile>();

        var asset = PrefabUtility.SaveAsPrefabAsset(bolt, Gen + "/EnemyBolt.prefab");
        Object.DestroyImmediate(bolt);
        return asset.GetComponent<EnemyProjectile>();
    }

    static EnemyAI BuildEnemyPrefab(Material bodyMat, Material visorMat, EnemyProjectile bolt)
    {
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule); // pivot at centre, 2m tall
        enemy.name = "Enemy";
        enemy.GetComponent<Renderer>().sharedMaterial = bodyMat;

        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        Object.DestroyImmediate(visor.GetComponent<Collider>());
        visor.transform.SetParent(enemy.transform, false);
        visor.transform.localPosition = new Vector3(0f, 0.45f, 0.38f);
        visor.transform.localScale = new Vector3(0.6f, 0.18f, 0.25f);
        visor.GetComponent<Renderer>().sharedMaterial = visorMat;

        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(enemy.transform, false);
        muzzle.localPosition = new Vector3(0f, 0.45f, 0.75f); // outside the capsule so sight rays don't hit ourselves

        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.baseOffset = 1f; // capsule pivot is 1m above its feet
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.speed = 3.8f;
        agent.angularSpeed = 360f;
        agent.acceleration = 14f;
        agent.stoppingDistance = 0.5f;

        enemy.AddComponent<EnemyHealth>();
        var ai = enemy.AddComponent<EnemyAI>();
        ai.muzzle = muzzle;
        ai.projectilePrefab = bolt;

        var asset = PrefabUtility.SaveAsPrefabAsset(enemy, Gen + "/Enemy.prefab");
        Object.DestroyImmediate(enemy);
        return asset.GetComponent<EnemyAI>();
    }

    static void BuildPickup(Vector3 position, Material mat)
    {
        var root = new GameObject("ArmorPickup");
        root.transform.position = position;
        var trigger = root.AddComponent<SphereCollider>();
        trigger.radius = 1.2f;
        trigger.isTrigger = true;

        var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = "Crystal";
        Object.DestroyImmediate(crystal.GetComponent<Collider>());
        crystal.transform.SetParent(root.transform, false);
        crystal.transform.localScale = Vector3.one * 0.6f;
        crystal.GetComponent<Renderer>().sharedMaterial = mat;

        var pickup = root.AddComponent<ArmorPickup>();
        pickup.visual = crystal.transform;
    }

    // ===================================================================
    // HUD
    // ===================================================================

    static void BuildHUD(PlayerHealth health, TemporaryArmor armor, RicochetWeapon weapon, WaveSpawner spawner)
    {
        var canvasGO = new GameObject("HUD");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform c = canvasGO.transform;

        var hud = canvasGO.AddComponent<HUD>();
        hud.player = health;
        hud.armor = armor;
        hud.weapon = weapon;
        hud.spawner = spawner;

        // Shield tint (drawn first = behind everything). The armor script toggles it.
        var overlay = Img("ShieldOverlay", c, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.3f, 0.75f, 1f, 0.10f));
        var ort = overlay.rectTransform;
        ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
        armor.shieldVisual = overlay.gameObject;

        Vector2 mid = new Vector2(0.5f, 0.5f);
        Img("Crosshair", c, mid, mid, Vector2.zero, new Vector2(6, 6), Color.white);
        var hit = Img("Hitmarker", c, mid, mid, Vector2.zero, new Vector2(16, 16), Color.white);
        hit.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
        hud.hitmarker = hit;

        // Damage indicator: a pivot at screen centre with a bar offset "up" (= forward). Rotated by HUD.
        var diPivot = new GameObject("DamageIndicatorPivot", typeof(RectTransform)).GetComponent<RectTransform>();
        diPivot.SetParent(c, false);
        diPivot.anchorMin = diPivot.anchorMax = mid;
        diPivot.sizeDelta = Vector2.zero;
        hud.damageIndicatorPivot = diPivot;
        hud.damageIndicator = Img("DamageIndicator", diPivot, mid, mid, new Vector2(0, 170), new Vector2(90, 14), Color.red);

        // Bottom-left: health + armor
        Vector2 bl = Vector2.zero;
        Txt("HPLabel", c, bl, bl, new Vector2(40, 66), new Vector2(200, 30), "HEALTH", 20, TextAnchor.LowerLeft, Color.white);
        hud.healthFill = Bar("Health", c, bl, bl, new Vector2(40, 40), new Vector2(420, 22),
                             new Color(0, 0, 0, 0.5f), new Color(0.35f, 0.9f, 0.45f));

        var armorGroup = new GameObject("ArmorGroup", typeof(RectTransform)).GetComponent<RectTransform>();
        armorGroup.SetParent(c, false);
        armorGroup.anchorMin = armorGroup.anchorMax = bl;
        armorGroup.pivot = bl;
        armorGroup.anchoredPosition = Vector2.zero;
        armorGroup.sizeDelta = new Vector2(600, 200);
        hud.armorGroup = armorGroup.gameObject;
        hud.armorFill = Bar("ArmorPoints", armorGroup, bl, bl, new Vector2(40, 100), new Vector2(420, 14),
                            new Color(0, 0, 0, 0.5f), new Color(0.3f, 0.8f, 1f));
        hud.armorTimeFill = Bar("ArmorTime", armorGroup, bl, bl, new Vector2(40, 92), new Vector2(420, 5),
                                new Color(0, 0, 0, 0.5f), Color.white);
        hud.armorText = Txt("ArmorText", armorGroup, bl, bl, new Vector2(40, 118), new Vector2(420, 30),
                            "AEGIS", 22, TextAnchor.LowerLeft, new Color(0.5f, 0.85f, 1f));

        // Bottom-centre: charge meter (Absorb & Return)
        Vector2 bc = new Vector2(0.5f, 0f);
        Txt("ChargeLabel", c, bc, bc, new Vector2(0, 58), new Vector2(300, 26), "CHARGE", 18, TextAnchor.LowerCenter, new Color(0.75f, 0.6f, 1f));
        var chargeBg = Bar("Charge", c, bc, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(300, 12),
                           new Color(0, 0, 0, 0.5f), new Color(0.65f, 0.45f, 1f));
        hud.chargeFill = chargeBg;

        // Corners
        Vector2 br = new Vector2(1f, 0f), tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f);
        hud.ammoText  = Txt("Ammo",  c, br, br, new Vector2(-40, 40),  new Vector2(400, 60), "12 / 12", 44, TextAnchor.LowerRight, Color.white);
        hud.waveText  = Txt("Wave",  c, tl, tl, new Vector2(40, -30),  new Vector2(700, 40), "WAVE",   28, TextAnchor.UpperLeft,  Color.white);
        hud.scoreText = Txt("Score", c, tr, tr, new Vector2(-40, -30), new Vector2(500, 40), "SCORE 0", 28, TextAnchor.UpperRight, Color.white);

        hud.popupText  = Txt("Popup",  c, mid, mid, new Vector2(0, -90), new Vector2(900, 50),  "", 34, TextAnchor.MiddleCenter, new Color(0.4f, 0.9f, 1f));
        hud.centerText = Txt("Center", c, mid, mid, new Vector2(0, 40),  new Vector2(1500, 500), "", 40, TextAnchor.MiddleCenter, Color.white);
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, float yaw, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    static Material MakeMat(string name, Color color)
    {
        Shader shader = null;
        if (GraphicsSettings.currentRenderPipeline != null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader) { color = color }; // works for both: URP marks _BaseColor as [MainColor]
        SaveAsset(mat, Gen + "/M_" + name + ".mat");
        return mat;
    }

    static void SaveAsset(Object asset, string path)
    {
        AssetDatabase.DeleteAsset(path); // rebuild-safe
        AssetDatabase.CreateAsset(asset, path);
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
    }

    static void AddSceneToBuildSettings(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true)); // index 0 = what R-restart reloads in builds
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static Font UIFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 2022.2+
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf"); // older versions
        return f;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Img(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        var img = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                    string text, int fontSize, TextAnchor align, Color color)
    {
        var t = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Text>();
        t.font = UIFont();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.6f);
        return t;
    }

    /// <summary>Background + fill. Fill is stretched with its pivot on the left so HUD can scale X 0..1.</summary>
    static RectTransform Bar(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color bg, Color fill)
    {
        var back = Img(name + "Bg", parent, anchor, pivot, pos, size, bg);
        var rt = new GameObject(name + "Fill", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(back.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = fill;
        img.raycastTarget = false;
        return rt;
    }
}
#endif
