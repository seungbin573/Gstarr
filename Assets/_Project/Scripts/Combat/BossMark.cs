using System.Collections.Generic;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>보스 방의 석상과 섀도우맨. 체력바와 2페이즈 표시가 이 이름을 쓴다.</summary>
    public sealed class BossMark : MonoBehaviour
    {
        private static readonly List<BossMark> living = new List<BossMark>();

        public string DisplayName = "보스";
        public int Phase { get; private set; } = 1;

        public static IReadOnlyList<BossMark> Living => living;

        public void EnterPhase(int phase)
        {
            Phase = Mathf.Max(Phase, phase);
        }

        private void OnEnable()
        {
            if (!living.Contains(this)) living.Add(this);
        }

        private void OnDisable()
        {
            living.Remove(this);
        }
    }
}
