using System.Collections.Generic;
using System.Linq;
using Gstarr.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gstarr.Game
{
    /// <summary>볼륨 / 그래픽 / 키 설정. PlayerPrefs 에 저장되고 게임 시작 시 적용된다.</summary>
    public static class GameSettings
    {
        private const string KeyMaster = "gstarr.masterVolume";
        private const string KeyMusic = "gstarr.musicVolume";
        private const string KeySfx = "gstarr.sfxVolume";
        private const string KeyAudioOffset = "gstarr.audioOffsetMs";
        private const string KeyQuality = "gstarr.quality";
        private const string KeyFullscreen = "gstarr.fullscreen";
        private const string KeyVSync = "gstarr.vsync";
        private const string KeyResW = "gstarr.resW";
        private const string KeyResH = "gstarr.resH";
        private const string KeyBindings = "gstarr.bindings";
        private const string KeyRhythmAssist = "gstarr.rhythmAssist";
        private const string KeyArcadeMode = "gstarr.arcadeMode";

        public const int MinAudioOffsetMs = -200;
        public const int MaxAudioOffsetMs = 200;

        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static int AudioOffsetMs { get; private set; }
        public static bool RhythmAssist { get; private set; }
        public static bool ArcadeMode { get; private set; }

        public static string AudioOffsetLabel =>
            AudioOffsetMs > 0 ? "+" + AudioOffsetMs + " ms" : AudioOffsetMs + " ms";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
            MusicVolume = PlayerPrefs.GetFloat(KeyMusic, 1f);
            SfxVolume = PlayerPrefs.GetFloat(KeySfx, 1f);
            AudioOffsetMs = Mathf.Clamp(PlayerPrefs.GetInt(KeyAudioOffset, 0), MinAudioOffsetMs, MaxAudioOffsetMs);
            RhythmAssist = PlayerPrefs.GetInt(KeyRhythmAssist, 0) == 1;
            ArcadeMode = PlayerPrefs.GetInt(KeyArcadeMode, 0) == 1;
            AudioListener.volume = MasterVolume;

            if (PlayerPrefs.HasKey(KeyQuality))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality), 0, QualitySettings.names.Length - 1), true);
            if (PlayerPrefs.HasKey(KeyVSync))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(KeyVSync);
            if (PlayerPrefs.HasKey(KeyResW) && !Application.isEditor)
                Screen.SetResolution(PlayerPrefs.GetInt(KeyResW), PlayerPrefs.GetInt(KeyResH), Fullscreen);

            var actions = Actions;
            if (actions != null && PlayerPrefs.HasKey(KeyBindings))
                actions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(KeyBindings));
        }

        public static UnityEngine.InputSystem.InputActionAsset Actions =>
            GameAssets.Instance != null ? GameAssets.Instance.inputActions : null;

        public static void SetMasterVolume(float v)
        {
            MasterVolume = Mathf.Clamp01(v);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
        }

        public static void SetMusicVolume(float v)
        {
            MusicVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
        }

        public static void SetSfxVolume(float v)
        {
            SfxVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeySfx, SfxVolume);
        }

        public static void SetAudioOffsetMs(int ms)
        {
            AudioOffsetMs = Mathf.Clamp(ms, MinAudioOffsetMs, MaxAudioOffsetMs);
            PlayerPrefs.SetInt(KeyAudioOffset, AudioOffsetMs);
        }

        public static void SetRhythmAssist(bool enabled)
        {
            RhythmAssist = enabled;
            PlayerPrefs.SetInt(KeyRhythmAssist, enabled ? 1 : 0);
        }

        public static void SetArcadeMode(bool enabled)
        {
            ArcadeMode = enabled;
            PlayerPrefs.SetInt(KeyArcadeMode, enabled ? 1 : 0);
        }

        public static int Quality
        {
            get => QualitySettings.GetQualityLevel();
            set
            {
                int q = (int)Mathf.Repeat(value, QualitySettings.names.Length);
                QualitySettings.SetQualityLevel(q, true);
                PlayerPrefs.SetInt(KeyQuality, q);
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
                Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }
        }

        public static bool VSync
        {
            get => QualitySettings.vSyncCount > 0;
            set
            {
                QualitySettings.vSyncCount = value ? 1 : 0;
                PlayerPrefs.SetInt(KeyVSync, QualitySettings.vSyncCount);
            }
        }

        public static List<Vector2Int> Resolutions =>
            Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Distinct()
                .OrderBy(r => r.x * r.y)
                .ToList();

        public static Vector2Int CurrentResolution =>
            new Vector2Int(PlayerPrefs.GetInt(KeyResW, Screen.width), PlayerPrefs.GetInt(KeyResH, Screen.height));

        public static void SetResolution(Vector2Int res)
        {
            PlayerPrefs.SetInt(KeyResW, res.x);
            PlayerPrefs.SetInt(KeyResH, res.y);
            Screen.SetResolution(res.x, res.y, Fullscreen);
        }

        public static void SaveBindings()
        {
            var actions = Actions;
            if (actions == null) return;
            PlayerPrefs.SetString(KeyBindings, actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
