using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;

// Собирает всю сцену супермаркета автоматически из моделей Assets/Models и текстур Assets/Textures.
// Меню: Supermarket -> Собрать сцену
public static class SupermarketBuilder
{
    const string ScenePath = "Assets/Scenes/Supermarket.unity";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static bool urp;

    static readonly string[] RequiredModels =
    {
        "Shelf", "Basket", "Milk", "Bread", "Cola", "Apple", "Soap", "Cereal", "Banknote", "Counter", "Register", "Stairs", "Plant",
        "Tree", "Pine", "Bush",
        "DCarton", "DBottle", "DCan", "DBox", "DBag", "DJar", "DPack", "DTall"
    };

    static readonly string[] RequiredFiles =
        { "Assets/Models/HandSkin.bytes", "Assets/Models/Person_Anna.parts", "Assets/Models/Person_Boris.parts", "Assets/Models/Person_Vera.parts", "Assets/Textures/decor_catalog.txt" };

    static readonly string[] SteamVRFolders =
        { "Assets/SteamVR", "Assets/SteamVR_Input", "Assets/SteamVR_Resources", "Assets/StreamingAssets/SteamVR" };

    // верх досок стеллажа (из Shelf.obj)
    static readonly float[] BoardTops = { 0.14f, 0.59f, 1.04f, 1.49f };

    class ProductDef
    {
        public string file, rus;
        public bool sphere;
        public ProductDef(string f, string r, bool s = false) { file = f; rus = r; sphere = s; }
    }

    static readonly ProductDef Milk = new ProductDef("Milk", "Молоко");
    static readonly ProductDef Bread = new ProductDef("Bread", "Хлеб");
    static readonly ProductDef Cola = new ProductDef("Cola", "Кола");
    static readonly ProductDef Apple = new ProductDef("Apple", "Яблоко", true);
    static readonly ProductDef Soap = new ProductDef("Soap", "Мыло");
    static readonly ProductDef Cereal = new ProductDef("Cereal", "Хлопья");

    class Op   // проём в стене (координаты вдоль стены — мировые)
    {
        public float a, b, sill, head;
        public bool glass;
        public Op(float a, float b, float sill, float head, bool glass = true) { this.a = a; this.b = b; this.sill = sill; this.head = head; this.glass = glass; }
    }

    // ================= проверки при открытии проекта =================
    [InitializeOnLoadMethod]
    static void OnLoad()
    {
#if !ENABLE_INPUT_SYSTEM
        EditorApplication.delayCall += AskEnableInputSystem;
#endif
        EditorApplication.delayCall += AskRemoveSteamVR;
        EditorApplication.delayCall += AskBuild;
    }

#if !ENABLE_INPUT_SYSTEM
    static void AskEnableInputSystem()
    {
        if (SessionState.GetBool("SupermarketInputAsked", false)) return;
        SessionState.SetBool("SupermarketInputAsked", true);
        if (EditorUtility.DisplayDialog("Супермаркет VR",
            "Нужно включить новую систему ввода (Input System).\nНажмите «Включить» — редактор перезапустится.",
            "Включить", "Позже"))
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                var so = new SerializedObject(assets[0]);
                so.FindProperty("activeInputHandler").intValue = 2; // Both
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Не удалось включить автоматически: " + e.Message +
                    "\nСделайте вручную: Edit > Project Settings > Player > Other Settings > Active Input Handling = Both, затем перезапуск.");
            }
        }
    }
#endif

    static bool HasSteamVRPlugin()
    {
        foreach (var f in SteamVRFolders)
            if (AssetDatabase.IsValidFolder(f)) return true;
        return false;
    }

    static void AskRemoveSteamVR()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SessionState.GetBool("SupermarketSteamVRAsked", false)) return;
        SessionState.SetBool("SupermarketSteamVRAsked", true);
        if (!HasSteamVRPlugin()) return;
        if (EditorUtility.DisplayDialog("Супермаркет VR: конфликт плагинов",
            "В проекте найден плагин SteamVR (Asset Store). Он конфликтует с OpenXR, на котором работает игра, " +
            "и даёт ошибки (BuggyBuddy, WheelCollider и др.).\n\nSteamVR как программа остаётся и работает как рантайм OpenXR — " +
            "плагин в проекте не нужен.\n\nУдалить папки плагина из проекта?",
            "Удалить плагин", "Оставить"))
        {
            foreach (var f in SteamVRFolders)
                if (AssetDatabase.IsValidFolder(f)) AssetDatabase.DeleteAsset(f);
            AssetDatabase.Refresh();
        }
    }

    static void AskBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SessionState.GetBool("SupermarketBuildAsked", false)) return;
        SessionState.SetBool("SupermarketBuildAsked", true);
        string version = "v9";
        string flag = "Library/SupermarketBuilt_" + version;
        if (File.Exists(flag) && File.Exists(ScenePath)) return;
        foreach (var m in RequiredModels)
            if (!File.Exists("Assets/Models/" + m + ".parts") && AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/" + m + ".obj") == null) return; // модели ещё импортируются
        if (EditorUtility.DisplayDialog("Супермаркет VR", "Собрать (или пересобрать) сцену магазина сейчас?", "Да, собрать", "Позже"))
            Build();
    }

    [MenuItem("Supermarket/Проверка VR-настроек")]
    public static void CheckVR()
    {
        string manifest = File.Exists("Packages/manifest.json") ? File.ReadAllText("Packages/manifest.json") : "";
        var sb = new System.Text.StringBuilder();
        bool ok = true;
        if (!manifest.Contains("com.unity.xr.openxr")) { sb.AppendLine("• Нет пакета OpenXR: Window > Package Manager > Unity Registry > OpenXR Plugin > Install."); ok = false; }
        if (!manifest.Contains("com.unity.xr.management")) { sb.AppendLine("• Нет пакета XR Plug-in Management."); ok = false; }
        if (manifest.Contains("openvr")) { sb.AppendLine("• В проекте есть пакет OpenVR — он конфликтует с OpenXR. Удалите его в Package Manager."); ok = false; }
        if (HasSteamVRPlugin()) { sb.AppendLine("• Найден плагин SteamVR (Assets/SteamVR) — удалите папку, он конфликтует с OpenXR."); ok = false; }
#if !ENABLE_INPUT_SYSTEM
        sb.AppendLine("• Не включена новая система ввода: Edit > Project Settings > Player > Other Settings > Active Input Handling = Both.");
        ok = false;
#endif
        sb.AppendLine();
        sb.AppendLine("Проверьте вручную (3 клика):");
        sb.AppendLine("1) Project Settings > XR Plug-in Management > вкладка Windows: галочка OpenXR.");
        sb.AppendLine("2) Project Settings > XR Plug-in Management > OpenXR: добавьте профили Oculus Touch, Valve Index, HTC Vive.");
        sb.AppendLine("3) SteamVR: Настройки > Разработчик > «Set SteamVR as OpenXR runtime».");
        EditorUtility.DisplayDialog("Проверка VR-настроек", (ok ? "Конфликтов не найдено.\n\n" : "Найдены проблемы:\n\n") + sb, "OK");
    }

    // ================= основная сборка =================
    [MenuItem("Supermarket/Собрать сцену")]
    public static void Build()
    {
        urp = GraphicsSettings.currentRenderPipeline != null;

        foreach (var m in RequiredModels)
        {
            if (!File.Exists("Assets/Models/" + m + ".parts") && AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/" + m + ".obj") == null)
            {
                EditorUtility.DisplayDialog("Супермаркет VR", "Не найдена модель Assets/Models/" + m + ".obj\nПодождите окончания импорта и повторите.", "OK");
                return;
            }
        }
        foreach (var f in RequiredFiles)
        {
            if (!File.Exists(f))
            {
                EditorUtility.DisplayDialog("Супермаркет VR", "Не найден файл " + f + ". Скопируйте папку Assets из архива целиком.", "OK");
                return;
            }
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");
        mats.Clear();
        meshCache.Clear();
        EnsureTextMaterial();
        AnimationFactory.BuildAll(-3.0f + 0.18f);     // клипы и контроллеры (дверь, ящик, вывеска, деревья, NPC)

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.5f;
        sun.shadows = LightShadows.None;
        sun.lightmapBakeType = LightmapBakeType.Mixed;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var building = new GameObject("Building").transform;
        var shelves = new GameObject("Shelves").transform;
        var products = new GameObject("Products").transform;
        var decor = new GameObject("Decor").transform;
        var npc = new GameObject("NPC").transform;
        var props = new GameObject("Props").transform;

        BuildBuilding(building);
        var door = BuildDoor(building);
        BuildShelvesAndProducts(shelves, products, decor);
        BuildEntranceDecor(decor);
        BuildNature(decor);
        // указатели «что лежит на этажах»: внизу у лестницы (оба этажа) и на втором этаже у выхода с лестницы
        DirectoryBoard(props, new Vector3(5.0f, 1.45f, -3.0f), 90f, new Vector2(2.9f, 1.5f), new[] { 0, 1 }, true);
        DirectoryBoard(props, new Vector3(7.95f, 5.45f, 0.0f), 90f, new Vector2(2.3f, 1.6f), new[] { 1 }, false);

        // --- касса (в западной части, далеко от входа) ---
        const float cx = -4.5f, cz = -3.0f;
        Model("Counter", "Прилавок", props, new Vector3(cx, 0f, cz), 0f, M("Counter", new Color(0.55f, 0.38f, 0.22f)), true);
        var reg = Model("Register", "Касса", props, new Vector3(cx - 0.8f, 0.96f, cz), 0f, M("Register", new Color(0.2f, 0.22f, 0.25f)), false);
        AddBoxCollider(reg);

        var counterZone = MakeZone("CounterZone", props, new Vector3(cx + 0.55f, 1.2f, cz + 0.05f), new Vector3(1.1f, 0.5f, 0.7f));
        var registerZone = MakeZone("RegisterZone", props, new Vector3(cx - 0.8f, 1.2f, cz), new Vector3(0.5f, 0.4f, 0.5f));

        FlatLabel(props, new Vector3(cx + 0.55f, 0.975f, cz + 0.05f), "ПОСТАВЬТЕ\nКОРЗИНУ СЮДА", 0.08f, new Color(1f, 1f, 1f));
        FlatLabel(props, new Vector3(cx - 0.35f, 0.975f, cz + 0.28f), "КУПЮРУ -> В КАССУ", 0.05f, new Color(0.7f, 1f, 0.7f));

        // --- корзина ---
        var basket = Model("Basket", "Корзина", props, new Vector3(cx + 1.9f, 0.05f, cz + 0.8f), 0f, M("Basket", new Color(0.9f, 0.3f, 0.1f)), false);
        var rbB = basket.AddComponent<Rigidbody>();
        rbB.mass = 1f;
        rbB.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        AddBox(basket, "Дно", new Vector3(0f, 0.01f, 0f), new Vector3(0.5f, 0.02f, 0.34f));
        AddBox(basket, "Стенка1", new Vector3(0f, 0.12f, -0.16f), new Vector3(0.5f, 0.22f, 0.02f));
        AddBox(basket, "Стенка2", new Vector3(0f, 0.12f, 0.16f), new Vector3(0.5f, 0.22f, 0.02f));
        AddBox(basket, "Стенка3", new Vector3(-0.24f, 0.12f, 0f), new Vector3(0.02f, 0.22f, 0.30f));
        AddBox(basket, "Стенка4", new Vector3(0.24f, 0.12f, 0f), new Vector3(0.02f, 0.22f, 0.30f));
        basket.AddComponent<Grabbable>();
        var bz = MakeZone("Внутри", basket.transform, Vector3.zero, Vector3.one);
        bz.transform.localPosition = new Vector3(0f, 0.14f, 0f);
        var bzBox = bz.GetComponent<BoxCollider>();
        bzBox.center = Vector3.zero;
        bzBox.size = new Vector3(0.44f, 0.26f, 0.28f);
        var bc = basket.AddComponent<BasketContents>();
        bc.zone = bz;

        // --- купюра (шаблон, появляется у покупателя) ---
        var money = Model("Banknote", "Купюра_шаблон", props, new Vector3(0f, -20f, 0f), 0f, MT("Money", Color.white, "Tex_Banknote", Vector2.one), false);
        var rbM = money.AddComponent<Rigidbody>();
        rbM.mass = 0.02f;
        rbM.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var mb = money.AddComponent<BoxCollider>();
        mb.center = new Vector3(0f, 0.005f, 0f);
        mb.size = new Vector3(0.16f, 0.012f, 0.07f);
        money.AddComponent<Grabbable>();
        money.AddComponent<Money>();
        money.SetActive(false);

        // --- покупатели ---
        // в каждый заказ добавлен один товар «с обычных» стеллажей (теперь все товары рабочие)
        var orng = new System.Random(5);
        string d1 = PickDecorName(orng), d2 = PickDecorName(orng), d3 = PickDecorName(orng);
        var c1 = MakeCustomer("Анна", "Anna", 1, new[] { "Молоко", "Хлеб", d1 }, npc);
        var c2 = MakeCustomer("Борис", "Boris", 2, new[] { "Яблоко", "Мыло", d2 }, npc);
        var c3 = MakeCustomer("Вера", "Vera", 3, new[] { "Кола", "Хлопья", d3 }, npc);

        // --- доски с текстом ---
        var board = Board(props, new Vector3(cx, 2.2f, -5.95f), new Vector2(3.4f, 1.6f), 180f,
            "ЗАДАНИЕ", 0.16f, new Color(1f, 1f, 0.6f));
        Board(props, new Vector3(-7.95f, 2.2f, -1.2f), new Vector2(3.2f, 2.8f), -90f,
            "УПРАВЛЕНИЕ В ШЛЕМЕ\nСтик вперёд - дуга телепорта,\nотпустить - переместиться\nСтик влево/вправо - поворот\nGrip/триггер - взять / отпустить\nX или Y - заказ на левой руке\nКнопка A - рестарт после финала\n\nБЕЗ ШЛЕМА\nWASD - ходить, мышь - смотреть\nЛКМ - взять / отпустить\nКолёсико - дальность руки\nShift - бег\n\nEsc или кнопка меню - пауза и выход", 0.1f, new Color(0.8f, 0.95f, 1f));

        var statusGo = new GameObject("СтатусVR");
        statusGo.transform.SetParent(props, false);
        statusGo.transform.position = new Vector3(-7.9f, 3.15f, -1.2f);
        statusGo.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        var status = statusGo.AddComponent<Label>();
        status.text = "РЕЖИМ: ...";
        status.size = 0.11f;
        status.color = new Color(1f, 0.75f, 0.3f);

        // --- игрок ---
        var player = BuildPlayer(new Vector3(cx, 0.05f, cz + 1.0f));
        player.vp.statusLabel = status;

        // --- «браслет»: заказ над левой рукой ---
        var wd = new GameObject("Браслет");
        wd.transform.SetParent(player.root, false);
        var wdc = wd.AddComponent<WristDisplay>();
        var content = new GameObject("Содержимое");
        content.transform.SetParent(wd.transform, false);
        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "Фон";
        Object.DestroyImmediate(panel.GetComponent<Collider>());
        panel.transform.SetParent(content.transform, false);
        panel.transform.localScale = new Vector3(0.36f, 0.2f, 1f);
        panel.GetComponent<Renderer>().sharedMaterial = M("WristBack", new Color(0.05f, 0.08f, 0.12f));
        var wl = new GameObject("Текст");
        wl.transform.SetParent(content.transform, false);
        wl.transform.localPosition = new Vector3(0f, 0f, -0.006f);
        var wristLabel = wl.AddComponent<Label>();
        wristLabel.text = "...";
        wristLabel.size = 0.032f;
        wristLabel.color = new Color(1f, 1f, 0.7f);
        wdc.hand = player.left.transform;
        wdc.head = player.head;
        wdc.content = content;
        content.SetActive(false);

        // --- дверь следит за игроком и покупателями ---
        door.watch = new Transform[] { player.head, c1.transform, c2.transform, c3.transform };

        // --- менеджер заданий ---
        var tmGo = new GameObject("TaskManager");
        var tm = tmGo.AddComponent<TaskManager>();
        tm.customers = new[] { c1, c2, c3 };
        tm.counterZone = counterZone;
        tm.registerZone = registerZone;
        tm.moneyTemplate = money;
        tm.board = board;
        tm.wrist = wristLabel;
        // покупатель входит в дверь (x=4), идёт вдоль южной стены к кассе
        tm.pathIn = new[] { new Vector3(4f, 0f, -9.5f), new Vector3(4f, 0f, -4.6f), new Vector3(cx, 0f, -4.6f), new Vector3(cx, 0f, -4.2f) };
        tm.pathOut = new[] { new Vector3(cx, 0f, -4.6f), new Vector3(4f, 0f, -4.6f), new Vector3(4f, 0f, -9.5f) };
        tm.cashierPos = new Vector3(cx, 0f, cz + 1.0f);
        tm.moneySpawnPos = new Vector3(cx + 1.05f, 1.0f, cz - 0.3f);

        BuildExtras(props, building, shelves, decor, cx, cz, player, tm, reg, basket, money, new[] { c1, c2, c3 });

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        try { File.WriteAllText("Library/SupermarketBuilt_v9", "ok"); } catch { }
        EditorUtility.DisplayDialog("Супермаркет VR", "Сцена собрана: " + ScenePath + "\nНажмите Play.", "OK");
    }

    // Материал для 3D-текста (шейдер Supermarket/Text3D): текст не просвечивает через стены
    static void EnsureTextMaterial()
    {
        Directory.CreateDirectory("Assets/Resources");
        var sh = Shader.Find("Supermarket/Text3D");
        if (sh == null)
        {
            Debug.LogWarning("Шейдер Supermarket/Text3D не найден (проверьте Assets/Shaders/Text3D.shader) — надписи могут просвечивать через стены.");
            return;
        }
        const string path = "Assets/Resources/Text3D.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
    }

    // ================= здание =================
    static void BuildBuilding(Transform root)
    {
        var wallM = M("Wall", new Color(0.93f, 0.91f, 0.85f));
        var slabM = M("Slab", new Color(0.72f, 0.74f, 0.76f));
        var groundM = M("Ground", new Color(0.33f, 0.42f, 0.33f));
        var lightM = M("LightPanel", new Color(1f, 1f, 0.95f));
        var railM = M("Rail", new Color(0.3f, 0.3f, 0.35f));
        var glassM = M("Glass", new Color(0.72f, 0.88f, 0.97f, 0.22f));
        MakeTransparent(glassM);

        Cube("Земля", root, new Vector3(0f, -0.07f, -3f), new Vector3(70f, 0.1f, 60f), groundM);
        Cube("Тротуар", root, new Vector3(0f, -0.005f, -8.4f), new Vector3(26f, 0.03f, 4.2f), M("Sidewalk", new Color(0.62f, 0.62f, 0.6f)));
        Cube("Дорога", root, new Vector3(0f, -0.03f, -15f), new Vector3(70f, 0.04f, 5.5f), M("Road", new Color(0.18f, 0.18f, 0.2f)));
        Cube("Пол1", root, new Vector3(0f, -0.1f, 0f), new Vector3(16.6f, 0.2f, 12.6f), MT("Floor1", Color.white, "Tex_Floor", new Vector2(8.3f, 6.3f))).AddComponent<TeleportSurface>();

        // --- стены с окнами. Этаж 1: y 0..3.5, этаж 2: y 3.5..7.2 ---
        // Юг (z=-6.15): дверь в x 2.8..5.2
        Wall(root, "Юг1", true, -6.15f, -8.3f, 16.6f, 0.3f, 0f, 3.5f,
            new[] { new Op(-1.8f, 1.0f, 0.9f, 2.7f), new Op(2.8f, 5.2f, 0f, 2.6f, false), new Op(6.0f, 7.4f, 0.9f, 2.7f) }, wallM, glassM);
        Wall(root, "Юг2", true, -6.15f, -8.3f, 16.6f, 0.3f, 3.5f, 7.2f,
            new[] { new Op(-6.8f, -3.0f, 4.4f, 6.2f), new Op(-1.8f, 1.0f, 4.4f, 6.2f), new Op(2.8f, 5.2f, 4.4f, 6.2f), new Op(6.0f, 7.4f, 4.4f, 6.2f) }, wallM, glassM);
        // Север (z=6.15)
        Wall(root, "Север1", true, 6.15f, -8.3f, 16.6f, 0.3f, 0f, 3.5f,
            new[] { new Op(-6.5f, -3.5f, 0.9f, 2.7f), new Op(-1.5f, 1.5f, 0.9f, 2.7f), new Op(3.5f, 6.5f, 0.9f, 2.7f) }, wallM, glassM);
        Wall(root, "Север2", true, 6.15f, -8.3f, 16.6f, 0.3f, 3.5f, 7.2f,
            new[] { new Op(-6.5f, -3.5f, 4.4f, 6.2f), new Op(-1.5f, 1.5f, 4.4f, 6.2f), new Op(3.5f, 6.5f, 4.4f, 6.2f) }, wallM, glassM);
        // Запад (x=-8.15), вдоль z от -6 до 6
        Wall(root, "Запад1", false, -8.15f, -6f, 12f, 0.3f, 0f, 3.5f,
            new[] { new Op(1.6f, 4.6f, 0.9f, 2.7f) }, wallM, glassM);
        Wall(root, "Запад2", false, -8.15f, -6f, 12f, 0.3f, 3.5f, 7.2f,
            new[] { new Op(-4.5f, -1.0f, 4.4f, 6.2f), new Op(1.6f, 4.6f, 4.4f, 6.2f) }, wallM, glassM);
        // Восток (x=8.15)
        Wall(root, "Восток1", false, 8.15f, -6f, 12f, 0.3f, 0f, 3.5f,
            new[] { new Op(1.2f, 4.8f, 0.9f, 2.7f) }, wallM, glassM);
        Wall(root, "Восток2", false, 8.15f, -6f, 12f, 0.3f, 3.5f, 7.2f,
            new[] { new Op(-5.0f, -1.2f, 4.4f, 6.2f), new Op(1.2f, 4.8f, 4.4f, 6.2f) }, wallM, glassM);

        // --- перекрытие 2 этажа (верх на y=3.5). Проём под лестницу: x 5.5..7.5, z -6..-0.3 ---
        Cube("Перекрытие A", root, new Vector3(-1.25f, 3.4f, 0f), new Vector3(13.5f, 0.2f, 12f), MT("Floor2A", Color.white, "Tex_Floor", new Vector2(6.75f, 6f))).AddComponent<TeleportSurface>();
        Cube("Перекрытие B", root, new Vector3(6.75f, 3.4f, 2.85f), new Vector3(2.5f, 0.2f, 6.3f), MT("Floor2B", Color.white, "Tex_Floor", new Vector2(1.25f, 3.15f))).AddComponent<TeleportSurface>();
        Cube("Перекрытие C", root, new Vector3(7.75f, 3.4f, -3.15f), new Vector3(0.5f, 0.2f, 5.7f), MT("Floor2C", Color.white, "Tex_Floor", new Vector2(0.25f, 2.85f))).AddComponent<TeleportSurface>();
        Cube("Перила", root, new Vector3(5.5f, 4.0f, -3.1f), new Vector3(0.06f, 1.0f, 5.6f), railM);
        Cube("Крыша", root, new Vector3(0f, 7.3f, 0f), new Vector3(16.6f, 0.2f, 12.6f), slabM);

        // лестница у входа: x 5.5..7.5, поднимается к +Z (z от -5.9 до -0.3)
        Model("Stairs", "Лестница", root, new Vector3(6.5f, 0f, -5.9f), 0f, M("Stairs", new Color(0.62f, 0.62f, 0.65f)), true).AddComponent<TeleportSurface>();

        // светильники
        foreach (float y in new[] { 3.3f, 7.0f })
        {
            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    var p = new Vector3(-6f + ix * 4f, y, -3.5f + iz * 4.5f);
                    Cube("Светильник", root, p, new Vector3(1.2f, 0.05f, 0.5f), lightM, false);
                    if ((ix + iz) % 2 == 0)
                    {
                        var l = new GameObject("Лампа").AddComponent<Light>();
                        l.transform.SetParent(root, false);
                        l.transform.position = p - new Vector3(0f, 0.4f, 0f);
                        l.type = LightType.Point;
                        l.range = 8f;
                        l.intensity = 0.8f;
                        l.shadows = LightShadows.None;
                    }
                }
            }
        }

        // вывеска над входом (снаружи)
        Cube("Вывеска", root, new Vector3(4f, 3.05f, -6.35f), new Vector3(3.6f, 0.7f, 0.1f), M("SignBack", new Color(0.1f, 0.35f, 0.15f)));
        var sign = new GameObject("ВывескаТекст");
        sign.transform.SetParent(root, false);
        sign.transform.position = new Vector3(4f, 3.05f, -6.42f);
        var sl = sign.AddComponent<Label>();
        sl.text = "СУПЕРМАРКЕТ";
        sl.size = 0.42f;
        sl.color = new Color(1f, 1f, 0.7f);
    }

    // Стена вдоль оси (X или Z) с проёмами. Координаты проёмов — мировые вдоль стены.
    static void Wall(Transform root, string name, bool alongX, float fixedCoord, float a0, float length, float thick,
                     float y0, float y1, Op[] ops, Material wallM, Material glassM)
    {
        float cur = a0, end = a0 + length;
        foreach (var o in ops)
        {
            WallBox(root, name + " столб", alongX, fixedCoord, cur, o.a, thick, y0, y1, wallM);
            WallBox(root, name + " подоконник", alongX, fixedCoord, o.a, o.b, thick, y0, o.sill, wallM);
            WallBox(root, name + " перемычка", alongX, fixedCoord, o.a, o.b, thick, o.head, y1, wallM);
            if (o.glass && glassM != null)
                WallBox(root, name + " окно", alongX, fixedCoord, o.a, o.b, 0.04f, o.sill, o.head, glassM);
            cur = o.b;
        }
        WallBox(root, name + " столб", alongX, fixedCoord, cur, end, thick, y0, y1, wallM);
    }

    static void WallBox(Transform root, string name, bool alongX, float fixedCoord, float s0, float s1, float thick, float ya, float yb, Material m)
    {
        float len = s1 - s0, h = yb - ya;
        if (len < 0.01f || h < 0.01f) return;
        float sc = (s0 + s1) * 0.5f, yc = (ya + yb) * 0.5f;
        if (alongX) Cube(name, root, new Vector3(sc, yc, fixedCoord), new Vector3(len, h, thick), m);
        else Cube(name, root, new Vector3(fixedCoord, yc, sc), new Vector3(thick, h, len), m);
    }

    static AutoDoor BuildDoor(Transform root)
    {
        var doorRoot = new GameObject("Дверь").transform;
        doorRoot.SetParent(root, false);
        doorRoot.position = new Vector3(4f, 0f, -6.15f);
        var glass = M("DoorGlass", new Color(0.6f, 0.8f, 0.92f, 0.3f));
        MakeTransparent(glass);
        var l = Cube("Створка левая", doorRoot, new Vector3(-0.6f, 1.3f, 0f), new Vector3(1.2f, 2.6f, 0.06f), glass);
        var r = Cube("Створка правая", doorRoot, new Vector3(0.6f, 1.3f, 0f), new Vector3(1.2f, 2.6f, 0.06f), glass);
        var d = doorRoot.gameObject.AddComponent<AutoDoor>();
        d.leftPanel = l.transform;
        d.rightPanel = r.transform;
        var an = doorRoot.gameObject.AddComponent<Animator>();
        an.runtimeAnimatorController = AnimationFactory.Door;
        an.applyRootMotion = false;
        d.animator = an;
        return d;
    }

    static void BuildEntranceDecor(Transform decor)
    {
        // стопка корзин и растения у входа
        for (int i = 0; i < 3; i++)
            Model("Basket", "Корзина (декор)", decor, new Vector3(1.9f, i * 0.09f, -5.4f), 0f, M("Basket", new Color(0.9f, 0.3f, 0.1f)), false);
        var plant = M("Plant", new Color(0.3f, 0.55f, 0.3f));
        Model("Plant", "Растение", decor, new Vector3(1.0f, 0f, -5.5f), 0f, plant, false);
        Model("Plant", "Растение", decor, new Vector3(-7.3f, 0f, -5.4f), 0f, plant, false);
        Model("Plant", "Растение", decor, new Vector3(7.3f, 3.5f, 5.3f), 0f, plant, false);
    }

    // ================= элементы под критерии проекта =================
    static AudioClip Clip(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".wav");
    }

    static void MarkStatic(GameObject g, bool gi, bool occluder)
    {
        var f = StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
        if (gi) f |= StaticEditorFlags.ContributeGI;
        if (occluder) f |= StaticEditorFlags.OccluderStatic;
        GameObjectUtility.SetStaticEditorFlags(g, f);
    }

    static void BuildExtras(Transform props, Transform building, Transform shelves, Transform decor, float cx, float cz,
                            PlayerParts player, TaskManager tm, GameObject reg, GameObject basket, GameObject money, Customer[] customers)
    {
        var fx = new GameObject("Эффекты и свет").transform;
        var warm = new Color(1f, 0.93f, 0.8f);

        // ---------- ОСВЕЩЕНИЕ: Directional (Sun) + Point (лампы) + Spot (прожекторы) + запекаемые Mixed ----------
        foreach (var l in building.GetComponentsInChildren<Light>()) l.lightmapBakeType = LightmapBakeType.Mixed;
        var s1 = new GameObject("Прожектор кассы").AddComponent<Light>();
        s1.transform.SetParent(fx, false);
        s1.transform.position = new Vector3(cx, 3.1f, cz + 0.4f);
        s1.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        s1.type = LightType.Spot; s1.spotAngle = 80f; s1.innerSpotAngle = 40f; s1.range = 6f; s1.intensity = 2.5f;
        s1.color = warm; s1.shadows = LightShadows.None; s1.lightmapBakeType = LightmapBakeType.Mixed;
        var s2 = new GameObject("Прожектор вывески").AddComponent<Light>();
        s2.transform.SetParent(fx, false);
        s2.transform.position = new Vector3(4f, 4.8f, -9.0f);
        s2.transform.LookAt(new Vector3(4f, 3.0f, -6.4f));
        s2.type = LightType.Spot; s2.spotAngle = 50f; s2.range = 8f; s2.intensity = 3f;
        s2.color = new Color(1f, 1f, 0.85f); s2.shadows = LightShadows.None; s2.lightmapBakeType = LightmapBakeType.Mixed;
        var s3 = new GameObject("Прожектор лестницы").AddComponent<Light>();
        s3.transform.SetParent(fx, false);
        s3.transform.position = new Vector3(4.9f, 3.1f, -3.0f);
        s3.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        s3.type = LightType.Spot; s3.spotAngle = 70f; s3.range = 4.5f; s3.intensity = 2f;
        s3.color = warm; s3.shadows = LightShadows.None; s3.lightmapBakeType = LightmapBakeType.Mixed;

        // Reflection probes (по одному на этаж) и Light Probe Group для динамических объектов (покупатели, предметы)
        foreach (float y in new[] { 1.7f, 5.2f })
        {
            var rp = new GameObject("Reflection Probe " + (y < 3f ? "1" : "2") + " этаж").AddComponent<ReflectionProbe>();
            rp.transform.SetParent(fx, false);
            rp.transform.position = new Vector3(0f, y, 0f);
            rp.size = new Vector3(16f, 3.4f, 12f);
            rp.mode = ReflectionProbeMode.Baked;
            rp.boxProjection = true;
            rp.resolution = 128;
        }
        var lpg = new GameObject("Light Probes").AddComponent<LightProbeGroup>();
        lpg.transform.SetParent(fx, false);
        var pts = new List<Vector3>();
        foreach (float y in new[] { 1.0f, 2.3f, 4.5f, 5.8f })
            for (float x = -7f; x <= 7.1f; x += 3.5f)
                for (float z = -5f; z <= 5.1f; z += 3.3f) pts.Add(new Vector3(x, y, z));
        lpg.probePositions = pts.ToArray();

        // ---------- ЧАСТИЦЫ ----------
        var dustMat = ParticleMat("Particle_Dust", "Tex_Particle");
        var sparkMat = ParticleMat("Particle_Spark", "Tex_Spark");
        var leafMat = ParticleMat("Particle_Leaf", "Tex_Leaf");
        MakeDust(fx, new Vector3(-0.8f, 1.9f, 0f), dustMat, "Пыль в воздухе (этаж 1)");
        MakeDust(fx, new Vector3(-0.8f, 5.4f, 0f), dustMat, "Пыль в воздухе (этаж 2)");
        MakeLeaves(fx, leafMat);
        tm.payBurst = MakeBurst(fx, "Искры при оплате", sparkMat, new Color(1f, 0.85f, 0.2f), new Color(1f, 1f, 0.6f), 36, 0.07f, 2.2f, 0.6f, 35f);
        player.vp.teleportBurst = MakeBurst(fx, "Вспышка телепорта", sparkMat, new Color(0.3f, 0.8f, 1f), new Color(0.7f, 1f, 1f), 26, 0.06f, 1.1f, 0.1f, 70f);

        // ---------- АНИМАЦИЯ: ящик кассы (Animator, клип Drawer_Open) ----------
        var drawer = Cube("Ящик кассы", props, new Vector3(cx - 0.8f, 0.96f + 0.075f, cz + 0.18f), new Vector3(0.3f, 0.06f, 0.12f), M("Drawer", new Color(0.55f, 0.57f, 0.6f)), false);
        var dan = drawer.AddComponent<Animator>();
        dan.runtimeAnimatorController = AnimationFactory.Drawer;
        dan.applyRootMotion = false;
        tm.drawer = dan;

        // ---------- АНИМАЦИЯ: мигающая вывеска «ОТКРЫТО» ----------
        var sign = new GameObject("Вывеска ОТКРЫТО");
        sign.transform.SetParent(props, false);
        sign.transform.position = new Vector3(1.9f, 2.0f, -6.36f);
        var panel = Cube("Панель", sign.transform, Vector3.zero, new Vector3(1.3f, 0.45f, 0.06f), M("SignDark", new Color(0.05f, 0.08f, 0.06f)), false);
        var neon = new GameObject("Надпись");
        neon.transform.SetParent(sign.transform, false);
        neon.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        var nl = neon.AddComponent<Label>();
        nl.text = "ОТКРЫТО"; nl.size = 0.26f; nl.color = new Color(0.4f, 1f, 0.5f);
        var lamp = new GameObject("Лампа").AddComponent<Light>();
        lamp.transform.SetParent(sign.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 0f, -0.6f);
        lamp.type = LightType.Point; lamp.range = 3.5f; lamp.intensity = 1.2f; lamp.color = new Color(0.4f, 1f, 0.5f); lamp.shadows = LightShadows.None;
        var san = sign.AddComponent<Animator>();
        san.runtimeAnimatorController = AnimationFactory.Sign;
        san.applyRootMotion = false;

        // ---------- НАВИГАЦИЯ: точки телепорта (с «магнитом») ----------
        MakeTeleportPoint(fx, "Касса", new Vector3(cx, 0f, cz + 1.0f));
        MakeTeleportPoint(fx, "Вход", new Vector3(4.0f, 0f, -4.3f));
        MakeTeleportPoint(fx, "Лестница вниз", new Vector3(4.6f, 0f, -5.0f));
        MakeTeleportPoint(fx, "Лестница верх", new Vector3(6.5f, 3.5f, 0.7f));
        MakeTeleportPoint(fx, "Продукты 1 этаж", new Vector3(-3.0f, 0f, 2.0f));
        MakeTeleportPoint(fx, "Продукты 2 этаж", new Vector3(-3.0f, 3.5f, 2.0f));

        // ---------- ЗВУК ----------
        var snd = new GameObject("SoundManager").AddComponent<SoundManager>();
        snd.grab = Clip("grab"); snd.drop = Clip("drop"); snd.thud = Clip("thud"); snd.beep = Clip("beep"); snd.error = Clip("error");
        snd.ching = Clip("ching"); snd.drawer = Clip("drawer"); snd.door = Clip("door"); snd.teleport = Clip("teleport");
        snd.step = Clip("step"); snd.click = Clip("click"); snd.hello = Clip("hello"); snd.fanfare = Clip("fanfare");
        snd.music = Clip("music"); snd.ambience = Clip("ambience");

        // ---------- UI: кассовый планшет (World-Space Canvas) ----------
        BuildTablet(fx, new Vector3(cx - 0.75f, 1.15f, cz + 0.65f), player, tm);
        BuildPauseMenu(fx, player);

        // ---------- ОПТИМИЗАЦИЯ: static-флаги (lightmap / occlusion / batching), occlusion у камеры ----------
        foreach (Transform t in building)
        {
            if (t.name == "Дверь" || t.name.StartsWith("Лампа")) continue;
            bool glass = t.name.Contains("окно");
            bool noGI = t.name == "Лестница" || t.name == "Земля" || t.name == "Дорога" || t.name == "Тротуар";
            foreach (var c in t.GetComponentsInChildren<Transform>()) MarkStatic(c.gameObject, !noGI, !glass);
        }
        foreach (Transform t in shelves) foreach (var c in t.GetComponentsInChildren<Transform>()) MarkStatic(c.gameObject, false, true);
        foreach (Transform t in decor) if (t.name != "Природа") foreach (var c in t.GetComponentsInChildren<Transform>()) MarkStatic(c.gameObject, false, false);
        player.head.GetComponent<Camera>().useOcclusionCulling = true;

        // ---------- СТРУКТУРА ПРОЕКТА: префабы ----------
        Directory.CreateDirectory("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(basket, "Assets/Prefabs/Basket.prefab");
        PrefabUtility.SaveAsPrefabAsset(money, "Assets/Prefabs/Banknote.prefab");
        foreach (var c in customers) PrefabUtility.SaveAsPrefabAsset(c.gameObject, "Assets/Prefabs/Customer_" + c.displayName + ".prefab");
    }

    // ---- частицы ----
    static Material ParticleMat(string name, string tex)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var sh = Shader.Find("Sprites/Default");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + tex + ".png");
        m.color = Color.white;
        EditorUtility.SetDirty(m);
        return m;
    }

    static ParticleSystem NewPS(string name, Transform parent, Vector3 pos, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return ps;
    }

    static void MakeDust(Transform parent, Vector3 pos, Material mat, string name)
    {
        var ps = NewPS(name, parent, pos, mat);
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = 14f; main.startSpeed = 0.03f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
        main.startColor = new Color(1f, 1f, 1f, 0.5f);
        main.maxParticles = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 10f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(15f, 3f, 11f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.3f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0.6f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    static void MakeLeaves(Transform parent, Material mat)
    {
        var ps = NewPS("Листья на ветру", parent, new Vector3(0f, 7.5f, -1f), mat);
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = 18f; main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.gravityModifier = 0.06f; main.maxParticles = 70;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 2.5f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(34f, 0.5f, 30f);
        var vel = ps.velocityOverLifetime; vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(0.5f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0.15f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.25f;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
    }

    static ParticleSystem MakeBurst(Transform parent, string name, Material mat, Color a, Color b, int count, float size, float speed, float gravity, float angle)
    {
        var ps = NewPS(name, parent, new Vector3(0f, 0.5f, 0f), mat);
        ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);       // конус смотрит вверх
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size, size * 1.6f);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = 0.06f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        return ps;
    }

    // ---- точки телепорта ----
    static void MakeTeleportPoint(Transform parent, string name, Vector3 pos)
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "Точка: " + name;
        pad.transform.SetParent(parent, false);
        pad.transform.position = pos + Vector3.up * 0.012f;
        pad.transform.localScale = new Vector3(0.9f, 0.008f, 0.9f);
        Object.DestroyImmediate(pad.GetComponent<Collider>());
        var m = M("TeleportPad", new Color(0.2f, 0.8f, 1f));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.9f) * 1.5f);
        pad.GetComponent<Renderer>().sharedMaterial = m;
        var tp = pad.AddComponent<TeleportPoint>();
        tp.pointName = name;
        var lg = new GameObject("Подпись точки: " + name);
        lg.transform.SetParent(parent, false);
        lg.transform.position = pos + Vector3.up * 0.55f;
        var l = lg.AddComponent<Label>();
        l.text = name; l.size = 0.1f; l.color = new Color(0.6f, 0.95f, 1f); l.billboard = true;
    }

    // ---- меню паузы / выхода (Esc, P или кнопка меню контроллера) ----
    static void BuildPauseMenu(Transform parent, PlayerParts player)
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }

        var root = new GameObject("Меню паузы");
        root.transform.SetParent(parent, false);
        var panel = new GameObject("Панель меню", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = panel.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4f;
        var crt = panel.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(560f, 440f);
        crt.localScale = Vector3.one * 0.0013f;

        var bg = UIRect("Фон", crt, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.07f, 0.1f, 0.97f);
        bg.raycastTarget = false;
        UIText("Заголовок", crt, "ПАУЗА", 44, new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter, new Vector2(0f, 0.82f), new Vector2(1f, 0.98f), font);
        Text l1, l2, l3;
        var resume = UIButton("Продолжить", crt, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.77f), new Color(0.15f, 0.6f, 0.25f), "ПРОДОЛЖИТЬ", 30, font, out l1);
        var restart = UIButton("Заново", crt, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.54f), new Color(0.2f, 0.35f, 0.6f), "НАЧАТЬ ЗАНОВО", 30, font, out l2);
        var quit = UIButton("Выйти", crt, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.31f), new Color(0.65f, 0.18f, 0.18f), "ВЫЙТИ ИЗ ИГРЫ", 30, font, out l3);
        UIText("Подсказка", crt, "Esc / кнопка меню — закрыть", 20, new Color(0.7f, 0.8f, 0.88f), TextAnchor.MiddleCenter, new Vector2(0f, 0.01f), new Vector2(1f, 0.1f), font);

        var pm = root.AddComponent<PauseMenu>();
        pm.panel = panel;
        pm.head = player.head;
        pm.resumeButton = resume;
        pm.restartButton = restart;
        pm.quitButton = quit;
        pm.quitLabel = l3;

        var poke = root.AddComponent<PokeUI>();
        poke.buttons = new[] { resume, restart, quit };
        poke.hands = new[] { player.left, player.vp.rightHand };
        poke.head = player.head;
    }

    // ---- UI-планшет ----
    static RectTransform UIRect(string name, RectTransform parent, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        return r;
    }

    static Text UIText(string name, RectTransform parent, string text, int size, Color color, TextAnchor anchor, Vector2 aMin, Vector2 aMax, Font font)
    {
        var r = UIRect(name, parent, aMin, aMax);
        var t = r.gameObject.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button UIButton(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Color col, string label, int size, Font font, out Text labelText)
    {
        var r = UIRect(name, parent, aMin, aMax);
        var img = r.gameObject.AddComponent<Image>();
        img.color = col;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        labelText = UIText("Надпись", r, label, size, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, font);
        return b;
    }

    static void BuildTablet(Transform parent, Vector3 pos, PlayerParts player, TaskManager tm)
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }

        var root = new GameObject("Кассовый планшет");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(30f, 180f, 0f);        // наклон к кассиру

        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Стойка";
        Object.DestroyImmediate(post.GetComponent<Collider>());
        post.transform.SetParent(parent, false);
        post.transform.position = new Vector3(pos.x, (pos.y - 0.12f) * 0.5f, pos.z + 0.1f);
        post.transform.localScale = new Vector3(0.05f, (pos.y - 0.12f) * 0.5f, 0.05f);
        post.GetComponent<Renderer>().sharedMaterial = M("Rail", new Color(0.3f, 0.3f, 0.35f));

        var cgo = new GameObject("Canvas", typeof(RectTransform));
        cgo.transform.SetParent(root.transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4f;
        var crt = cgo.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(520f, 340f);
        crt.localScale = Vector3.one * 0.001f;
        crt.localPosition = Vector3.zero;

        var bg = UIRect("Фон", crt, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.12f, 0.96f);
        bg.raycastTarget = false;

        var ui = root.AddComponent<GameUI>();
        ui.title = UIText("Заголовок", crt, "СУПЕРМАРКЕТ VR", 34, new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter, new Vector2(0f, 0.84f), new Vector2(1f, 1f), font);
        ui.status = UIText("Статус", crt, "Нажмите СТАРТ", 22, Color.white, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.70f), new Vector2(0.97f, 0.84f), font);
        ui.timerText = UIText("Время", crt, "Время: 00:00", 24, new Color(0.8f, 0.95f, 1f), TextAnchor.MiddleLeft, new Vector2(0.05f, 0.54f), new Vector2(0.5f, 0.68f), font);
        ui.servedText = UIText("Обслужено", crt, "Обслужено: 0/3", 24, new Color(0.8f, 0.95f, 1f), TextAnchor.MiddleLeft, new Vector2(0.5f, 0.54f), new Vector2(0.97f, 0.68f), font);
        ui.scoreText = UIText("Баллы", crt, "Баллы: 0", 24, new Color(0.8f, 0.95f, 1f), TextAnchor.MiddleLeft, new Vector2(0.05f, 0.40f), new Vector2(0.5f, 0.54f), font);
        ui.fpsText = UIText("FPS", crt, "FPS: --", 24, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleLeft, new Vector2(0.5f, 0.40f), new Vector2(0.97f, 0.54f), font);
        Text sl, ml, ql;
        ui.startButton = UIButton("Кнопка СТАРТ", crt, new Vector2(0.03f, 0.06f), new Vector2(0.38f, 0.34f), new Color(0.15f, 0.6f, 0.25f), "СТАРТ", 26, font, out sl);
        ui.muteButton = UIButton("Кнопка ЗВУК", crt, new Vector2(0.40f, 0.06f), new Vector2(0.68f, 0.34f), new Color(0.2f, 0.35f, 0.6f), "ЗВУК: ВКЛ", 19, font, out ml);
        ui.quitButton = UIButton("Кнопка ВЫХОД", crt, new Vector2(0.70f, 0.06f), new Vector2(0.97f, 0.34f), new Color(0.65f, 0.18f, 0.18f), "ВЫХОД", 24, font, out ql);
        ui.startLabel = sl;
        ui.muteLabel = ml;
        ui.quitLabel = ql;

        var poke = root.AddComponent<PokeUI>();
        poke.buttons = new[] { ui.startButton, ui.muteButton, ui.quitButton };
        poke.hands = new[] { player.left, player.vp.rightHand };
        poke.head = player.head;
        tm.ui = ui;
    }

    // ================= деревья и кусты вокруг здания =================
    static void BuildNature(Transform parent)
    {
        var holder = new GameObject("Природа").transform;
        holder.SetParent(parent, false);
        var mat = MT("Nature", Color.white, "Tex_Nature", Vector2.one);
        var rnd = new System.Random(21);
        var spots = new List<KeyValuePair<string, Vector3>>();
        // кусты вдоль стен. Крупный куст выступает от центра примерно на 1 м, поэтому центр стоит в 1.5 м от наружной грани стены —
        // так они не прорастают внутрь здания и не торчат в окнах (наружные грани: x=±8.3, z=±6.3)
        foreach (float x in new[] { -7.6f, -5.6f, -3.6f, -1.6f, 0.4f, 7.8f }) spots.Add(new KeyValuePair<string, Vector3>("Bush", new Vector3(x, 0f, -7.9f)));
        foreach (float z in new[] { -4.0f, -1.5f, 1.0f, 3.5f }) spots.Add(new KeyValuePair<string, Vector3>("Bush", new Vector3(9.9f, 0f, z)));
        foreach (float z in new[] { -4.0f, -1.5f, 1.0f, 3.5f }) spots.Add(new KeyValuePair<string, Vector3>("Bush", new Vector3(-9.9f, 0f, z)));
        foreach (float x in new[] { -7.0f, -3.5f, 0f, 3.5f, 7.0f }) spots.Add(new KeyValuePair<string, Vector3>("Bush", new Vector3(x, 0f, 7.9f)));
        // деревья и ёлки подальше
        spots.Add(new KeyValuePair<string, Vector3>("Tree", new Vector3(-12.5f, 0f, -8.5f)));
        spots.Add(new KeyValuePair<string, Vector3>("Tree", new Vector3(12.5f, 0f, -8.0f)));
        spots.Add(new KeyValuePair<string, Vector3>("Tree", new Vector3(12.0f, 0f, 3.0f)));
        spots.Add(new KeyValuePair<string, Vector3>("Tree", new Vector3(-12.5f, 0f, 2.5f)));
        spots.Add(new KeyValuePair<string, Vector3>("Tree", new Vector3(-8.0f, 0f, 11.5f)));
        spots.Add(new KeyValuePair<string, Vector3>("Tree", new Vector3(8.5f, 0f, 11.0f)));
        spots.Add(new KeyValuePair<string, Vector3>("Pine", new Vector3(-12.0f, 0f, 9.0f)));
        spots.Add(new KeyValuePair<string, Vector3>("Pine", new Vector3(12.5f, 0f, 8.5f)));
        spots.Add(new KeyValuePair<string, Vector3>("Pine", new Vector3(0f, 0f, 12.5f)));
        spots.Add(new KeyValuePair<string, Vector3>("Pine", new Vector3(-12.5f, 0f, -3.5f)));
        foreach (var sp in spots)
        {
            var pos = sp.Value + new Vector3((float)(rnd.NextDouble() - 0.5) * 0.2f, 0f, (float)(rnd.NextDouble() - 0.5) * 0.2f);
            var g = Model(sp.Key, sp.Key, holder, pos, (float)rnd.NextDouble() * 360f, mat, false);
            float k = sp.Key == "Bush" ? 0.75f + (float)rnd.NextDouble() * 0.25f : 0.9f + (float)rnd.NextDouble() * 0.4f;
            g.transform.localScale = Vector3.one * k;
            if (sp.Key == "Bush")
            {
                foreach (var t in g.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
            else
            {
                var an = g.AddComponent<Animator>();                    // покачивание на ветру (клип Tree_Sway)
                an.runtimeAnimatorController = AnimationFactory.Tree;
                an.applyRootMotion = false;
                an.speed = 0.7f + (float)rnd.NextDouble() * 0.6f;
            }
        }
    }

    // ================= стеллажи и продукты =================
    class ShelfDef
    {
        public Vector3 pos; public float yaw; public string label; public ProductDef product;
        public ShelfDef(float x, float y, float z, float yaw, string label, ProductDef p = null)
        { pos = new Vector3(x, y, z); this.yaw = yaw; this.label = label; product = p; }
    }

    static void BuildShelvesAndProducts(Transform shelves, Transform products, Transform decor)
    {
        var decorTex = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("Decor_ t:Texture2D", new[] { "Assets/Textures" }))
            decorTex.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
        decorTex.Sort();

        var defs = new List<ShelfDef>();
        // ---- этаж 1 (y=0). Ряд A (z=0.8) смотрит на север, B (z=3.2) на юг — проход между ними; C у северной стены
        defs.Add(new ShelfDef(-6.4f, 0f, 0.8f, 0f, "МОЛОКО", Milk));
        defs.Add(new ShelfDef(-4.2f, 0f, 0.8f, 0f, "ХЛЕБ", Bread));
        defs.Add(new ShelfDef(-2.0f, 0f, 0.8f, 0f, ""));
        defs.Add(new ShelfDef(0.2f, 0f, 0.8f, 0f, ""));
        defs.Add(new ShelfDef(2.6f, 0f, 0.8f, 0f, ""));
        defs.Add(new ShelfDef(-6.4f, 0f, 3.2f, 180f, "НАПИТКИ", Cola));
        defs.Add(new ShelfDef(-4.2f, 0f, 3.2f, 180f, "ФРУКТЫ", Apple));
        defs.Add(new ShelfDef(-2.0f, 0f, 3.2f, 180f, ""));
        defs.Add(new ShelfDef(0.2f, 0f, 3.2f, 180f, ""));
        defs.Add(new ShelfDef(2.6f, 0f, 3.2f, 180f, ""));
        foreach (float x in new[] { -6.4f, -4.2f, -2.0f, 0.2f, 2.4f, 4.4f }) defs.Add(new ShelfDef(x, 0f, 5.55f, 180f, ""));
        // ---- этаж 2 (y=3.5)
        defs.Add(new ShelfDef(-6.4f, 3.5f, 0.8f, 0f, "МЫЛО", Soap));
        defs.Add(new ShelfDef(-4.2f, 3.5f, 0.8f, 0f, "ХЛОПЬЯ", Cereal));
        defs.Add(new ShelfDef(-2.0f, 3.5f, 0.8f, 0f, ""));
        defs.Add(new ShelfDef(0.2f, 3.5f, 0.8f, 0f, ""));
        defs.Add(new ShelfDef(2.6f, 3.5f, 0.8f, 0f, ""));
        foreach (float x in new[] { -6.4f, -4.2f, -2.0f, 0.2f, 2.6f }) defs.Add(new ShelfDef(x, 3.5f, 3.2f, 180f, ""));
        foreach (float x in new[] { -6.4f, -4.2f, -2.0f, 0.2f, 2.4f, 4.4f }) defs.Add(new ShelfDef(x, 3.5f, 5.55f, 180f, ""));

        LoadDecorCatalog();
        decorUsed.Clear();
        floorProducts[0].Clear();
        floorProducts[1].Clear();
        var rnd = new System.Random(11);
        foreach (var d in defs)
        {
            int fl = d.pos.y > 1f ? 1 : 0;
            if (d.product != null)
            {
                Shelf(shelves, d.pos, d.yaw, d.label);
                StockInteractive(products, d);
                ShelfSign(shelves, d.pos, d.yaw, new[] { d.product.rus.ToUpper() });
                AddFloorProduct(fl, d.product.rus);
            }
            else if (decorTex.Count > 0)
            {
                // три полки — три разных товара; на табличке перечислены сверху вниз
                var picks = new[] { decorTex[rnd.Next(decorTex.Count)], decorTex[rnd.Next(decorTex.Count)], decorTex[rnd.Next(decorTex.Count)] };
                var lines = new[] { DecorName(picks[2]).ToUpper(), DecorName(picks[1]).ToUpper(), DecorName(picks[0]).ToUpper() };
                Shelf(shelves, d.pos, d.yaw, string.Join(" / ", lines));
                StockDecor(products, d, picks);
                ShelfSign(shelves, d.pos, d.yaw, lines);
                foreach (var pk in picks) AddFloorProduct(fl, DecorName(pk));
            }
            else Shelf(shelves, d.pos, d.yaw, "ТОВАРЫ");
        }
    }

    static readonly List<string>[] floorProducts = { new List<string>(), new List<string>() };

    static void AddFloorProduct(int floor, string name)
    {
        if (!floorProducts[floor].Contains(name)) floorProducts[floor].Add(name);
    }

    // Указатель-перечень: что лежит на этажах. Столбцы по 11 строк. yaw — как у Board (зритель — с противоположной стороны).
    static void DirectoryBoard(Transform parent, Vector3 pos, float yaw, Vector2 size, int[] floors, bool standing)
    {
        var root = new GameObject("Указатель").transform;
        root.SetParent(parent, false);
        root.position = pos;
        root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var panel = Cube("Панель", root, Vector3.zero, new Vector3(size.x, size.y, 0.05f), M("Board", new Color(0.08f, 0.1f, 0.12f)), false);
        panel.transform.localRotation = Quaternion.identity;
        if (standing)
        {
            foreach (float sx in new[] { -1f, 1f })
                Cube("Стойка", root, new Vector3(sx * (size.x * 0.5f - 0.1f), -pos.y * 0.5f - size.y * 0.25f, 0f),
                     new Vector3(0.06f, pos.y - size.y * 0.5f, 0.06f), M("Rail", new Color(0.3f, 0.3f, 0.35f)), false);
        }
        float blockW = size.x / floors.Length;
        for (int b = 0; b < floors.Length; b++)
        {
            var list = new List<string>(floorProducts[floors[b]]);
            list.Sort(System.StringComparer.CurrentCultureIgnoreCase);
            float left = -size.x * 0.5f + b * blockW;
            // заголовок этажа
            var hd = new GameObject("Заголовок");
            hd.transform.SetParent(root, false);
            hd.transform.localPosition = new Vector3(left + blockW * 0.5f, size.y * 0.5f - 0.13f, -0.04f);
            var hl = hd.AddComponent<Label>();
            hl.text = floors[b] == 0 ? "ЭТАЖ 1 — ТОВАРЫ" : "ЭТАЖ 2 — ТОВАРЫ";
            hl.size = 0.12f;
            hl.color = new Color(1f, 0.9f, 0.3f);
            // столбцы
            const int perCol = 11;
            int cols = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)perCol));
            float colW = blockW / cols;
            for (int c = 0; c < cols; c++)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = c * perCol; i < Mathf.Min(list.Count, (c + 1) * perCol); i++) sb.Append(list[i]).Append('\n');
                var cg = new GameObject("Столбец");
                cg.transform.SetParent(root, false);
                cg.transform.localPosition = new Vector3(left + c * colW + 0.07f, size.y * 0.5f - 0.3f, -0.04f);
                var cl = cg.AddComponent<Label>();
                cl.text = sb.ToString().TrimEnd('\n');
                cl.size = 0.075f;
                cl.color = new Color(0.9f, 0.97f, 1f);
                cl.anchor = TextAnchor.UpperLeft;
                cl.alignment = TextAlignment.Left;
            }
        }
    }

    // ---- каталог товаров «с обычных» стеллажей: имя текстуры -> название товара ----
    static readonly Dictionary<string, string> decorNames = new Dictionary<string, string>();
    static readonly List<string> decorUsed = new List<string>();

    static void LoadDecorCatalog()
    {
        decorNames.Clear();
        const string path = "Assets/Textures/decor_catalog.txt";
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadAllLines(path, System.Text.Encoding.UTF8))
        {
            var parts = line.Split('|');
            if (parts.Length != 2 || parts[1].Length == 0) continue;
            string label = parts[1].Trim();
            decorNames[parts[0].Trim()] = char.ToUpper(label[0]) + label.Substring(1).ToLower();
        }
    }

    static string DecorName(string tex)
    {
        string n;
        return decorNames.TryGetValue(tex, out n) ? n : "Товар";
    }

    static string PickDecorName(System.Random r)
    {
        if (decorUsed.Count == 0) return "Хлеб";
        return decorUsed[r.Next(decorUsed.Count)];
    }

    static GameObject Shelf(Transform parent, Vector3 pos, float yaw, string label)
    {
        var s = Model("Shelf", "Стеллаж " + label, parent, pos, yaw, M("Shelf", new Color(0.78f, 0.78f, 0.84f)), false);
        foreach (var mf in s.GetComponentsInChildren<MeshFilter>())
        {
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
        return s;
    }

    // Статичная табличка на верхней кромке стеллажа, повёрнутая к проходу (сторона, откуда берут товар).
    // Список — сверху вниз по полкам. Табличка никуда не поворачивается.
    static void ShelfSign(Transform parent, Vector3 shelfPos, float shelfYaw, string[] lines)
    {
        var front = Quaternion.Euler(0f, shelfYaw, 0f) * Vector3.forward;
        float h = lines.Length == 1 ? 0.34f : 0.55f;
        float ts = lines.Length == 1 ? 0.2f : 0.125f;
        var pos = shelfPos + front * 0.2f + new Vector3(0f, 2.03f + h * 0.5f, 0f);
        Board(parent, pos, new Vector2(1.9f, h), shelfYaw + 180f, string.Join("\n", lines), ts, new Color(1f, 0.95f, 0.6f), false);
    }

    static void StockInteractive(Transform products, ShelfDef d)
    {
        var rot = Quaternion.Euler(0f, d.yaw, 0f);
        for (int lvl = 1; lvl <= 3; lvl++)
        {
            for (int i = 0; i < 4; i++)
            {
                var local = new Vector3(-0.6f + i * 0.4f, BoardTops[lvl] + 0.003f, 0f);
                MakeProduct(d.product, products, d.pos + rot * local, d.yaw);
            }
        }
    }

    // Обычные стеллажи: товары тоже рабочие — их можно взять, положить в корзину и продать
    static void StockDecor(Transform products, ShelfDef d, string[] texNames)
    {
        var rot = Quaternion.Euler(0f, d.yaw, 0f);
        for (int lvl = 1; lvl <= 3; lvl++)
        {
            string tex = texNames[lvl - 1];                 // Decor_<Модель>_<номер>
            string model = tex.Split('_')[1];
            string rus = DecorName(tex);
            if (!decorUsed.Contains(rus)) decorUsed.Add(rus);
            var mat = MT("D_" + tex, Color.white, tex, Vector2.one);
            mat.enableInstancing = true;                       // GPU instancing: одинаковые товары рисуются пачкой
            for (int i = 0; i < 5; i++)
            {
                var local = new Vector3(-0.7f + i * 0.35f, BoardTops[lvl] + 0.003f, 0f);
                MakeProduct(model, rus, false, mat, products, d.pos + rot * local, d.yaw);
            }
        }
    }

    static GameObject MakeProduct(ProductDef d, Transform parent, Vector3 pos, float yaw)
    {
        return MakeProduct(d.file, d.rus, d.sphere, MT("P_" + d.file, Color.white, "Tex_" + d.file, Vector2.one), parent, pos, yaw);
    }

    static GameObject MakeProduct(string file, string rus, bool sphere, Material mat, Transform parent, Vector3 pos, float yaw)
    {
        var g = Model(file, rus, parent, pos, yaw, mat, false);
        var rb = g.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var b = LocalBounds(g);
        if (sphere)
        {
            var sc = g.AddComponent<SphereCollider>();
            sc.center = b.center;
            sc.radius = b.extents.x;
        }
        else
        {
            var bc = g.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
        }
        g.AddComponent<Grabbable>();
        g.AddComponent<Product>().productName = rus;
        return g;
    }

    // ================= NPC =================
    static Customer MakeCustomer(string who, string file, int index, string[] order, Transform parent)
    {
        var g = TryCustomModel(index, who, parent);
        bool custom = g != null;
        if (!custom) g = BuildPersonFromParts(who, file, parent);
        var cap = g.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0f, 0.9f, 0f);
        cap.height = 1.8f;
        cap.radius = 0.28f;
        var c = g.AddComponent<Customer>();
        c.displayName = who;
        c.order = order;
        c.signMaterial = M("Board", new Color(0.08f, 0.1f, 0.12f));
        if (!custom)
        {
            var an = g.AddComponent<Animator>();
            an.runtimeAnimatorController = AnimationFactory.Npc;
            an.applyRootMotion = false;
            c.head = g.transform.Find("Head");
            c.armPos = g.transform.Find("ArmPos");
            c.armNeg = g.transform.Find("ArmNeg");
            c.legPos = g.transform.Find("LegPos");
            c.legNeg = g.transform.Find("LegNeg");
        }
        g.SetActive(false);
        return c;
    }

    // Человек из отдельных частей (тело, голова, руки, ноги) — чтобы их можно было двигать.
    // Данные читаются из Person_<имя>.parts (создаёт Tools/generate_models.py), без импортёра OBJ.
    static GameObject BuildPersonFromParts(string who, string file, Transform parent)
    {
        var root = new GameObject(who);
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(4f, 0f, -9.5f);
        var mat = MT("Person_" + file, Color.white, "Tex_Person_" + file, Vector2.one);
        Directory.CreateDirectory("Assets/Models/Meshes");
        using (var br = new BinaryReader(File.OpenRead("Assets/Models/Person_" + file + ".parts")))
        {
            int count = br.ReadInt32();
            for (int k = 0; k < count; k++)
            {
                int len = br.ReadInt32();
                string pname = System.Text.Encoding.UTF8.GetString(br.ReadBytes(len));
                var pivot = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                int nv = br.ReadInt32(), nt = br.ReadInt32();
                var v = new Vector3[nv];
                var n = new Vector3[nv];
                var uv = new Vector2[nv];
                var tris = new int[nt * 3];
                for (int i = 0; i < nv; i++) v[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                for (int i = 0; i < nv; i++) n[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                for (int i = 0; i < nv; i++) uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                for (int i = 0; i < nt * 3; i++) tris[i] = br.ReadInt32();

                var mesh = new Mesh();
                mesh.name = "Person_" + file + "_" + pname;
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = v;
                mesh.normals = n;
                mesh.uv = uv;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                string assetPath = "Assets/Models/Meshes/" + mesh.name + ".asset";
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(mesh, assetPath);

                var part = new GameObject(pname);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = pivot;     // точка вращения (шея, плечо, бедро)
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }
        return root;
    }

    // Если положить свою модель в Assets/Models/Custom/Customer1.fbx (.obj/.prefab) — она заменит встроенную
    static GameObject TryCustomModel(int index, string who, Transform parent)
    {
        foreach (var ext in new[] { "fbx", "obj", "prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Custom/Customer" + index + "." + ext);
            if (prefab == null) continue;
            var root = new GameObject(who);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(4f, 0f, -9.5f);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(root.transform, false);
            var rs = inst.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.DestroyImmediate(root); continue; }
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float k = b.size.y > 0.01f ? 1.75f / b.size.y : 1f;
            inst.transform.localScale = inst.transform.localScale * k;
            b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            inst.transform.position += new Vector3(root.transform.position.x - b.center.x, root.transform.position.y - b.min.y, root.transform.position.z - b.center.z);
            Debug.Log("Для покупателя " + who + " использована своя модель: " + prefab.name);
            return root;
        }
        return null;
    }

    // ================= игрок =================
    class PlayerParts { public Transform head; public VRPlayer vp; public Transform root; public Hand left; }

    static PlayerParts BuildPlayer(Vector3 pos)
    {
        var player = new GameObject("Player");
        player.transform.position = pos;
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // лицом к покупателям (на юг)
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.25f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.03f;
        cc.slopeLimit = 50f;
        var vp = player.AddComponent<VRPlayer>();

        var head = new GameObject("Head");
        head.transform.SetParent(player.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        head.tag = "MainCamera";
        var cam = head.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
        head.AddComponent<AudioListener>();
        var hp = head.AddComponent<XRPose>();
        hp.node = XRNode.Head;

        var left = MakeHand(player.transform, "LeftHand", XRNode.LeftHand);
        var right = MakeHand(player.transform, "RightHand", XRNode.RightHand);
        vp.head = head.transform;
        vp.leftHand = left;
        vp.rightHand = right;
        return new PlayerParts { head = head.transform, vp = vp, root = player.transform, left = left };
    }

    // --- рука: цельная гладкая сетка на скелете (HandSkin.bytes создаёт Tools/hand_skin.py) ---
    static readonly Vector3[] FingerBase =
    {
        new Vector3(0f, 0.034f, 0.046f), new Vector3(0f, 0.0115f, 0.050f),
        new Vector3(0f, -0.011f, 0.048f), new Vector3(0f, -0.031f, 0.042f)
    };
    static readonly float[] FingerScale = { 1f, 1.08f, 1f, 0.82f };
    static readonly float[] FingerSpread = { -5f, -1.5f, 2.5f, 7f };
    static readonly float[] SegLen = { 0.040f, 0.026f, 0.020f };
    const string HandBytes = "Assets/Models/HandSkin.bytes";

    static Mesh LoadHandMesh(bool mirror)
    {
        var bytes = File.ReadAllBytes(HandBytes);
        using (var br = new BinaryReader(new MemoryStream(bytes)))
        {
            int n = br.ReadInt32(), t = br.ReadInt32();
            var v = new Vector3[n];
            var nr = new Vector3[n];
            var uv = new Vector2[n];
            var bw = new BoneWeight[n];
            var ids = new int[n * 4];
            for (int i = 0; i < n; i++) v[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < n; i++) nr[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < n * 4; i++) ids[i] = br.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                bw[i].boneIndex0 = ids[i * 4]; bw[i].boneIndex1 = ids[i * 4 + 1];
                bw[i].boneIndex2 = ids[i * 4 + 2]; bw[i].boneIndex3 = ids[i * 4 + 3];
                bw[i].weight0 = br.ReadSingle(); bw[i].weight1 = br.ReadSingle();
                bw[i].weight2 = br.ReadSingle(); bw[i].weight3 = br.ReadSingle();
                uv[i] = new Vector2(0.25f, 0.5f);
            }
            var tris = new int[t * 3];
            for (int i = 0; i < t; i++)
            {
                int a = br.ReadInt32(), b = br.ReadInt32(), c = br.ReadInt32();
                if (mirror) { tris[i * 3] = a; tris[i * 3 + 1] = c; tris[i * 3 + 2] = b; }
                else { tris[i * 3] = a; tris[i * 3 + 1] = b; tris[i * 3 + 2] = c; }
            }
            if (mirror)
                for (int i = 0; i < n; i++) { v[i].x = -v[i].x; nr[i].x = -nr[i].x; }
            var mesh = new Mesh();
            mesh.name = mirror ? "HandSkinned_L" : "HandSkinned_R";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.normals = nr;
            mesh.uv = uv;
            mesh.boneWeights = bw;
            mesh.triangles = tris;
            return mesh;
        }
    }

    // Рука на скелете: 15 костей (корень, 4 пальца по 3, большой палец 2). Анимирует HandAnimator.
    static Hand MakeHand(Transform parent, string name, XRNode node)
    {
        bool left = node == XRNode.LeftHand;
        float sx = left ? -1f : 1f;     // левая рука — зеркало правой (без отрицательного масштаба)

        var h = new GameObject(name);
        h.transform.SetParent(parent, false);
        h.transform.localPosition = new Vector3(left ? -0.25f : 0.25f, 1.1f, 0.3f);
        var pose = h.AddComponent<XRPose>();
        pose.node = node;
        var hand = h.AddComponent<Hand>();
        hand.node = node;

        var model = new GameObject("HandModel");
        model.transform.SetParent(h.transform, false);
        var meshGo = new GameObject("Mesh");
        meshGo.transform.SetParent(model.transform, false);

        var bones = new Transform[15];
        bones[0] = model.transform;
        for (int f = 0; f < 4; f++)
        {
            Transform p = model.transform;
            for (int j = 0; j < 3; j++)
            {
                var b = new GameObject("Палец" + (f + 1) + "_" + (j + 1)).transform;
                b.SetParent(p, false);
                if (j == 0)
                {
                    b.localPosition = new Vector3(sx * FingerBase[f].x, FingerBase[f].y, FingerBase[f].z);
                    b.localRotation = Quaternion.Euler(FingerSpread[f], 0f, 0f);
                }
                else b.localPosition = new Vector3(0f, 0f, SegLen[j - 1] * FingerScale[f]);
                bones[1 + 3 * f + j] = b;
                p = b;
            }
        }
        var t1 = new GameObject("Большой_1").transform;
        t1.SetParent(model.transform, false);
        t1.localPosition = new Vector3(sx * -0.014f, 0.030f, 0.012f);
        t1.localRotation = Quaternion.FromToRotation(Vector3.forward, new Vector3(sx * -0.15f, 0.75f, 0.65f).normalized);
        var t2 = new GameObject("Большой_2").transform;
        t2.SetParent(t1, false);
        t2.localPosition = new Vector3(0f, 0f, 0.038f);
        bones[13] = t1;
        bones[14] = t2;

        var mesh = LoadHandMesh(left);
        var binds = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            binds[i] = bones[i].worldToLocalMatrix * meshGo.transform.localToWorldMatrix;
        mesh.bindposes = binds;
        mesh.RecalculateBounds();
        string assetPath = "Assets/Models/HandSkinned_" + (left ? "L" : "R") + ".asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(mesh, assetPath);

        var smr = meshGo.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = bones;
        smr.rootBone = model.transform;
        smr.sharedMaterial = MT("Hand", Color.white, "Tex_Hand", Vector2.one);
        smr.localBounds = new Bounds(new Vector3(0f, 0.02f, 0.02f), new Vector3(0.2f, 0.22f, 0.32f));

        var anim = h.AddComponent<HandAnimator>();
        anim.node = node;
        anim.hand = hand;
        anim.side = sx;
        anim.index = new[] { bones[1], bones[2], bones[3] };
        anim.middle = new[] { bones[4], bones[5], bones[6] };
        anim.ring = new[] { bones[7], bones[8], bones[9] };
        anim.pinky = new[] { bones[10], bones[11], bones[12] };
        anim.thumb = new[] { t1, t2 };
        return hand;
    }

    // ================= помощники =================
    static Material M(string name, Color c)
    {
        Material cached;
        if (mats.TryGetValue(name, out cached)) return cached;
        string path = "Assets/Materials/" + name + ".mat";
        Shader sh = urp ? Shader.Find("Universal Render Pipeline/Lit") : null;
        if (sh == null) sh = Shader.Find("Standard");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    // материал с текстурой из Assets/Textures
    static Material MT(string name, Color c, string texName, Vector2 tiling)
    {
        var m = M(name, c);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + texName + ".png");
        if (tex != null)
        {
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            EditorUtility.SetDirty(m);
        }
        return m;
    }

    static void MakeTransparent(Material m)
    {
        if (m.shader != null && m.shader.name.Contains("Universal"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
        else
        {
            m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
    }

    static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Model(string file, string name, Transform parent, Vector3 pos, float yRot, Material m, bool meshCollider)
    {
        GameObject g;
        string partsPath = "Assets/Models/" + file + ".parts";
        if (File.Exists(partsPath)) g = BuildMeshObject(file, partsPath);
        else g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/" + file + ".obj"));
        g.name = name;
        if (parent != null) g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        if (m != null)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
            }
        }
        if (meshCollider)
        {
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }
        return g;
    }

    // Модель из файла .parts (одна часть). Читается напрямую — результат не зависит от настроек импортёра OBJ.
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    static GameObject BuildMeshObject(string file, string path)
    {
        Mesh mesh;
        if (!meshCache.TryGetValue(file, out mesh) || mesh == null)
        {
            using (var br = new BinaryReader(File.OpenRead(path)))
            {
                br.ReadInt32();                                   // число частей (здесь всегда 1)
                int len = br.ReadInt32();
                br.ReadBytes(len);                                // имя
                br.ReadSingle(); br.ReadSingle(); br.ReadSingle();    // pivot
                int nv = br.ReadInt32(), nt = br.ReadInt32();
                var v = new Vector3[nv];
                var n = new Vector3[nv];
                var uv = new Vector2[nv];
                var tris = new int[nt * 3];
                for (int i = 0; i < nv; i++) v[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                for (int i = 0; i < nv; i++) n[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                for (int i = 0; i < nv; i++) uv[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                for (int i = 0; i < nt * 3; i++) tris[i] = br.ReadInt32();
                mesh = new Mesh();
                mesh.name = file;
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = v;
                mesh.normals = n;
                mesh.uv = uv;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
            }
            Directory.CreateDirectory("Assets/Models/Meshes");
            string assetPath = "Assets/Models/Meshes/" + file + ".asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(mesh, assetPath);
            meshCache[file] = mesh;
        }
        var g = new GameObject(file);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>();
        return g;
    }

    static Bounds LocalBounds(GameObject g)
    {
        bool first = true;
        var result = new Bounds();
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = g.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (first) { result = new Bounds(p, Vector3.zero); first = false; }
                else result.Encapsulate(p);
            }
        }
        return result;
    }

    static void AddBoxCollider(GameObject g)
    {
        var b = LocalBounds(g);
        var bc = g.AddComponent<BoxCollider>();
        bc.center = b.center;
        bc.size = b.size;
    }

    static void AddBox(GameObject g, string name, Vector3 center, Vector3 size)
    {
        var child = new GameObject(name);
        child.transform.SetParent(g.transform, false);
        var bc = child.AddComponent<BoxCollider>();
        bc.center = center;
        bc.size = size;
    }

    static Zone MakeZone(string name, Transform parent, Vector3 pos, Vector3 size)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        var bc = g.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = size;
        return g.AddComponent<global::Zone>();
    }

    static void FlatLabel(Transform parent, Vector3 pos, string text, float size, Color color)
    {
        var g = new GameObject("Надпись");
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
        var l = g.AddComponent<Label>();
        l.text = text;
        l.size = size;
        l.color = color;
    }

    // Доска с текстом. yaw — поворот вокруг Y так, чтобы текст читался со стороны зрителя
    // (180 — зритель на +Z, -90 — зритель на +X).
    static Label Board(Transform parent, Vector3 pos, Vector2 size, float yaw, string text, float textSize, Color color, bool collider = true)
    {
        var rot = Quaternion.Euler(0f, yaw, 0f);
        var back = Cube("Доска", parent, pos, new Vector3(size.x, size.y, 0.05f), M("Board", new Color(0.08f, 0.1f, 0.12f)), collider);
        back.transform.rotation = rot;
        var t = new GameObject("Текст");
        t.transform.SetParent(parent, false);
        t.transform.position = pos - rot * Vector3.forward * 0.04f;
        t.transform.rotation = rot;
        var l = t.AddComponent<Label>();
        l.text = text;
        l.size = textSize;
        l.color = color;
        return l;
    }
}
