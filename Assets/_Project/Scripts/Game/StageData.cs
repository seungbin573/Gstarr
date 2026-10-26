using System.Linq;
using Gstarr.Items;
using UnityEngine;

namespace Gstarr.Game
{
    [System.Serializable]
    public class StageMusicCue
    {
        [Tooltip("이 시각에 도달하면 다음 마디에서 구간을 전환")]
        public float atSeconds;
        [Tooltip("마디 시작에 맞춰 반복 재생할 음악 구간")]
        public AudioClip loop;
    }

    /// <summary>
    /// 스테이지 한 개. Assets/Music/(폴더) 에 wav + json 을 넣으면
    /// 에디터가 Assets/Resources/Gstarr/Stages/(폴더).asset 을 자동으로 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "Gstarr/Stage", fileName = "Stage")]
    public class StageData : ScriptableObject
    {
        public const string ResourcesFolder = "Gstarr/Stages";

        [Tooltip("스테이지 목록 정렬 순서")]
        public int order;
        public string displayName = "STAGE";
        public AudioClip music;
        [Tooltip("선택: 같은 BPM/박자에 맞춘 반복 음악 구간. 지정 시 atSeconds 이후 다음 마디에서 전환")]
        public StageMusicCue[] musicCues;
        [Tooltip("(선택) 박자 JSON. 음악 파일 이름 끝에 -118 처럼 BPM 이 있으면 무시됨")]
        public TextAsset beatChart;
        [Tooltip("글로리 킬 노트 순서 (noteforge-chart). 시간은 무시하고 적힌 순서만 쓴다")]
        public TextAsset gloryChart;
        [Tooltip("BPM 으로 박자를 만들 때 첫 박자 위치 (초). 노래 앞에 무음이 있으면 조정")]
        public float firstBeatOffset;

        public bool HasBpm(out float bpm)
        {
            bpm = 0f;
            return music != null && Gstarr.Rhythm.BeatChart.TryParseBpm(music.name, out bpm);
        }

        public bool IsPlayable => music != null && (HasBpm(out _) || beatChart != null);
        [Tooltip("비우면 GameAssets.defaultStageScene 사용")]
        public string sceneName;

        public string SceneName
        {
            get
            {
                if (!string.IsNullOrEmpty(sceneName)) return sceneName;
                var assets = GameAssets.Instance;
                return assets != null ? assets.defaultStageScene : "Stage_1";
            }
        }

        public string BestScoreKey => "gstarr.best." + name;
        public long BestScore => long.TryParse(PlayerPrefs.GetString(BestScoreKey, "0"), out var v) ? v : 0;

        public void SubmitScore(long score)
        {
            if (score <= BestScore) return;
            PlayerPrefs.SetString(BestScoreKey, score.ToString());
            PlayerPrefs.Save();
        }

        public static StageData[] LoadAll() =>
            Resources.LoadAll<StageData>(ResourcesFolder)
                .OrderBy(s => s.order)
                .ThenBy(s => s.name)
                .ToArray();
    }
}
