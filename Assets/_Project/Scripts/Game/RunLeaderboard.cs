using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gstarr.Game
{
    /// <summary>최근 플레이 결과 중 상위 5개 점수를 저장하는 로컬 리더보드.</summary>
    public static class RunLeaderboard
    {
        private const string Key = "gstarr.leaderboard";
        private const string NicknameKey = "gstarr.nickname";
        private const int Limit = 5;

        public static string Nickname
        {
            get => PlayerPrefs.GetString(NicknameKey, "PLAYER");
            set => SetNickname(value);
        }

        [Serializable]
        private class Entry
        {
            public long score;
            public string stage;
            public string nickname;
        }

        [Serializable]
        private class SaveData
        {
            public List<Entry> entries = new List<Entry>();
        }

        public static IReadOnlyList<(long score, string nickname, string stage)> GetTopScores()
        {
            var saved = Load();
            var result = new List<(long score, string nickname, string stage)>(saved.entries.Count);
            foreach (var entry in saved.entries)
                result.Add((entry.score, string.IsNullOrEmpty(entry.nickname) ? Nickname : entry.nickname,
                    entry.stage ?? "STAGE"));
            return result;
        }

        public static void SetNickname(string nickname)
        {
            nickname = string.IsNullOrWhiteSpace(nickname) ? "PLAYER" : nickname.Trim();
            if (nickname.Length > 12) nickname = nickname.Substring(0, 12);
            PlayerPrefs.SetString(NicknameKey, nickname);
            PlayerPrefs.Save();
        }

        public static void Submit(long score, string stage)
        {
            var saved = Load();
            saved.entries.Add(new Entry { score = score, nickname = Nickname, stage = stage ?? "STAGE" });
            saved.entries.Sort((a, b) => b.score.CompareTo(a.score));
            if (saved.entries.Count > Limit) saved.entries.RemoveRange(Limit, saved.entries.Count - Limit);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(saved));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        private static SaveData Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return new SaveData();
            try
            {
                var saved = JsonUtility.FromJson<SaveData>(json);
                if (saved == null || saved.entries == null) return new SaveData();
                saved.entries.RemoveAll(entry => entry == null);
                return saved;
            }
            catch (ArgumentException)
            {
                return new SaveData();
            }
        }
    }
}
