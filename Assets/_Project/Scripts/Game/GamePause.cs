using UnityEngine;

namespace Gstarr.Game
{
    /// <summary>일시정지 / 카운트다운 / 게임 오버 동안 플레이어와 물리 입력을 잠근다.</summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }
        public static bool IsCountdown { get; private set; }
        public static bool BlocksGameplay => IsPaused || IsCountdown;

        public static void SetPaused(bool paused)
        {
            IsPaused = paused;
            ApplyTimeScale();
        }

        public static void SetCountdown(bool countdown)
        {
            IsCountdown = countdown;
            ApplyTimeScale();
        }

        public static void Clear()
        {
            IsPaused = false;
            IsCountdown = false;
            ApplyTimeScale();
        }

        private static void ApplyTimeScale()
        {
            Time.timeScale = BlocksGameplay ? 0f : 1f;
        }
    }
}
