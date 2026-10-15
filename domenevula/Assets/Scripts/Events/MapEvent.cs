using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RPG Maker 이벤트 에디터처럼 쓰는 범용 이벤트 컴포넌트.
/// NPC, 상자, 스위치, 배경 연출, 컷신 트리거 등 "맵에 놓고 어떤 조건에 어떤 동작을 하게 만들고
/// 싶은 모든 것"에 이거 하나만 붙이면 된다. NPCInteractable 같은 전용 스크립트를 따로 안 만들어도 됨.
///
/// 붙이는 곳: 아무 빈 오브젝트 (NPC 스프라이트가 달린 오브젝트에 바로 붙여도 되고,
///          보이지 않는 트리거 영역 전용 오브젝트로 따로 둬도 됨)
///
/// 트리거 종류:
///   - ActionButton: 플레이어가 가까이 와서 상호작용 키(Enter/Z/Space/클릭)를 누르면 실행
///   - PlayerTouch:  플레이어와 닿으면(Collider2D Is Trigger 필요) 자동 실행
///   - Autorun:      조건이 맞는 순간 자동 실행 (실행 중엔 플레이어 조작이 막힘, 컷신용)
///   - Parallel:     조건이 맞는 동안 백그라운드에서 계속 실행 (조작은 안 막힘, 배경 연출용)
///
/// 발동 조건: Required Switch를 지정하면 그 전역 스위치가 Required Value와 같을 때만 발동.
/// Require Self Switch를 같이 켜두면, "한 번 실행하면 SetSelfSwitchCommand로 꺼질 때까지
/// 다시 발동 안 함" 같은 걸 이벤트 자신만의 상태로 관리할 수 있다 (RPG Maker 셀프 스위치).
/// </summary>
public class MapEvent : MonoBehaviour
{
    public enum TriggerType { ActionButton, PlayerTouch, Autorun, Parallel }

    [Header("언제 실행할지")]
    [SerializeField] private TriggerType trigger = TriggerType.ActionButton;

    [Tooltip("ActionButton일 때 플레이어가 이 거리 안에 있어야 상호작용 키가 먹힌다")]
    [SerializeField] private float interactRange = 1.2f;

    [Tooltip("상호작용 가능할 때 보여줄 표시(말풍선 아이콘 등). 없어도 됨")]
    [SerializeField] private GameObject promptIndicator;

    [Header("발동 조건 (전부 선택 사항)")]
    [Tooltip("비워두면 조건 없음. 지정하면 이 전역 스위치가 Required Value와 같아야 발동")]
    [SerializeField] private string requiredSwitch;
    [SerializeField] private bool requiredValue = true;

    [Tooltip("비워두면 self switch 조건 없음. 지정하면 이 이벤트만의 self switch가 " +
             "Required Self Switch Value와 같아야 발동 (SetSelfSwitchCommand로 바꿀 수 있음)")]
    [SerializeField] private string requiredSelfSwitch;
    [SerializeField] private bool requiredSelfSwitchValue = false;

    [Header("실행할 내용 (RPG Maker 이벤트 커맨드에 해당)")]
    [SerializeReference] private List<EventCommand> commands = new List<EventCommand>();

    /// <summary>지금 씬에서 실행 중인 이벤트(Autorun/ActionButton/PlayerTouch)가 하나라도 있는지.
    /// GameplayLock이 이 값을 보고 플레이어 입력을 막는다.</summary>
    public static int RunningEventCount { get; private set; }

    private readonly Dictionary<string, bool> selfSwitches = new Dictionary<string, bool>();
    private bool isRunning;
    private Transform player;

    /// <summary>MapEvent 목록. PlayerInteractor 등에서 ActionButton 이벤트를 찾을 때 쓸 수 있다.</summary>
    public static readonly List<MapEvent> All = new List<MapEvent>();

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        SetPrompt(false);

        if (trigger == TriggerType.Autorun)
            StartCoroutine(AutorunWatch());
        else if (trigger == TriggerType.Parallel)
            StartCoroutine(ParallelLoop());
    }

    private void Update()
    {
        if (trigger != TriggerType.ActionButton) return;
        if (isRunning || player == null) return;

        bool inRange = Vector2.Distance(transform.position, player.position) <= interactRange;
        SetPrompt(inRange && ConditionsMet());

        if (!inRange) return;
        if (!ConditionsMet()) return;
        if (GameplayLock.IsActionLocked()) return;
        if (!DialogueManager.WasInteractKeyPressedThisFrame()) return;

        StartCoroutine(RunCommands());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (trigger != TriggerType.PlayerTouch) return;
        if (isRunning) return;
        if (!other.CompareTag("Player")) return;
        if (!ConditionsMet()) return;

        StartCoroutine(RunCommands());
    }

    private IEnumerator AutorunWatch()
    {
        // 조건이 맞아떨어지는 순간 딱 한 번 실행. 보통 SetSelfSwitchCommand로 조건을 꺼서
        // 다시 실행되지 않게 만드는 컷신/이벤트에 쓴다.
        while (true)
        {
            if (!isRunning && ConditionsMet())
                yield return RunCommands();

            yield return null;
        }
    }

    private IEnumerator ParallelLoop()
    {
        // 조건이 맞는 동안 계속 반복 실행 (조작을 막지 않음). 끝나면 조건을 다시 확인해서
        // 맞으면 또 실행 - 배경 연출을 계속 반복시키고 싶을 때 쓴다.
        while (true)
        {
            if (!isRunning && ConditionsMet())
                yield return RunCommands();
            else
                yield return null;
        }
    }

    private bool ConditionsMet()
    {
        if (!string.IsNullOrEmpty(requiredSwitch) && GameFlags.GetSwitch(requiredSwitch) != requiredValue)
            return false;

        if (!string.IsNullOrEmpty(requiredSelfSwitch) && GetSelfSwitch(requiredSelfSwitch) != requiredSelfSwitchValue)
            return false;

        return true;
    }

    private IEnumerator RunCommands()
    {
        isRunning = true;

        // ActionButton/PlayerTouch/Autorun은 컷신처럼 플레이어 조작을 막는다.
        // Parallel(배경 연출)만 조작을 막지 않는다.
        bool locksMovement = trigger != TriggerType.Parallel;
        if (locksMovement) RunningEventCount++;

        var ctx = new EventExecutionContext { Self = this, Player = player };

        foreach (EventCommand cmd in commands)
        {
            if (cmd == null) continue;
            yield return cmd.Execute(ctx);
        }

        if (locksMovement) RunningEventCount--;
        isRunning = false;
    }

    public bool GetSelfSwitch(string key) => selfSwitches.TryGetValue(key, out bool v) && v;
    public void SetSelfSwitch(string key, bool value) => selfSwitches[key] = value;

    private void SetPrompt(bool show)
    {
        if (promptIndicator != null) promptIndicator.SetActive(show);
    }
}
