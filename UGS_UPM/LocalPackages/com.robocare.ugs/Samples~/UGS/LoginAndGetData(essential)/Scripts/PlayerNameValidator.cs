namespace RoboCare.UGS
{
    public static class PlayerNameValidator
    {
        private static readonly string[] BANNED_WORDS = new string[]
        {
            "시발", "씨발", "씨팔", "시팔", "ㅅㅂ", "ㅆㅂ", "ㅆㅃ",
            "개새", "개색", "개세이", "개섹",
            "좆", "좇", "존나", "졸라", "ㅈㄴ",
            "병신", "븅신", "ㅂㅅ",
            "미친놈", "미친년", "또라이", "닥쳐",
            "꺼져", "엿먹", "뒤져", "디져", "가슴",
            "보지", "자지", "젖탱", "섹스", "섹수", "야동", "야설",
            "강간", "자살", "걸레", "창녀", "창놈",
            "느금", "느그", "니애미", "니에미", "애미",
            "fuck", "fck", "shit", "bitch", "asshole", "cunt",
            "dick", "pussy", "porn", "nigger", "nigga"
        };

        public static bool IsValid(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string normalized = name.Replace(" ", string.Empty).ToLowerInvariant();
            for (int i = 0; i < BANNED_WORDS.Length; i++)
            {
                if (normalized.Contains(BANNED_WORDS[i])) return false;
            }
            return true;
        }
    }
}
