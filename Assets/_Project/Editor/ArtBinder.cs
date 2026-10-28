using Gstarr.Items;
using UnityEditor;
using UnityEngine;

namespace Gstarr.EditorTools
{
    /// <summary>
    /// 이미지 요청서에서 '승빈 연결'인 그림을, 파일이 생기면 GameAssets 에 붙인다.
    /// 파일이 없으면 지금 쓰던 그림을 그대로 둔다.
    /// </summary>
    [InitializeOnLoad]
    public static class ArtBinder
    {
        private const string GameAssetsPath = "Assets/Resources/Gstarr/GameAssets.asset";

        static ArtBinder()
        {
            EditorApplication.delayCall += Bind;
        }

        private static void Bind()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (assets == null) return;

            bool changed = false;
            changed |= Assign(ref assets.bossSprite, "Assets/arts/character/boss.png");
            changed |= Assign(ref assets.moleSprite, "Assets/arts/character/enemy5.png");
            changed |= Assign(ref assets.titleLogo, "Assets/arts/UI/TitleLogo.png");
            changed |= Assign(ref assets.menuBackground, "Assets/arts/UI/MenuBackground.png");
            changed |= Assign(ref assets.heartSprite, "Assets/arts/UI/heartbar.png");
            if (!changed) return;

            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
        }

        private static bool Assign(ref Sprite slot, string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || slot == sprite) return false;
            slot = sprite;
            return true;
        }
    }
}
