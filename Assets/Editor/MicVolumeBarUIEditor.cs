using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MicVolumeBarUI))]
public class MicVolumeBarUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var hud = (MicVolumeBarUI)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "勾选「Use Scene Rect Layout」后，在 Hierarchy 展开 MicVolumeHUD_Canvas → MicBarPanel，" +
            "用 Rect Tool (T) 在 Scene 视图直接拖动、缩放音量条。",
            MessageType.Info);

        if (GUILayout.Button("从子物体自动绑定引用"))
            hud.EditorAutoWireReferences();

        if (GUILayout.Button("生成/重建音量条 UI"))
            hud.EditorRebuildUi();
    }
}
