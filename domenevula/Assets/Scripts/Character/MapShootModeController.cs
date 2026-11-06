using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 맵 씬에서 C키로 "상호작용 모드" ↔ "슈팅 모드"를 전환하는 토글.
/// 지금은 모드 전환 자체와 화면 상단 알림(ModeToast)/전환 쿨타임만 담당한다.
/// 실제로 슈팅 모드에서 총알을 쏘는 동작(서브 미니게임용)은 나중에 이 모드
/// (MapShootModeController.CurrentMode)를 읽어서 따로 구현하면 됨.
///
/// 붙이는 곳: 빈 오브젝트(예: Systems/ShootModeController), 씬에 하나만 두면 됨.
/// 인스펙터:
///   allowModeToggle ← 기본 꺼짐. 켜야만 C키로 모드가 바뀐다 (꺼져 있으면 항상 상호작용 모드).
///   toggleCooldown  ← 전환한 뒤 이 시간(초) 동안은 다시 전환할 수 없음 (너무 빠른 토글 방지).
///   toast           ← 화면 상단에 "OO 모드로 전환" 문구를 몇 초간 띄울 ModeToast (선택, 없어도 전환 자체는 동작함).
///
/// 대사/이벤트 진행 중이거나 메뉴(ESC)가 열려있을 때는 GameplayLock에 걸려서 전환되지 않는다.
/// </summary>
public class MapShootModeController : MonoBehaviour
{
    public enum Mode { Interact, Shoot }

    /// <summary>지금 상호작용 모드인지 슈팅 모드인지. 메뉴의 모드 표시, 나중의 발사 로직 등이 읽어서 쓴다.</summary>
    public static Mode CurrentMode { get; private set; } = Mode.Interact;

    /// <summary>모드가 바뀔 때마다 호출됨 (ModeToast가 구독해서 알림을 띄움).</summary>
    public static event Action<Mode> OnModeChanged;

    [Tooltip("체크해야 C키로 상호작용/슈팅 모드를 바꿀 수 있다. 기본은 꺼짐(상호작용 모드만 사용).")]
    [SerializeField] private bool allowModeToggle = false;
    [SerializeField] private float toggleCooldown = 1f;
    [SerializeField] private ModeToast toast;

    private float cooldownUntil;

    private void Update()
    {
        if (!allowModeToggle) return;
        if (GameplayLock.IsActionLocked()) return; // 대사/이벤트/메뉴 중엔 전환 못 함
        if (Time.time < cooldownUntil) return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.cKey.wasPressedThisFrame) return;

        CurrentMode = CurrentMode == Mode.Interact ? Mode.Shoot : Mode.Interact;
        cooldownUntil = Time.time + toggleCooldown;

        OnModeChanged?.Invoke(CurrentMode);
        if (toast != null) toast.Show(CurrentMode);
    }
}
