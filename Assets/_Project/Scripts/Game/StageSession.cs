using Gstarr.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gstarr.Game
{
    /// <summary>메뉴에서 고른 스테이지를 씬 전환 너머로 전달.</summary>
    public static class StageSession
    {
        public static StageData Current { get; private set; }

        public static void Play(StageData stage)
        {
            Current = stage;
            Load(stage.SceneName);
        }

        public static void Retry() => Load(SceneManager.GetActiveScene().name);

        public static void ToMainMenu()
        {
            var assets = GameAssets.Instance;
            Load(assets != null ? assets.mainMenuScene : "MainMenu");
        }

        private static void Load(string scene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }
    }
}
