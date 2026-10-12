using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WASD / 방향키로 캐릭터를 이동시키고, CharacterSpriteAnimator에 입력을 넘겨준다.
///
/// 붙이는 곳: Player 오브젝트 (CharacterSpriteAnimator와 같은 오브젝트)
/// 필요 조건: Rigidbody2D가 있으면 물리 기반으로 움직여서 다른 Collider와 충돌 처리가 됨 (추천).
///          없으면 그냥 Transform을 직접 움직인다 (벽 통과함, 테스트용으로만 적합).
///          Rigidbody2D를 쓸 거면 Body Type을 Kinematic으로 두는 걸 추천 (탑다운이라 중력 불필요).
///
/// IPartyMovementState 구현: 지금 실제로 달리는 중인지(Shift 눌렀는지)를 외부에서 읽을 수 있게 해서,
/// PartyFollower(따라오는 파티원)가 이 값을 그대로 따라가 플레이어와 같은 속도/애니메이션으로 달리게 한다.
/// </summary>
public class PlayerMovement2D : MonoBehaviour, IPartyMovementState
{
    [SerializeField] private float moveSpeed = 3f; // 걷기 속도, 초당 이동 거리 (유닛)
    [SerializeField] private float runSpeed = 5f;  // 달리기 속도
    [SerializeField] private CharacterSpriteAnimator animator;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isRunning;

    /// <summary>지금 실제로 달리는 중인지. PartyFollower가 이 값을 읽어서 따라온다.</summary>
    public bool IsRunning => isRunning;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<CharacterSpriteAnimator>();
    }

    private void Update()
    {
        if (GameplayLock.IsActionLocked())
        {
            // 대사 중 등 조작이 막힌 상황: 입력 무시하고 제자리에 멈춤 (idle로 전환됨)
            moveInput = Vector2.zero;
            isRunning = false;
            animator.UpdateFromInput(moveInput, isRunning);
            return;
        }

        moveInput = ReadInput();
        isRunning = ReadRunHeld();
        animator.UpdateFromInput(moveInput, isRunning);

        // Rigidbody2D가 없을 때만 여기서 직접 이동 (물리 없이 테스트용)
        if (rb == null)
        {
            float speed = isRunning ? runSpeed : moveSpeed;
            transform.position += (Vector3)(moveInput * speed * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        // Rigidbody2D가 있으면 물리 스텝에서 이동 (충돌 처리됨)
        if (rb != null)
        {
            float speed = isRunning ? runSpeed : moveSpeed;
            rb.MovePosition(rb.position + moveInput * speed * Time.fixedDeltaTime);
        }
    }

    private static bool ReadRunHeld()
    {
        if (Keyboard.current == null) return false;
        return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
    }

    private static Vector2 ReadInput()
    {
        if (Keyboard.current == null) return Vector2.zero;

        float x = 0f, y = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;

        Vector2 v = new Vector2(x, y);
        return v.sqrMagnitude > 1f ? v.normalized : v; // 대각선 이동 시 속도가 더 빨라지지 않게
    }
}
