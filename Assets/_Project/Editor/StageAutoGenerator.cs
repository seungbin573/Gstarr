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
    /// Assets/Music/(폴더) 마다 wav + json 을 찾아 StageData 를 자동 생성/갱신하고,
    /// MainMenu 씬과 빌드 설정을 맞춘다. 코드 수정 없이 폴더만 추가하면 스테이지가 늘어난다.
    /// </summary>
    [InitializeOnLoad]
    public class StageAutoGenerator : AssetPostprocessor
    {
        private const string MusicRoot = "Assets/Music";
        private const string StagesFolder = "Assets/Resources/Gstarr/Stages";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

        private static bool queued;

        static StageAutoGenerator()
        {
            Queue();
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p => p.StartsWith(MusicRoot + "/")))
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
            Debug.Log("[Gstarr] 스테이지/메인 메뉴/빌드 설정 동기화 완료");
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
                var chart = AssetDatabase.FindAssets("t:TextAsset", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith(".json"))
                    .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                    .FirstOrDefault(t => t != null);

                string path = $"{StagesFolder}/{folderName}.asset";
                var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
                if (stage == null)
                {
                    if (clip == null && chart == null) continue;
                    stage = ScriptableObject.CreateInstance<StageData>();
                    stage.order = ParseNumber(folderName);
                    stage.displayName = PrettyName(folderName);
                    AssetDatabase.CreateAsset(stage, path);
                }

                if (stage.music != clip || stage.beatChart != chart)
                {
                    stage.music = clip;
                    stage.beatChart = chart;
                    EditorUtility.SetDirty(stage);
                }
            }
            AssetDatabase.SaveAssets();
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
                var p = AssetDatabase.FindAssets("t:Scene " + n)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == n);
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
