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

        // MapEvent(ActionButton/PlayerTouch/Autorun)가 실행 중이면 컷신처럼 조작을 막는다.
        // Parallel 타입은 RunningEventCount에 포함되지 않으므로(배경 연출용) 영향 없음.
        if (MapEvent.RunningEventCount > 0)
            return true;

        // 메뉴(ESC)가 열려 있는 동안 이동/상호작용을 막을지는 GameMenuController의
        // lockMovementWhileOpen 인스펙터 값으로 결정한다 (기본 true). 메뉴를 "여는" 동작 자체는
        // 대사 중에도 가능해야 해서 GameMenuController.Update()는 이 클래스를 거치지 않는다.
        if (GameMenuController.Instance != null && GameMenuController.Instance.IsOpen && GameMenuController.Instance.LocksMovement)
            return true;

        return false;
    }
}
