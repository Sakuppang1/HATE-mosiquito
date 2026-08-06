using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 从 GameScene 提取玩法核心对象，生成 GameplayRoot.prefab，并挂到 TutorialScene（默认禁用）。
/// 菜单：HATE / Build GameplayRoot Prefab And Wire Tutorial
/// </summary>
public static class GameplayRootPrefabBuilder
{
    const string PrefabPath = "Assets/Prefabs/GameplayRoot.prefab";
    const string GameScenePath = "Assets/Scenes/GameScene.unity";
    const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";

    static readonly string[] GameplayRootChildNames =
    {
        "background_0",
        "frog",
        "CenterZone",
        "BeatManager",
        "ScoreManager",
        "ScoreDisplay",
        "SpawnPoint",
        "WordSpawnerManager",
        "MicVolumeHud"
    };

    [MenuItem("HATE/Build GameplayRoot Prefab And Wire Tutorial")]
    public static void BuildAndWireTutorial()
    {
        BuildAndWireTutorialInternal(showDialogs: true);
    }

    public static void BuildAndWireTutorialBatch()
    {
        BuildAndWireTutorialInternal(showDialogs: false);
    }

    static void BuildAndWireTutorialInternal(bool showDialogs)
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "Scenes/GameScene.unity")))
        {
            if (showDialogs)
                EditorUtility.DisplayDialog("GameplayRoot", "找不到 GameScene。", "OK");
            else
                Debug.LogError("GameplayRootPrefabBuilder: 找不到 GameScene。");
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        var activePath = activeScene.path;

        if (showDialogs && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        var rootGo = new GameObject("GameplayRoot");
        var gameplayRoot = rootGo.AddComponent<GameplayRoot>();

        foreach (var name in GameplayRootChildNames)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning("GameplayRootPrefabBuilder: 未找到 " + name);
                continue;
            }

            var copy = Object.Instantiate(go);
            copy.name = name;
            copy.transform.SetParent(rootGo.transform, true);
        }

        FixMicHudCanvasScale(rootGo);
        RewireGameplayReferences(rootGo);

        var wordSpawner = rootGo.GetComponentInChildren<WordSpawner>(true);
        var tutorialWordList = AssetDatabase.LoadAssetAtPath<WordData>("Assets/TutorialWordList.asset");
        if (tutorialWordList != null)
        {
            var so = new SerializedObject(gameplayRoot);
            so.FindProperty("tutorialWordLibrary").objectReferenceValue = tutorialWordList;
            so.FindProperty("wordSpawner").objectReferenceValue = wordSpawner;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs"));
        var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
        Object.DestroyImmediate(rootGo);

        if (prefab == null)
        {
            if (showDialogs)
                EditorUtility.DisplayDialog("GameplayRoot", "保存 Prefab 失败。", "OK");
            else
                Debug.LogError("GameplayRootPrefabBuilder: 保存 Prefab 失败。");
            EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
            return;
        }

        Debug.Log("已保存 " + PrefabPath);

        CopyPrefabToResources();

        WireTutorialScene(prefab, tutorialWordList);

        if (!string.IsNullOrEmpty(activePath))
            EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);

        if (showDialogs)
        {
            EditorUtility.DisplayDialog(
                "GameplayRoot",
                "已生成 " + PrefabPath + " 并挂到 TutorialScene（默认禁用）。\n" +
                "请在 GameScene 中手动将相同对象替换为 Prefab 实例（可选，便于两场景共用）。",
                "OK");
        }
    }

    static void FixMicHudCanvasScale(GameObject rootGo)
    {
        var canvases = rootGo.GetComponentsInChildren<Canvas>(true);
        foreach (var canvas in canvases)
        {
            var rt = canvas.GetComponent<RectTransform>();
            if (rt != null && rt.localScale == Vector3.zero)
                rt.localScale = Vector3.one;
        }
    }

    static void RewireGameplayReferences(GameObject rootGo)
    {
        var frog = rootGo.transform.Find("frog");
        var centerZone = rootGo.transform.Find("CenterZone");
        var scoreDisplay = rootGo.transform.Find("ScoreDisplay");
        var spawnPoint = rootGo.transform.Find("SpawnPoint");
        var micHud = rootGo.GetComponentInChildren<MicVolumeBarUI>(true);

        if (centerZone != null && frog != null)
        {
            var center = centerZone.GetComponent<CenterZoneController>();
            if (center != null)
            {
                center.frogAnimator = frog.GetComponent<Animator>();
                if (micHud != null)
                {
                    var centerSo = new SerializedObject(center);
                    centerSo.FindProperty("micVolumeBarUi").objectReferenceValue = micHud;
                    centerSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        var scoreManager = rootGo.GetComponentInChildren<ScoreManager>(true);
        if (scoreManager != null && scoreDisplay != null)
        {
            var tmp = scoreDisplay.GetComponent<TMPro.TextMeshPro>();
            scoreManager.scoreText = tmp;
        }

        var wordSpawner = rootGo.GetComponentInChildren<WordSpawner>(true);
        if (wordSpawner != null && spawnPoint != null)
            wordSpawner.spawnPoint = spawnPoint;

        if (micHud != null && centerZone != null)
        {
            var center = centerZone.GetComponent<CenterZoneController>();
            var micSo = new SerializedObject(micHud);
            micSo.FindProperty("micController").objectReferenceValue = center;
            micSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void WireTutorialScene(GameObject prefab, WordData tutorialWordList)
    {
        EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

        var existing = GameObject.Find("GameplayRoot");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "GameplayRoot";
        instance.SetActive(false);

        var controller = Object.FindFirstObjectByType<TutorialSceneController>();
        if (controller != null)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("gameplayRoot").objectReferenceValue = instance.GetComponent<GameplayRoot>();
            var pages = so.FindProperty("pages");
            if (pages != null && pages.arraySize >= 3)
            {
                var demoPage = pages.GetArrayElementAtIndex(2);
                demoPage.FindPropertyRelative("interactiveDemo").boolValue = true;
                demoPage.FindPropertyRelative("caption").stringValue =
                    "听发音，等重音块 PU 到中间判定区时大声喊！";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("TutorialScene 已挂载 GameplayRoot（禁用）并关联 TutorialController。");
    }

    static void CopyPrefabToResources()
    {
        const string resourcesPath = "Assets/Resources/Prefabs/GameplayRoot.prefab";
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Prefabs"));
        if (AssetDatabase.LoadAssetAtPath<GameObject>(resourcesPath) != null)
            AssetDatabase.DeleteAsset(resourcesPath);
        if (!AssetDatabase.CopyAsset(PrefabPath, resourcesPath))
            Debug.LogWarning("GameplayRootPrefabBuilder: 复制到 Resources 失败。");
        else
            Debug.Log("已复制到 " + resourcesPath);
    }
}
