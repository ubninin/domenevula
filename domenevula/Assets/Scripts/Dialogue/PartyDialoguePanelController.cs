using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PartyDialoguePanel(파티 3인 전용 대사창)에 붙이는 컴포넌트.
///
/// 붙이는 곳: Hierarchy의 PartyDialoguePanel 오브젝트
/// 인스펙터 연결: dialogueText ← PartyDialogueText, portraitImage ← PartySpeakerPortrait,
///              backgroundImage ← PartyDialogueBg, frameImage ← PartyDialogueFrame
/// (전부 자식 오브젝트를 드래그해서 슬롯에 넣으면 됨. backgroundImage/frameImage는
///  줄마다 이미지를 교체하는 기능을 안 쓸 거면 비워둬도 동작함)
///
/// 화면 위/아래 위치 전환: panelRect를 비워두면 자동으로 이 오브젝트의 RectTransform을 씀.
/// bottomPreset/topPreset에 각각 "화면 아래에 있을 때"와 "화면 위에 있을 때"의 Anchor/Pivot/위치를
/// 미리 세팅해두면, DialogueLine.panelPosition 값에 따라 자동으로 그 자리로 옮겨간다.
/// 기본값(Bottom: anchor 아래 꽉 채움, Top: anchor 위 꽉 채움)은 Canvas 1600x900, 창이 화면
/// 좌우로 꽉 찬 레이아웃을 가정한 값이라, 실제 디자인에 맞게 Inspector에서 조정해서 쓰면 된다.
/// </summary>
public class PartyDialoguePanelController : MonoBehaviour, IDialogueDisplay
{
    [Serializable]
    public struct PanelAnchorPreset
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
    }

    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image frameImage;

    [Header("타자 애니메이션")]
    [SerializeField] private float charsPerSecond = 30f;

    [Header("화면 위/아래 위치 전환")]
    [Tooltip("비워두면 이 오브젝트의 RectTransform을 자동으로 사용")]
    [SerializeField] private RectTransform panelRect;

    [Tooltip("PartyDialogueShadow 등 창 그림자 오브젝트 (선택). Bg와 같은 자리로 따라 이동하면서, " +
             "그 자리에서 자기 원점 기준으로 상하 반전(localScale.y)만 된다.")]
    [SerializeField] private RectTransform shadowRect;

    [Tooltip("Shadow가 Bg와 다른 위치(예: 살짝 아래로 어긋난 그림자)를 쓰고 싶을 때만 따로 지정. " +
             "비워두면(기본) Bottom/Top Preset 값을 Bg와 동일하게 그대로 따라간다.")]
    [SerializeField] private bool useSeparateShadowPreset = false;
    [SerializeField] private PanelAnchorPreset shadowBottomPreset;
    [SerializeField] private PanelAnchorPreset shadowTopPreset;

    [SerializeField]
    private PanelAnchorPreset bottomPreset = new PanelAnchorPreset
    {
        anchorMin = new Vector2(0f, 0f),
        anchorMax = new Vector2(1f, 0f),
        pivot = new Vector2(0.5f, 0f),
        anchoredPosition = new Vector2(0f, 40f),
    };

    [SerializeField]
    private PanelAnchorPreset topPreset = new PanelAnchorPreset
    {
        anchorMin = new Vector2(0f, 1f),
        anchorMax = new Vector2(1f, 1f),
        pivot = new Vector2(0.5f, 1f),
        anchoredPosition = new Vector2(0f, -40f),
    };

    private Sprite defaultBackground;
    private Sprite defaultFrame;
    private TypewriterEffect typewriter;

    public bool IsRevealing => typewriter.IsRevealing;
    public void SkipReveal() => typewriter.SkipToEnd();

    private void Awake()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();

        typewriter = new TypewriterEffect(this, dialogueText);
        if (backgroundImage != null) defaultBackground = backgroundImage.sprite;
        if (frameImage != null) defaultFrame = frameImage.sprite;
        Hide();
    }

    /// <summary>원문 그대로 표시 (줄바꿈/페이지 분할 없이). 짧은 대사용.</summary>
    public void Show(DialogueLine line)
    {
        ShowPage(line, line.text);
    }

    /// <summary>
    /// 초상화/창 이미지는 line에서 그대로 쓰되, 본문 텍스트만 pageText로 교체해서 표시.
    /// DialogueManager가 긴 대사를 페이지로 쪼갤 때 쓴다.
    /// </summary>
    public void ShowPage(DialogueLine line, string pageText)
    {
        gameObject.SetActive(true);
        typewriter.Play(pageText, charsPerSecond);

        ApplyPanelPosition(line.panelPosition);

        Sprite portrait = line.ResolvePortrait();
        portraitImage.sprite = portrait;
        portraitImage.enabled = portrait != null;

        if (backgroundImage != null)
        {
            Sprite bg = line.ResolveWindowBackground();
            backgroundImage.sprite = bg != null ? bg : defaultBackground;
        }

        if (frameImage != null)
        {
            Sprite frame = line.ResolveWindowFrame();
            frameImage.sprite = frame != null ? frame : defaultFrame;
        }
    }

    private void ApplyPanelPosition(PartyPanelPosition position)
    {
        if (panelRect == null) return;

        PanelAnchorPreset preset = position == PartyPanelPosition.Top ? topPreset : bottomPreset;
        panelRect.anchorMin = preset.anchorMin;
        panelRect.anchorMax = preset.anchorMax;
        panelRect.pivot = preset.pivot;
        panelRect.anchoredPosition = preset.anchoredPosition;

        if (shadowRect != null)
        {
            PanelAnchorPreset shadowPreset = useSeparateShadowPreset
                ? (position == PartyPanelPosition.Top ? shadowTopPreset : shadowBottomPreset)
                : preset; // 기본: Bg랑 똑같은 자리로 따라간다

            shadowRect.anchorMin = shadowPreset.anchorMin;
            shadowRect.anchorMax = shadowPreset.anchorMax;
            shadowRect.pivot = shadowPreset.pivot;
            shadowRect.anchoredPosition = shadowPreset.anchoredPosition;

            Vector3 scale = shadowRect.localScale;
            float absY = Mathf.Abs(scale.y);
            scale.y = position == PartyPanelPosition.Top ? -absY : absY;
            shadowRect.localScale = scale;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
