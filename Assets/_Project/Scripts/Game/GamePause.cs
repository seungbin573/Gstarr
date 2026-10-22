using UnityEngine;

namespace Gstarr.Game
{
    /// <summary>일시정지 / 카운트다운 / 게임 오버 동안 플레이어와 물리 입력을 잠근다.</summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }
        public static bool IsCountdown { get; private set; }
        public static bool IsGloryKill { get; private set; }
        public static bool BlocksGameplay => IsPaused || IsCountdown || IsGloryKill;

        // 도메인 리로드를 끈 상태에서도 이전 플레이의 timeScale 0 이 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOnPlay()
        {
            IsPaused = false;
            IsCountdown = false;
            IsGloryKill = false;
            Time.timeScale = 1f;
        }

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

        public static void SetGloryKill(bool active)
        {
            IsGloryKill = active;
            ApplyTimeScale();
        }

        public static void Clear()
        {
            IsPaused = false;
            IsCountdown = false;
            IsGloryKill = false;
            ApplyTimeScale();
        }

        private static void ApplyTimeScale()
        {
            if (IsPaused || IsCountdown) Time.timeScale = 0f;
            else if (IsGloryKill) Time.timeScale = 0.22f;
            else Time.timeScale = 1f;
        }
    }
}
