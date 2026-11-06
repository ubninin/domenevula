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
        // 전투 씬은 맵 씬 위에 Additive 로 얹히기 때문에 맵의 스크립트들이 전투 중에도 계속 돌아간다.
        // 전투 중에는 맵 쪽 조작(이벤트 시작, 대화, 이동 등)을 전부 막는다.
        if (BattleLauncher.Active)
            return true;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
            return true;

        // MapEvent(ActionButton/PlayerTouch/Autorun)가 실행 중이면 컷신처럼 조작을 막는다.
        // Parallel 타입은 RunningEventCount에 포함되지 않으므로(배경 연출용) 영향 없음.
        if (MapEvent.RunningEventCount > 0)
            return true;

        // 메뉴(ESC)가 열려 있으면 이동/상호작용을 막는다 (메뉴 쪽 inspector 의 lockMovementWhileOpen 으로 끌 수 있음).
        if (GameMenuController.Instance != null && GameMenuController.Instance.IsOpen
            && GameMenuController.Instance.LocksMovement)
            return true;

        return false;
    }
}
