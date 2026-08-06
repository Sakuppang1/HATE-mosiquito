using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialSceneController))]
public class TutorialSceneControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var ctrl = (TutorialSceneController)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "调节图片大小：\n" +
            "1) 改 Image Anchor Min/Max（屏幕比例 0~1）\n" +
            "2) 或点下面按钮生成 UI 到场景 → 勾选 Use Scene Rect Layout → 在 Scene 里拖 TutorialImageFrame\n" +
            "若图显示不全：在 Pages 里勾选 Use Full Texture，或换更大的 Sprite。\n" +
            "玩法演示：菜单 HATE → Build GameplayRoot Prefab And Wire Tutorial（只需运行一次）。",
            MessageType.Info);

        if (GUILayout.Button("生成教程 UI 到场景"))
            ctrl.EditorBuildUiToScene();

        if (GUILayout.Button("应用布局（Inspector 锚点 → UI）"))
            ctrl.ApplyLayout();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("构建 GameplayRoot 并挂到本场景（演示第三页必需）"))
            GameplayRootPrefabBuilder.BuildAndWireTutorial();
    }
}
