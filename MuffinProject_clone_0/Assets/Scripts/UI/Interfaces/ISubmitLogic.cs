using TMPro;

namespace UI.Interfaces
{
    public interface ISubmitLogic
    {
        /// <summary>
        /// 초기 설정 메소드
        /// </summary>
        /// <param name="input">InputField</param>
        void Init(TMP_InputField input);
    
        /// <summary>
        /// Submit 버튼 눌렀을 때 실행될 메소드
        /// </summary>
        /// <param name="input">InputField</param>
        void Execute(TMP_InputField input);
    }
}