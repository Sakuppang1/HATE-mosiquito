#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 若 GameplayRoot.prefab 缺失，在编辑器域重载后自动构建并挂到 TutorialScene。
/// </summary>
[InitializeOnLoad]
static class GameplayRootAutoSetup
{
    const string PrefabPath = "Assets/Prefabs/GameplayRoot.prefab";

    static GameplayRootAutoSetup()
    {
        EditorApplication.delayCall += TryBuildIfMissing;
    }

    static void TryBuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;

        Debug.Log("GameplayRootAutoSetup: 未找到 GameplayRoot.prefab，正在自动构建…");
        GameplayRootPrefabBuilder.BuildAndWireTutorialBatch();
    }
}
#endif
