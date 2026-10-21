using System;
using System.IO;
using UnityEngine;

namespace Gstarr.EditorTools
{
    /// <summary>
    /// WAV 파일을 직접 읽어 노래가 실제로 시작하는 지점과 박자 위상을 찾는다.
    /// 1) 앞부분 무음이 끝나는 곳 = 노래 시작
    /// 2) 소리가 커지는 순간(온셋)들이 BPM 격자에 가장 잘 맞는 위상을 탐색
    /// 결과 = 노래 시작 이후 첫 박자 시각 (초)
    /// </summary>
    public static class BeatAnalyzer
    {
        private const double Hop = 0.005;

        public struct Result
        {
            public double songStart;
            public double firstBeat;
        }

        public static bool TryAnalyze(string wavPath, float bpm, out Result result)
        {
            result = default;
            if (bpm <= 0f || !File.Exists(wavPath)) return false;
            if (!TryReadEnvelope(wavPath, out float[] energy)) return false;
            int n = energy.Length;
            if (n < 100) return false;

            // 노래 시작: 최대 RMS 의 3% 를 처음 넘는 곳
            float max = 0f;
            for (int i = 0; i < n; i++) max = Mathf.Max(max, energy[i]);
            if (max <= 0f) return false;
            int startHop = 0;
            while (startHop < n && energy[startHop] < max * 0.03f) startHop++;
            double songStart = startHop * Hop;

            // 온셋 강도: 로그 에너지의 양의 변화량
            var flux = new float[n];
            float prev = Mathf.Log(1e-6f + energy[0]);
            for (int i = 1; i < n; i++)
            {
                float cur = Mathf.Log(1e-6f + energy[i]);
                flux[i] = Mathf.Max(0f, cur - prev);
                prev = cur;
            }

            // 위상 탐색: φ + k * interval 위치의 온셋 합이 최대인 φ
            double interval = 60.0 / bpm;
            double bestPhase = 0, bestScore = -1;
            for (double phase = 0; phase < interval; phase += 0.002)
            {
                double score = 0;
                for (double t = songStart + phase; t < n * Hop; t += interval)
                    score += Sample(flux, t / Hop);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPhase = phase;
                }
            }

            // 노래 시작 직후 첫 박자 (시작 지점 약간 앞까지 허용)
            double first = songStart + bestPhase;
            while (first - interval >= songStart - 0.03) first -= interval;

            result = new Result { songStart = songStart, firstBeat = first };
            return true;
        }

        private static float Sample(float[] a, double x)
        {
            int i = (int)Math.Round(x);
            if (i <= 0 || i >= a.Length - 1) return 0f;
            return Mathf.Max(a[i], Mathf.Max(a[i - 1], a[i + 1]) * 0.6f);
        }

        /// <summary>WAV(PCM 16/24/32bit, float32) → 5ms 단위 RMS 에너지</summary>
        private static bool TryReadEnvelope(string path, out float[] energy)
        {
            energy = null;
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            if (new string(br.ReadChars(4)) != "RIFF") return false;
            br.ReadInt32();
            if (new string(br.ReadChars(4)) != "WAVE") return false;

            int format = 0, channels = 0, sampleRate = 0, bits = 0;
            while (fs.Position + 8 <= fs.Length)
            {
                string id = new string(br.ReadChars(4));
                int size = br.ReadInt32();
                long next = fs.Position + size + (size & 1);
                if (id == "fmt ")
                {
                    format = br.ReadInt16();
                    channels = br.ReadInt16();
                    sampleRate = br.ReadInt32();
                    br.ReadInt32();
                    br.ReadInt16();
                    bits = br.ReadInt16();
                    if (format == -2 && size >= 40) // WAVE_FORMAT_EXTENSIBLE
                    {
                        br.ReadInt16(); br.ReadInt16(); br.ReadInt32();
                        format = br.ReadInt16();
                    }
                }
                else if (id == "data" && channels > 0)
                {
                    int bytesPer = bits / 8;
                    int frameBytes = bytesPer * channels;
                    long frames = Math.Min(size, fs.Length - fs.Position) / frameBytes;
                    int hopFrames = Math.Max(1, (int)(sampleRate * Hop));
                    var env = new float[frames / hopFrames + 1];
                    var buffer = br.ReadBytes((int)(frames * frameBytes));

                    double sum = 0;
                    int count = 0, idx = 0;
                    for (long f = 0; f < frames; f++)
                    {
                        long o = f * frameBytes;
                        float v = 0f;
                        for (int c = 0; c < channels; c++, o += bytesPer)
                            v += ReadSample(buffer, (int)o, bits, format);
                        v /= channels;
                        sum += v * v;
                        if (++count == hopFrames)
                        {
                            env[idx++] = (float)Math.Sqrt(sum / count);
                            sum = 0;
                            count = 0;
                        }
                    }
                    Array.Resize(ref env, idx);
                    energy = env;
                    return true;
                }
                fs.Position = next;
            }
            return false;
        }

        private static float ReadSample(byte[] b, int o, int bits, int format)
        {
            switch (bits)
            {
                case 16: return BitConverter.ToInt16(b, o) / 32768f;
                case 24: return ((b[o] | (b[o + 1] << 8) | (b[o + 2] << 16)) << 8 >> 8) / 8388608f;
                case 32: return format == 3 ? BitConverter.ToSingle(b, o) : BitConverter.ToInt32(b, o) / 2147483648f;
                case 8: return (b[o] - 128) / 128f;
                default: return 0f;
            }
        }
    }
}
