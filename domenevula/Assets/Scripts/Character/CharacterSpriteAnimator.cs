using UnityEngine;

/// <summary>
/// idle/walk 두 모션을 방향별로 재생하는 애니메이터.
/// 달리기는 별도 모션이 아니라 walk 프레임을 더 빠른 속도로 재생하는 것으로 처리한다.
/// PlayerMovement2D가 매 프레임 UpdateFromInput()을 호출해서 상태를 알려주면
/// 이 스크립트가 알아서 맞는 프레임을 SpriteRenderer에 넣는다.
///
/// 붙이는 곳: Player 오브젝트 (SpriteRenderer가 이미 있는 그 오브젝트)
/// 인스펙터 연결: spriteRenderer ← 같은 오브젝트의 SpriteRenderer (비워두면 자동으로 찾음)
///              idle, walk 배열에 Sprite Editor로 슬라이스한 조각들을 방향별로 채워 넣기
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterSpriteAnimator : MonoBehaviour
{
    private enum MotionState { Idle, Walk, Run }

    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("모션별 방향 프레임 (Run은 Walk 프레임을 재사용)")]
    [SerializeField] private DirectionalFrameSet idle;
    [SerializeField] private DirectionalFrameSet walk;

    [Header("재생 속도 (초당 프레임 수)")]
    [SerializeField] private float idleFrameRate = 6f;
    [SerializeField] private float walkFrameRate = 15f;
    [SerializeField] private float runFrameRate = 20f;

    private FacingDirection currentDirection = FacingDirection.Down;
    private MotionState currentMotion = MotionState.Idle;
    private int frameIndex;
    private float frameTimer;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 이동 입력 벡터와 달리기 여부를 넘기면 방향/모션을 알아서 계산한다.
    /// </summary>
    /// <param name="moveInput">이동 입력 (크기 0이면 정지)</param>
    /// <param name="isRunning">true면 walk 프레임을 runFrameRate로 재생 (이동 중일 때만 의미 있음)</param>
    public void UpdateFromInput(Vector2 moveInput, bool isRunning = false)
    {
        bool moving = moveInput.sqrMagnitude > 0.0001f;
        MotionState newMotion = !moving ? MotionState.Idle : (isRunning ? MotionState.Run : MotionState.Walk);

        if (moving)
        {
            FacingDirection newDir = VectorToDirection(moveInput);
            if (newDir != currentDirection)
            {
                currentDirection = newDir;
                ResetFrame();
            }
        }

        // Walk <-> Run은 같은 프레임 배열을 쓰므로 프레임 인덱스를 유지해 끊김 없이 전환.
        // Idle로 들어가거나 Idle에서 나올 때만 리셋.
        bool enteringOrLeavingIdle = (currentMotion == MotionState.Idle) != (newMotion == MotionState.Idle);
        currentMotion = newMotion;
        if (enteringOrLeavingIdle) ResetFrame();
    }

    private void Update()
    {
        DirectionalFrameSet activeSet = currentMotion == MotionState.Idle ? idle : walk;
        Sprite[] frames = activeSet.Get(currentDirection);
        if (frames == null || frames.Length == 0) return;

        float frameRate = currentMotion switch
        {
            MotionState.Run => runFrameRate,
            MotionState.Walk => walkFrameRate,
            _ => idleFrameRate,
        };
        frameTimer += Time.deltaTime;

        float frameDuration = 1f / Mathf.Max(0.01f, frameRate);
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % frames.Length;
        }

        Sprite target = frames[frameIndex];
        if (target != null) spriteRenderer.sprite = target;
    }

    private void ResetFrame()
    {
        frameIndex = 0;
        frameTimer = 0f;
    }

    /// <summary>
    /// 좌/우/상/하 중 하나로 변환. 대각선 입력이면 더 큰 축을 우선하고,
    /// 크기가 같으면(정확히 대각선) 상하를 우선한다.
    /// </summary>
    private static FacingDirection VectorToDirection(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return v.x > 0 ? FacingDirection.Right : FacingDirection.Left;
        else
            return v.y > 0 ? FacingDirection.Up : FacingDirection.Down;
    }
}
