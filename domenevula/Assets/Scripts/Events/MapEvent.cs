using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RPG Maker의 "이벤트 페이지" 한 장에 해당. 조건을 만족하면 이 페이지의 모습(스프라이트)과
/// 트리거/커맨드가 그 이벤트의 현재 동작이 된다.
/// </summary>
[System.Serializable]
public class MapEventPage
{
    public enum TriggerType
    {
        [InspectorName("액션 버튼 (가까이서 상호작용 키)")] ActionButton,
        [InspectorName("플레이어 접촉 (닿으면 자동 실행)")] PlayerTouch,
        [InspectorName("자동 실행 (조건 충족 시 1회, 조작 막힘)")] Autorun,
        [InspectorName("병렬 처리 (계속 반복, 조작 안 막힘)")] Parallel,
    }

    [Header("이 페이지 조건 (전부 선택 사항, 비워두면 항상 통과)")]
    [Tooltip("비워두면 조건 없음. 지정하면 이 전역 스위치가 Required Value와 같아야 이 페이지가 활성화됨")]
    public string requiredSwitch;
    public bool requiredValue = true;

    [Tooltip("비워두면 조건 없음. 지정하면 이 이벤트만의 self switch가 Required Self Switch Value와 같아야 활성화됨")]
    public string requiredSelfSwitch;
    public bool requiredSelfSwitchValue = false;

    [Header("이 페이지일 때의 모습")]
    [Tooltip("비워두면 스프라이트를 숨김(안 보임). 트리거/커맨드는 그대로 동작하므로 " +
             "'안 보이는 트리거 영역'을 만들 때도 쓸 수 있다")]
    public Sprite sprite;

    [Header("이 페이지의 동작")]
    public TriggerType trigger = TriggerType.ActionButton;
    [SerializeReference] public List<EventCommand> commands = new List<EventCommand>();

    /// <summary>이 페이지의 조건이 지금 만족되는지.</summary>
    public bool ConditionsMet(MapEvent owner)
    {
        if (!string.IsNullOrEmpty(requiredSwitch) && GameFlags.GetSwitch(requiredSwitch) != requiredValue)
            return false;
        if (!string.IsNullOrEmpty(requiredSelfSwitch) && owner.GetSelfSwitch(requiredSelfSwitch) != requiredSelfSwitchValue)
            return false;
        return true;
    }
}

/// <summary>
/// RPG Maker 이벤트 에디터의 "이벤트 페이지" 기능을 구현한 범용 이벤트 컴포넌트.
/// 페이지를 여러 장 만들면, 위에서부터 검사해서 조건을 만족하는 "마지막" 페이지가 자동으로 활성화된다
/// (1번 페이지에 조건을 안 걸어두면 "그 외 전부"를 담당하는 기본 페이지가 된다 — RPG Maker와 동일).
///
/// 페이지마다 모습(스프라이트)·트리거 종류·실행할 커맨드가 따로 있으므로,
/// "대사/상호작용이 끝나면 이 이벤트가 사라지거나 다른 모습으로 바뀐다" 같은 것을
/// SetSelfSwitchCommand 하나로 구현할 수 있다.
///
/// 예) 아이템 상자:
///   페이지 1 (조건 없음): 상자 스프라이트, Action Button,
///       커맨드 = [대사 "상자를 열었다", 아이템 지급, 셀프 스위치 A = true]
///   페이지 2 (Self Switch A = true): 스프라이트 비워둠(사라짐) 또는 "빈 상자" 스프라이트, 커맨드 없음
/// → 한 번 열면 셀프 스위치 A가 켜지고, 다음 프레임부터 페이지 2가 활성화되어 사라지거나 빈 상자로 바뀐다.
///
/// 붙이는 곳: 아무 오브젝트 (NPC, 상자, 트리거 영역 등 전부 동일)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MapEvent : MonoBehaviour
{
    [Tooltip("Action Button 트리거일 때 플레이어가 이 거리 안에 있어야 상호작용 키가 먹힌다 (모든 페이지 공통)")]
    [SerializeField] private float interactRange = 1.2f;

    [Tooltip("상호작용 가능할 때 보여줄 표시(말풍선 아이콘 등). 없어도 됨")]
    [SerializeField] private GameObject promptIndicator;

    [Tooltip("페이지 모습을 바꿀 때 쓸 SpriteRenderer. 비워두면 같은 오브젝트에서 자동으로 찾는다")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("페이지 (RPG Maker 이벤트 페이지와 동일한 개념)")]
    [Tooltip("위에서부터 검사해서 조건을 만족하는 '마지막' 페이지가 활성화된다. 1번 페이지는 조건 없이 기본 모습으로 두는 걸 추천")]
    [SerializeField] private List<MapEventPage> pages = new List<MapEventPage> { new MapEventPage() };

    /// <summary>지금 씬에서 실행 중인 이벤트(ActionButton/PlayerTouch/Autorun)가 하나라도 있는지.
    /// GameplayLock이 이 값을 보고 플레이어 입력을 막는다.</summary>
    public static int RunningEventCount { get; private set; }

    /// <summary>MapEvent 목록 (필요하면 다른 스크립트에서 순회용으로 사용).</summary>
    public static readonly List<MapEvent> All = new List<MapEvent>();

    private readonly Dictionary<string, bool> selfSwitches = new Dictionary<string, bool>();
    private bool isRunning;
    private Transform player;
    private int activePageIndex = -1;
    private Coroutine passiveRoutine;

    public MapEventPage CurrentPage =>
        (activePageIndex >= 0 && activePageIndex < pages.Count) ? pages[activePageIndex] : null;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        SetPrompt(false);
        RefreshActivePage(force: true);
    }

    private void Update()
    {
        RefreshActivePage(force: false);

        MapEventPage page = CurrentPage;
        if (page == null) return;
        if (page.trigger != MapEventPage.TriggerType.ActionButton) return;
        if (isRunning || player == null) return;

        bool inRange = Vector2.Distance(transform.position, player.position) <= interactRange;
        SetPrompt(inRange);

        if (!inRange) return;
        if (GameplayLock.IsActionLocked()) return;
        if (!DialogueManager.WasInteractKeyPressedThisFrame()) return;

        StartCoroutine(RunCommands(page));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        MapEventPage page = CurrentPage;
        if (page == null) return;
        if (page.trigger != MapEventPage.TriggerType.PlayerTouch) return;
        if (isRunning) return;
        if (!other.CompareTag("Player")) return;

        StartCoroutine(RunCommands(page));
    }

    /// <summary>조건에 맞는 마지막 페이지를 찾아 활성 페이지로 삼고, 바뀌었으면 모습/패시브 루틴을 갱신한다.</summary>
    private void RefreshActivePage(bool force)
    {
        int best = 0;
        for (int i = pages.Count - 1; i >= 0; i--)
        {
            if (pages[i].ConditionsMet(this)) { best = i; break; }
        }

        if (!force && best == activePageIndex) return;

        activePageIndex = best;
        MapEventPage page = pages[activePageIndex];
        ApplyAppearance(page);
        if (page.trigger != MapEventPage.TriggerType.ActionButton) SetPrompt(false);
        RestartPassiveRoutine(page);
    }

    private void ApplyAppearance(MapEventPage page)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.sprite = page.sprite;
        spriteRenderer.enabled = page.sprite != null;
    }

    private void RestartPassiveRoutine(MapEventPage page)
    {
        if (passiveRoutine != null) { StopCoroutine(passiveRoutine); passiveRoutine = null; }

        if (page.trigger == MapEventPage.TriggerType.Autorun)
            passiveRoutine = StartCoroutine(AutorunWatch(page));
        else if (page.trigger == MapEventPage.TriggerType.Parallel)
            passiveRoutine = StartCoroutine(ParallelLoop(page));
    }

    private IEnumerator AutorunWatch(MapEventPage page)
    {
        // 페이지가 유지되는 동안, 실행 중이 아니면 바로 한 번 실행한다.
        // 보통 커맨드 안에서 SetSelfSwitchCommand로 조건을 바꿔 다른 페이지로 넘어가게 만든다.
        while (CurrentPage == page)
        {
            if (!isRunning)
            {
                yield return RunCommands(page);
                yield break; // 페이지가 안 바뀌었다면 멈춰서 무한 반복 실행을 막는다 (필요하면 Parallel을 쓸 것)
            }
            yield return null;
        }
    }

    private IEnumerator ParallelLoop(MapEventPage page)
    {
        while (CurrentPage == page)
        {
            if (!isRunning) yield return RunCommands(page);
            else yield return null;
        }
    }

    private IEnumerator RunCommands(MapEventPage page)
    {
        isRunning = true;

        // ActionButton/PlayerTouch/Autorun은 컷신처럼 플레이어 조작을 막는다.
        // Parallel(배경 연출)만 조작을 막지 않는다.
        bool locksMovement = page.trigger != MapEventPage.TriggerType.Parallel;
        if (locksMovement) RunningEventCount++;

        var ctx = new EventExecutionContext { Self = this, Player = player };

        foreach (EventCommand cmd in page.commands)
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
