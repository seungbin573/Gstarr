using System.Collections;
using Gstarr.CameraSystem;
using Gstarr.Combat;
using Gstarr.Game;
using Gstarr.Player;
using Gstarr.Rhythm;
using UnityEngine;

namespace Gstarr.World
{
    /// <summary>
    /// 처음 조작할 수 있을 때 이동, 점프, 대시를 순서대로 보여 준다.
    /// 처음 아이템을 먹었을 때 고르기와 사용, 처음 토끼를 만났을 때 대사 뒤에 공격과 무기 교체,
    /// 적에게 글로리 킬이 처음 켜지면 그 설명을 시간을 멈추고 보여 준다.
    /// 이미 봤다는 표시는 게임을 시작할 때마다 지운다.
    /// </summary>
    public static class CombatLesson
    {
        private const float MeetRange = 12f;
        private const float MeetHeight = 8f;
        private const float BunnyMeetRange = 8.6f;

        private static bool moveSeen;
        private static bool jumpSeen;
        private static bool dashSeen;
        private static bool itemSeen;
        private static bool combatSeen;
        private static bool glorySeen;
        private static bool itemWaiting;
        private static bool gloryWaiting;
        private static bool bunnyIntroSeen;
        private static bool bunnyIntroPlaying;
        private static Runner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOnGameStart()
        {
            moveSeen = false;
            jumpSeen = false;
            dashSeen = false;
            itemSeen = false;
            combatSeen = false;
            glorySeen = false;
            itemWaiting = false;
            gloryWaiting = false;
            bunnyIntroSeen = false;
            bunnyIntroPlaying = false;
        }

        public static void ConsiderPlayer(Transform player)
        {
            if (player == null || !CanShow()) return;
            var body = player.GetComponent<PlayerController>();
            if (body != null && body.IsScripted) return;

            if (bunnyIntroPlaying) return;

            if (!moveSeen)
                Show(SignDetail.Lesson.Move);
            else if (!jumpSeen)
                Show(SignDetail.Lesson.Jump);
            else if (!dashSeen)
                Show(SignDetail.Lesson.Dash);
            else if (gloryWaiting && !glorySeen)
                Show(SignDetail.Lesson.Glory);
            else if (itemWaiting && !itemSeen)
                Show(SignDetail.Lesson.Items);
            else if (!combatSeen && EnemyActor.AnyoneNear(player.position, MeetRange, MeetHeight, true))
            {
                if (!bunnyIntroSeen)
                {
                    var close = FindMeetBunny(player.position, BunnyMeetRange);
                    if (close != null)
                    {
                        BeginBunnyIntro(player, close);
                        return;
                    }

                    if (FindMeetBunny(player.position, MeetRange) != null) return;
                }

                Show(SignDetail.Lesson.Combat);
            }
        }

        private static void BeginBunnyIntro(Transform player, NormalBunny bunny)
        {
            bunnyIntroSeen = true;
            bunnyIntroPlaying = true;
            if (runner == null)
            {
                var go = new GameObject("BunnyMeet");
                runner = go.AddComponent<Runner>();
            }

            runner.StartCoroutine(PlayBunnyIntro(player, bunny));
        }

        /// <summary>토끼를 알아보는 말, 돌진, 비명. 그 다음에 전투 설명을 연다.</summary>
        private static IEnumerator PlayBunnyIntro(Transform player, NormalBunny bunny)
        {
            var body = player != null ? player.GetComponent<PlayerController>() : null;
            body?.SetScripted(true);
            GamePause.HaltMotion();
            GamePause.SetPaused(true);
            RhythmConductor.Instance?.DuckForStory(true);

            if (bunny != null) bunny.HoldForIntro();

            var bubble = StoryBubble.Open();
            var lines = StageStory.BunnyMeetLines;
            var cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
            float fromSize = Camera.main != null ? Camera.main.orthographicSize : 6f;
            bool zoomDone = false;
            if (cameraFollow != null && bunny != null)
                runner.StartCoroutine(ZoomOntoBunny(cameraFollow, bunny.transform, fromSize, () => zoomDone = true));
            else
                zoomDone = true;

            yield return bubble.TypeLine(lines[0], true);
            while (!zoomDone) yield return null;

            GamePause.SetPaused(false);
            if (bunny != null) bunny.BeginIntroCharge(player);
            float guard = 0f;
            while (bunny != null && !bunny.IntroChargeFinished && guard < 2.8f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return bubble.Hold(0.2f);
            GamePause.HaltMotion();
            GamePause.SetPaused(true);
            yield return bubble.TypeLine(lines.Length > 1 ? lines[1] : "으악!", true);

            bubble.Close();
            if (cameraFollow != null) cameraFollow.EndFocus();
            if (bunny != null) bunny.ReleaseIntroHold();
            body?.GetComponent<Gstarr.Combat.Health>()?.GrantInvulnerability(2f);
            body?.SetScripted(false);
            bunnyIntroPlaying = false;
            GamePause.SetPaused(false);
            if (!Show(SignDetail.Lesson.Combat))
                RhythmConductor.Instance?.DuckForStory(false);
        }

        /// <summary>토끼는 그대로 두고, 잠깐 뒤 화면만 토끼 쪽으로 확대한다.</summary>
        private static IEnumerator ZoomOntoBunny(CameraFollow2D follow, Transform bunny, float fromSize, System.Action done)
        {
            const float zoomSize = 3.15f;
            const float stillSeconds = 0.35f;
            const float zoomSeconds = 0.75f;
            for (float t = 0f; t < stillSeconds; t += Time.unscaledDeltaTime) yield return null;
            if (bunny == null)
            {
                done?.Invoke();
                yield break;
            }

            follow.BeginFocus(bunny, fromSize, Vector3.up * 0.9f);
            for (float t = 0f; t < zoomSeconds; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / zoomSeconds);
                p = p * p * (3f - 2f * p);
                follow.SetFocusSize(Mathf.Lerp(fromSize, zoomSize, p));
                yield return null;
            }

            follow.SetFocusSize(zoomSize);
            done?.Invoke();
        }

        private static NormalBunny FindMeetBunny(Vector3 point, float range)
        {
            var all = Object.FindObjectsByType<NormalBunny>(FindObjectsSortMode.None);
            NormalBunny best = null;
            float bestDist = range;
            for (int i = 0; i < all.Length; i++)
            {
                var bunny = all[i];
                if (bunny == null) continue;
                var actor = bunny.GetComponent<EnemyActor>();
                if (actor == null || actor.Kind != EnemyKind.Bunny || actor.IsDead) continue;
                if (ArenaDoorSpawner.SeparatedByDoor(actor.transform.position.x, point.x)) continue;
                float dx = Mathf.Abs(actor.transform.position.x - point.x);
                float dy = Mathf.Abs(actor.transform.position.y - point.y);
                if (dy > MeetHeight || dx > bestDist) continue;
                bestDist = dx;
                best = bunny;
            }

            return best;
        }

        private sealed class Runner : MonoBehaviour
        {
        }

        /// <summary>바닥에 있는 아이템을 처음 먹었을 때. 시작할 때 들고 있는 것은 세지 않는다.</summary>
        public static void OnItemCollected()
        {
            if (itemSeen) return;
            itemWaiting = true;
        }

        /// <summary>적이 비틀거리며 글로리 킬이 가능해진 순간.</summary>
        public static void OnGloryActivated()
        {
            if (glorySeen) return;
            gloryWaiting = true;
        }

        private static bool Show(SignDetail.Lesson lesson)
        {
            if (!SignDetail.OpenLesson(lesson)) return false;
            switch (lesson)
            {
                case SignDetail.Lesson.Move:
                    moveSeen = true;
                    break;
                case SignDetail.Lesson.Jump:
                    jumpSeen = true;
                    break;
                case SignDetail.Lesson.Dash:
                    dashSeen = true;
                    break;
                case SignDetail.Lesson.Items:
                    itemSeen = true;
                    itemWaiting = false;
                    break;
                case SignDetail.Lesson.Glory:
                    glorySeen = true;
                    gloryWaiting = false;
                    break;
                default:
                    combatSeen = true;
                    break;
            }
            return true;
        }

        private static bool CanShow() => !SignDetail.IsOpen && !GamePause.BlocksGameplay;
    }
}
