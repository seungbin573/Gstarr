namespace Gstarr.Game
{
    /// <summary>일시정지/게임 오버 중에는 플레이어 입력을 받지 않도록 하는 공용 플래그.</summary>
    public static class GamePause
    {
        public static bool IsPaused { get; set; }
    }
}
