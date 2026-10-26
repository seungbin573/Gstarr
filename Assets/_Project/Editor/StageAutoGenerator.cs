using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gstarr.Game;
using Gstarr.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gstarr.EditorTools
{
    /// <summary>
    /// Assets/Music/(??) ?? ?? ??? ?? StageData ? ?? ??/????,
    /// stageN ??? Stage_N ?? ????. ?? ?? ?? ?? ??? ???.
    /// ?? ?? ?? ?? -118 ?? BPM ? ??? JSON ?? ??? ?????.
    /// </summary>
    [InitializeOnLoad]
    public class StageAutoGenerator : AssetPostprocessor
    {
        private const string MusicRoot = "Assets/Music";
        private const string StagesFolder = "Assets/Resources/Gstarr/Stages";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        public const string StageScenePrefix = "Stage_";

        private static bool queued;

        static StageAutoGenerator()
        {
            Queue();
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(p => p.StartsWith(MusicRoot + "/") || p.EndsWith(".unity")))
                Queue();
        }

        private static void Queue()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () =>
            {
                queued = false;
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                SyncStages();
                EnsureMainMenuScene();
                SyncBuildSettings();
            };
        }

        [MenuItem("Tools/Gstarr/Sync Stages + Main Menu")]
        public static void SyncAll()
        {
            SyncStages();
            EnsureMainMenuScene();
            SyncBuildSettings();
            Debug.Log("[Gstarr] ???? / ?? ?? / ?? ?? ??? ??");
        }

        public static void SyncStages()
        {
            if (!AssetDatabase.IsValidFolder(MusicRoot)) return;
            EnsureFolder(StagesFolder);

            foreach (var folder in AssetDatabase.GetSubFolders(MusicRoot))
            {
                string folderName = Path.GetFileName(folder);
                var clip = AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
                    .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                    .FirstOrDefault(c => c != null);
                TextAsset beat = null;
                TextAsset glory = null;
                foreach (var jsonPath in AssetDatabase.FindAssets("t:TextAsset", new[] { folder })
                             .Select(g => AssetDatabase.GUIDToAssetPath(g))
                             .Where(p => p.EndsWith(".json")))
                {
                    var text = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
                    if (text == null) continue;
                    string body = text.text ?? "";
                    if (body.Contains("\"noteforge-chart\"")) glory = text;
                    else if (beat == null) beat = text;
                }

                string path = $"{StagesFolder}/{folderName}.asset";
                var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
                if (stage == null)
                {
                    if (clip == null) continue;
                    stage = ScriptableObject.CreateInstance<StageData>();
                    stage.order = ParseNumber(folderName);
                    stage.displayName = PrettyName(folderName);
                    AssetDatabase.CreateAsset(stage, path);
                }

                if (stage.music != clip || stage.beatChart != beat || stage.gloryChart != glory)
                {
                    stage.music = clip;
                    stage.beatChart = beat;
                    stage.gloryChart = glory;
                    EditorUtility.SetDirty(stage);
                }

                if (clip != null && Gstarr.Rhythm.BeatChart.TryParseBpm(clip.name, out float bpm)
                    && BeatAnalyzer.TryAnalyze(AssetDatabase.GetAssetPath(clip), bpm, out var analyzed))
                {
                    float offset = (float)analyzed.firstBeat;
                    if (!Mathf.Approximately(stage.firstBeatOffset, offset))
                    {
                        stage.firstBeatOffset = offset;
                        EditorUtility.SetDirty(stage);
                        Debug.Log($"[Gstarr] {folderName}: ?? ?? {analyzed.songStart:0.000}s, ? ?? {offset:0.000}s ({bpm} BPM)");
                    }
                }

                // stageN ?? ? Stage_N ? ?? ?? (?? ?? ?? ??? ??? ??)
                string autoScene = StageScenePrefix + stage.order;
                bool autoExists = FindScenePath(autoScene) != null;
                bool currentMissing = string.IsNullOrEmpty(stage.sceneName) || FindScenePath(stage.sceneName) == null;
                if (autoExists && (currentMissing || stage.sceneName == folderName) && stage.sceneName != autoScene)
                {
                    stage.sceneName = autoScene;
                    EditorUtility.SetDirty(stage);
                }
                else if (!autoExists && !string.IsNullOrEmpty(stage.sceneName) && FindScenePath(stage.sceneName) == null)
                {
                    stage.sceneName = "";
                    EditorUtility.SetDirty(stage);
                }
            }
            var liveFolders = new HashSet<string>();
            foreach (var folder in AssetDatabase.GetSubFolders(MusicRoot))
                liveFolders.Add(Path.GetFileName(folder));
            foreach (var guid in AssetDatabase.FindAssets("t:StageData", new[] { StagesFolder }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!liveFolders.Contains(Path.GetFileNameWithoutExtension(assetPath)))
                    AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.SaveAssets();
        }

        public static string FindScenePath(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            return AssetDatabase.FindAssets("t:Scene " + sceneName)
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == sceneName);
        }

        /// <summary>Assets ??? ?? ??? ?? ?? ?? Stage_N ??</summary>
        public static int NextStageNumber()
        {
            var used = new HashSet<int>();
            foreach (var g in AssetDatabase.FindAssets("t:Scene " + StageScenePrefix))
            {
                var m = Regex.Match(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)), @"^Stage_(\d+)$");
                if (m.Success) used.Add(int.Parse(m.Groups[1].Value));
            }
            int n = 1;
            while (used.Contains(n)) n++;
            return n;
        }

        public static void EnsureMainMenuScene()
        {
            if (File.Exists(MainMenuPath)) return;
            EnsureFolder("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.1f);
            SceneManager.MoveGameObjectToScene(camGo, scene);

            var menuGo = new GameObject("MainMenu", typeof(MainMenu));
            SceneManager.MoveGameObjectToScene(menuGo, scene);

            EditorSceneManager.SaveScene(scene, MainMenuPath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
        }

        public static void SyncBuildSettings()
        {
            var assets = Resources.Load<GameAssets>("Gstarr/GameAssets");
            var wanted = new List<string>();
            if (File.Exists(MainMenuPath)) wanted.Add(MainMenuPath);

            var sceneNames = new HashSet<string>();
            if (assets != null && !string.IsNullOrEmpty(assets.defaultStageScene)) sceneNames.Add(assets.defaultStageScene);
            foreach (var g in AssetDatabase.FindAssets("t:StageData", new[] { StagesFolder }))
            {
                var s = AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(g));
                if (s != null && !string.IsNullOrEmpty(s.sceneName)) sceneNames.Add(s.sceneName);
            }
            foreach (var n in sceneNames)
            {
                var p = FindScenePath(n);
                if (p != null) wanted.Add(p);
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            bool changed = false;
            foreach (var p in wanted)
            {
                int i = scenes.FindIndex(s => s.path == p);
                if (i < 0) { scenes.Add(new EditorBuildSettingsScene(p, true)); changed = true; }
                else if (!scenes[i].enabled) { scenes[i].enabled = true; changed = true; }
            }
            int menu = scenes.FindIndex(s => s.path == MainMenuPath);
            if (menu > 0)
            {
                var m = scenes[menu];
                scenes.RemoveAt(menu);
                scenes.Insert(0, m);
                changed = true;
            }
            if (changed) EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static int ParseNumber(string s)
        {
            var m = Regex.Match(s, @"\d+");
            return m.Success ? int.Parse(m.Value) : 999;
        }

        private static string PrettyName(string folder)
        {
            var m = Regex.Match(folder, @"^([A-Za-z]+)\s*(\d+)$");
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() + " " + m.Groups[2].Value : folder.ToUpperInvariant();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
