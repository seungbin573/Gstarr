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

        [Header("Rhythm")]
        [Tooltip("끄면 박자 판정 없이 점프/대쉬/아이템을 자유롭게 사용")]
        public bool enableRhythm = true;
        public AudioClip music;
        [Tooltip("박자 정보 JSON (kickforge-chart)")]
        public TextAsset beatChart;
        public Sprite beatNoteSprite;
        public Sprite beatCenterSprite;

        private static GameAssets instance;

        public static GameAssets Instance =>
            instance != null ? instance : instance = Resources.Load<GameAssets>("Gstarr/GameAssets");
    }
}
