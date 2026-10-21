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
        [SerializeField] private float perfectGain = 24f;
        [SerializeField] private float goodGain = 13f;
        [Tooltip("랭크가 높을수록 게이지가 덜 오름 (랭크당 감소 비율)")]
        [SerializeField] private float gainFalloffPerRank = 0.2f;
        [Tooltip("미스 시 잃는 게이지 (100 = 한 랭크)")]
        [SerializeField] private float missPenalty = 100f;
        [Tooltip("마지막 성공 후 이 시간이 지나면 게이지가 줄기 시작")]
        [SerializeField] private float decayDelay = 1.5f;
        [SerializeField] private float decayPerSecond = 15f;

        [Header("점수")]
        [SerializeField] private int perfectScore = 100;
        [SerializeField] private int goodScore = 50;
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

        private RhythmConductor conductor;
        private float sinceLastHit;

        private void Awake()
        {
            Instance = this;
            conductor = GetComponent<RhythmConductor>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable() => conductor.Judged += OnJudged;
        private void OnDisable() => conductor.Judged -= OnJudged;

        private void Update()
        {
            sinceLastHit += Time.deltaTime;
            if (sinceLastHit > decayDelay && Gauge > 0f)
                SetGauge(Gauge - decayPerSecond * Time.deltaTime);
        }

        private void OnJudged(BeatAction action, BeatJudgement judgement, double error)
        {
            if (judgement == BeatJudgement.Miss)
            {
                Combo = 0;
                ComboChanged?.Invoke(Combo);
                SetGauge(Gauge - missPenalty);
                return;
            }

            Combo++;
            MaxCombo = Mathf.Max(MaxCombo, Combo);
            sinceLastHit = 0f;
            ComboChanged?.Invoke(Combo);

            float gain = judgement == BeatJudgement.Perfect ? perfectGain : goodGain;
            int rankIndex = Mathf.Max(0, (int)Rank + 1);
            SetGauge(Gauge + gain / (1f + rankIndex * gainFalloffPerRank));

            int baseScore = judgement == BeatJudgement.Perfect ? perfectScore : goodScore;
            int points = Mathf.RoundToInt(baseScore * ComboMultiplier * RankMultiplier);
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
