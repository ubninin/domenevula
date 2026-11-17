using System;
using UnityEngine;

/// <summary>
/// 이 줄을 어느 창으로 표시할지.
/// Auto: 화자가 없으면(지문) 기본 대사창, 화자가 있으면 화자의 Dialogue Window 설정을 따른다.
/// </summary>
public enum DialogueDisplayMode
{
    [InspectorName("자동 (화자 설정 따름)")]
    Auto,
    [InspectorName("캐릭터 대사창으로 표시")]
    ForceParty,
    [InspectorName("말풍선으로 표시")]
    ForceBalloon,
    [InspectorName("기본 대사창으로 표시 (지문, 얼굴 없음)")]
    ForceDefault,
    [InspectorName("하율 대사창으로 표시")]
    ForceHayul,
}

/// <summary>실제로 쓸 대사 출력 창.</summary>
public enum DialogueWindowKind
{
    Balloon,
    Character,
    Hayul,
    Default,
}

/// <summary>파티 대사창을 화면 어디에 띄울지 (이 줄에서만 적용).</summary>
public enum PartyPanelPosition
{
    // 숫자는 이미 저장된 대사 에셋과 호환되도록 고정: 예전 기본값(0)은 이제 '자동'으로 읽힌다.
    [InspectorName("자동 (플레이어 반대편)")]
    Auto = 0,
    [InspectorName("위")]
    Top = 1,
    [InspectorName("아래")]
    Bottom = 2,
}

/// <summary>말풍선을 어느 오브젝트 위에 띄울지.</summary>
public enum BalloonTarget
{
    [InspectorName("이 이벤트 / 대화 상대 (기본)")]
    Self,
    [InspectorName("플레이어 1")]
    Player1,
    [InspectorName("플레이어 2")]
    Player2,
    [InspectorName("다른 오브젝트 (이름으로 지정)")]
    OtherObject,
}

/// <summary>이 줄이 하는 일.</summary>
public enum DialogueLineKind
{
    [InspectorName("대사 (기본)")]
    Say,
    [InspectorName("전투 시작")]
    Battle,
}

/// <summary>
/// 대사 한 줄. DialogueSequence 안에 배열로 들어간다.
/// </summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("대사(기본) / 전투 시작. '전투 시작'이면 이 줄에서 전투가 열리고, 끝나면 아래 승리/패배 대화가 이어진 뒤 " +
             "다음 줄로 넘어간다. 전투 줄에서는 Text 가 적혀 있으면 전투 시작 전에 그 대사(화자·표시 방식 설정 그대로)가 먼저 뜨고, 비어 있으면 바로 전투가 시작된다.")]
    public DialogueLineKind kind = DialogueLineKind.Say;

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

    [Tooltip("자동이면 화자 설정을 따름: 화자가 없으면(지문) 기본 대사창, 화자가 있으면 그 캐릭터의 Dialogue Window 설정(캐릭터/하율 대사창 또는 말풍선)")]
    public DialogueDisplayMode displayMode = DialogueDisplayMode.Auto;

    [Tooltip("대사창으로 표시될 때, 화면 위/아래 중 어디에 띄울지. 자동 = 플레이어가 화면 위쪽 절반에 있으면 아래에, 아래쪽 절반에 있으면 위에 띄움. 말풍선 표시에는 영향 없음")]
    public PartyPanelPosition panelPosition = PartyPanelPosition.Auto;

    [Header("말풍선 위치 (말풍선으로 표시될 때만 적용)")]
    [Tooltip("말풍선을 띄울 대상. '이 이벤트 / 대화 상대'는 이 대사를 실행한 이벤트(NPC) 위. " +
             "플레이어 1/2는 씬의 Player1 / Player2 오브젝트 위.")]
    public BalloonTarget balloonTarget = BalloonTarget.Self;

    [Tooltip("Balloon Target 이 '다른 오브젝트'일 때, 씬 Hierarchy 에 있는 그 오브젝트의 이름 (정확히 일치해야 함)")]
    public string balloonTargetName;

    [Tooltip("말풍선의 기본 위치에서 추가로 위(+)/아래(-)로 옮길 거리 (월드 단위, 타일 1칸 = 1). 0이면 기본 위치 그대로")]
    public float balloonOffsetY = 0f;

    [Header("대사창/말풍선 이미지 교체 (선택, 비워두면 기본 스킨 그대로)")]
    [Tooltip("파티 대사창의 배경 이미지(PartyDialogueBg)를 이 줄에서만 교체")]
    public Sprite windowBackgroundOverride;

    [Tooltip("파티 대사창의 테두리 이미지(PartyDialogueFrame)를 이 줄에서만 교체")]
    public Sprite windowFrameOverride;

    [Tooltip("말풍선 몸체 이미지(SpeechBalloonBubble)를 이 줄에서만 교체")]
    public Sprite balloonBubbleOverride;

    [Tooltip("말풍선 꼬리 이미지(SpeechBalloonTail)를 이 줄에서만 교체")]
    public Sprite balloonTailOverride;

    [Header("[전투] Kind 가 '전투 시작'일 때만 사용 (Text 에 적은 대사는 전투 전에 먼저 나옴)")]
    [Tooltip("시작할 전투. Project 창에서 Create → 도메네뷸라 → Battle Encounter 로 만든 에셋을 넣는다")]
    public BattleEncounter battle;

    [Tooltip("체크하면 패배해도 게임오버가 되지 않고 '패배 시 대화'로 이어진다. 체크 해제하면 패배 = 게임오버")]
    public bool allowDefeat;

    [Tooltip("승리했을 때 이어서 재생할 대화 (비워두면 바로 다음 줄)")]
    public DialogueSequence onVictory;

    [Tooltip("패배했을 때 이어서 재생할 대화 — Allow Defeat 가 체크된 경우에만 쓰인다 (비워두면 바로 다음 줄)")]
    public DialogueSequence onDefeat;

    /// <summary>이 줄을 어느 창으로 보여줄지 계산.</summary>
    public DialogueWindowKind ResolveWindow()
    {
        switch (displayMode)
        {
            case DialogueDisplayMode.ForceParty: return DialogueWindowKind.Character;
            case DialogueDisplayMode.ForceBalloon: return DialogueWindowKind.Balloon;
            case DialogueDisplayMode.ForceDefault: return DialogueWindowKind.Default;
            case DialogueDisplayMode.ForceHayul: return DialogueWindowKind.Hayul;
        }

        // 자동: 화자가 없으면 지문 → 기본 대사창
        if (speaker == null) return DialogueWindowKind.Default;

        switch (speaker.dialogueWindow)
        {
            case DialogueWindowType.Balloon: return DialogueWindowKind.Balloon;
            case DialogueWindowType.Character: return DialogueWindowKind.Character;
            case DialogueWindowType.Hayul: return DialogueWindowKind.Hayul;
            default: return speaker.isPartyMember ? DialogueWindowKind.Character : DialogueWindowKind.Balloon;
        }
    }

    /// <summary>(예전 이름) 말풍선이 아니라 대사창으로 표시되는 줄인지.</summary>
    public bool ShouldUsePartyPanel() => ResolveWindow() != DialogueWindowKind.Balloon;

    // 각 이미지: 이 줄 지정 > speaker 기본값 > null(패널 원래 기본 스킨 유지) 순으로 결정.
    public Sprite ResolveWindowBackground() => windowBackgroundOverride != null ? windowBackgroundOverride : speaker?.windowBackground;
    public Sprite ResolveWindowFrame() => windowFrameOverride != null ? windowFrameOverride : speaker?.windowFrame;
    public Sprite ResolveBalloonBubble() => balloonBubbleOverride != null ? balloonBubbleOverride : speaker?.balloonBubble;
    public Sprite ResolveBalloonTail() => balloonTailOverride != null ? balloonTailOverride : speaker?.balloonTail;
}
