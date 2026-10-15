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

/// <summary>지정한 모션(DirectionalFrameSet)을 한 번만 재생한다. (예: NPC가 손을 흔드는 동작 한 번)</summary>
[System.Serializable]
public class PlayCharacterAnimationCommand : EventCommand
{
    [Tooltip("비워두면 이 이벤트 자신(Self)에 붙은 Character Sprite Animator를 사용")]
    public CharacterSpriteAnimator target;
    [Tooltip("재생할 모션 (idle/walk 전용 모션이 아니어도 됨 - 방향별 프레임 묶음이면 아무거나)")]
    public DirectionalFrameSet animation;
    public float frameRate = 12f;
    [Tooltip("재생할 방향. 비워두면(체크 해제) 지금 보고 있는 방향 그대로 재생")]
    public bool overrideDirection = false;
    public FacingDirection direction = FacingDirection.Down;
    [Tooltip("체크하면 애니메이션이 끝날 때까지 기다렸다 다음 커맨드로 넘어간다. 체크 해제하면 재생만 시켜놓고 바로 다음 커맨드로 진행")]
    public bool waitForCompletion = true;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        CharacterSpriteAnimator anim = ResolveTarget(target, ctx);
        if (anim == null || animation == null) yield break;

        FacingDirection? dir = overrideDirection ? direction : (FacingDirection?)null;
        if (waitForCompletion)
            yield return anim.PlayOnceRoutine(animation, frameRate, dir);
        else
            anim.PlayOnce(animation, frameRate, dir);
    }

    internal static CharacterSpriteAnimator ResolveTarget(CharacterSpriteAnimator target, EventExecutionContext ctx)
    {
        if (target != null) return target;
        return ctx.Self != null ? ctx.Self.GetComponent<CharacterSpriteAnimator>() : null;
    }
}

/// <summary>대기/걷기 모션 자체를 바꾼다 (한 번 재생이 아니라, 이후 계속 이 모션으로 움직이게 됨).</summary>
[System.Serializable]
public class SetCharacterMotionCommand : EventCommand
{
    [Tooltip("비워두면 이 이벤트 자신(Self)에 붙은 Character Sprite Animator를 사용")]
    public CharacterSpriteAnimator target;
    [Tooltip("비워두면 대기 모션은 안 바꿈")]
    public MotionPreset newIdleMotion;
    [Tooltip("비워두면 걷기 모션은 안 바꿈")]
    public MotionPreset newWalkMotion;

    [Tooltip("체크하면 모션을 바꿈과 동시에 아래 방향으로 바라보게 고정한다. 체크 해제하면 바라보던 방향 그대로 유지")]
    public bool overrideDirection = false;
    public FacingDirection direction = FacingDirection.Down;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        CharacterSpriteAnimator anim = PlayCharacterAnimationCommand.ResolveTarget(target, ctx);
        anim?.SetMotionSets(newIdleMotion, newWalkMotion);
        if (overrideDirection) anim?.SetFacing(direction);
        yield break;
    }
}

/// <summary>이동 루트 한 단계. RPG Maker의 "이동 설정"에 해당.</summary>
[System.Serializable]
public class MoveRouteStep
{
    public enum StepType
    {
        [InspectorName("왼쪽으로 이동")] MoveLeft,
        [InspectorName("오른쪽으로 이동")] MoveRight,
        [InspectorName("위로 이동")] MoveUp,
        [InspectorName("아래로 이동")] MoveDown,
        [InspectorName("특정 좌표로 이동")] MoveToPosition,
        [InspectorName("다른 이벤트 쪽으로 이동")] MoveToEvent,
        [InspectorName("대기")] Wait,
        [InspectorName("바라보는 방향만 바꾸기")] Face,
    }

    public StepType type = StepType.MoveDown;

    [Tooltip("MoveLeft/Right/Up/Down 일 때 이동 거리(유닛)")]
    public float distance = 1f;

    [Tooltip("MoveToPosition 일 때 목표 좌표 (월드 좌표)")]
    public Vector3 targetPosition;

    [Tooltip("MoveToEvent 일 때 목표 이벤트")]
    public MapEvent targetEvent;
    [Tooltip("MoveToEvent 일 때, 이 거리까지만 가까워지면 멈춘다 (0이면 거의 겹칠 때까지 이동)")]
    public float stopDistance = 0f;

    [Tooltip("Wait 일 때 대기 시간(초)")]
    public float waitSeconds = 0.5f;

    [Tooltip("Face 일 때 바라볼 방향")]
    public FacingDirection faceDirection = FacingDirection.Down;
}

/// <summary>
/// RPG Maker의 "이동 설정(이동 루트)"에 해당. 여러 이동 단계를 순서대로 실행한다.
/// 대상에 Character Sprite Animator가 있으면 이동하는 동안 걷기 모션/방향도 자동으로 맞춰준다.
/// </summary>
[System.Serializable]
public class MoveRouteCommand : EventCommand
{
    [Tooltip("비워두면 이 이벤트 자신(Self)을 이동시킨다. 플레이어를 이동시키고 싶으면 Player의 Transform을 직접 끌어다 넣는다.\n" +
             "주의: 파티원(동료)은 Play 중에 PartyManager가 복제본으로 만들어내기 때문에, 씬에 미리 있는 오브젝트를 " +
             "여기 끌어다 넣어도 실제로 움직이는 그 캐릭터가 아닐 수 있다. 파티원을 움직이고 싶으면 이 필드 대신 " +
             "바로 아래 '파티원(캐릭터로 지정)'에 Character Definition을 넣는 걸 추천")]
    public Transform target;

    [Tooltip("파티원을 Character Definition으로 지정해서 움직이고 싶을 때 사용. " +
             "실행 시점에 PartyManager에서 그 캐릭터의 실제 복제본을 찾아서 움직인다. " +
             "Target과 둘 다 채우면 이쪽이 우선 적용됨")]
    public CharacterDefinition partyMember;

    public float moveSpeed = 2f;
    [Tooltip("체크하면 이동하는 동안 Character Sprite Animator의 걷기 모션을 자동 재생한다")]
    public bool animateWalking = true;
    public List<MoveRouteStep> steps = new List<MoveRouteStep>();

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        Transform t = ResolveMoveTarget(ctx);
        if (t == null) yield break;

        // "바라보는 방향만 바꾸기" 단계는 Animate Walking 체크와 무관하게 항상 동작해야 하므로
        // Character Sprite Animator 자체는 항상 찾아두고, 이동 중 걷기 모션 재생만 animateWalking으로 따로 제어한다.
        // (파티원 프리팹처럼 애니메이터가 target 바로 그 오브젝트가 아니라 자식 쪽에 붙어있는 구조도 있어서
        //  GetComponent 대신 자식까지 뒤지는 GetComponentInChildren을 쓴다)
        CharacterSpriteAnimator anim = t.GetComponentInChildren<CharacterSpriteAnimator>();

        foreach (MoveRouteStep step in steps)
        {
            switch (step.type)
            {
                case MoveRouteStep.StepType.MoveLeft:
                    yield return MoveBy(t, anim, Vector2.left * step.distance);
                    break;
                case MoveRouteStep.StepType.MoveRight:
                    yield return MoveBy(t, anim, Vector2.right * step.distance);
                    break;
                case MoveRouteStep.StepType.MoveUp:
                    yield return MoveBy(t, anim, Vector2.up * step.distance);
                    break;
                case MoveRouteStep.StepType.MoveDown:
                    yield return MoveBy(t, anim, Vector2.down * step.distance);
                    break;
                case MoveRouteStep.StepType.MoveToPosition:
                    yield return MoveTo(t, anim, step.targetPosition, 0f);
                    break;
                case MoveRouteStep.StepType.MoveToEvent:
                    if (step.targetEvent != null)
                        yield return MoveTo(t, anim, step.targetEvent.transform.position, step.stopDistance);
                    break;
                case MoveRouteStep.StepType.Wait:
                    yield return new WaitForSeconds(step.waitSeconds);
                    break;
                case MoveRouteStep.StepType.Face:
                    anim?.SetFacing(step.faceDirection);
                    break;
            }
        }

        if (animateWalking) anim?.UpdateFromInput(Vector2.zero, false); // 이동 끝나면 대기 모션으로
    }

    /// <summary>
    /// 파티원(Character Definition)이 지정되어 있으면 PartyManager에서 지금 실제로 화면에 움직이고 있는
    /// 복제본을 찾아 그걸 돌려준다(씬에 미리 놓아둔 비활성 오브젝트가 아니라). 못 찾으면 target, 그것도
    /// 없으면 Self 순으로 돌려준다.
    /// </summary>
    private Transform ResolveMoveTarget(EventExecutionContext ctx)
    {
        if (partyMember != null)
        {
            Transform found = PartyManager.Instance != null ? PartyManager.Instance.FindMemberTransform(partyMember) : null;
            if (found != null) return found;
            Debug.LogWarning($"MoveRouteCommand: 파티원 '{partyMember.name}'의 실제 오브젝트를 찾지 못했습니다 (아직 파티 합류 전이거나 PartyManager가 없음). Target 필드로 대체합니다.");
        }
        if (target != null) return target;
        return ctx.Self != null ? ctx.Self.transform : null;
    }

    private IEnumerator MoveBy(Transform t, CharacterSpriteAnimator anim, Vector2 delta)
    {
        Vector3 target = t.position + (Vector3)delta;
        yield return MoveTowards(t, anim, target);
    }

    private IEnumerator MoveTo(Transform t, CharacterSpriteAnimator anim, Vector3 targetPos, float stopDistance)
    {
        targetPos.z = t.position.z;
        if (stopDistance > 0f)
        {
            Vector3 toTarget = targetPos - t.position;
            float dist = toTarget.magnitude;
            if (dist <= stopDistance) yield break;
            targetPos = t.position + toTarget.normalized * (dist - stopDistance);
        }
        yield return MoveTowards(t, anim, targetPos);
    }

    private IEnumerator MoveTowards(Transform t, CharacterSpriteAnimator anim, Vector3 targetPos)
    {
        while ((t.position - targetPos).sqrMagnitude > 0.0001f)
        {
            Vector3 toTarget = targetPos - t.position;
            float step = Mathf.Max(0.01f, moveSpeed) * Time.deltaTime;

            if (step * step >= toTarget.sqrMagnitude)
            {
                t.position = targetPos;
            }
            else
            {
                Vector2 dir = toTarget.normalized;
                t.position += (Vector3)(dir * step);
                if (animateWalking) anim?.UpdateFromInput(dir, false);
            }
            yield return null;
        }
        if (animateWalking) anim?.UpdateFromInput(Vector2.zero, false);
    }
}

/// <summary>MotionPreset 에셋(Project 창에서 Create → 도메네뷸라 → Character Motion으로 만든 것)을 재생한다 (예: "motion_sit").</summary>
[System.Serializable]
public class PlayMotionPresetCommand : EventCommand
{
    [Tooltip("비워두면 이 이벤트 자신(Self)에 붙은 Character Sprite Animator를 사용")]
    public CharacterSpriteAnimator target;
    [Tooltip("재생할 모션 프리셋 에셋")]
    public MotionPreset preset;
    [Tooltip("재생할 방향. 비워두면(체크 해제) 지금 보고 있는 방향 그대로")]
    public bool overrideDirection = false;
    public FacingDirection direction = FacingDirection.Down;
    [Tooltip("체크하면 끝날 때까지 기다렸다 다음 커맨드로 넘어간다. " +
             "주의: 프리셋이 Loop 켜져 있으면 절대 안 끝나므로, Loop 프리셋에는 체크하지 말 것")]
    public bool waitForCompletion = false;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        CharacterSpriteAnimator anim = PlayCharacterAnimationCommand.ResolveTarget(target, ctx);
        if (anim == null || preset == null) yield break;

        FacingDirection? dir = overrideDirection ? direction : (FacingDirection?)null;
        Coroutine routine = anim.PlayPreset(preset, dir);
        if (waitForCompletion && routine != null)
            yield return routine;
    }
}

/// <summary>재생 중인 모션 프리셋을 멈추고 평소 대기/걷기 재생으로 되돌린다.</summary>
[System.Serializable]
public class StopMotionPresetCommand : EventCommand
{
    [Tooltip("비워두면 이 이벤트 자신(Self)에 붙은 Character Sprite Animator를 사용")]
    public CharacterSpriteAnimator target;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        CharacterSpriteAnimator anim = PlayCharacterAnimationCommand.ResolveTarget(target, ctx);
        anim?.StopPreset();
        yield break;
    }
}

/// <summary>화면 전체를 지정한 색으로 서서히 덮는다 (암전 등). 기본은 완전히 불투명한 검은색.</summary>
[System.Serializable]
public class FadeOutCommand : EventCommand
{
    [Tooltip("덮을 색. A(알파) 값이 최종 불투명도 - 기본값(검은색, 알파 1)은 완전히 가린다")]
    public Color color = Color.black;
    public float duration = 0.6f;
    [Tooltip("체크하면 다 덮일 때까지 기다렸다가 다음 커맨드로 넘어간다. 체크 해제하면 재생만 시켜놓고 바로 다음 커맨드로 진행")]
    public bool waitForCompletion = true;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (waitForCompletion)
            yield return ScreenFader.FadeOutRoutine(color, duration);
        else
            ScreenFader.FadeOut(color, duration);
    }
}

/// <summary>직전에 페이드아웃으로 덮인 화면을 서서히 걷어낸다.</summary>
[System.Serializable]
public class FadeInCommand : EventCommand
{
    public float duration = 0.6f;
    [Tooltip("체크하면 다 걷힐 때까지 기다렸다가 다음 커맨드로 넘어간다. 체크 해제하면 재생만 시켜놓고 바로 다음 커맨드로 진행")]
    public bool waitForCompletion = true;

    public override IEnumerator Execute(EventExecutionContext ctx)
    {
        if (waitForCompletion)
            yield return ScreenFader.FadeInRoutine(duration);
        else
            ScreenFader.FadeIn(duration);
    }
}
