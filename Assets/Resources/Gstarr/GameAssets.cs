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

        [Tooltip("스타일 랭크 이미지 (C, B, A, S 순서)")]
        public Sprite[] rankSprites = new Sprite[4];

        [Header("Game")]
        [Tooltip("키 설정이 적용되는 공용 입력 에셋")]
        public UnityEngine.InputSystem.InputActionAsset inputActions;
        [Tooltip("스테이지에 씬이 지정되지 않았을 때 쓸 기본 스테이지 씬")]
        public string defaultStageScene = "Level_01";
        public string mainMenuScene = "MainMenu";
        [Tooltip("적 스프라이트 (Assets/arts/character/enemy1.png)")]
        public Sprite enemySprite;

        [Header("Glory Kill")]
        [Tooltip("글로리 킬 노트. 순서: 위(X), 오른쪽(A), 아래(B), 왼쪽(Y). PNG를 바꾸면 모양이 바뀐다.")]
        public Sprite[] gloryNoteSprites = new Sprite[4];
        [Tooltip("노트 중앙 링. Assets/arts/UI/Glory/Note_Ring.png")]
        public Sprite gloryRingSprite;

        private static GameAssets instance;

        public static GameAssets Instance =>
            instance != null ? instance : instance = Resources.Load<GameAssets>("Gstarr/GameAssets");
    }
}
