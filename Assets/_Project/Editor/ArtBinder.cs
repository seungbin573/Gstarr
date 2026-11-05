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
            changed |= AssignFolder(ref assets.angelSprites, "Assets/arts/character/angel");
            changed |= AssignFolder(ref assets.annoyingKidSprites, "Assets/arts/character/ankid");
            changed |= AssignFolder(ref assets.blackKidSprites, "Assets/arts/character/blackKid");
            changed |= AssignFolder(ref assets.blackKidThrowSprites, "Assets/arts/character/blackKidThrow");
            changed |= AssignFolder(ref assets.bunnySprites, "Assets/arts/character/bunny");
            changed |= AssignFolder(ref assets.distortedBunnySprites, "Assets/arts/character/d_bunny");
            changed |= AssignFolder(ref assets.twinBunnySprites, "Assets/arts/character/twinBunny");
            changed |= AssignFolder(ref assets.shadowmanSprites, "Assets/arts/character/shadowman");
            if (!changed) return;

            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
        }

        private static bool Assign(ref Sprite slot, string path)
        {
            var sprite = LoadSprite(path);
            if (sprite == null || slot == sprite) return false;
            slot = sprite;
            return true;
        }

        private static Sprite LoadSprite(string path)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                    if (all[i] is Sprite sprite) return sprite;
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static bool AssignFolder(ref Sprite[] slot, string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            System.Array.Sort(guids, (a, b) =>
                string.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b), System.StringComparison.Ordinal));
            var loaded = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < guids.Length; i++)
            {
                var sprite = LoadSprite(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (sprite != null) loaded.Add(sprite);
            }

            if (loaded.Count == 0) return false;
            if (slot != null && slot.Length == loaded.Count)
            {
                bool same = true;
                for (int i = 0; i < loaded.Count; i++)
                    if (slot[i] != loaded[i]) { same = false; break; }
                if (same) return false;
            }

            slot = loaded.ToArray();
            return true;
        }
    }
}
