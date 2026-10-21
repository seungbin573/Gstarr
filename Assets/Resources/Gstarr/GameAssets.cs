using UnityEngine;

namespace Gstarr.Items
{
    /// <summary>
    /// 런타임에 참조가 필요한 공용 에셋. Assets/Resources/Gstarr/GameAssets.asset 로 로드된다.
    /// </summary>
    public class GameAssets : ScriptableObject
    {
        public Sprite slotSprite;
        public ItemData[] defaultItems;
        public int defaultItemCount = 3;

        private static GameAssets instance;

        public static GameAssets Instance =>
            instance != null ? instance : instance = Resources.Load<GameAssets>("Gstarr/GameAssets");
    }
}
