using System;
using System.Collections.Generic;
using Gstarr.Game;
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
    /// 음악을 재생하고 박자 판정을 담당. 이동은 자유롭게 두고 공격/아이템 사용을 박자에 맞춘다.
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
        [Tooltip("스테이지를 도는 동안 곡을 반복한다. 곡 끝은 패배가 아니다.")]
        [SerializeField] private bool loop = true;

        [Header("판정")]
        [Tooltip("입력/출력 지연 보정 (ms). 판정이 늦게 느껴지면 +, 빠르게 느껴지면 -")]
        [SerializeField] private float inputOffsetMs = 0f;
        [SerializeField] private float perfectWindow = 0.07f;
        [SerializeField] private float goodWindow = 0.16f;

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
        public bool Loops => loop;
        public float Progress
        {
            get
            {
                if (loopLength <= 0 || SongTime < 0) return 0f;
                float ratio = (float)(SongTime / loopLength);
                return loop ? Mathf.Repeat(ratio, 1f) : Mathf.Clamp01(ratio);
            }
        }
        public double Length => loopLength;
        public AudioClip Music => music;
        public int CountdownBeats => countdownBeats;
        public float CountdownInterval { get; private set; } = 0.5f;
        public bool IsPaused { get; private set; }

        private double pauseReal, pauseDsp;
        private bool pausedBeforeStart;
        private double countdownReleaseAt = double.MaxValue;
        private AudioSource cueSource;
        private StageMusicCue[] musicCues;
        private int nextMusicCue;
        private int activeSource;
        private bool cueScheduled;
        private double cueSwitchTime;
        private bool bossThemeOn;
        private float bossFade;
        private float storyDuck = 1f;
        private float storyDuckTarget = 1f;

        /// <summary>이야기 중에는 곡을 끄지 않고 소리만 잠시 낮춘다.</summary>
        public void DuckForStory(bool ducked) => storyDuckTarget = ducked ? 0.22f : 1f;

        /// <summary>보스 방에서는 다른 곡을 내고, 박자 시계는 스테이지 곡을 유지한다.</summary>
        public void SetBossTheme(AudioClip clip, bool enabled)
        {
            if (clip == null || source == null || cueSource == null) return;
            if (enabled == bossThemeOn && (!enabled || cueSource.clip == clip)) return;
            bossThemeOn = enabled;
            if (!enabled) return;

            float pitch = 1f;
            if (Chart != null && Chart.Bpm > 1f && BeatChart.TryParseBpm(clip.name, out float clipBpm) && clipBpm > 1f)
                pitch = Chart.Bpm / clipBpm;
            if (cueSource.clip != clip || !cueSource.isPlaying)
            {
                cueSource.clip = clip;
                cueSource.loop = true;
                cueSource.pitch = pitch;
                cueSource.time = 0f;
                cueSource.Play();
            }
            else cueSource.pitch = pitch;
        }

        public void Pause()
        {
            if (IsPaused || !IsReady || IsFinished) return;
            IsPaused = true;
            pauseReal = UnityEngine.Time.realtimeSinceStartupAsDouble;
            pauseDsp = AudioSettings.dspTime;
            pausedBeforeStart = SongTime < 0;
            if (pausedBeforeStart) { source.Stop(); cueSource.Stop(); }
            else { source.Pause(); cueSource.Pause(); }
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            double pausedFor = UnityEngine.Time.realtimeSinceStartupAsDouble - pauseReal;
            realStart += pausedFor;
            dspStart += AudioSettings.dspTime - pauseDsp;
            if (countdownReleaseAt != double.MaxValue) countdownReleaseAt += pausedFor;
            if (pausedBeforeStart) source.PlayScheduled(dspStart);
            else { source.UnPause(); cueSource.UnPause(); }
        }

        public void CapturePlayback(out double songTime, out bool heardBoss, out float bossTime)
        {
            songTime = SongTime;
            heardBoss = bossThemeOn && cueSource != null && cueSource.isPlaying;
            bossTime = heardBoss && cueSource.clip != null ? cueSource.time : 0f;
        }

        /// <summary>죽은 시점의 박자와 그 자리의 곡으로 맞춘다.</summary>
        public void SeekTo(double songTime, bool heardBoss, float bossTime)
        {
            if (!IsReady || source == null) return;
            if (songTime < 0) songTime = 0;
            IsPaused = false;
            SongTime = songTime;
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            realStart = now - songTime;
            dspStart = AudioSettings.dspTime - songTime;
            countdownReleaseAt = now;
            nextBeat = FirstBeatAtOrAfter(songTime + 0.0001);
            nextHalfBeat = FirstHalfBeatAtOrAfter(songTime + 0.0001);
            nextMusicCue = 0;
            cueScheduled = false;
            activeSource = 0;
            bossThemeOn = false;
            bossFade = 0f;
            Gstarr.Game.GamePause.SetCountdown(false);

            AudioClip clip = music;
            double clipPos = loopLength > 0.01 ? songTime % loopLength : songTime;
            if (musicCues != null)
            {
                for (int i = 0; i < musicCues.Length; i++)
                {
                    var cue = musicCues[i];
                    if (cue == null || cue.loop == null)
                    {
                        nextMusicCue = i + 1;
                        continue;
                    }
                    if (songTime < cue.atSeconds)
                    {
                        nextMusicCue = i;
                        break;
                    }
                    clip = cue.loop;
                    clipPos = songTime - cue.atSeconds;
                    nextMusicCue = i + 1;
                }
            }
            if (clip != null && clip.length > 0.05f) clipPos %= clip.length;

            source.Stop();
            cueSource.Stop();
            source.clip = clip != null ? clip : music;
            source.loop = loop || clip != music;
            source.time = (float)Math.Max(0, clipPos);
            source.Play();

            if (!heardBoss) return;
            var stage = Gstarr.Game.StageSession.Current;
            if (stage == null || stage.bossMusic == null) return;
            SetBossTheme(stage.bossMusic, true);
            if (cueSource.clip != null)
                cueSource.time = Mathf.Clamp(bossTime, 0f, Mathf.Max(0.01f, cueSource.clip.length - 0.05f));
            bossFade = 1f;
        }

        /// <summary>눈 뜨는 장면과 함께 3, 2, 1 을 지금 시작한다. 눈이 뜨는 순간 곡이 시작한다.</summary>
        public void StartCountdownNow(int beats)
        {
            if (!IsReady || source == null) return;
            float beat = Mathf.Max(0.05f, CountdownInterval);
            int count = Mathf.Max(1, beats);
            double delay = count * beat;
            IsPaused = false;
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            realStart = now + delay;
            dspStart = AudioSettings.dspTime + delay;
            SongTime = -delay;
            countdownReleaseAt = now + delay;
            nextBeat = 0;
            nextHalfBeat = 0;
            source.Stop();
            cueSource.Stop();
            source.PlayScheduled(dspStart);
            Gstarr.Game.GamePause.SetCountdown(true);
        }

        /// <summary>처음 이야기가 시작될 때 곡을 틀거나, 이미 나오던 곡을 다시 이어 튼다.</summary>
        public void BeginWithStory()
        {
            if (!IsReady || source == null) return;
            if (IsPaused && SongTime >= 0)
            {
                Resume();
                return;
            }
            if (SongTime < 0) SkipOpeningCountdown();
        }

        /// <summary>눈 뜨는 카운트를 건너뛰면 곡을 바로 시작한다.</summary>
        public void SkipOpeningCountdown()
        {
            if (!IsReady || source == null || SongTime >= 0) return;
            IsPaused = false;
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            realStart = now;
            dspStart = AudioSettings.dspTime;
            SongTime = 0;
            countdownReleaseAt = now;
            nextBeat = FirstBeatAtOrAfter(0);
            nextHalfBeat = FirstHalfBeatAtOrAfter(0);
            source.Stop();
            source.time = 0f;
            source.Play();
            Gstarr.Game.GamePause.SetCountdown(false);
        }

        public BeatChart Chart { get; private set; }
        /// <summary>음악 시작 기준 누적 시간 (루프해도 계속 증가). 시작 전에는 음수.</summary>
        public double SongTime { get; private set; }
        public float GoodWindow => goodWindow;
        public double InputOffsetSeconds => (inputOffsetMs + Gstarr.Game.GameSettings.AudioOffsetMs) * 0.001;
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
            cueSource = gameObject.AddComponent<AudioSource>();
            cueSource.playOnAwake = false;
            cueSource.spatialBlend = 0f;

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
            musicCues = Gstarr.Game.StageSession.Current != null
                ? Gstarr.Game.StageSession.Current.musicCues
                : Array.Empty<StageMusicCue>();

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
            float musicVol = volume * Gstarr.Game.GameSettings.MusicVolume;
            float target = bossThemeOn ? 1f : 0f;
            bossFade = Mathf.MoveTowards(bossFade, target, Time.unscaledDeltaTime / 0.45f);
            storyDuck = Mathf.MoveTowards(storyDuck, storyDuckTarget, Time.unscaledDeltaTime / 0.55f);
            source.volume = musicVol * (1f - bossFade) * storyDuck;
            cueSource.volume = musicVol * bossFade * storyDuck;
            if (!bossThemeOn && bossFade <= 0.001f && cueSource.isPlaying) cueSource.Stop();

            // dspTime 은 오디오 버퍼 단위로만 갱신되므로 실시간 시계로 부드럽게 진행하고 dsp 에 천천히 맞춤.
            // 다른 창에 갔다 오면 실시간만 흐르고 오디오 시계는 멈춘다. 그 간격을 박자로 흘리면
            // 커서가 미래로 점프한 채 남아 적들이 한참 동안 박자를 받지 못한다.
            double dspT = AudioSettings.dspTime - dspStart;
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            double drift = dspT - (now - realStart);
            if (Math.Abs(drift) > 0.5)
            {
                realStart = now - dspT;
                SongTime = dspT;
                nextBeat = FirstBeatAtOrAfter(SongTime);
                nextHalfBeat = FirstHalfBeatAtOrAfter(SongTime);
            }
            else
            {
                double step = Math.Abs(drift) > 0.2 ? drift * 0.15 : Math.Clamp(drift * 0.08, -0.0015, 0.0015);
                realStart -= step;
                SongTime = now - realStart;
            }
            if (!bossThemeOn) UpdateMusicCues();
            if (Gstarr.Game.GamePause.IsCountdown && SongTime >= 0)
                Gstarr.Game.GamePause.SetCountdown(false);

            DispatchBeats(ref nextBeat, false);
            if (Chart.HalfBeats != null && Chart.HalfBeats.Length > 0)
                DispatchBeats(ref nextHalfBeat, true);

            if (!loop && SongTime >= loopLength)
            {
                IsFinished = true;
                source.Stop();
                cueSource.Stop();
                Finished?.Invoke();
            }
        }

        /// <summary>한 프레임에 박자를 몰아 쏘지 않는다. 크게 밀리면 놓친 박자는 건너뛴다.</summary>
        private void DispatchBeats(ref long cursor, bool half)
        {
            int fired = 0;
            while (fired < 2)
            {
                double when = half ? HalfBeatTime(cursor) : BeatTime(cursor);
                if (when > SongTime) return;
                if (half) HalfBeat?.Invoke(cursor);
                else Beat?.Invoke(cursor);
                cursor++;
                fired++;
            }

            double pending = half ? HalfBeatTime(cursor) : BeatTime(cursor);
            if (pending <= SongTime)
                cursor = half ? FirstHalfBeatAtOrAfter(SongTime + 0.0001) : FirstBeatAtOrAfter(SongTime + 0.0001);
        }

        private void UpdateMusicCues()
        {
            if (cueScheduled && SongTime >= cueSwitchTime)
            {
                activeSource = 1 - activeSource;
                cueScheduled = false;
            }
            if (cueScheduled || musicCues == null || nextMusicCue >= musicCues.Length) return;

            var cue = musicCues[nextMusicCue];
            if (cue == null || cue.loop == null) { nextMusicCue++; return; }
            if (SongTime < cue.atSeconds) return;

            long beat = FirstBeatAtOrAfter(Math.Max(SongTime, cue.atSeconds));
            long barBeat = ((beat + 3) / 4) * 4;
            double switchTime = BeatTime(barBeat);
            if (double.IsInfinity(switchTime)) { nextMusicCue++; return; }

            AudioSource outgoing = activeSource == 0 ? source : cueSource;
            AudioSource incoming = activeSource == 0 ? cueSource : source;
            double switchDsp = dspStart + switchTime;
            outgoing.SetScheduledEndTime(switchDsp);
            incoming.Stop();
            incoming.clip = cue.loop;
            incoming.loop = true;
            incoming.volume = volume * Gstarr.Game.GameSettings.MusicVolume;
            incoming.PlayScheduled(switchDsp);
            cueSwitchTime = switchTime;
            cueScheduled = true;
            nextMusicCue++;
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
            => TryPerform(action, reportMiss, allowHalfBeats, out _, false);

        public bool TryPerform(BeatAction action, bool reportMiss, bool allowHalfBeats,
            out BeatJudgement judgement, bool acceptMiss = false)
        {
            judgement = BeatJudgement.Perfect;
            if (!IsReady || !enabled) return true;
            if (IsPaused || IsFinished || Gstarr.Game.GamePause.BlocksGameplay) return false;

            double t = SongTime - InputOffsetSeconds;
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
                judgement = BeatJudgement.Miss;
                if (reportMiss) Judged?.Invoke(action, BeatJudgement.Miss, error);
                if (acceptMiss) lastUsedBeat[action] = beat;
                return acceptMiss;
            }

            lastUsedBeat[action] = beat;
            judgement = abs <= perfectWindow ? BeatJudgement.Perfect : BeatJudgement.Good;
            Judged?.Invoke(action, judgement, error);
            return true;
        }
    }

    /// <summary>박자 판정 진입점. 씬에 지휘자가 없으면 항상 허용.</summary>
    public static class BeatGate
    {
        public static bool TryAct(BeatAction action) => TryPerform(action, true);

        public static bool TryPerform(BeatAction action, bool reportMiss, bool allowHalfBeats = false)
            => TryPerform(action, reportMiss, allowHalfBeats, out _, false);

        public static bool TryPerform(BeatAction action, bool reportMiss, bool allowHalfBeats,
            out BeatJudgement judgement, bool acceptMiss = false)
        {
            var c = RhythmConductor.Instance;
            if (c == null)
            {
                judgement = BeatJudgement.Perfect;
                return true;
            }
            return c.TryPerform(action, reportMiss, allowHalfBeats, out judgement, acceptMiss);
        }
    }
}
