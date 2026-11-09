using Gstarr.Combat;
using Gstarr.Game;
using Gstarr.Player;
using UnityEngine;

namespace Gstarr.World
{
    /// <summary>
    /// 처음 조작할 수 있을 때 이동, 점프, 대시를 순서대로 보여 준다.
    /// 처음 아이템을 먹었을 때 고르기와 사용, 처음 적을 만났을 때 공격과 무기 교체,
    /// 적에게 글로리 킬이 처음 켜지면 그 설명을 시간을 멈추고 보여 준다.
    /// 이미 봤다는 표시는 게임을 시작할 때마다 지운다.
    /// </summary>
    public static class CombatLesson
    {
        private const float MeetRange = 12f;
        private const float MeetHeight = 8f;

        private static bool moveSeen;
        private static bool jumpSeen;
        private static bool dashSeen;
        private static bool itemSeen;
        private static bool combatSeen;
        private static bool glorySeen;
        private static bool itemWaiting;
        private static bool gloryWaiting;

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
        }

        public static void ConsiderPlayer(Transform player)
        {
            if (player == null || !CanShow()) return;
            var body = player.GetComponent<PlayerController>();
            if (body != null && body.IsScripted) return;

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
                Show(SignDetail.Lesson.Combat);
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
