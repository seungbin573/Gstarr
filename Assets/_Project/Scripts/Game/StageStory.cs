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
            Title = "임시 · 뒤틀린 학교",
            Body = "(임시) 눈을 뜨자 에이든은 뒤틀린 학교 복도에 서 있었다.\n(임시) 형광등 아래, 복도가 끝없이 이어진다."
        };

        /// <summary>
        /// 장소가 처음 바뀔 때만. 같은 장소의 다음 그림(출구, 통로 중간, 둘째 눈숲)은 비운다.
        /// </summary>
        public static Beat ForBackground(int index)
        {
            switch (index)
            {
                case 1:
                    return new Beat
                    {
                        Title = "하늘문",
                        Body = "구름 위의 문. 문틈으로 하얀 빛이 샌다."
                    };
                case 2:
                    return new Beat
                    {
                        Title = "생일 파티",
                        Body = "촛불이 켜진 케이크, 그리고 키즈카페로 난 아치."
                    };
                case 3:
                    return new Beat
                    {
                        Title = "키즈카페",
                        Body = "공으로 가득 찬 놀이터. 발밑이 꿈처럼 출렁인다."
                    };
                case 5:
                    return new Beat
                    {
                        Title = "뒤틀린 통로",
                        Body = "별 장식 창 사이로 보랏빛이 새는 복도."
                    };
                case 8:
                    return new Beat
                    {
                        Title = "화려한 집",
                        Body = "장미와 거울. 거울마다 다른 하늘이 비친다."
                    };
                case 9:
                    return new Beat
                    {
                        Title = "눈 쌓인 숲",
                        Body = "집 끝에 펼쳐진 눈숲. 발소리가 눈에 묻힌다."
                    };
                case 11:
                    return new Beat
                    {
                        Title = "교회",
                        Body = "눈 숲 끝의 붉은 교회."
                    };
                default:
                    return null;
            }
        }
    }
}
