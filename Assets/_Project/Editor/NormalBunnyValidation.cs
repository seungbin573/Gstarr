using System;
using System.Collections.Generic;
using Gstarr.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in an isolated, unsaved scene; exercises real FixedUpdate and physics callbacks.
[InitializeOnLoad]
public static class NormalBunnyValidation
{
    private static EnemyActor bunny;
    private static GameObject player;
    private static Health health;
    private static readonly HashSet<Sprite> seen = new();
    private static float started;
    private static int step;
    private static Vector3 origin;
    private static double deadline;

    static NormalBunnyValidation()
    {
        if (SessionState.GetBool("NormalBunnyValidation", false)) Attach();
    }

    [MenuItem("Tools/Gstarr/Validate Normal Bunny")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("NormalBunnyValidation", true);
        Attach();
        EditorApplication.isPlaying = true;
    }

    private static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 90;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Bunny validation timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (step == 0)
            {
                Gstarr.Game.GamePause.Clear();
                var floor = new GameObject("Test floor");
                floor.layer = LayerMask.NameToLayer("Ground");
                floor.transform.position = new Vector3(0, -0.5f);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(50, 1);
                player = new GameObject("Test player");
                player.tag = "Player";
                player.transform.position = new Vector3(20, 1);
                player.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 1.9f);
                health = player.AddComponent<Health>();
                health.Configure(5, false);
                // Exercise the retired spawn API as well as the stage's Bunny tile.
                var legacy = EnemyActor.Spawn(null, new Vector3(-20, 0), EnemyKind.BeatChaser);
                Check(legacy.Kind == EnemyKind.Bunny && legacy.GetComponent<NormalBunny>() != null,
                    "Legacy spawn still creates a walking rabbit");
                Check(legacy.GetComponent<Gstarr.Game.SpriteMotion>() == null
                    && legacy.transform.Find("BodyVisual") == null, "Legacy walking visual remains");
                UnityEngine.Object.Destroy(legacy.gameObject);
                bunny = EnemyActor.Spawn(null, Vector3.zero, EnemyKind.Bunny);
                Check(bunny.GetComponent<NormalBunny>() != null && bunny.GetComponent<CharacterFoe>() == null,
                    "Legacy bunny controller still attached");
                started = Time.time;
                step = 1;
                return;
            }
            var sprite = bunny.GetComponent<SpriteRenderer>().sprite;
            float elapsed = Time.time - started;
            if (step == 1)
            {
                Check(Mathf.Abs(bunny.transform.position.x) < 0.01f, "Idle bunny moved");
                Check(health.Current == 5, "Idle bunny dealt damage");
                if (elapsed < 0.5f) return;
                player.transform.position = new Vector3(2.6f, 1);
                seen.Clear();
                started = Time.time;
                step = 2;
                return;
            }
            if (step == 2)
            {
                seen.Add(sprite);
                if (elapsed < 0.35f)
                {
                    Check(Mathf.Abs(bunny.transform.position.x) < 0.01f, "Windup bunny moved");
                    Check(health.Current == 5, "Windup dealt damage");
                }
                if (elapsed < 1.1f) return;
                Check(bunny.transform.position.x > 0.5f, "Bunny never dashed");
                Check(health.Current == 4, "Dash must deal exactly one damage, including sustained contact");
                foreach (var frame in CharacterArt.Frames(EnemyKind.Bunny, CharacterArt.Pose.Charge))
                    Check(seen.Contains(frame), "Missing animation frame: " + frame.name);
                origin = bunny.transform.position;
                started = Time.time;
                step = 3;
                return;
            }
            Check(Mathf.Abs(bunny.transform.position.x - origin.x) < 0.01f, "Recovery bunny moved");
            Check(health.Current == 4, "Recovery dealt damage");
            if (elapsed < 0.3f) return;
            Debug.Log("NORMAL_BUNNY_OK: idle/windup stationary, 3 attack frames, dash movement, exactly 1 contact damage, stationary recovery");
            Finish(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool("NormalBunnyValidation", false);
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
