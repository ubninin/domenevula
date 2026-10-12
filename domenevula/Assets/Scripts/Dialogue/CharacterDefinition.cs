using UnityEngine;

/// <summary>
/// 대사에 등장하는 화자 하나를 정의하는 데이터.
/// 파티원이든 NPC든 전부 이 타입의 에셋으로 만든다.
///
/// 만드는 법 (Unity 에디터):
/// Project 창에서 우클릭 → Create → 도메네뷸라 → Character Definition
/// 파일 이름은 chr_(이름) 형식으로. 예: chr_lime, chr_hayul, chr_npc_example
/// </summary>
[CreateAssetMenu(fileName = "chr_new", menuName = "도메네뷸라/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    [Tooltip("대사창/말풍선에 표시될 이름")]
    public string displayName;

    [Tooltip("기본 얼굴(초상화) 이미지")]
    public Sprite portrait;

    [Tooltip("체크하면 파티 대사창(PartyDialoguePanel)으로, 체크 해제하면 말풍선으로 출력됨")]
    public bool isPartyMember;

    [Header("이 캐릭터 전용 대사창/말풍선 스킨 (선택, 비워두면 기본 스킨)")]
    [Tooltip("이 캐릭터가 파티 대사창에 나올 때 쓸 배경 이미지. DialogueLine의 Window Background Override가 " +
             "지정돼 있으면 그쪽이 우선하고, 둘 다 없으면 대사창 원래 기본 이미지를 씀")]
    public Sprite windowBackground;

    [Tooltip("이 캐릭터 전용 대사창 테두리 이미지 (위와 동일한 우선순위)")]
    public Sprite windowFrame;

    [Tooltip("이 캐릭터가 말풍선으로 나올 때 쓸 몸체 이미지 (위와 동일한 우선순위)")]
    public Sprite balloonBubble;

    [Tooltip("이 캐릭터 전용 말풍선 꼬리 이미지 (위와 동일한 우선순위)")]
    public Sprite balloonTail;
}
