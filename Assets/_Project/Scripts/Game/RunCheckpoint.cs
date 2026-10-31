using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gstarr.Game
{
    /// <summary>보스를 쓰러뜨린 자리. 이어 하면 점수는 새 판이라 0부터다.</summary>
    public static class RunCheckpoint
    {
        public static bool HasSave { get; private set; }
        public static bool ResumedThisLoad { get; private set; }
        public static Vector3 Position { get; private set; }
        public static float FloorY { get; private set; }
        public static int ClearedThrough { get; private set; }
        public static double SongTime { get; private set; }
        public static bool HasSong { get; private set; }
        public static bool DeathHeardBoss { get; private set; }
        public static float BossTime { get; private set; }
        public static bool SuppressBossTheme { get; private set; }

        private static bool pending;
        private static string scene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HasSave = false;
            ResumedThisLoad = false;
            HasSong = false;
            DeathHeardBoss = false;
            SuppressBossTheme = false;
            pending = false;
            scene = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene loaded, LoadSceneMode mode)
        {
            ResumedThisLoad = false;
        }

        public static void Save(string sceneName, Vector3 standPosition, float floorY, int clearedThrough)
        {
            HasSave = true;
            scene = sceneName;
            Position = standPosition;
            FloorY = floorY;
            ClearedThrough = clearedThrough;
        }

        public static void RememberDeathSong(double songTime, bool heardBoss, float bossTime)
        {
            SongTime = songTime;
            HasSong = true;
            DeathHeardBoss = heardBoss;
            BossTime = bossTime;
            SuppressBossTheme = !heardBoss;
        }

        public static void ReleaseBossTheme() => SuppressBossTheme = false;

        public static void ArmContinue()
        {
            if (!HasSave) return;
            pending = true;
        }

        public static bool TryConsume(string sceneName, out Vector3 position, out float floorY, out int clearedThrough)
        {
            position = Position;
            floorY = FloorY;
            clearedThrough = ClearedThrough;
            if (!pending || scene != sceneName)
            {
                pending = false;
                return false;
            }

            pending = false;
            ResumedThisLoad = true;
            return true;
        }

        public static void Forget()
        {
            HasSave = false;
            pending = false;
            ResumedThisLoad = false;
            HasSong = false;
            DeathHeardBoss = false;
            SuppressBossTheme = false;
            scene = null;
        }
    }
}
