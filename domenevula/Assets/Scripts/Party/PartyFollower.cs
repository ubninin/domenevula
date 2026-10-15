using UnityEngine;

/// <summary>
/// "앞사람"(플레이어 또는 바로 앞 파티원)을 따라가는 파티원 캐릭터에 붙이는 컴포넌트.
/// RPG Maker 필드 파티원처럼: 거리가 멀어지면 쫓아오고, followDistance만큼 간격을 유지한다.
///
/// 달리기 여부는 거리로 추측하지 않고, 앞사람이 IPartyMovementState를 구현하고 있으면
/// 그 값(실제로 Shift를 눌러 달리는 중인지)을 그대로 따라간다. 그래서 플레이어가 달리면
/// 파티원도 즉시 같은 속도/애니메이션으로 달린다.
///
/// 붙이는 곳: 파티원 캐릭터 프리팹 최상위 (SpriteRenderer + CharacterSpriteAnimator와 같은 오브젝트 또는 그 부모)
/// 인스펙터 연결: animator ← 같은 오브젝트(또는 자식)의 CharacterSpriteAnimator
/// target/followDistance는 PartyManager가 SpawnFollowers 시점에 자동으로 넣어주므로
/// 직접 연결 안 해도 됨 (혼자 테스트할 땐 인스펙터에서 target을 수동으로 넣어도 됨).
/// </summary>
public class PartyFollower : MonoBehaviour, IPartyMovementState
{
    [SerializeField] private Transform target;
    [SerializeField] private float followDistance = 0.6f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float runSpeed = 5f;

    [Tooltip("target이 IPartyMovementState를 구현 안 했을 때만 쓰는 예비 기준: " +
             "이 followDistance의 몇 배 이상 벌어지면 달리기로 간주")]
    [SerializeField] private float runCatchUpMultiplier = 2.5f;

    [SerializeField] private CharacterSpriteAnimator animator;

    private IPartyMovementState targetMovementState;

    /// <summary>지금 이 파티원이 달리는 중인지. 뒷사람이 체인으로 이 값을 그대로 물려받는다.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>PartyManager가 체인을 구성할 때 호출.</summary>
    public void Configure(Transform newTarget, float distance)
    {
        target = newTarget;
        followDistance = distance;
        targetMovementState = newTarget != null ? newTarget.GetComponent<IPartyMovementState>() : null;
    }

    private void Awake()
    {
        if (target != null && targetMovementState == null)
            targetMovementState = target.GetComponent<IPartyMovementState>();
    }

    private void Update()
    {
        // 대사/이벤트 중에는 플레이어도 멈춰 있으니, 아직 followDistance만큼 안 따라붙었어도
        // 파티원이 계속 쫓아오며 걷는 모션을 재생하지 않도록 같이 멈춘다.
        if (GameplayLock.IsActionLocked())
        {
            IsRunning = false;
            animator?.UpdateFromInput(Vector2.zero, false);
            return;
        }

        if (target == null)
        {
            IsRunning = false;
            animator?.UpdateFromInput(Vector2.zero, false);
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.z = 0f;
        float distance = toTarget.magnitude;

        Vector2 moveInput = Vector2.zero;
        bool wantsToMove = distance > followDistance;

        // 앞사람의 실제 달리기 상태를 그대로 따라간다. 모르면(인터페이스 없으면) 거리 기준으로 추측.
        bool shouldRun = targetMovementState != null
            ? targetMovementState.IsRunning
            : distance > followDistance * runCatchUpMultiplier;

        IsRunning = wantsToMove && shouldRun;

        if (wantsToMove)
        {
            float speed = IsRunning ? runSpeed : moveSpeed;

            Vector3 direction = toTarget.normalized;
            float step = speed * Time.deltaTime;
            float remaining = distance - followDistance;
            if (step > remaining) step = remaining;

            transform.position += direction * step;
            moveInput = new Vector2(direction.x, direction.y);
        }

        if (animator != null)
            animator.UpdateFromInput(moveInput, IsRunning);
    }
}
