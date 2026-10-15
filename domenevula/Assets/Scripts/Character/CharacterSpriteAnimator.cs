using System.Collections;
using UnityEngine;

/// <summary>
/// idle/walk 두 모션을 방향별로 재생하는 애니메이터.
/// 달리기는 별도 모션이 아니라 walk 프레임을 더 빠른 속도로 재생하는 것으로 처리한다.
/// PlayerMovement2D가 매 프레임 UpdateFromInput()을 호출해서 상태를 알려주면
/// 이 스크립트가 알아서 맞는 프레임을 SpriteRenderer에 넣는다.
///
/// NPC처럼 전혀 움직이지 않는 캐릭터에도 그대로 쓸 수 있다: UpdateFromInput()을
/// 한 번도 안 불러도 Update()가 기본 상태(Idle, initialDirection 방향)로 계속
/// 애니메이션을 재생하므로, Idle 배열만 채워서 붙이기만 하면 대기 모션이 자동으로 돈다.
///
/// 붙이는 곳: Player 오브젝트, 또는 NPC 오브젝트 (SpriteRenderer가 이미 있는 그 오브젝트)
/// 인스펙터 연결: spriteRenderer ← 같은 오브젝트의 SpriteRenderer (비워두면 자동으로 찾음)
///              idle, walk 배열에 Sprite Editor로 슬라이스한 조각들을 방향별로 채워 넣기
///              (NPC는 walk를 안 채워도 됨 - 이동을 안 하니까 쓰이지 않음)
///              initialDirection ← NPC가 기본으로 바라볼 방향 (플레이어는 보통 Down 그대로 둠)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CharacterSpriteAnimator : MonoBehaviour
{
    private enum MotionState { Idle, Walk, Run }

    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("UpdateFromInput()으로 한 번도 움직인 적 없을 때(NPC 등) 기본으로 바라보는 방향")]
    [SerializeField] private FacingDirection initialDirection = FacingDirection.Down;

    [Header("모션 에셋 (Project 창에서 Create → 도메네뷸라 → Character Motion으로 생성)")]
    [Tooltip("대기 모션. 방향별 프레임/재생속도/크기 배율을 이 에셋 하나로 관리한다")]
    [SerializeField] private MotionPreset idleMotion;
    [Tooltip("걷기 모션 (Run은 이 프레임을 더 빠른 속도로 재사용해서 재생한다)")]
    [SerializeField] private MotionPreset walkMotion;

    [Header("Run 전용 배속 (Walk 모션 에셋의 프레임을 이 속도로 재생)")]
    [SerializeField] private float runFrameRate = 20f;

    [Tooltip("자주 나오는 주요 인물이면 연결 (선택). 위 Idle/Walk Motion을 비워두면 " +
             "이 캐릭터 에셋에 등록된 모션을 대신 쓴다. 우선순위가 가장 낮아서, 위 Idle/Walk Motion을 " +
             "따로 채워두면 그쪽이 항상 이긴다.")]
    [SerializeField] private CharacterDefinition characterDefinition;

    private MotionPreset ResolvedIdleMotion =>
        idleMotion != null ? idleMotion : (characterDefinition != null ? characterDefinition.idleMotion : null);
    private MotionPreset ResolvedWalkMotion =>
        walkMotion != null ? walkMotion : (characterDefinition != null ? characterDefinition.walkMotion : null);

    // DepthScale이 같이 붙어있으면 각 모션 에셋의 크기 배율 위에 거리 가중치를 곱해서 적용한다.
    // DepthScale이 없으면 이 스크립트가 직접 Transform.localScale에 반영한다.
    private DirectionalFrameSet idle => ResolvedIdleMotion != null ? ResolvedIdleMotion.frames : null;
    private DirectionalFrameSet walk => ResolvedWalkMotion != null ? ResolvedWalkMotion.frames : null;
    private float idleFrameRate => ResolvedIdleMotion != null ? ResolvedIdleMotion.frameRate : 6f;
    private float walkFrameRate => ResolvedWalkMotion != null ? ResolvedWalkMotion.frameRate : 15f;
    private Vector3 idleScale => ResolvedIdleMotion != null ? ResolvedIdleMotion.scale : Vector3.one;
    private Vector3 walkScale => ResolvedWalkMotion != null ? ResolvedWalkMotion.scale : Vector3.one;

    private FacingDirection currentDirection;
    private MotionState currentMotion = MotionState.Idle;
    private int frameIndex;
    private float frameTimer;
    private Vector3 baseLocalScale;
    private DepthScale depthScale;

    /// <summary>지금 프리셋이 재생 중이면 그 프리셋, 아니면 null.</summary>
    private MotionPreset activePreset;
    private Coroutine presetRoutine;

    /// <summary>지금 모션에 해당하는 크기 배율. DepthScale이 이 값을 읽어서 가중치를 곱한다.</summary>
    public Vector3 CurrentMotionScale =>
        activePreset != null ? activePreset.scale : (currentMotion == MotionState.Idle ? idleScale : walkScale);

    /// <summary>true인 동안은 Update()의 평소 idle/walk 재생을 멈추고, 한 번 재생 중인
    /// 커스텀 애니메이션(PlayOnceRoutine/프리셋)이 직접 SpriteRenderer를 조작한다.</summary>
    private bool playingOneShot;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        currentDirection = initialDirection;
        baseLocalScale = transform.localScale;
        depthScale = GetComponent<DepthScale>();
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

    /// <summary>
    /// NPC처럼 움직이진 않지만 특정 방향을 바라보게 하고 싶을 때(예: 플레이어를 보고 서있기) 쓴다.
    /// 이동 중(UpdateFromInput이 매번 방향을 다시 계산하는 상태)에는 호출해도 다음 입력에 덮어써진다.
    /// </summary>
    public void SetFacing(FacingDirection direction)
    {
        if (direction == currentDirection) return;
        currentDirection = direction;
        ResetFrame();
    }

    /// <summary>
    /// 대기/걷기 모션 에셋 자체를 통째로 바꾼다 (이벤트 페이지 전환 등에서 사용).
    /// 프레임뿐 아니라 그 에셋에 설정된 재생 속도/크기 배율까지 같이 바뀐다. null로 넘긴 쪽은 그대로 유지된다.
    /// </summary>
    public void SetMotionSets(MotionPreset idlePreset, MotionPreset walkPreset)
    {
        if (idlePreset != null) idleMotion = idlePreset;
        if (walkPreset != null) walkMotion = walkPreset;
        ResetFrame();
    }

    /// <summary>
    /// 지정한 모션을 한 번(처음부터 끝까지 한 바퀴)만 재생하고, 끝나면 원래 하던 idle/walk 재생으로 돌아간다.
    /// 재생하는 동안은 UpdateFromInput()을 불러도 무시된다(끝날 때까지 기다렸다가 반영됨).
    /// </summary>
    public IEnumerator PlayOnceRoutine(DirectionalFrameSet motion, float frameRate, FacingDirection? direction = null)
    {
        if (motion == null) yield break;

        FacingDirection dir = direction ?? currentDirection;
        Sprite[] frames = motion.Get(dir);
        if (frames == null || frames.Length == 0) yield break;

        playingOneShot = true;
        float frameDuration = 1f / Mathf.Max(0.01f, frameRate);
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) spriteRenderer.sprite = frames[i];
            yield return new WaitForSeconds(frameDuration);
        }
        playingOneShot = false;
        ResetFrame();
    }

    /// <summary>PlayOnceRoutine을 기다리지 않고 바로 재생만 시작하고 싶을 때.</summary>
    public void PlayOnce(DirectionalFrameSet motion, float frameRate, FacingDirection? direction = null)
    {
        StartCoroutine(PlayOnceRoutine(motion, frameRate, direction));
    }

    /// <summary>
    /// MotionPreset 에셋을 직접 넘겨서 재생한다 (Project 창의 모션 에셋을 그대로 끌어다 쓰면 됨).
    /// Loop 프리셋은 StopPreset()을 부를 때까지 계속 재생되고, Loop가 꺼진 프리셋은 한 바퀴 돌면
    /// 자동으로 평소 대기/걷기 재생으로 돌아간다. 돌려주는 Coroutine에 yield return하면
    /// (Loop가 꺼진 프리셋에 한해) 끝날 때까지 기다릴 수 있다.
    /// </summary>
    public Coroutine PlayPreset(MotionPreset preset, FacingDirection? direction = null)
    {
        if (preset == null)
        {
            Debug.LogWarning($"CharacterSpriteAnimator({name}): 재생할 모션 프리셋 에셋이 비어 있습니다.");
            return null;
        }

        StopPreset();
        presetRoutine = StartCoroutine(PlayPresetRoutine(preset, direction ?? currentDirection));
        return presetRoutine;
    }

    /// <summary>재생 중인 프리셋을 멈추고 평소 대기/걷기 재생(UpdateFromInput 기준)으로 돌아간다.</summary>
    public void StopPreset()
    {
        if (presetRoutine != null) { StopCoroutine(presetRoutine); presetRoutine = null; }
        if (activePreset != null)
        {
            activePreset = null;
            playingOneShot = false;
            ResetFrame();
        }
    }

    private IEnumerator PlayPresetRoutine(MotionPreset preset, FacingDirection dir)
    {
        Sprite[] frames = preset.frames != null ? preset.frames.Get(dir) : null;
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning($"CharacterSpriteAnimator({name}): 프리셋 '{preset.name}'(에셋)에 {dir} 방향 프레임이 없습니다.");
            yield break;
        }

        activePreset = preset;
        playingOneShot = true; // 평소 idle/walk Update 루프를 멈춘다
        float frameDuration = 1f / Mathf.Max(0.01f, preset.frameRate);
        int i = 0;

        while (true)
        {
            if (frames[i] != null) spriteRenderer.sprite = frames[i];
            if (depthScale == null) transform.localScale = Vector3.Scale(baseLocalScale, preset.scale);

            yield return new WaitForSeconds(frameDuration);

            i++;
            if (i >= frames.Length)
            {
                if (preset.loop) i = 0;
                else break;
            }
        }

        activePreset = null;
        playingOneShot = false;
        presetRoutine = null;
        ResetFrame();
    }

    private void Update()
    {
        if (playingOneShot) return; // 커스텀 1회 애니메이션 재생 중에는 평소 idle/walk 갱신을 건너뛴다

        DirectionalFrameSet activeSet = currentMotion == MotionState.Idle ? idle : walk;
        if (activeSet == null) return; // 모션 에셋이 비어있으면 재생하지 않고 지금 스프라이트를 그대로 둔다
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

        // DepthScale이 있으면 그쪽 LateUpdate가 모션 배율 위에 거리 가중치까지 곱해서 최종 적용하므로
        // 여기서는 건드리지 않는다 (두 스크립트가 같은 프레임에 서로 다른 값으로 localScale을 덮어쓰는 걸 방지).
        if (depthScale == null)
            transform.localScale = Vector3.Scale(baseLocalScale, CurrentMotionScale);
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
