using UnityEngine;

/// <summary>
/// idle/walk 외의 커스텀 모션 하나를 독립된 에셋으로 만들어 쓴다 (예: "Sit").
/// 캐릭터마다 따로 안 만들고 한 번 만들어서 여러 NPC/파티원에게 공유해서 끼울 수 있다.
///
/// 만드는 법: Project 창 우클릭 → Create → 도메네뷸라 → Character Motion
/// 파일 이름은 motion_(동작 이름) 형식으로. 예: motion_sit
///
/// 쓰는 법: CharacterSpriteAnimator.PlayPreset(이 에셋) 으로 재생하거나,
///          MapEvent 커맨드 "모션 프리셋 재생"의 Preset 슬롯에 이 에셋을 직접 끌어다 넣는다.
/// </summary>
[CreateAssetMenu(fileName = "motion_new", menuName = "도메네뷸라/Character Motion")]
public class MotionPreset : ScriptableObject
{
    [Tooltip("방향별 프레임")]
    public DirectionalFrameSet frames;
    public float frameRate = 8f;
    [Tooltip("체크하면 정지시킬 때까지 반복 재생. 체크 해제하면 한 바퀴만 재생하고 자동으로 평소 대기/걷기로 돌아감")]
    public bool loop = true;
    [Tooltip("이 모션이 재생되는 동안 적용할 크기 배율")]
    public Vector3 scale = Vector3.one;
}
