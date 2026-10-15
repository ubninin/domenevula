using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자주 쓸 기본 이벤트 커맨드 모음. MapEvent의 커맨드 리스트에서 "+" 눌러서 이 중 하나를
/// 고르면 된다 (Unity가 EventCommand를 상속한 타입들을 자동으로 목록에 보여준다).
///
/// 새 커맨드가 더 필요해지면 이 파일에 같은 방식으로 추가하거나, 별도 파일에 추가해도 된다.
/// (한 파일에 여러 [Serializable] 클래스를 넣어도 문제 없음)
/// </summary>

/// <summary>대사(파티 대사창 또는 말풍선)를 재생하고 끝날 때까지 기다린다.</summary>
[System.Serializable]
public class ShowDialogueCommand : EventCommand
{
    public DialogueSequence sequence;

    [Tooltip("체크하면 이 이벤트가 붙은 오브젝트(예: NPC) 밑의 말풍선을 앵커로 쓴다. " +
             "파티 전용 대사만 있는 시퀀스면 체크 안 해도 됨")]
    public bool useSelfAsBalloonAnchor = true;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (sequence == null || DialogueManager.Instance == null) yield break;

        bool done = false;
        Transform anchor = useSelfAsBalloonAnchor && ctx.Self != null ? ctx.Self.transform : null;
        DialogueManager.Instance.PlayDialogue(sequence, anchor, () => done = true);
        yield return new WaitUntil(() => done);
    }
}

/// <summary>지정한 시간(초)만큼 그냥 기다린다.</summary>
[System.Serializable]
public class WaitCommand : EventCommand
{
    public float seconds = 1f;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        yield return new WaitForSeconds(seconds);
    }
}

/// <summary>전역 스위치(bool)를 켜거나 끈다. 조건부 분기/다른 이벤트의 발동 조건에서 읽힌다.</summary>
[System.Serializable]
public class SetSwitchCommand : EventCommand
{
    public string switchKey;
    public bool value = true;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (!string.IsNullOrEmpty(switchKey)) GameFlags.SetSwitch(switchKey, value);
        yield break;
    }
}

/// <summary>전역 변수(int)를 더하거나 뺀다 (퀘스트 진행 카운트, 처치 수 등에 사용).</summary>
[System.Serializable]
public class AddVariableCommand : EventCommand
{
    public string variableKey;
    public int amount = 1;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (!string.IsNullOrEmpty(variableKey)) GameFlags.AddVariable(variableKey, amount);
        yield break;
    }
}

/// <summary>
/// 이 이벤트만의 self switch(RPG Maker의 셀프 스위치 A~D에 해당)를 켠다.
/// "한 번 본 대사는 다시 안 뜨게" 같은 걸 MapEvent의 발동 조건(Require Self Switch)과
/// 엮어서 쓰면 된다.
/// </summary>
[System.Serializable]
public class SetSelfSwitchCommand : EventCommand
{
    public string selfSwitchKey = "A";
    public bool value = true;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        ctx.Self?.SetSelfSwitch(selfSwitchKey, value);
        yield break;
    }
}

/// <summary>
/// switchKey 값에 따라 then/else 커맨드 목록 중 하나를 실행한다.
/// then/else 안에 또 다른 모든 커맨드(심지어 또 다른 조건 분기)를 넣을 수 있다.
/// </summary>
[System.Serializable]
public class ConditionalBranchCommand : EventCommand
{
    public string switchKey;
    public bool expectedValue = true;

    [SerializeReference] public List<EventCommand> thenCommands = new List<EventCommand>();
    [SerializeReference] public List<EventCommand> elseCommands = new List<EventCommand>();

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        bool actual = !string.IsNullOrEmpty(switchKey) && GameFlags.GetSwitch(switchKey);
        List<EventCommand> toRun = (actual == expectedValue) ? thenCommands : elseCommands;

        foreach (EventCommand cmd in toRun)
        {
            if (cmd == null) continue;
            yield return cmd.Execute(ctx);
        }
    }
}

/// <summary>
/// 전투를 시작한다. 대사의 "전투 시작" 줄과 똑같이 BattleLauncher.Run을 통해 들어가므로,
/// BattleEncounter에 설정된 화면 전환 연출(BattleTransitionSettings)이 그대로 재생된다.
/// 전투가 끝나면 결과를 GameFlags 전역 스위치 "LastBattleVictory"에 저장하므로,
/// 뒤에 ConditionalBranchCommand(switchKey="LastBattleVictory")를 이어 붙이면
/// 승리/패배에 따라 다른 커맨드를 실행할 수 있다.
/// </summary>
[System.Serializable]
public class StartBattleCommand : EventCommand
{
    [Tooltip("시작할 전투. Project 창에서 Create → 도메네뷸라 → Battle Encounter 로 만든 에셋")]
    public BattleEncounter encounter;

    [Tooltip("체크하면 패배해도 게임오버로 안 가고 그대로 다음 커맨드로 이어짐. " +
             "체크 해제하면 패배 시 처리는 BattleLauncher/BattleManager 쪽 기본 동작을 따름")]
    public bool allowDefeat;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (encounter == null)
        {
            Debug.LogWarning("StartBattleCommand: Encounter가 비어 있습니다. 이 커맨드를 건너뜁니다.");
            yield break;
        }

        BattleOutcome outcome = BattleOutcome.Failed;
        yield return BattleLauncher.Run(encounter, allowDefeat, o => outcome = o);

        GameFlags.SetSwitch("LastBattleVictory", outcome == BattleOutcome.Victory);
    }
}

/// <summary>퀘스트를 시작한다 (QuestManager에 등록).</summary>
[System.Serializable]
public class StartQuestCommand : EventCommand
{
    public string questId;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (!string.IsNullOrEmpty(questId) && QuestManager.Instance != null)
            QuestManager.Instance.StartQuest(questId);
        yield break;
    }
}

/// <summary>퀘스트를 완료 처리한다.</summary>
[System.Serializable]
public class CompleteQuestCommand : EventCommand
{
    public string questId;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (!string.IsNullOrEmpty(questId) && QuestManager.Instance != null)
            QuestManager.Instance.CompleteQuest(questId);
        yield break;
    }
}

/// <summary>플레이어를 이 씬 안의 다른 좌표로 즉시 이동시킨다.</summary>
[System.Serializable]
public class TeleportPlayerCommand : EventCommand
{
    public Vector3 position;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (ctx.Player != null) ctx.Player.position = position;
        yield break;
    }
}

/// <summary>
/// 파티클/연출용 프리팹을 하나 생성한다 (배경 효과, 화면 연출 등). spawnAtSelf가 false면
/// customPosition에 직접 지정한 좌표에 생성한다.
/// </summary>
[System.Serializable]
public class PlayEffectCommand : EventCommand
{
    public GameObject effectPrefab;
    public bool spawnAtSelf = true;
    public Vector3 customPosition;
    [Tooltip("0이면 자동으로 안 없앰 (effectPrefab이 알아서 사라지는 파티클일 때)")]
    public float autoDestroyAfter = 0f;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (effectPrefab == null) yield break;

        Vector3 pos = spawnAtSelf && ctx.Self != null ? ctx.Self.transform.position : customPosition;
        GameObject instance = Object.Instantiate(effectPrefab, pos, Quaternion.identity);
        if (autoDestroyAfter > 0f) Object.Destroy(instance, autoDestroyAfter);
    }
}

/// <summary>
/// 이 이벤트 오브젝트 자체를 비활성화한다 (한 번 쓰면 사라지는 아이템, 상자 등).
/// </summary>
[System.Serializable]
public class DeactivateSelfCommand : EventCommand
{
    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        ctx.Self?.gameObject.SetActive(false);
        yield break;
    }
}
