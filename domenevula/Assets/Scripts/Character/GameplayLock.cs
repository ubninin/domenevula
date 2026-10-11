/// <summary>
/// 지금 플레이어 조작(이동, 달리기, 나중에 만들 공격 등)을 막아야 하는 상황인지 한곳에서 판단.
/// 새 입력 기능(공격 등)을 추가할 때도 이 클래스의 IsActionLocked()만 확인하면 됨.
///
/// 메뉴창(ESC)은 대사 중에도 열 수 있어야 하므로, 메뉴 토글 스크립트는 이 클래스를 쓰지 않는다.
/// </summary>
public static class GameplayLock
{
    /// <summary>true면 이동/공격 등 필드 조작 입력을 무시해야 함.</summary>
    public static bool IsActionLocked()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
            return true;

        // 나중에 메뉴가 열려 있을 때도 이동을 막고 싶어지면 여기에 조건을 추가.
        // 메뉴는 대사 중에도 열 수 있어야 한다는 요구사항이 있으니, 이동 잠금에 메뉴 상태를
        // 넣을지는 별개로 결정할 것.

        return false;
    }
}
