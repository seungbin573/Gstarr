using Gstarr.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gstarr.Combat
{
    /// <summary>씬에 적이 하나도 없으면 플레이어 앞쪽에 셋을 둔다. 직접 배치한 적이 있으면 추가하지 않는다.</summary>
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

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            var sprite = player.GetComponent<SpriteRenderer>() != null
                ? player.GetComponent<SpriteRenderer>().sprite
                : null;
            Vector3 origin = player.transform.position;
            EnemyActor.Spawn(sprite, origin + new Vector3(9f, 0.2f, 0f));
            EnemyActor.Spawn(sprite, origin + new Vector3(18f, 0.2f, 0f));
            EnemyActor.Spawn(sprite, origin + new Vector3(28f, 0.2f, 0f));
        }
    }
}
