using System;
using UnityEngine;

/// <summary>
/// 이 줄을 파티 대사창/말풍선 중 어디로 표시할지.
/// Auto면 speaker.isPartyMember 값을 그대로 따른다 (기존 동작과 동일).
/// </summary>
public enum DialogueDisplayMode
{
    [InspectorName("자동 (화자 설정 따름)")]
    Auto,
    [InspectorName("파티 대사창으로 표시")]
    ForceParty,
    [InspectorName("말풍선으로 표시")]
    ForceBalloon,
}

/// <summary>
/// 대사 한 줄. DialogueSequence 안에 배열로 들어간다.
/// </summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("이 줄을 말하는 캐릭터 (선택). 비워두면 아래 Portrait Override와 Display Mode를 직접 지정해서 " +
             "캐릭터 에셋 없이 이 줄 하나만으로 완결된 대사를 만들 수 있다. 자주 나오는 캐릭터는 에셋으로 " +
             "만들어두면 얼굴/파티 여부를 매번 다시 지정할 필요가 없어서 편함.")]
    public CharacterDefinition speaker;

    [Tooltip("대사 내용")]
    [TextArea(2, 5)]
    public string text;

    [Tooltip("비워두면 speaker의 기본 portrait를 사용 (speaker도 없으면 얼굴 없이 표시). " +
             "표정 차이를 주거나, speaker 없이 이 줄만의 얼굴을 지정할 때 사용")]
    public Sprite portraitOverride;

    /// <summary>얼굴: 이 줄 지정 &gt; speaker 기본값 &gt; 없음(null, 얼굴 숨김) 순으로 결정.</summary>
    public Sprite ResolvePortrait() => portraitOverride != null ? portraitOverride : speaker?.portrait;

    [Tooltip("자동이면 speaker.isPartyMember를 따름. speaker가 없으면 자동은 '말풍선'으로 처리됨")]
    public DialogueDisplayMode displayMode = DialogueDisplayMode.Auto;

    [Header("대사창/말풍선 이미지 교체 (선택, 비워두면 기본 스킨 그대로)")]
    [Tooltip("파티 대사창의 배경 이미지(PartyDialogueBg)를 이 줄에서만 교체")]
    public Sprite windowBackgroundOverride;

    [Tooltip("파티 대사창의 테두리 이미지(PartyDialogueFrame)를 이 줄에서만 교체")]
    public Sprite windowFrameOverride;

    [Tooltip("말풍선 몸체 이미지(SpeechBalloonBubble)를 이 줄에서만 교체")]
    public Sprite balloonBubbleOverride;

    [Tooltip("말풍선 꼬리 이미지(SpeechBalloonTail)를 이 줄에서만 교체")]
    public Sprite balloonTailOverride;

    /// <summary>이 줄을 실제로 파티 대사창에 표시할지 계산.</summary>
    public bool ShouldUsePartyPanel()
    {
        switch (displayMode)
        {
            case DialogueDisplayMode.ForceParty: return true;
            case DialogueDisplayMode.ForceBalloon: return false;
            default: return speaker != null && speaker.isPartyMember; // speaker 없으면 false(말풍선)
        }
    }

    // 각 이미지: 이 줄 지정 > speaker 기본값 > null(패널 원래 기본 스킨 유지) 순으로 결정.
    public Sprite ResolveWindowBackground() => windowBackgroundOverride != null ? windowBackgroundOverride : speaker?.windowBackground;
    public Sprite ResolveWindowFrame() => windowFrameOverride != null ? windowFrameOverride : speaker?.windowFrame;
    public Sprite ResolveBalloonBubble() => balloonBubbleOverride != null ? balloonBubbleOverride : speaker?.balloonBubble;
    public Sprite ResolveBalloonTail() => balloonTailOverride != null ? balloonTailOverride : speaker?.balloonTail;
}
