/// <summary>
/// 타자 애니메이션을 가진 대사 표시 컴포넌트가 구현하는 인터페이스.
/// DialogueManager가 "지금 글자가 나오는 중인지" 확인하고, 그렇다면 진행 키 입력을
/// 다음 줄로 넘기는 대신 "즉시 끝까지 보여주기"로 먼저 처리하는 데 쓴다.
/// </summary>
public interface IDialogueDisplay
{
    bool IsRevealing { get; }
    void SkipReveal();
}
