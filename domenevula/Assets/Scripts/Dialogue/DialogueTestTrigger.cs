using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트 전용. T 키를 누르면 지정한 대사를 재생한다.
/// 확인 끝나면 이 스크립트는 지워도 된다.
///
/// 씬에 아무 빈 오브젝트(예: TestTrigger)를 만들어 붙이고,
/// 인스펙터의 Sequence에 테스트용 Dialogue Sequence 에셋을 넣는다.
/// NPC 대사를 테스트하려면 Balloon Anchor에 그 NPC를 드래그한다 (파티 대사면 비워둠).
/// </summary>
public class DialogueTestTrigger : MonoBehaviour
{
    [SerializeField] private DialogueSequence sequence;
    [SerializeField] private Transform balloonAnchor;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            DialogueManager.Instance.PlayDialogue(sequence, balloonAnchor);
        }
    }
}
