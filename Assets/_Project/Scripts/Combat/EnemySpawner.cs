using Gstarr.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gstarr.Combat
{
    /// <summary>씬에 적을 직접 배치하지 않았으면 다섯 종류를 플레이어 앞쪽에 배치한다.</summary>
    public static class EnemySpawner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Object.FindAnyObjectByType<MainMenu>() != null) return;
            if (Object.FindAnyObjectByType<EnemyActor>() != null) return;
            if (Object.FindAnyObjectByType<EnemyTileSpawner>() != null) return;
            if (EnemyTileSpawner.HasPlacedEnemies()) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Vector3 origin = player.transform.position;
            EnemyActor.Spawn(null, origin + new Vector3(9f, 0.2f, 0f), EnemyKind.BeatChaser);
            EnemyActor.Spawn(null, origin + new Vector3(19f, 0.2f, 0f), EnemyKind.RangedBeat);
            EnemyActor.Spawn(null, origin + new Vector3(25f, 0.2f, 0f), EnemyKind.RangedHalfBeat);
            EnemyActor.Spawn(null, origin + new Vector3(31f, 3f, 0f), EnemyKind.Flyer);
            EnemyActor.Spawn(null, origin + new Vector3(38f, 0.2f, 0f), EnemyKind.Mole);
        }
    }
}
