using System;
using System.Collections.Generic;
using System.Reflection;
using Gstarr.Combat;
using UnityEditor;
using UnityEngine;

public static class AngelMonsterValidation
{
    [MenuItem("Tools/Gstarr/Validate Angel Monsters (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run this check in Play Mode.");
        var created = new List<GameObject>();
        try
        {
            string[] names = { CharacterArt.Cross, CharacterArt.Eye, CharacterArt.EyeTwo,
                CharacterArt.GunHand, CharacterArt.SummonedHand, CharacterArt.PullHand,
                CharacterArt.HandSkill, CharacterArt.MultiHand };
            foreach (var name in names)
            {
                var sprite = CharacterArt.Monster(name);
                if (sprite == null || Mathf.Abs(Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) - 1.2f) > 0.01f)
                    throw new Exception("Missing or incorrectly sized monster: " + name);
            }
            EnemyKind[] kinds = { EnemyKind.AngelEye, EnemyKind.AngelEyeTwo, EnemyKind.AngelGunHand };
            foreach (var kind in kinds)
            {
                var enemy = EnemyActor.Spawn(null, new Vector3(1000, 100, 0), kind, 3);
                created.Add(enemy.gameObject);
                if (enemy.GetComponent<AngelSummon>() == null || enemy.GetComponent<CharacterFoe>() != null
                    || enemy.GetComponent<Rigidbody2D>().gravityScale != 0f || !enemy.Floats)
                    throw new Exception("Incorrect summon controller: " + kind);
            }
            var boss = EnemyActor.Spawn(null, new Vector3(1000, 100, 0), EnemyKind.Angel, 32);
            created.Add(boss.gameObject);
            var angel = boss.GetComponent<Angel>();
            angel.BecomeBoss();
            var patterns = new HashSet<string>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            for (int i = 0; i < 8; i++)
            {
                typeof(Angel).GetMethod("FinishRecover", flags).Invoke(angel, null);
                patterns.Add(typeof(Angel).GetField("pattern", flags).GetValue(angel).ToString());
            }
            foreach (var pattern in new[] { "Quake", "GunHands", "Eyes", "PullMobs", "MultiHands" })
                if (!patterns.Contains(pattern)) throw new Exception("Unreachable boss pattern: " + pattern);
            for (int i = 0; i < 7; i++)
                typeof(Angel).GetMethod("SpawnMinion", flags).Invoke(angel,
                    new object[] { EnemyKind.AngelGunHand, new Vector2(1000, 100) });
            var summons = (List<GameObject>)typeof(Angel).GetField("summons", flags).GetValue(angel);
            if (summons.Count != 5) throw new Exception("Summon limit failed");
            var spawned = summons.ToArray();
            typeof(Angel).GetMethod("ClearSummons", flags).Invoke(angel, null);
            foreach (var go in spawned)
                if (go != null && go.activeSelf) throw new Exception("Summon still counted alive after cleanup");
            Debug.Log("ANGEL_MONSTERS_OK: 8 images, 3 flying mob controllers, reachable patterns, cap and cleanup");
        }
        finally
        {
            foreach (var go in created) if (go != null) { go.SetActive(false); UnityEngine.Object.Destroy(go); }
        }
    }
}
