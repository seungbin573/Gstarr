using UnityEngine;
using Gstarr.Combat;
using Gstarr.Player;

namespace Gstarr.Game
{
    /// <summary>일시정지 / 카운트다운 / 게임 오버 동안 플레이어와 물리 입력을 잠근다.</summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }
        public static bool IsCountdown { get; private set; }
        public static bool IsGloryKill { get; private set; }
        public static bool BlocksGameplay => IsPaused || IsCountdown || IsGloryKill;

        private static float hitStopUntil = -1f;

        // 도메인 리로드를 끈 상태에서도 이전 플레이의 timeScale 0 이 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOnPlay()
        {
            IsPaused = false;
            IsCountdown = false;
            IsGloryKill = false;
            hitStopUntil = -1f;
            Time.timeScale = 1f;
        }

        /// <summary>맞는 순간만 아주 짧게 멈춘다. 일시정지·글로리 킬 중에는 무시한다.</summary>
        public static void HitStop(float seconds)
        {
            if (BlocksGameplay || seconds <= 0f) return;
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
            ApplyTimeScale();
        }

        public static void TickHitStop()
        {
            if (hitStopUntil < 0f || Time.unscaledTime < hitStopUntil) return;
            hitStopUntil = -1f;
            if (!BlocksGameplay) Time.timeScale = 1f;
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
            if (active) StopGameplayActors();
            ApplyTimeScale();
        }

        private static void StopGameplayActors()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            var playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
            if (playerBody != null) playerBody.linearVelocity = Vector2.zero;

            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                var body = enemies[i].GetComponent<Rigidbody2D>();
                if (body != null) body.linearVelocity = Vector2.zero;
            }
        }

        public static void Clear()
        {
            IsPaused = false;
            IsCountdown = false;
            IsGloryKill = false;
            hitStopUntil = -1f;
            ApplyTimeScale();
        }

        private static void ApplyTimeScale()
        {
            if (IsPaused || IsCountdown || IsGloryKill)
            {
                hitStopUntil = -1f;
                Time.timeScale = 0f;
                return;
            }

            Time.timeScale = hitStopUntil > Time.unscaledTime ? 0.04f : 1f;
        }
    }
}
