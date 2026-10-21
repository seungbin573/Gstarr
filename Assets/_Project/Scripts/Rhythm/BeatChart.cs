using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gstarr.Rhythm
{
    /// <summary>kickforge-chart JSON 에서 박자 시각(초)만 뽑아낸 정렬된 목록.</summary>
    public class BeatChart
    {
        public double[] Beats { get; private set; }
        /// <summary>박자 사이 반박</summary>
        public double[] HalfBeats { get; private set; }
        /// <summary>판정에 쓰는 박자 + 반박 합친 목록</summary>
        public double[] JudgeTimes { get; private set; }
        public float Bpm { get; private set; }
        public double Duration { get; private set; }

        public static BeatChart Parse(string json)
        {
            var raw = JsonUtility.FromJson<ChartJson>(json);
            var times = new List<double>();

            if (raw.tracks != null)
            {
                if (raw.tracks.kicks != null)
                    foreach (var b in raw.tracks.kicks) times.Add(b.timeSeconds);
                if (raw.tracks.nonKickBeats != null)
                    foreach (var b in raw.tracks.nonKickBeats) times.Add(b.timeSeconds);
            }

            // 비트 목록이 비어 있으면 BPM 으로 그리드 생성
            if (times.Count == 0 && raw.timing != null && raw.timing.bpm > 0f && raw.audio != null)
            {
                double interval = 60.0 / raw.timing.bpm;
                for (double t = raw.timing.firstBeatSeconds; t < raw.audio.duration; t += interval)
                    times.Add(t);
            }

            times.Sort();
            for (int i = times.Count - 1; i > 0; i--)
                if (times[i] - times[i - 1] < 0.001) times.RemoveAt(i);

            var halves = new List<double>();
            if (raw.tracks != null && raw.tracks.halfBeats != null)
                foreach (var b in raw.tracks.halfBeats) halves.Add(b.timeSeconds);
            halves.Sort();

            var judge = new List<double>(times);
            judge.AddRange(halves);
            judge.Sort();
            for (int i = judge.Count - 1; i > 0; i--)
                if (judge[i] - judge[i - 1] < 0.001) judge.RemoveAt(i);

            return new BeatChart
            {
                Beats = times.ToArray(),
                JudgeTimes = judge.ToArray(),
                HalfBeats = halves.ToArray(),
                Bpm = raw.timing != null ? raw.timing.bpm : 0f,
                Duration = raw.audio != null ? raw.audio.duration : 0.0
            };
        }

        private static readonly System.Text.RegularExpressions.Regex BpmSuffix =
            new System.Text.RegularExpressions.Regex(@"[-_]\s*(\d+(?:\.\d+)?)\s*(?:bpm)?\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>"곡 이름-118" 처럼 이름 끝의 BPM 을 읽는다.</summary>
        public static bool TryParseBpm(string name, out float bpm)
        {
            bpm = 0f;
            if (string.IsNullOrEmpty(name)) return false;
            var m = BpmSuffix.Match(name);
            return m.Success
                   && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out bpm)
                   && bpm >= 20f && bpm <= 400f;
        }

        /// <summary>BPM 만으로 박자/반박 생성. 박자 = offset + n * (60/bpm), 반박 = 그 정확히 중간.</summary>
        public static BeatChart FromBpm(float bpm, double duration, double firstBeatOffset = 0.0)
        {
            double interval = 60.0 / bpm;
            int count = (int)Math.Floor((duration - firstBeatOffset) / interval) + 1;
            var beats = new List<double>(count);
            var halves = new List<double>(count);
            var judge = new List<double>(count * 2);
            for (int i = 0; i < count; i++)
            {
                double t = firstBeatOffset + i * interval;
                if (t < 0 || t > duration) continue;
                beats.Add(t);
                judge.Add(t);
                double h = t + interval * 0.5;
                if (h <= duration)
                {
                    halves.Add(h);
                    judge.Add(h);
                }
            }

            return new BeatChart
            {
                Beats = beats.ToArray(),
                HalfBeats = halves.ToArray(),
                JudgeTimes = judge.ToArray(),
                Bpm = bpm,
                Duration = duration
            };
        }

        /// <summary>t 이상인 첫 박자의 인덱스 (없으면 Beats.Length)</summary>
        public int LowerBound(double t) => LowerBound(Beats, t);

        public static int LowerBound(double[] arr, double t)
        {
            int lo = 0, hi = arr.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (arr[mid] < t) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        [Serializable] private class ChartJson
        {
            public AudioJson audio;
            public TimingJson timing;
            public TracksJson tracks;
        }

        [Serializable] private class AudioJson
        {
            public double duration;
        }

        [Serializable] private class TimingJson
        {
            public float bpm;
            public double firstBeatSeconds;
        }

        [Serializable] private class TracksJson
        {
            public BeatJson[] kicks;
            public BeatJson[] nonKickBeats;
            public BeatJson[] halfBeats;
        }

        [Serializable] private class BeatJson
        {
            public double timeSeconds;
        }
    }
}
