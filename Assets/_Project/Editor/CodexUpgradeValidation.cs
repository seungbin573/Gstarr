using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
public static class CodexUpgradeValidation
{
    [MenuItem("Tools/Gstarr/Validate Unity Upgrade")]
    public static void Run()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
        var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
        if (pipeline == null || renderer == null) throw new Exception("URP assets failed to load");
        var data = new SerializedObject(pipeline);
        var list = data.FindProperty("m_RendererDataList");
        var index = data.FindProperty("m_DefaultRendererIndex").intValue;
        if (index < 0 || index >= list.arraySize || list.GetArrayElementAtIndex(index).objectReferenceValue != renderer)
            throw new Exception("Default renderer reference is invalid");
        if (pipeline.scriptableRenderer == null) throw new Exception("Renderer creation failed");
        var go = new GameObject("Camera check", typeof(Camera));
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        var follow = go.AddComponent<Gstarr.CameraSystem.CameraFollow2D>();
        follow.Target = go.transform;
        follow.BeginFocus(go.transform, 3.1f);
        if (!Mathf.Approximately(cam.orthographicSize, 3.1f)) throw new Exception("Focus zoom failed");
        follow.BeginFocus(go.transform, 2f);
        follow.EndFocus();
        follow.EndFocus();
        if (!Mathf.Approximately(cam.orthographicSize, 5f) || follow.Target != go.transform) throw new Exception("Focus restore failed");
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("CODEX_VALIDATION_OK: camera focus/restore, compilation, URP assets, default Renderer2D creation");
    }
}


