using System.Text.RegularExpressions;

namespace UI.NickName
{
    public static class NicknameValidator
    {
        public const int MAX_LENGTH = 16;
        public const int MIN_LENGTH = 2;

        public static bool Validate(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                return false;
            }
        
            if (nickname.Length is < MIN_LENGTH or > MAX_LENGTH)
            {
                return false;
            }
        
            // 공백 포함 여부
            if (nickname.Contains(" "))
            {
                return false;
            }
        
            // 허용 문자 검사
            var regex = new Regex(@"^[가-힣a-zA-Z0-9]+$");
            if (!regex.IsMatch(nickname))
            {
                return false;
            }
        
            // 한글 자음 모음 포함
            var koreanJamoRegex = new Regex(@"[ㄱ-ㅎㅏ-ㅣ]");
            if (koreanJamoRegex.IsMatch(nickname))
            {
                return false;
            }
        
            return true;
        }
    }
}
