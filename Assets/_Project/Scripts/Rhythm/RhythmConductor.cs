using System;
using System.Collections.Generic;
using Gstarr.Items;
using UnityEngine;

namespace Gstarr.Rhythm
{
    public enum BeatAction
    {
        Jump,
        Dash,
        UseItem,
        Attack
    }

    public enum BeatJudgement
    {
        Perfect,
        Good,
        Miss
    }

    /// <summary>
    /// 음악을 재생하고 박자 판정을 담당. 점프/대쉬/아이템 사용은 박자에 맞을 때만 허용된다.
    /// 시간은 AudioSettings.dspTime 기준이라 프레임레이트와 무관하게 정확하다.
    /// </summary>
    public class RhythmConductor : MonoBehaviour
    {
        public static RhythmConductor Instance { get; private set; }

        [SerializeField] private AudioClip music;
        [Tooltip("박자 정보가 담긴 kickforge-chart JSON")]
        [SerializeField] private TextAsset chartJson;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;
        [Tooltip("씬 시작 후 음악이 시작되기까지의 대기 시간 (초)")]
        [SerializeField] private float startDelay = 1f;
        [Tooltip("끄면 음악이 끝날 때 Finished 이벤트 (게임 오버)")]
        [SerializeField] private bool loop = false;

        [Header("판정")]
        [Tooltip("입력/출력 지연 보정 (ms). 판정이 늦게 느껴지면 +, 빠르게 느껴지면 -")]
        [SerializeField] private float inputOffsetMs = 0f;
        [SerializeField] private float perfectWindow = 0.05f;
        [SerializeField] private float goodWindow = 0.12f;

        /// <summary>박자를 지날 때 (절대 박자 번호)</summary>
        public event Action<long> Beat;
        /// <summary>입력 판정 (행동, 판정, 오차 초: 음수 = 빠름)</summary>
        public event Action<BeatAction, BeatJudgement, double> Judged;
        /// <summary>루프하지 않는 곡이 끝났을 때</summary>
        public event Action Finished;

        public bool IsFinished { get; private set; }
        /// <summary>곡 진행도 0~1</summary>
        public float Progress => loopLength > 0 ? Mathf.Clamp01((float)(SongTime / loopLength)) : 0f;
        public double Length => loopLength;
        public AudioClip Music => music;

        public BeatChart Chart { get; private set; }
        /// <summary>음악 시작 기준 누적 시간 (루프해도 계속 증가). 시작 전에는 음수.</summary>
        public double SongTime { get; private set; }
        public float GoodWindow => goodWindow;
        public float PerfectWindow => perfectWindow;
        public bool IsReady => Chart != null && Chart.Beats.Length > 0;

        private AudioSource source;
        private double dspStart;
        private double realStart;
        private double loopLength;
        private long nextBeat;
        private readonly Dictionary<BeatAction, long> lastUsedBeat = new Dictionary<BeatAction, long>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<RhythmConductor>() != null) return;
            if (FindAnyObjectByType<Gstarr.Game.MainMenu>() != null) return;
            var assets = GameAssets.Instance;
            if (assets == null || !assets.enableRhythm) return;

            // 메뉴에서 고른 스테이지가 있으면 그 곡, 없으면 GameAssets 기본 곡
            var stage = Gstarr.Game.StageSession.Current;
            var clip = stage != null && stage.music != null ? stage.music : assets.music;
            var chart = stage != null && stage.beatChart != null ? stage.beatChart : assets.beatChart;
            if (clip == null || chart == null) return;

            var go = new GameObject("RhythmConductor");
            go.SetActive(false);
            var conductor = go.AddComponent<RhythmConductor>();
            conductor.music = clip;
            conductor.chartJson = chart;
            go.SetActive(true);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            if (GetComponent<BeatBarUI>() == null) gameObject.AddComponent<BeatBarUI>();
            if (GetComponent<ComboSystem>() == null) gameObject.AddComponent<ComboSystem>();
            if (GetComponent<ComboUI>() == null) gameObject.AddComponent<ComboUI>();
            if (GetComponent<Gstarr.Game.StageHUD>() == null) gameObject.AddComponent<Gstarr.Game.StageHUD>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (chartJson == null || music == null)
            {
                Debug.LogWarning("[RhythmConductor] 음악 또는 박자 JSON 이 없어 박자 판정을 끕니다.", this);
                enabled = false;
                return;
            }

            Chart = BeatChart.Parse(chartJson.text);
            loopLength = music.length;

            source.clip = music;
            source.loop = loop;
            source.volume = volume;
            dspStart = AudioSettings.dspTime + startDelay;
            realStart = UnityEngine.Time.realtimeSinceStartupAsDouble + startDelay;
            source.PlayScheduled(dspStart);
            SongTime = -startDelay;
        }

        private void Update()
        {
            if (!IsReady || IsFinished) return;
            source.volume = volume * Gstarr.Game.GameSettings.MusicVolume;

            // dspTime 은 오디오 버퍼 단위로만 갱신되므로 실시간 시계로 부드럽게 진행하고 dsp 에 천천히 맞춤
            double dspT = AudioSettings.dspTime - dspStart;
            double realT = UnityEngine.Time.realtimeSinceStartupAsDouble - realStart;
            double drift = dspT - realT;
            realStart -= Math.Abs(drift) > 0.05 ? drift : drift * 0.05;
            SongTime = UnityEngine.Time.realtimeSinceStartupAsDouble - realStart;

            while (BeatTime(nextBeat) <= SongTime)
            {
                Beat?.Invoke(nextBeat);
                nextBeat++;
            }

            if (!loop && SongTime >= loopLength)
            {
                IsFinished = true;
                source.Stop();
                Finished?.Invoke();
            }
        }

        /// <summary>절대 박자 번호의 시각. 루프하지 않고 곡이 끝났으면 무한대.</summary>
        public double BeatTime(long absBeat) => TimeAt(Chart?.Beats, absBeat);

        /// <summary>t 이상인 첫 박자 번호</summary>
        public long FirstBeatAtOrAfter(double t) => FirstAtOrAfter(Chart?.Beats, t);

        /// <summary>반박 절대 번호의 시각</summary>
        public double HalfBeatTime(long absIndex) => TimeAt(Chart?.HalfBeats, absIndex);

        /// <summary>t 이상인 첫 반박 번호</summary>
        public long FirstHalfBeatAtOrAfter(double t) => FirstAtOrAfter(Chart?.HalfBeats, t);

        private double TimeAt(double[] arr, long absIndex)
        {
            if (!IsReady || arr == null || arr.Length == 0 || absIndex < 0) return double.NegativeInfinity;
            int n = arr.Length;
            long lap = absIndex / n;
            if (lap > 0 && !loop) return double.PositiveInfinity;
            return lap * loopLength + arr[absIndex % n];
        }

        private long FirstAtOrAfter(double[] arr, double t)
        {
            if (!IsReady || arr == null || arr.Length == 0) return 0;
            int n = arr.Length;
            long lap = t <= 0 ? 0 : (long)(t / loopLength);
            int i = BeatChart.LowerBound(arr, t - lap * loopLength);
            return lap * n + i;
        }

        /// <summary>박자 + 반박 중 가장 가까운 판정 지점</summary>
        public long NearestJudgePoint(double t, out double error)
        {
            var arr = Chart.JudgeTimes;
            long after = FirstAtOrAfter(arr, t);
            long before = after - 1;
            double ea = t - TimeAt(arr, after);
            double eb = before >= 0 ? t - TimeAt(arr, before) : double.PositiveInfinity;
            if (Math.Abs(eb) < Math.Abs(ea))
            {
                error = eb;
                return before;
            }
            error = ea;
            return after;
        }

        /// <summary>지금 이 행동을 박자에 맞게 눌렀는지 판정. 같은 박자에 같은 행동은 한 번만.</summary>
        public bool TryAct(BeatAction action)
        {
            if (!IsReady || !enabled) return true;

            double t = SongTime - inputOffsetMs * 0.001;
            long beat = NearestJudgePoint(t, out double error);
            double abs = Math.Abs(error);

            bool alreadyUsed = lastUsedBeat.TryGetValue(action, out long used) && used == beat;
            if (abs > goodWindow || alreadyUsed)
            {
                Judged?.Invoke(action, BeatJudgement.Miss, error);
                return false;
            }

            lastUsedBeat[action] = beat;
            Judged?.Invoke(action, abs <= perfectWindow ? BeatJudgement.Perfect : BeatJudgement.Good, error);
            return true;
        }
    }

    /// <summary>박자 판정 진입점. 씬에 지휘자가 없으면 항상 허용.</summary>
    public static class BeatGate
    {
        public static bool TryAct(BeatAction action)
        {
            var c = RhythmConductor.Instance;
            return c == null || c.TryAct(action);
        }
    }
}
