using System;
using System.Collections.Generic;
using Gstarr.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyVisualValidation
{
    private static readonly List<EnemyActor> samples = new();
    private static readonly Dictionary<EnemyActor, Sprite> initial = new();
    private static bool changed;
    private static double started;
    private static int step;

    static EnemyVisualValidation()
    {
        if (SessionState.GetBool("EnemyVisualValidation", false))
            EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Gstarr/Validate Enemy Visuals")]
    public static void Run()
    {
        samples.Clear();
        initial.Clear();
        changed = false;
        step = 0;
        SessionState.SetBool("EnemyVisualValidation", true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorSceneManager.OpenScene("Assets/Scenes/Stage_1.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (step == 0)
            {
                started = EditorApplication.timeSinceStartup;
                step = 1;
                return;
            }
            if (EditorApplication.timeSinceStartup - started < 2) return;
            if (step == 1)
            {
                var stageEnemies = UnityEngine.Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
                if (stageEnemies.Length == 0) throw new Exception("Stage_1 spawned no enemies");
                foreach (var actor in stageEnemies)
                    if (actor.GetComponent<SpriteRenderer>().sprite == null)
                        throw new Exception("Stage enemy missing art: " + actor.Kind);
                Debug.Log("ENEMY_STAGE_OK count=" + stageEnemies.Length);
                Gstarr.Game.GamePause.SetPaused(false);
                Time.timeScale = 1f;
                foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                {
                    var actor = EnemyActor.Spawn(null, new Vector3(1000 + (int)kind * 4, 100, 0), kind);
                    actor.GetComponent<Rigidbody2D>().simulated = false;
                    var sr = actor.GetComponent<SpriteRenderer>();
                    if (sr.sprite == null || sr.sprite.bounds.size.y > 8 || sr.sprite.bounds.size.y < 0.6f)
                        throw new Exception("Missing or incorrectly scaled art: " + kind);
                    samples.Add(actor);
                    initial[actor] = sr.sprite;
                }
                started = EditorApplication.timeSinceStartup;
                step = 2;
                return;
            }
            foreach (var actor in samples)
            {
                if (actor.Kind == EnemyKind.Bunny || actor.Kind == EnemyKind.TwinBunny || actor.Kind == EnemyKind.DistortedBunny)
                {
                    if (actor.GetComponent<NormalBunny>() == null || actor.GetComponent<CharacterFoe>() != null)
                        throw new Exception("Bunny must use only NormalBunny");
                    continue;
                }
                if (actor.Kind == EnemyKind.Angel)
                {
                    if (actor.GetComponent<Angel>() == null || actor.GetComponent<CharacterFoe>() != null)
                        throw new Exception("Angel must use only Angel");
                    continue;
                }
                if (actor.Kind == EnemyKind.AnnoyingKid)
                {
                    if (actor.GetComponent<AnnoyingKid>() == null || actor.GetComponent<CharacterFoe>() != null)
                        throw new Exception("Annoying kid must use only AnnoyingKid");
                    continue;
                }
                if (actor.Kind == EnemyKind.BlackShadowKid)
                {
                    if (actor.GetComponent<BlackKid>() == null || actor.GetComponent<CharacterFoe>() != null)
                        throw new Exception("Black kid must use only BlackKid");
                    continue;
                }
                if (actor.Kind == EnemyKind.BlackShadowThrow)
                {
                    if (actor.GetComponent<BlackKidThrow>() == null || actor.GetComponent<CharacterFoe>() != null)
                        throw new Exception("Black kid throw must use only BlackKidThrow");
                    continue;
                }
                if (actor.Kind == EnemyKind.Shadowman)
                {
                    if (actor.GetComponent<ShadowMan>() == null || actor.GetComponent<CharacterFoe>() != null)
                        throw new Exception("Shadowman must use only ShadowMan");
                    continue;
                }
                var foe = actor.GetComponent<CharacterFoe>();
                if (foe == null) continue;
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(CharacterFoe).GetMethod("SetPose", flags)
                    .Invoke(foe, new object[] { CharacterArt.Pose.Charge, 0.48f });
                typeof(CharacterFoe).GetMethod("Animate", flags).Invoke(foe, new object[] { 0.2f });
                if (actor.GetComponent<SpriteRenderer>().sprite != initial[actor])
                    changed = true;
            }
            if (!changed && EditorApplication.timeSinceStartup - started < 5) return;
            if (!changed && HasCharacterFoe()) throw new Exception("Character animation did not advance in Play mode");
            CheckAnimationSequences();
            Debug.Log("ENEMY_VISUALS_OK: Stage_1 spawn, all 15 kinds have correctly sized sprites, Character animation advanced in Play mode");
            Finish(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Finish(1);
        }
    }

    private static void CheckAnimationSequences()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var setPose = typeof(CharacterFoe).GetMethod("SetPose", flags);
        var animate = typeof(CharacterFoe).GetMethod("Animate", flags);
        var covered = new HashSet<string>();
        bool any = false;
        foreach (var actor in samples)
        {
            var foe = actor.GetComponent<CharacterFoe>();
            if (foe == null) continue;
            any = true;
            var renderer = actor.GetComponent<SpriteRenderer>();
            foreach (CharacterArt.Pose pose in Enum.GetValues(typeof(CharacterArt.Pose)))
            {
                var frames = CharacterArt.Frames(actor.Kind, pose);
                if (frames.Length == 0) throw new Exception($"No frames: {actor.Kind}/{pose}");
                setPose.Invoke(foe, new object[] { pose == CharacterArt.Pose.Idle ? CharacterArt.Pose.Charge : CharacterArt.Pose.Idle, 0f });
                setPose.Invoke(foe, new object[] { pose, frames.Length * 0.1f });
                for (int i = 0; i < frames.Length; i++)
                {
                    if (i > 0) animate.Invoke(foe, new object[] { 0.10001f });
                    if (renderer.sprite != frames[i]) throw new Exception($"Frame skipped: {actor.Kind}/{pose}/{i}");
                    covered.Add(frames[i].name);
                }
                bool loop = pose == CharacterArt.Pose.Idle
                    || (pose == CharacterArt.Pose.Charge && actor.Kind == EnemyKind.BlackShadowKid);
                animate.Invoke(foe, new object[] { 0.10001f });
                if (renderer.sprite != frames[loop ? 0 : frames.Length - 1])
                    throw new Exception($"Incorrect loop/hold: {actor.Kind}/{pose}");
            }
        }
        if (!any)
        {
            Debug.Log("ENEMY_ANIMATION_SEQUENCES_OK: character foes use dedicated controllers");
            return;
        }
        if (covered.Count != 10) throw new Exception("Expected 10 non-bunny character images, got " + covered.Count);
        Debug.Log("ENEMY_ANIMATION_SEQUENCES_OK: 10 non-bunny images, ordered frames, loop/hold behavior");
    }

    private static bool HasCharacterFoe()
    {
        for (int i = 0; i < samples.Count; i++)
            if (samples[i] != null && samples[i].GetComponent<CharacterFoe>() != null)
                return true;
        return false;
    }

    private static void Finish(int code)
    {
        SessionState.SetBool("EnemyVisualValidation", false);
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else foreach (var actor in samples) if (actor != null) UnityEngine.Object.Destroy(actor.gameObject);
    }
}
