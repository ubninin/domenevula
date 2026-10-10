using UnityEngine;

/// <summary>
/// 대사 한 묶음(한 번의 대화 전체). 기획자가 유니티 에디터에서
/// 코드 없이 직접 만들고 채워 넣는 데이터 에셋.
///
/// 만드는 법 (Unity 에디터):
/// Project 창에서 우클릭 → Create → 도메네뷸라 → Dialogue Sequence
/// 파일 이름은 dlg_(상황) 형식으로. 예: dlg_ch1_intro, dlg_npc_example_greet
///
/// NPC와의 대화라면 DialogueManager.PlayDialogue(이 에셋, 그 NPC의 Transform)으로 재생.
/// 파티끼리의 대화라면 balloonAnchor 없이 PlayDialogue(이 에셋)만 호출.
/// </summary>
[CreateAssetMenu(fileName = "dlg_new", menuName = "도메네뷸라/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    public DialogueLine[] lines;
}
