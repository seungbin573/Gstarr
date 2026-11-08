using System;
using System.Reflection;
using Gstarr.Player;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerPuppetValidation
{
    static PlayerPuppetValidation()
    {
        if (SessionState.GetBool("PlayerPuppetValidation", false)) EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Gstarr/Validate Player Puppet")]
    public static void Run()
    {
        SessionState.SetBool("PlayerPuppetValidation", true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.update -= Tick;
        SessionState.SetBool("PlayerPuppetValidation", false);
        int code = 0;
        try { Check(); }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.isPlaying = false;
    }

    private static void Check()
    {
        var go = new GameObject("PuppetValidation");
        go.SetActive(false);
        try
        {
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<PlayerController>();
            var puppet = go.AddComponent<PlayerPuppet>();
            Call(puppet, "Awake");
            var rig = go.transform.Find("PlayerRig");
            var back = rig.Find("BootBack");
            var front = rig.Find("BootFront");
            if (back == null || front == null) throw new Exception("Feet must be independent of torso transforms");
            var head = rig.Find("Hoodie/Head").GetComponent<SpriteRenderer>();
            var open = head.sprite;
            if (open == null || !head.flipX) throw new Exception("Open head missing or facing left");
            Call(puppet, "TickBlink", 3f);
            var closed = head.sprite;
            if (closed == null || closed == open) throw new Exception("Blink did not close eyes");
            if (open.rect != closed.rect || open.pivot != closed.pivot || open.pixelsPerUnit != closed.pixelsPerUnit)
                throw new Exception("Blink changes head alignment");
            Call(puppet, "TickBlink", 0.13f);
            if (head.sprite != open) throw new Exception("Blink did not reopen eyes");
            for (int facing = -1; facing <= 1; facing += 2)
            {
                go.transform.localScale = new Vector3(facing, 1, 1);
                for (int i = 0; i < 60; i++)
                {
                    float phase = i * Mathf.PI / 30f;
                    Set(puppet, "walkCycle", phase);
                    Set(puppet, "walkWeight", 1f);
                    Call(puppet, "PoseWalk", Mathf.Sin(phase), 8f);
                    if (Quaternion.Angle(back.rotation, Quaternion.identity) > 0.01f || Quaternion.Angle(front.rotation, Quaternion.identity) > 0.01f)
                        throw new Exception("Walking tilts soles");
                    if (Mathf.Min(back.localPosition.y, front.localPosition.y) < 0f)
                        throw new Exception("Foot dropped below rest anchor");
                }
            }
            Debug.Log("PLAYER_PUPPET_OK: both facings, 60 gait samples, independent feet, aligned blink frames and reopen");
        }
        finally { UnityEngine.Object.Destroy(go); }
    }

    private static void Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
