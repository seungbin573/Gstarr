using System;
using UnityEngine;

namespace Gstarr.Rhythm
{
    public enum StyleRank
    {
        None = -1,
        C,
        B,
        A,
        S
    }

    /// <summary>
    /// 박자 판정으로 콤보/스타일 게이지/점수를 관리 (데빌 메이 크라이 스타일 랭크).
    /// 게이지: 0~100 랭크 없음, 100~200 C, 200~300 B, 300~400 A, 400~500 S.
    /// </summary>
    [RequireComponent(typeof(RhythmConductor))]
    public class ComboSystem : MonoBehaviour
    {
        public static ComboSystem Instance { get; private set; }

        public const int RankCount = 4;

        [Header("게이지")]
        [SerializeField] private float gaugePerRank = 100f;
        [SerializeField] private float hitGain = 24f;
        [Tooltip("랭크가 높을수록 게이지가 덜 오름 (랭크당 감소 비율)")]
        [SerializeField] private float gainFalloffPerRank = 0.2f;
        [Tooltip("마지막 성공 후 이 시간이 지나면 게이지가 줄기 시작")]
        [SerializeField] private float decayDelay = 1.5f;
        [SerializeField] private float decayPerSecond = 15f;
        [Tooltip("랭크당 감소 속도 증가 비율 (S 랭크 = 기본의 1 + 4 * 값 배)")]
        [SerializeField] private float decayGrowthPerRank = 0.75f;
        [Tooltip("랭크당 감소 시작 대기시간 단축 비율")]
        [SerializeField] private float delayShrinkPerRank = 0.4f;
        [Header("점수")]
        [SerializeField] private int hitScore = 100;
        [Tooltip("콤보 1당 추가 배율")]
        [SerializeField] private float comboBonusPerHit = 0.1f;
        [SerializeField] private float maxComboMultiplier = 5f;
        [Tooltip("랭크별 점수 배율 (C, B, A, S)")]
        [SerializeField] private float[] rankMultipliers = { 1.5f, 2f, 3f, 5f };

        public event Action<int> ComboChanged;
        public event Action<StyleRank, StyleRank> RankChanged;
        /// <summary>(얻은 점수, 총점)</summary>
        public event Action<int, long> ScoreAdded;

        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public long Score { get; private set; }
        public float Gauge { get; private set; }
        public StyleRank Rank { get; private set; } = StyleRank.None;
        public float MaxGauge => gaugePerRank * (RankCount + 1);

        /// <summary>현재 랭크 구간 안에서의 진행도 (0~1)</summary>
        public float RankProgress
        {
            get
            {
                if (Gauge >= MaxGauge) return 1f;
                return Mathf.Repeat(Gauge, gaugePerRank) / gaugePerRank;
            }
        }

        public float ComboMultiplier => Mathf.Min(maxComboMultiplier, 1f + Combo * comboBonusPerHit);
        public float RankMultiplier => Rank == StyleRank.None ? 1f : rankMultipliers[Mathf.Min((int)Rank, rankMultipliers.Length - 1)];

        private float sinceLastHit;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>적이 플레이어 공격/아이템에 맞았을 때만 호출. 점프/대시/키 입력은 콤보에 넣지 않는다.</summary>
        public static void RegisterHit(GameObject victim)
        {
            if (Instance == null || victim == null || victim.CompareTag("Player")) return;
            Instance.OnEnemyHit();
        }

        public void AddBonus(int points)
        {
            if (points <= 0) return;
            Score += points;
            sinceLastHit = 0f;
            ScoreAdded?.Invoke(points, Score);
        }

        private void Update()
        {
            sinceLastHit += Time.deltaTime;
            // 랭크가 높을수록 빨리, 더 많이 떨어짐
            int rankIndex = (int)Rank + 1;
            float delay = decayDelay / (1f + rankIndex * delayShrinkPerRank);
            if (sinceLastHit > delay && Gauge > 0f)
            {
                float rate = decayPerSecond * (1f + rankIndex * decayGrowthPerRank);
                SetGauge(Gauge - rate * Time.deltaTime);
                if (Gauge <= 0f && Combo > 0)
                {
                    Combo = 0;
                    ComboChanged?.Invoke(Combo);
                }
            }
        }

        private void OnEnemyHit()
        {
            Combo++;
            MaxCombo = Mathf.Max(MaxCombo, Combo);
            sinceLastHit = 0f;
            ComboChanged?.Invoke(Combo);

            int rankIndex = Mathf.Max(0, (int)Rank + 1);
            SetGauge(Gauge + hitGain / (1f + rankIndex * gainFalloffPerRank));

            int points = Mathf.RoundToInt(hitScore * ComboMultiplier * RankMultiplier);
            Score += points;
            ScoreAdded?.Invoke(points, Score);
        }

        private void SetGauge(float value)
        {
            Gauge = Mathf.Clamp(value, 0f, MaxGauge);

            var newRank = Gauge < gaugePerRank
                ? StyleRank.None
                : (StyleRank)Mathf.Min(RankCount - 1, (int)(Gauge / gaugePerRank) - 1);

            if (newRank != Rank)
            {
                var old = Rank;
                Rank = newRank;
                RankChanged?.Invoke(old, newRank);
            }
        }
    }
}
