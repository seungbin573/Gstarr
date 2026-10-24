using UnityEngine;

namespace Gstarr.Game
{
    /// <summary>
    /// 효과음 재생. 마스터 볼륨은 AudioListener 가 곱하고, 여기서는 효과음 볼륨만 곱한다.
    /// 클립이 없으면 짧은 합성음으로 만든다.
    /// </summary>
    public static class GameAudio
    {
        public enum Id
        {
            Melee,
            Gun,
            Shotgun,
            Hit,
            Hurt,
            Jump,
            Dash,
            Item,
            EnemySwing,
            EnemyShot,
            Ui,
            GloryNote,
            GlorySuccess,
            Door,
            Calibrate
        }

        private const float Mix = 0.38f;

        private static AudioSource source;
        private static AudioClip[] clips;

        public static void Play(Id id, float scale = 1f)
        {
            if (GameSettings.SfxVolume <= 0.001f || scale <= 0f) return;
            Ensure();
            var clip = clips[(int)id];
            if (clip == null) return;
            source.PlayOneShot(clip, GameSettings.SfxVolume * Mathf.Clamp01(scale) * Mix);
        }

        public static void PlayClip(AudioClip clip, float scale = 1f)
        {
            if (clip == null || GameSettings.SfxVolume <= 0.001f || scale <= 0f) return;
            Ensure();
            source.PlayOneShot(clip, GameSettings.SfxVolume * Mathf.Clamp01(scale) * Mix);
        }

        private static void Ensure()
        {
            if (source != null) return;
            var go = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            BuildClips();
        }

        private static void BuildClips()
        {
            clips = new AudioClip[15];
            clips[(int)Id.Melee] = Noise("sfx-melee", 0.09f, 22f, 0.55f, 0.4f, 140f);
            clips[(int)Id.Gun] = Noise("sfx-gun", 0.05f, 48f, 0.5f, 0.15f, 220f);
            clips[(int)Id.Shotgun] = Noise("sfx-shotgun", 0.14f, 16f, 0.62f, 0.7f, 90f);
            clips[(int)Id.Hit] = Noise("sfx-hit", 0.07f, 30f, 0.58f, 0.45f, 180f);
            clips[(int)Id.Hurt] = Sweep("sfx-hurt", 0.16f, 240f, 90f, 0.45f, 8f);
            clips[(int)Id.Jump] = Sweep("sfx-jump", 0.09f, 420f, 860f, 0.28f, 10f);
            clips[(int)Id.Dash] = Noise("sfx-dash", 0.12f, 14f, 0.32f, 0.05f, 0f);
            clips[(int)Id.Item] = Chord("sfx-item");
            clips[(int)Id.EnemySwing] = Noise("sfx-enemy-swing", 0.11f, 18f, 0.4f, 0.55f, 110f);
            clips[(int)Id.EnemyShot] = Sweep("sfx-enemy-shot", 0.08f, 520f, 280f, 0.32f, 12f);
            clips[(int)Id.Ui] = Sweep("sfx-ui", 0.035f, 1400f, 1800f, 0.22f, 28f);
            clips[(int)Id.GloryNote] = Sweep("sfx-glory-note", 0.07f, 880f, 1320f, 0.4f, 16f);
            clips[(int)Id.GlorySuccess] = Success();
            clips[(int)Id.Door] = Noise("sfx-door", 0.16f, 12f, 0.7f, 0.85f, 70f);
            clips[(int)Id.Calibrate] = Sweep("sfx-calibrate", 0.04f, 1000f, 1000f, 0.45f, 24f);
        }

        private static AudioClip Noise(string name, float duration, float decay, float amp, float bodyAmp, float bodyFreq)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * duration));
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)rate;
                float env = Mathf.Exp(-u * decay);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float body = bodyAmp > 0f ? Mathf.Sin(u * bodyFreq * Mathf.PI * 2f) * bodyAmp : 0f;
                data[i] = (noise * 0.65f + body) * amp * env;
            }
            return Clip(name, data, rate);
        }

        private static AudioClip Sweep(string name, float duration, float from, float to, float amp, float decay)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * duration));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float freq = Mathf.Lerp(from, to, u);
                phase += freq / rate;
                float attack = Mathf.Clamp01(i / (rate * 0.004f));
                data[i] = Mathf.Sin(phase * Mathf.PI * 2f) * amp * attack * Mathf.Exp(-(i / (float)rate) * decay);
            }
            return Clip(name, data, rate);
        }

        private static AudioClip Chord(string name)
        {
            const int rate = 44100;
            int n = (int)(rate * 0.18f);
            var data = new float[n];
            AddTone(data, rate, 0f, 0.16f, 660f, 0.28f, 10f);
            AddTone(data, rate, 0.04f, 0.14f, 990f, 0.24f, 10f);
            return Clip(name, data, rate);
        }

        private static AudioClip Success()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.42f);
            var data = new float[n];
            AddTone(data, rate, 0f, 0.2f, 784f, 0.34f, 8f);
            AddTone(data, rate, 0.07f, 0.2f, 988f, 0.32f, 8f);
            AddTone(data, rate, 0.14f, 0.28f, 1318f, 0.36f, 6f);
            return Clip(name: "sfx-glory-success", data, rate);
        }

        private static void AddTone(float[] data, int rate, float start, float dur, float freq, float amp, float decay)
        {
            int i0 = Mathf.Clamp((int)(start * rate), 0, data.Length);
            int i1 = Mathf.Clamp((int)((start + dur) * rate), 0, data.Length);
            for (int i = i0; i < i1; i++)
            {
                float u = (i - i0) / (float)rate;
                float attack = Mathf.Clamp01(u / 0.005f);
                data[i] += Mathf.Sin(u * freq * Mathf.PI * 2f) * amp * attack * Mathf.Exp(-u * decay);
            }
        }

        private static AudioClip Clip(string name, float[] data, int rate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
