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

    public void Show(DialogueLine line)
    {
        gameObject.SetActive(true);
        dialogueText.text = line.text;

        Sprite portrait = line.portraitOverride != null ? line.portraitOverride : line.speaker.portrait;
        portraitImage.sprite = portrait;
        portraitImage.enabled = portrait != null;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
