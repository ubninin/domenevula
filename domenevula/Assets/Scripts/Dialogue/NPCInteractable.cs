using UnityEngine;

/// <summary>
/// 이 NPC와 대화할 수 있다는 표식 + 어떤 대사를 재생할지 정의.
///
/// 붙이는 곳: 대화 가능한 NPC 오브젝트 (예: Npc_Example).
/// 그 오브젝트 아래에 Canvas_SpeechBalloon 프리팹이 자식으로 있어야
/// DialogueManager가 말풍선을 찾아서 쓴다 (기존 구조 그대로).
///
/// 인스펙터 연결: sequence에 재생할 Dialogue Sequence 에셋을 넣는다.
/// promptIndicator(선택): 플레이어가 상호작용 범위 안에 들어왔을 때 보여줄 오브젝트
///   (예: NPC 머리 위에 떠 있는 느낌표나 말풍선 아이콘). 없으면 비워둬도 됨.
/// </summary>
public class NPCInteractable : MonoBehaviour
{
    [SerializeField] private DialogueSequence sequence;
    [SerializeField] private GameObject promptIndicator;

    public DialogueSequence Sequence => sequence;

    private static readonly System.Collections.Generic.List<NPCInteractable> all = new();
    public static System.Collections.Generic.IReadOnlyList<NPCInteractable> All => all;

    private void OnEnable()
    {
        all.Add(this);
        SetPrompt(false);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    public void SetPrompt(bool visible)
    {
        if (promptIndicator != null) promptIndicator.SetActive(visible);
    }
}
