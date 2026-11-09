using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gstarr.EditorTools
{
    /// <summary>tutorial 폴더의 gif가 아틀라스보다 새로우면 설명 창용 프레임을 다시 굽는다.</summary>
    [InitializeOnLoad]
    public static class TutorialGifBaker
    {
        static TutorialGifBaker()
        {
            if (NeedsBake()) EditorApplication.delayCall += Bake;
        }

        private static bool NeedsBake()
        {
            string src = Path.Combine(Application.dataPath, "arts", "UI", "tutorial");
            string dst = Path.Combine(Application.dataPath, "_Project", "Resources", "TutorialGifs");
            if (!Directory.Exists(src)) return false;
            foreach (string gif in Directory.GetFiles(src, "*.gif"))
            {
                string png = Path.Combine(dst, Path.GetFileNameWithoutExtension(gif) + ".png");
                if (!File.Exists(png) || File.GetLastWriteTimeUtc(gif) > File.GetLastWriteTimeUtc(png))
                    return true;
            }
            return false;
        }

        private static void Bake()
        {
            string script = Path.Combine(Application.dataPath, "_Project", "Editor", "bake_tutorial_gifs.py");
            if (!File.Exists(script)) return;
            var start = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "\"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            try
            {
                using (var process = Process.Start(start))
                {
                    if (process == null) return;
                    process.WaitForExit();
                }
                AssetDatabase.Refresh();
            }
            catch (System.Exception error)
            {
                UnityEngine.Debug.LogWarning("튜토리얼 gif를 다시 굽지 못했습니다. " + error.Message);
            }
        }
    }
}
