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

        public const string Speaker = "에이든";

        /// <summary>첫 장면. 교실에서 잠들기 전에 에이든이 하는 말.</summary>
        public static readonly string[] DrowsyLines =
        {
            "아 힘들어.. 지친다..",
            "...",
            "그냥 한번만.. 제대로 자자..",
        };

        /// <summary>다음 장면. 꿈에서 눈을 뜬 뒤 에이든이 하는 말.</summary>
        public static readonly string[] DreamLines =
        {
            "?? 뭐지.. 꿈인가.. 꿈인데 너무 현실적으로 몸이 느껴진다..",
            "(주머니속에 무언가 있다)",
            "엥? 이거 내 박스커터칼 아니야..? 이게 왜 여기있지?",
            "그리고 웬 껌총이...",
        };

        /// <summary>첫 토끼를 만났을 때. 화면이 확대된 뒤 토끼가 돌진하고, 그 다음 전투 설명이 열린다.</summary>
        public static readonly string[] BunnyMeetLines =
        {
            "뭐지? 토끼인가? 그런데 조금 이상한데...",
            "으악!",
        };

        public static readonly Beat Opening = new Beat
        {
            Title = Speaker,
            Body = string.Join("\n", DreamLines)
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
