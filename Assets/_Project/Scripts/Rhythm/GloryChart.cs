using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gstarr.Rhythm
{
    /// <summary>
    /// noteforge-chart 의 notes 배열. timeSeconds 는 쓰지 않고 적힌 순서만 남긴다.
    /// 방향은 글로리 킬 버튼 번호: 0 위(X), 1 오른쪽(A), 2 아래(B), 3 왼쪽(Y).
    /// </summary>
    public static class GloryChart
    {
        public static int[] Load(TextAsset asset)
        {
            return asset == null ? null : Parse(asset.text);
        }

        public static int[] Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var raw = JsonUtility.FromJson<ChartJson>(json);
            if (raw == null || raw.notes == null || raw.notes.Length == 0) return null;

            var faces = new List<int>(raw.notes.Length);
            for (int i = 0; i < raw.notes.Length; i++)
            {
                int face = ToFace(raw.notes[i].direction);
                if (face >= 0) faces.Add(face);
            }
            return faces.Count > 0 ? faces.ToArray() : null;
        }

        public static int ToFace(string direction)
        {
            if (string.IsNullOrEmpty(direction)) return -1;
            switch (direction.Trim().ToLowerInvariant())
            {
                case "up": return 0;
                case "right": return 1;
                case "down": return 2;
                case "left": return 3;
                default: return -1;
            }
        }

        [Serializable]
        private class ChartJson
        {
            public string format;
            public NoteJson[] notes;
        }

        [Serializable]
        private class NoteJson
        {
            public string direction;
            public string type;
        }
    }
}
