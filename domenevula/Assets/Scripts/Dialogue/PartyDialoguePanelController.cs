using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PartyDialoguePanel(파티 3인 전용 대사창)에 붙이는 컴포넌트.
///
/// 붙이는 곳: Hierarchy의 PartyDialoguePanel 오브젝트
/// 인스펙터 연결: dialogueText ← PartyDialogueText, portraitImage ← PartySpeakerPortrait
/// (둘 다 자식 오브젝트를 드래그해서 슬롯에 넣으면 됨)
/// </summary>
public class PartyDialoguePanelController : MonoBehaviour
{
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Image portraitImage;

    private void Awake()
    {
        Hide();
    }

    /// <summary>원문 그대로 표시 (줄바꿈/페이지 분할 없이). 짧은 대사용.</summary>
    public void Show(DialogueLine line)
    {
        ShowPage(line, line.text);
    }

    /// <summary>
    /// 초상화는 line에서 그대로 쓰되, 본문 텍스트만 pageText로 교체해서 표시.
    /// DialogueManager가 긴 대사를 페이지로 쪼갤 때 쓴다.
    /// </summary>
    public void ShowPage(DialogueLine line, string pageText)
    {
        gameObject.SetActive(true);
        dialogueText.text = pageText;

        Sprite portrait = line.portraitOverride != null ? line.portraitOverride : line.speaker.portrait;
        portraitImage.sprite = portrait;
        portraitImage.enabled = portrait != null;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
