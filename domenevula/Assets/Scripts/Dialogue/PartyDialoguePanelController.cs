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
/// </summary>
public class PartyDialoguePanelController : MonoBehaviour, IDialogueDisplay
{
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image frameImage;

    [Header("타자 애니메이션")]
    [SerializeField] private float charsPerSecond = 30f;

    private Sprite defaultBackground;
    private Sprite defaultFrame;
    private TypewriterEffect typewriter;

    public bool IsRevealing => typewriter.IsRevealing;
    public void SkipReveal() => typewriter.SkipToEnd();

    private void Awake()
    {
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

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
