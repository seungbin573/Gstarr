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
        [Tooltip("BPM 으로 박자를 만들 때 첫 박자 위치 (초)")]
        [SerializeField] private float firstBeatOffset = 0f;
        [Tooltip("음악 시작 전 3, 2, 1 카운트 (박자 단위)")]
        [SerializeField] private int countdownBeats = 3;
        [Tooltip("카운트다운 시작 전 여유 시간 (초)")]
        [SerializeField] private float countdownLead = 0.5f;

        private float startDelay = 1f;
        [Tooltip("끄면 음악이 끝날 때 Finished 이벤트 (게임 오버)")]
        [SerializeField] private bool loop = false;

        [Header("판정")]
        [Tooltip("입력/출력 지연 보정 (ms). 판정이 늦게 느껴지면 +, 빠르게 느껴지면 -")]
        [SerializeField] private float inputOffsetMs = 0f;
        [SerializeField] private float perfectWindow = 0.05f;
        [SerializeField] private float goodWindow = 0.12f;

        /// <summary>박자를 지날 때 (절대 박자 번호)</summary>
        public event Action<long> Beat;
        /// <summary>반박을 지날 때 (절대 반박 번호)</summary>
        public event Action<long> HalfBeat;
        /// <summary>입력 판정 (행동, 판정, 오차 초: 음수 = 빠름)</summary>
        public event Action<BeatAction, BeatJudgement, double> Judged;
        /// <summary>루프하지 않는 곡이 끝났을 때</summary>
        public event Action Finished;

        public bool IsFinished { get; private set; }
        /// <summary>곡 진행도 0~1</summary>
        public float Progress => loopLength > 0 ? Mathf.Clamp01((float)(SongTime / loopLength)) : 0f;
        public double Length => loopLength;
        public AudioClip Music => music;
        public int CountdownBeats => countdownBeats;
        public float CountdownInterval { get; private set; } = 0.5f;
        public bool IsPaused { get; private set; }

        private double pauseReal, pauseDsp;
        private bool pausedBeforeStart;
        private double countdownReleaseAt = double.MaxValue;

        public void Pause()
        {
            if (IsPaused || !IsReady || IsFinished) return;
            IsPaused = true;
            pauseReal = UnityEngine.Time.realtimeSinceStartupAsDouble;
            pauseDsp = AudioSettings.dspTime;
            pausedBeforeStart = SongTime < 0;
            if (pausedBeforeStart) source.Stop();
            else source.Pause();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            realStart += UnityEngine.Time.realtimeSinceStartupAsDouble - pauseReal;
            dspStart += AudioSettings.dspTime - pauseDsp;
            if (pausedBeforeStart) source.PlayScheduled(dspStart);
            else source.UnPause();
        }

        public BeatChart Chart { get; private set; }
        /// <summary>음악 시작 기준 누적 시간 (루프해도 계속 증가). 시작 전에는 음수.</summary>
        public double SongTime { get; private set; }
        public float GoodWindow => goodWindow;
        public double InputOffsetSeconds => inputOffsetMs * 0.001;
        public float PerfectWindow => perfectWindow;
        public bool IsReady => Chart != null && Chart.Beats.Length > 0;

        private AudioSource source;
        private double dspStart;
        private double realStart;
        private double loopLength;
        private long nextBeat;
        private long nextHalfBeat;
        private readonly Dictionary<BeatAction, long> lastUsedBeat = new Dictionary<BeatAction, long>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 메뉴에서 스테이지 씬으로 넘어갈 때도 매번 생성되도록 씬 로드마다 실행
        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (FindAnyObjectByType<RhythmConductor>() != null) return;
            if (FindAnyObjectByType<Gstarr.Game.MainMenu>() != null) return;
            var assets = GameAssets.Instance;
            if (assets == null || !assets.enableRhythm) return;

            // 메뉴에서 고른 스테이지가 있으면 그 곡, 없으면 GameAssets 기본 곡
            var stage = Gstarr.Game.StageSession.Current;
            var clip = stage != null && stage.music != null ? stage.music : assets.music;
            var chart = stage != null ? stage.beatChart : assets.beatChart;
            if (clip == null) return;
            if (chart == null && !BeatChart.TryParseBpm(clip.name, out _)) return;

            Gstarr.Game.GamePause.Clear();
            var go = new GameObject("RhythmConductor");
            go.SetActive(false);
            var conductor = go.AddComponent<RhythmConductor>();
            conductor.music = clip;
            conductor.chartJson = chart;
            if (stage != null) conductor.firstBeatOffset = stage.firstBeatOffset;
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
            if (GetComponent<Gstarr.Game.PauseMenu>() == null) gameObject.AddComponent<Gstarr.Game.PauseMenu>();
            countdownReleaseAt = UnityEngine.Time.realtimeSinceStartupAsDouble + 6.0;
            Gstarr.Game.GamePause.SetCountdown(true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (music == null)
            {
                Debug.LogWarning("[RhythmConductor] 음악이 없어 박자 판정을 끕니다.", this);
                Gstarr.Game.GamePause.SetCountdown(false);
                enabled = false;
                return;
            }

            // 파일 이름 끝의 BPM (예: "Song-118") 우선, 없으면 JSON
            if (BeatChart.TryParseBpm(music.name, out float bpm))
                Chart = BeatChart.FromBpm(bpm, music.length, firstBeatOffset);
            else if (chartJson != null)
                Chart = BeatChart.Parse(chartJson.text);
            else
            {
                Debug.LogWarning($"[RhythmConductor] '{music.name}' 이름 끝에 BPM(예: -118)이 없고 JSON 도 없어 박자 판정을 끕니다.", this);
                Gstarr.Game.GamePause.SetCountdown(false);
                enabled = false;
                return;
            }
            loopLength = music.length;

            float beatLen = Chart.Bpm > 0f ? 60f / Chart.Bpm : 0.5f;
            CountdownInterval = beatLen;
            startDelay = countdownLead + countdownBeats * beatLen;
            countdownReleaseAt = UnityEngine.Time.realtimeSinceStartupAsDouble + startDelay + 0.25;

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
            if (IsPaused) return;
            // 박자 차트가 비어도 카운트다운이 시간을 0으로 붙잡아 두지 않는다.
            if (Gstarr.Game.GamePause.IsCountdown &&
                (SongTime >= 0 || UnityEngine.Time.realtimeSinceStartupAsDouble >= countdownReleaseAt))
                Gstarr.Game.GamePause.SetCountdown(false);

            if (!IsReady || IsFinished) return;
            source.volume = volume * Gstarr.Game.GameSettings.MusicVolume;

            // dspTime 은 오디오 버퍼 단위로만 갱신되므로 실시간 시계로 부드럽게 진행하고 dsp 에 천천히 맞춤
            double dspT = AudioSettings.dspTime - dspStart;
            double realT = UnityEngine.Time.realtimeSinceStartupAsDouble - realStart;
            double drift = dspT - realT;
            // 한 프레임에 확 맞추면 노트와 화면이 끊겨 보인다
            double step = Math.Abs(drift) > 0.2 ? drift * 0.15 : Math.Clamp(drift * 0.08, -0.0015, 0.0015);
            realStart -= step;
            SongTime = UnityEngine.Time.realtimeSinceStartupAsDouble - realStart;
            if (Gstarr.Game.GamePause.IsCountdown && SongTime >= 0)
                Gstarr.Game.GamePause.SetCountdown(false);

            while (BeatTime(nextBeat) <= SongTime)
            {
                Beat?.Invoke(nextBeat);
                nextBeat++;
            }

            while (Chart.HalfBeats != null && Chart.HalfBeats.Length > 0
                   && HalfBeatTime(nextHalfBeat) <= SongTime)
            {
                HalfBeat?.Invoke(nextHalfBeat);
                nextHalfBeat++;
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

        /// <summary>가장 가까운 정박 인덱스</summary>
        public long NearestBeatPoint(double t, out double error) => NearestPoint(Chart?.Beats, t, out error);

        /// <summary>정박 + 반박 중 가장 가까운 판정 지점</summary>
        public long NearestJudgePoint(double t, out double error) => NearestPoint(Chart?.JudgeTimes, t, out error);

        private long NearestPoint(double[] times, double t, out double error)
        {
            if (!IsReady || times == null || times.Length == 0)
            {
                error = double.PositiveInfinity;
                return 0;
            }

            long after = FirstAtOrAfter(times, t);
            long before = after - 1;
            double ea = t - TimeAt(times, after);
            double eb = before >= 0 ? t - TimeAt(times, before) : double.PositiveInfinity;
            if (Math.Abs(eb) < Math.Abs(ea))
            {
                error = eb;
                return before;
            }
            error = ea;
            return after;
        }

        /// <summary>지금 이 행동을 박자에 맞게 눌렀는지 판정. 같은 박자에 같은 행동은 한 번만.</summary>
        public bool TryAct(BeatAction action) => TryPerform(action, reportMiss: true);

        /// <summary>
        /// 박자 창 안에서만 true. reportMiss 가 false 면 창 밖이어도 MISS 를 올리지 않는다.
        /// 이미 이 박자를 쓴 경우는 항상 조용히 거절한다.
        /// </summary>
        public bool TryPerform(BeatAction action, bool reportMiss, bool allowHalfBeats = false)
        {
            if (!IsReady || !enabled) return true;
            if (IsPaused || IsFinished || Gstarr.Game.GamePause.BlocksGameplay) return false;

            double t = SongTime - inputOffsetMs * 0.001;
            bool judgeHalfBeats = action != BeatAction.Attack || allowHalfBeats;
            long beat = judgeHalfBeats
                ? NearestJudgePoint(t, out double error)
                : NearestBeatPoint(t, out error);
            double abs = Math.Abs(error);
            double allowedWindow = goodWindow;
            if (action == BeatAction.Attack && !allowHalfBeats)
            {
                double beatTime = TimeAt(Chart.Beats, beat);
                double previousGap = beatTime - TimeAt(Chart.Beats, beat - 1);
                double nextGap = TimeAt(Chart.Beats, beat + 1) - beatTime;
                double nearestGap = Math.Min(previousGap, nextGap);
                if (nearestGap > 0 && !double.IsInfinity(nearestGap))
                    allowedWindow = Math.Min(allowedWindow, Math.Max(0, nearestGap * 0.5 - 0.001));
            }

            bool alreadyUsed = lastUsedBeat.TryGetValue(action, out long used) && used == beat;
            if (alreadyUsed) return false;
            if (abs > allowedWindow)
            {
                if (reportMiss) Judged?.Invoke(action, BeatJudgement.Miss, error);
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
        public static bool TryAct(BeatAction action) => TryPerform(action, true);

        public static bool TryPerform(BeatAction action, bool reportMiss, bool allowHalfBeats = false)
        {
            var c = RhythmConductor.Instance;
            return c == null || c.TryPerform(action, reportMiss, allowHalfBeats);
        }
    }
}
