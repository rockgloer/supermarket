using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Инструменты проекта: настройки качества, запекание света и occlusion culling, сборка EXE,
// быстрый переход к настройкам XR и отчёт по критериям.
public static class ProjectTools
{
    const string ScenePath = "Assets/Scenes/Supermarket.unity";

    [MenuItem("Supermarket/Оптимизация/1. Применить настройки качества для VR")]
    public static void ApplyQuality()
    {
        QualitySettings.vSyncCount = 0;                     // частоту кадров задаёт шлем
        QualitySettings.shadowDistance = 12f;
        QualitySettings.pixelLightCount = 4;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.skinWeights = SkinWeights.FourBones;
        QualitySettings.softParticles = false;
        QualitySettings.lodBias = 1f;
        QualitySettings.billboardsFaceCameraPosition = true;
        EditorUtility.DisplayDialog("Оптимизация", "Настройки качества применены: без VSync, тени до 12 м, 4 пиксельных света, анизотропия, 4 кости на вершину.", "OK");
    }

    [MenuItem("Supermarket/Оптимизация/2. Запечь освещение (lightmap) и occlusion culling")]
    public static void BakeAll()
    {
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("Оптимизация", "Сначала соберите сцену: Supermarket → Собрать сцену.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Запекание",
            "Будут запечены lightmap и reflection probes, затем occlusion culling. Это может занять несколько минут. Продолжить?", "Да", "Отмена")) return;

        EditorSceneManager.OpenScene(ScenePath);

        LightingSettings ls;
        if (!Lightmapping.TryGetLightingSettings(out ls))
        {
            ls = new LightingSettings();
            Lightmapping.lightingSettings = ls;
        }
        ls.bakedGI = true;
        ls.realtimeGI = false;
        ls.autoGenerate = false;
        ls.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        ls.lightmapResolution = 8f;
        ls.lightmapMaxSize = 1024;

        bool lm = Lightmapping.Bake();
        bool oc = StaticOcclusionCulling.Compute();
        EditorSceneManager.SaveOpenScenes();
        EditorUtility.DisplayDialog("Запекание",
            "Lightmap и reflection probes: " + (lm ? "готово" : "не удалось (см. Console)") +
            "\nOcclusion culling: " + (oc ? "готово" : "не удалось (см. Console)"), "OK");
    }

    [MenuItem("Supermarket/Собрать EXE (Windows)")]
    public static void BuildExe()
    {
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("Сборка", "Сначала соберите сцену: Supermarket → Собрать сцену.", "OK");
            return;
        }
        PlayerSettings.companyName = "Student Project";
        PlayerSettings.productName = "SupermarketVR";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;              // иначе игра встаёт на паузу при переключении на окно SteamVR
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        Directory.CreateDirectory("Builds/SupermarketVR");
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/SupermarketVR/SupermarketVR.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        EditorUtility.DisplayDialog("Сборка EXE", ok
            ? "Готово: Builds/SupermarketVR/SupermarketVR.exe\nЗапускайте при включённом SteamVR."
            : "Сборка не удалась. Подробности в Console.", "OK");
        if (ok) EditorUtility.RevealInFinder("Builds/SupermarketVR/SupermarketVR.exe");
    }

    [MenuItem("Supermarket/Открыть настройки XR (OpenXR)")]
    public static void OpenXRSettings()
    {
        SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
    }

    [MenuItem("Supermarket/Отчёт: проверка критериев")]
    public static void CriteriaReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ОТЧЁТ ПО КРИТЕРИЯМ (из проекта)");
        sb.AppendLine("3D-модели (.obj / .parts): " + Count("Assets/Models", "*.obj") + " / " + Count("Assets/Models", "*.parts") + "  (нужно ≥ 10)");
        sb.AppendLine("Текстуры (.png): " + Count("Assets/Textures", "*.png") + "  (нужно ≥ 10)");
        sb.AppendLine("Материалы (.mat): " + Count("Assets/Materials", "*.mat") + "  (нужно ≥ 10)");
        sb.AppendLine("Анимации (.anim): " + Count("Assets/Animations", "*.anim") + ", контроллеров: " + Count("Assets/Animations", "*.controller") + "  (нужно ≥ 5)");
        sb.AppendLine("Скрипты (.cs): " + Count("Assets/Scripts", "*.cs") + "  (нужно ≥ 5)");
        sb.AppendLine("Звуки (.wav): " + Count("Assets/Audio", "*.wav"));
        if (File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            int dir = 0, point = 0, spot = 0, area = 0, ps = 0, canvas = 0, probes = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) dir++;
                else if (l.type == LightType.Point) point++;
                else if (l.type == LightType.Spot) spot++;
                else area++;
            }
            ps = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length;
            canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length;
            probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length;
            sb.AppendLine("Свет: Directional " + dir + ", Point " + point + ", Spot " + spot + (area > 0 ? ", Area " + area : "") + "  (нужно ≥ 3 вида)");
            sb.AppendLine("Системы частиц: " + ps + ";  Canvas (UI): " + canvas + ";  Reflection Probes: " + probes);
            EditorSceneManager.CloseScene(scene, true);
        }
        else sb.AppendLine("Сцена ещё не собрана (Supermarket → Собрать сцену).");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Критерии", sb.ToString(), "OK");
    }

    static int Count(string dir, string pattern)
    {
        return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern, SearchOption.AllDirectories).Length : 0;
    }
}
