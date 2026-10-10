using System;
using UnityEngine;

/// <summary>
/// 대사 한 줄. DialogueSequence 안에 배열로 들어간다.
/// </summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("이 줄을 말하는 캐릭터")]
    public CharacterDefinition speaker;

    [Tooltip("대사 내용")]
    [TextArea(2, 5)]
    public string text;

    [Tooltip("비워두면 speaker의 기본 portrait를 사용. 표정 차이 등을 줄 때만 따로 지정")]
    public Sprite portraitOverride;
}
