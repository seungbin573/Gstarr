namespace Gstarr.Game
{
    /// <summary>
    /// 임시 스토리. 문장은 이 파일만 고치면 된다.
    /// 시작 대사는 스테이지에 들어올 때, 나머지는 배경이 바뀔 때 나온다.
    /// </summary>
    public static class StageStory
    {
        public sealed class Beat
        {
            public string Title;
            public string Body;
        }

        public static readonly Beat Opening = new Beat
        {
            Title = "임시 · 꿈속",
            Body = "(임시) 감은 눈꺼풀 너머로, 교실의 피아노가 멀어졌다.\n(임시) 눈을 뜨자 에이든은 숲 한가운데 서 있었다."
        };

        public static Beat ForBackground(int index)
        {
            switch (index)
            {
                case 1:
                    return new Beat
                    {
                        Title = "임시 · 볼풀",
                        Body = "(임시) 숲이 끝나고, 거대한 볼풀이 나타났다.\n(임시) 발밑의 공이 꿈처럼 출렁인다."
                    };
                case 2:
                    return new Beat
                    {
                        Title = "임시 · 눈 숲",
                        Body = "(임시) 어느새 숲에 눈이 내려앉았다.\n(임시) 붉은 하늘 아래로 발소리가 묻힌다."
                    };
                case 3:
                    return new Beat
                    {
                        Title = "임시 · 교회",
                        Body = "(임시) 눈 숲 끝에 붉은 교회가 서 있다.\n(임시) 여기서부터는 풍경이 더 바뀌지 않는다."
                    };
                default:
                    return null;
            }
        }
    }
}
